#!/usr/bin/env python3
"""
Converts animals from 0 A.D. (Wildfire Games, art CC-BY-SA 3.0) into the game's creature models.

0 A.D. describes each animal as an "actor": a COLLADA mesh, a fur texture, and a separate COLLADA
file per animation (idle, walk, run, attack, death), sometimes pulled in from shared variant
files. This follows those references in a checkout of the 0 A.D. repository, then has Blender put
each animal together, with its texture and all of its animations as named clips, and saves it as
one .glb in game/assets/creatures. The converted models keep 0 A.D.'s licence (CC-BY-SA 3.0).

Needs Blender 4.5 as a Python module (COLLADA was dropped in Blender 5):
  uv venv --python 3.11 blender45 && uv pip install --python blender45/bin/python "bpy>=4.5,<4.6"
  git clone --depth 1 --filter=blob:none --no-checkout https://github.com/0ad/0ad 0ad
  blender45/bin/python tools/creatures/zero_ad.py 0ad [only-this-animal]
"""
import os
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "game", "assets", "creatures")
ART = "binaries/data/mods/public/art"

# game name -> (0 A.D. actor, which fur texture to use when it has several)
ANIMALS = {
    "wolf": ("fauna/wolf.xml", "animal_wolf_grey.png"),
    "ice_wolf": ("fauna/arctic_wolf.xml", None),
    "fox": ("fauna/fox_red.xml", None),
    "bear": ("fauna/bear_brown.xml", None),
    "bear_black": ("fauna/bear_black.xml", None),
    "boar": ("fauna/boar.xml", None),
    "hawk": ("fauna/hawk.xml", "animal_hawk.png"),
    "bear_white": ("fauna/bear_white.xml", None),
    "goat": ("fauna/goat.xml", None),
    "fox_arctic": ("fauna/fox_arctic.xml", None),
    "crocodile": ("fauna/crocodile.xml", None),
    "tiger": ("fauna/tiger.xml", None),
    "lioness": ("fauna/lioness.xml", None),
    "bull": ("fauna/bull.xml", None),
    "rabbit": ("fauna/rabbit1.xml", None),
    "mastiff": ("fauna/mastiff.xml", None),
}

# 0 A.D.'s animation names, folded into the game's five.
CLIPS = {"idle": "Idle", "walk": "Walk", "run": "Run", "attack_melee": "Attack", "death": "Death"}


def git(repo, path):
    return subprocess.run(["git", "-C", repo, "show", f"HEAD:{path}"], capture_output=True, check=True).stdout


def resolve(repo, actor, texture_pick):
    """The mesh, texture and first animation of each kind an actor uses, following variant files."""
    mesh, textures, clips = None, [], {}

    def visit(element):
        nonlocal mesh
        for node in element.iter():
            if node.tag == "variant" and node.get("file"):
                visit(ET.fromstring(git(repo, f"{ART}/variants/{node.get('file')}")))
            elif node.tag == "mesh" and mesh is None:
                mesh = node.text.strip()
            elif node.tag == "texture" and node.get("name") == "baseTex":
                textures.append(node.get("file"))
            elif node.tag == "animation":
                name = CLIPS.get(node.get("name", "").lower())
                if name and name not in clips:
                    clips[name] = node.get("file")

    visit(ET.fromstring(git(repo, f"{ART}/actors/{actor}")))
    texture = next((t for t in textures if texture_pick and t.endswith(texture_pick)), textures[0] if textures else None)
    if "Run" not in clips and "Walk" in clips:
        clips["Run"] = clips["Walk"]
    return mesh, texture, clips


def build(repo, name, actor, pick, work):
    import bpy

    mesh, texture, clips = resolve(repo, actor, pick)
    print(f"{name}: {mesh}, {texture}, {sorted(clips)}")
    files = {}
    for kind, rel in [("mesh", f"meshes/{mesh}"), ("texture", f"textures/skins/{texture}")] + [(c, f"animation/{f}") for c, f in clips.items()]:
        path = os.path.join(work, kind + os.path.splitext(rel)[1])
        data = git(repo, f"{ART}/{rel}")
        if path.endswith(".dae"):
            # A few meshes (the rabbit's) name a texture with no texcoord, which Blender's importer refuses: the
            # material is replaced below anyway, so give it one.
            data = re.sub(rb'<texture texture="([^"]*)"\s*/>', rb'<texture texture="\1" texcoord="UVMap"/>', data)
        with open(path, "wb") as f:
            f.write(data)
        files[kind] = path

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.collada_import(filepath=files["mesh"])
    rig = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
    bodies = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    # Some rigs (the tiger's, the lioness's, the crocodile's, the rabbit's) carry a scale on the armature that the
    # glTF export puts on the skeleton but not the mesh, leaving the mesh's own numbers a hundredth of the size it's
    # drawn: apply every rotation and scale first, so mesh and skeleton agree.
    if not os.environ.get("ZERO_AD_NO_APPLY"):
        bpy.ops.object.select_all(action="SELECT")
        bpy.context.view_layer.objects.active = rig
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    # The fur.
    image = bpy.data.images.load(files["texture"])
    image.pack()
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    shader = material.node_tree.nodes["Principled BSDF"]
    tex = material.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = image
    material.node_tree.links.new(tex.outputs["Color"], shader.inputs["Base Color"])
    shader.inputs["Roughness"].default_value = 0.9
    for body in bodies:
        body.data.materials.clear()
        body.data.materials.append(material)

    # Each animation comes on its own copy of the skeleton: keep its action, drop the copy.
    rig.animation_data_create()
    for clip in ("Idle", "Walk", "Run", "Attack", "Death"):
        if clip not in files:
            continue
        before = set(bpy.context.scene.objects)
        bpy.ops.wm.collada_import(filepath=files[clip])
        added = [o for o in bpy.context.scene.objects if o not in before]
        source = next((o for o in added if o.type == "ARMATURE" and o.animation_data and o.animation_data.action), None)
        if source is not None:
            action = source.animation_data.action
            # These animations carry a scale that undoes the rig's own (2.5 on the pelvis against 0.4
            # on the armature), and positions measured in that scaled space: keep only the rotations,
            # which are the walk itself, or the animal balloons and floats.
            # Some (the tiger's, the goat's) also scale and move the whole armature object: drop those too, and keep
            # only the bones' rotations.
            for curve in [c for c in action.fcurves if not (c.data_path.startswith("pose.bones") and "rotation" in c.data_path)]:
                action.fcurves.remove(curve)
            action.name = clip
            action.use_fake_user = True
            track = rig.animation_data.nla_tracks.new()
            track.name = clip
            track.strips.new(clip, int(action.frame_range[0]), action)
        for o in added:
            bpy.data.objects.remove(o, do_unlink=True)

    os.makedirs(OUT, exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, f"{name}.glb"), export_format="GLB",
                              export_animations=True, export_animation_mode="NLA_TRACKS", export_yup=True)


def main():
    repo = sys.argv[1]
    only = sys.argv[2] if len(sys.argv) > 2 else None
    for name, (actor, pick) in ANIMALS.items():
        if only and name != only:
            continue
        with tempfile.TemporaryDirectory() as work:
            build(repo, name, actor, pick, work)
    with open(os.path.join(OUT, "CREDITS-0AD.md"), "w") as f:
        f.write("# Animals from 0 A.D.\n\n")
        f.write("These creatures are converted from 0 A.D. by Wildfire Games (https://play0ad.com,\n")
        f.write("https://github.com/0ad/0ad), whose art is licensed CC-BY-SA 3.0\n")
        f.write("(https://creativecommons.org/licenses/by-sa/3.0/). The converted .glb files are released under\n")
        f.write("the same licence. Converted by tools/creatures/zero_ad.py:\n\n")
        for name, (actor, _) in ANIMALS.items():
            f.write(f"- {name}.glb: {actor}\n")


if __name__ == "__main__":
    main()

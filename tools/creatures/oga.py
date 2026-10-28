#!/usr/bin/env python3
"""
Converts creatures from OpenGameArt.org into the game's .glb models: a giant rat, a goblin,
a forest monster (spriggans), a troll (frost trolls) and a dragon.

These were made in Blender 2.7x, whose materials newer Blender shows as plain white, so each material is
rebuilt from the texture packed in the file. Only the meshes skinned to the rig and visible are kept, the
named animations are renamed to the game's clips (Idle, Walk, Run, Attack, Death), and big textures
are shrunk to 1024 px for phones.

Download each model from its OpenGameArt page (listed in MODELS) into one folder, unpacking archives,
then, with Blender 4.5 as a Python module:
  blender45/bin/python tools/creatures/oga.py path/to/downloads [only-this-creature]
"""
import os
import sys

import bpy

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "game", "assets", "creatures")

# name -> page, licence, author, .blend inside the download folder, {material: texture or colour},
#         {game clip: action}, meshes to keep (None: every visible mesh on the rig)
MODELS = {
    "giant_rat": dict(
        page="evil-giant-rat", licence="CC0", author="CDmir (with TinyWorlds)",
        blend="evil-giant-rat/rat_2.blend",
        materials={"body": "rat-body", "head": "rat-head", "head.001": "rat-head", "Teeth": (0.85, 0.8, 0.65), "Material.002": (0.02, 0.02, 0.02)},
        clips={"Idle": "Idle.001", "Walk": "Walk", "Run": "Run", "Attack": "Attack.000", "Death": "Die"},
        keep=None),
    "goblin": dict(
        page="goblin-1", licence="CC0", author="CDmir (with TinyWorlds)",
        blend="goblin-1/goblin-FINAL.blend",
        materials={"goblin": "gobli-tex", "Eyes": (0.9, 0.75, 0.2), "Gold": (0.8, 0.6, 0.2)},
        clips={"Idle": "Idle.000", "Walk": "Walk", "Run": "Run", "Attack": "Attack.000", "Death": "Die"},
        keep=None, uv="UV"),
    "forest_monster": dict(
        page="forest-monster", licence="CC0", author="CDmir (with TinyWorlds)",
        blend="forest-monster/forest-monster_0/forest-monster-final.blend",
        materials={"Monster": "forest-monster-skin1.png", "Tree": "file:forest-monster/forest-monster_0/texture/tree.png"},
        clips={"Idle": "Idle", "Walk": "Walk", "Attack": "Attack", "Death": "Dying"},
        keep=None),
    "troll": dict(
        page="troll-mauler", licence="CC-BY 3.0", author="piacenti",
        blend="troll-mauler/troll.blend",
        materials={"med": "troll_baseTexBaked.png", "cloth": "cloth uv.png", "eye": "Material Diffuse Color"},
        clips={"Walk": "ArmatureAction", "Idle": "ArmatureAction"},
        keep={"med", "cloth", "eye med"}),
    "dragon": dict(
        page="cethiels-dragon-3d", licence="CC0", author="Drummyfish, from a design by Cethiel",
        blend="cethiels-dragon-3d/dragon_oga/dragon-2_80.blend",
        materials={"Material": "file:cethiels-dragon-3d/dragon_oga/dragon.png"},
        clips={"Idle": "Idle", "Walk": "Walk", "Attack": "Attack", "Death": "Die"},
        keep={"dragon"}),
}


def material(name, source, downloads):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    shader = m.node_tree.nodes["Principled BSDF"]
    shader.inputs["Roughness"].default_value = 0.85
    if isinstance(source, tuple):
        shader.inputs["Base Color"].default_value = (*source, 1)
        return m
    if source.startswith("file:"):
        image = bpy.data.images.load(os.path.join(downloads, source[5:]))
    else:
        image = bpy.data.images[source]
    if image.size[0] > 1024:
        image.scale(1024, 1024)
    image.pack()
    tex = m.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = image
    m.node_tree.links.new(tex.outputs["Color"], shader.inputs["Base Color"])
    return m


def build(name, spec, downloads):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(downloads, spec["blend"]), load_ui=False)
    scene = bpy.context.scene
    rig = next(o for o in scene.objects if o.type == "ARMATURE")
    keep = [o for o in scene.objects if o.type == "MESH"
            and any(md.type == "ARMATURE" and md.object == rig for md in o.modifiers)
            and (o.name in spec["keep"] if spec["keep"] else o.visible_get())]
    for o in list(scene.objects):
        if o is not rig and o not in keep:
            bpy.data.objects.remove(o, do_unlink=True)
    for o in keep:
        o.hide_set(False)
        o.hide_viewport = False
        for md in [md for md in o.modifiers if md.type not in ("ARMATURE",)]:
            o.modifiers.remove(md)

    # The texture is laid out on one UV map; glTF takes the first, so keep only that one.
    if spec.get("uv"):
        for o in keep:
            for layer in [u for u in o.data.uv_layers if u.name != spec["uv"]]:
                if spec["uv"] in o.data.uv_layers:
                    o.data.uv_layers.remove(layer)

    made = {}
    for o in keep:
        for slot in o.material_slots:
            old = slot.material.name if slot.material else ""
            source = spec["materials"].get(old)
            if source is None:
                continue
            if old not in made:
                made[old] = material(f"{name}_{old}", source, downloads)
            slot.material = made[old]

    rig.animation_data_create()
    rig.animation_data.action = None
    for track in list(rig.animation_data.nla_tracks):
        rig.animation_data.nla_tracks.remove(track)
    for clip, action_name in spec["clips"].items():
        source = bpy.data.actions[action_name]
        action = source.copy()
        action.name = clip
        track = rig.animation_data.nla_tracks.new()
        track.name = clip
        track.strips.new(clip, int(action.frame_range[0]), action)

    os.makedirs(OUT, exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, f"{name}.glb"), export_format="GLB",
                              export_animations=True, export_animation_mode="NLA_TRACKS", export_yup=True,
                              use_visible=False, export_apply=False)
    print(f"{name}: kept {[o.name for o in keep]}, clips {list(spec['clips'])}")


def main():
    downloads = sys.argv[1]
    only = sys.argv[2] if len(sys.argv) > 2 else None
    for name, spec in MODELS.items():
        if not only or name == only:
            build(name, spec, downloads)
    with open(os.path.join(OUT, "CREDITS-OGA.md"), "w") as f:
        f.write("# Creatures from OpenGameArt.org\n\nConverted by tools/creatures/oga.py.\n\n")
        for name, spec in MODELS.items():
            f.write(f"- {name}.glb: by {spec['author']}, {spec['licence']}, https://opengameart.org/content/{spec['page']}\n")


if __name__ == "__main__":
    main()

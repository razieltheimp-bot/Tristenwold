#!/usr/bin/env python3
"""
Converts Quaternius's animal packs (CC0, from OpenGameArt.org) into the game's .glb creatures: livestock for the
farms (cow, horse, sheep, pig), dogs and cats for the towns, chicks, an eagle and small birds for the sky, fish for
the lakes and a whale for the sea.

Each animal comes from its pack's .blend (the FBX files lose the farm animals' walks and everyone's colours). Its
animations are renamed to the game's clips (Idle, Walk, Run, Attack, Death); a bird or a fish moves with the one it
has. The farm animals keep their own flat colours; the older packs' materials are plain grey, so they are painted
here (a brown dog, a grey cat, a bald eagle, a slate-and-white whale).

Download the three packs from OpenGameArt and unzip them into one folder:
  https://opengameart.org/content/lowpoly-animated-farm-animal-pack   (Farm Animals by @Quaternius)
  https://opengameart.org/content/animated-animales-low-poly          (Animal Pack Vol.2 by @Quaternius)
  https://opengameart.org/content/5-low-poly-animals                  (Animals Pack by Quaternius)
then, with Blender 5.2 as a Python module (pip install bpy==5.2.3):
  python tools/creatures/quaternius.py path/to/unzipped [only-this-animal]
"""
import os
import sys

import bpy

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "game", "assets", "creatures")

# game name -> pack folder, .blend file, {game clip: [the pack's action names, ...]} (the first that exists wins; a clip
# may borrow another's action), {material: colour} for the packs whose materials are left plain grey
FARM, VOL2, PACK = "Farm Animals by @Quaternius", "Animal Pack Vol.2 by @Quaternius", "Animals Pack by Quaternius"
FOUR_LEGS = {"Idle": ["Idle"], "Walk": ["Walk"], "Run": ["Run"], "Attack": ["Jump"], "Death": ["Death"]}
ANIMALS = {
    "cow": (FARM, "Cow.blend", FOUR_LEGS, {}),
    "horse": (FARM, "Horse.blend", FOUR_LEGS, {}),
    "sheep": (FARM, "Sheep.blend", FOUR_LEGS, {}),
    "pig": (FARM, "Pig.blend", FOUR_LEGS, {}),
    "dog": (VOL2, "Dog.blend", {"Idle": ["Idle"], "Walk": ["Walking"]}, {"Dog": "#8a6a48"}),
    "cat": (VOL2, "Cat.blend", {"Idle": ["Idle"], "Walk": ["Walking"]}, {"Grey": "#6e6a66", "White": "#e8e4dc", "Pink": "#e0a0a8"}),
    "eagle": (VOL2, "Eagle.blend", {"Idle": ["Flying"], "Walk": ["Flying"]}, {"Wings": "#5a4030", "Head": "#f0ece0", "Beak": "#e0b030", "Claws": "#e0b030"}),
    "songbird": (PACK, "Bird.blend", {"Idle": ["ArmatureAction"], "Walk": ["ArmatureAction"]}, {"Material.002": "#8a5a3a", "Material.006": "#e0c890"}),
    "chick": (PACK, "Chick.blend", {"Idle": ["ArmatureAction.002"], "Walk": ["ArmatureAction.002"]}, {"Material.001": "#f0d040", "Material.002": "#e08020"}),
    "fish": (PACK, "Fish.blend", {"Idle": ["ArmatureAction"], "Walk": ["ArmatureAction"]}, {"Material.001": "#7a98a8"}),
    "whale": (PACK, "Whale.blend", {"Idle": ["ArmatureAction"], "Walk": ["ArmatureAction"]}, {"Material.001": "#3a4a5a", "Material.002": "#c8c8c0"}),
}


def rgb(hex_):
    """sRGB hex to the linear colour Blender keeps."""
    def lin(c):
        c /= 255
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(lin(int(hex_[i:i + 2], 16)) for i in (1, 3, 5)) + (1.0,)


def paint(material, colour):
    """A plain, opaque material of one colour: its own (kept as its display colour), or the one given."""
    colour = colour or tuple(material.diffuse_color[:3]) + (1.0,)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    out = nodes.new("ShaderNodeOutputMaterial")
    material.node_tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    bsdf.inputs["Base Color"].default_value = colour
    bsdf.inputs["Alpha"].default_value = 1.0
    bsdf.inputs["Roughness"].default_value = 0.8


def convert(name, folder, blend, clips, colours, source):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(source, folder, "Blends", blend))
    for o in list(bpy.data.objects):
        if o.type not in ("ARMATURE", "MESH"):
            bpy.data.objects.remove(o)
    for m in bpy.data.materials:
        paint(m, rgb(colours[m.name]) if m.name in colours else None)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    found = {a.name.split("|")[-1]: a for a in bpy.data.actions}
    for a in found.values():
        a.name = "pack:" + a.name  # out of the way of the game's names
    made = []
    for clip, wants in clips.items():
        action = next((found[w] for w in wants if w in found), None)
        if action is None:
            print(f"{name}: no action for {clip} (has {list(found)})")
            continue
        copy = action.copy()
        copy.name = clip
        copy.use_fake_user = True
        made.append(copy)
    for a in list(bpy.data.actions):
        if a not in made:
            bpy.data.actions.remove(a)
    arm.animation_data_create()
    arm.animation_data.action = made[0]
    out = os.path.join(OUT, f"{name}.glb")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_animations=True, export_animation_mode="ACTIONS",
                              export_yup=True, export_cameras=False, export_lights=False)
    print(f"{name}: {[a.name for a in made]} -> {os.path.relpath(out, ROOT)}")


if __name__ == "__main__":
    source = sys.argv[1]
    only = sys.argv[2] if len(sys.argv) > 2 else None
    for name, (folder, blend, clips, colours) in ANIMALS.items():
        if only is None or only == name:
            convert(name, folder, blend, clips, colours, source)

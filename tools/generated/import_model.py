#!/usr/bin/env python3
"""
Brings a generated 3D model into the game. Hunyuan3D-2, TRELLIS, UniForge3D and CharacterGen all write a textured
mesh (.glb, .obj or .fbx) with far too many triangles, no skeleton, at no particular size, facing no particular way;
Mixamo turns a character mesh into a rigged one with an animation per .fbx. This script makes any of them into what
the game loads:

  a prop       a static .glb under game/assets/props/generated/, stood on the ground, at a real size
  a creature   an animated .glb under game/assets/creatures/, its clips named Idle, Walk, Run, Attack, Death

With Blender 5.2 as a Python module (pip install bpy==5.2.3):

  python tools/generated/import_model.py prop     barrel.glb      --name tar_barrel --height 0.9
  python tools/generated/import_model.py creature wyvern.glb      --name wyvern --height 2.4 --tris 12000
  python tools/generated/import_model.py creature knight_idle.fbx --name iron_knight --height 1.8 \\
      --clip Idle=knight_idle.fbx --clip Walk=knight_walk.fbx --clip Attack=knight_slash.fbx --clip Death=knight_dying.fbx

A creature's first file carries the mesh and rig (Mixamo's "with skin" export); each --clip names a game clip and
the file (Mixamo's "without skin" exports are fine) whose animation becomes it. A clip not given is borrowed:
Run from Walk, and Attack and Death from Idle, so a creature with one animation still moves. Generated meshes
without a rig come in as props, or as creatures that hold still (give them a Mixamo pass first for a walk).

  --height   how tall it stands in the game, in metres (its feet are put on the ground); exact for a prop, and a
             guide for a creature, which the game measures and sizes again itself as it loads it
  --tris     the triangle budget it's cut down to (default 8000 for a creature, 4000 for a prop)
  --facing   which way the model's front points as generated: -y (default, Blender's front), +y, +x or -x; it's
             turned to face the way the game's creatures do (their yaw is 180: see Creature.Create)
  --texture-size  the largest side its textures are kept at (default 2048)
Then, in the game folder, `godot --headless --import` so Godot takes the new file in, and use it:
  a prop:      Models.Place(this, "res://assets/props/generated/tar_barrel.glb", at, 1)
  a creature:  Creature.Create("wyvern.glb", height) (a Body in Wildfolk or Lairfolk, like the imp)
"""
import argparse
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CLIPS = ["Idle", "Walk", "Run", "Attack", "Death"]
BORROW = {"Run": "Walk", "Attack": "Idle", "Death": "Idle", "Walk": "Idle", "Idle": "Walk"}


def load(path):
    """Imports a model file, returning the objects it brought in."""
    before = set(bpy.data.objects)
    ext = os.path.splitext(path)[1].lower()
    if ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path, ignore_leaf_bones=True, automatic_bone_orientation=False)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=path)
    else:
        sys.exit(f"Can't read {path}: .glb, .gltf, .fbx or .obj only")
    return [o for o in bpy.data.objects if o not in before]


def bounds(objs):
    """The world-space box round the meshes, at their rest pose."""
    lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
    for o in objs:
        if o.type != "MESH":
            continue
        for corner in o.bound_box:
            p = o.matrix_world @ Vector(corner)
            lo, hi = Vector(map(min, lo, p)), Vector(map(max, hi, p))
    return lo, hi


def decimate(meshes, budget):
    """Cuts the meshes down to about the triangle budget between them, keeping their UVs."""
    total = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
    if total <= budget:
        return total
    ratio = budget / total
    for o in meshes:
        mod = o.modifiers.new("decimate", "DECIMATE")
        mod.ratio = ratio
        mod.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)


def shrink_textures(limit):
    for img in bpy.data.images:
        if img.size[0] > limit or img.size[1] > limit:
            scale = limit / max(img.size)
            img.scale(int(img.size[0] * scale), int(img.size[1] * scale))


def rename_clip(action, clip):
    action.name = clip
    action.use_fake_user = True


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("kind", choices=["prop", "creature"])
    ap.add_argument("file")
    ap.add_argument("--name", required=True, help="the file name in the game (letters, digits, underscores)")
    ap.add_argument("--height", type=float, required=True, help="how tall it stands, in metres")
    ap.add_argument("--tris", type=int)
    ap.add_argument("--facing", default="-y", choices=["-y", "+y", "+x", "-x"])
    ap.add_argument("--texture-size", type=int, default=2048)
    ap.add_argument("--clip", action="append", default=[], metavar="CLIP=FILE", help="a game clip and the file whose animation becomes it")
    ap.add_argument("--out", help="write here instead of the game's folder")
    args = ap.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:])
    if not args.name.replace("_", "").isalnum():
        sys.exit("--name: letters, digits and underscores only")
    budget = args.tris or (8000 if args.kind == "creature" else 4000)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = load(os.path.abspath(args.file))
    meshes = [o for o in objs if o.type == "MESH"]
    rigs = [o for o in objs if o.type == "ARMATURE"]
    if not meshes:
        sys.exit("No mesh in the file")
    roots = [o for o in objs if o.parent is None or o.parent not in objs]

    # The animations: the first file's own, then one per --clip from the other files (the same rig, Mixamo's way).
    for clip in CLIPS:
        for a in list(bpy.data.actions):
            if a.name.lower().startswith(clip.lower()) and clip not in bpy.data.actions:
                rename_clip(a, clip)
    for spec in args.clip:
        clip, _, path = spec.partition("=")
        if clip not in CLIPS:
            sys.exit(f"--clip {clip}: one of {', '.join(CLIPS)}")
        before = set(bpy.data.actions)
        extra = load(os.path.abspath(path))
        new = [a for a in bpy.data.actions if a not in before]
        if not new:
            sys.exit(f"{path} has no animation")
        if clip in bpy.data.actions:
            bpy.data.actions.remove(bpy.data.actions[clip])
        rename_clip(new[0], clip)
        for a in new[1:]:
            bpy.data.actions.remove(a)
        for o in extra:  # only its animation was wanted
            bpy.data.objects.remove(o, do_unlink=True)
    if args.kind == "creature" and rigs:
        have = {a.name for a in bpy.data.actions if a.name in CLIPS}
        for clip in CLIPS:
            if clip not in have and BORROW[clip] in have:
                copy = bpy.data.actions[BORROW[clip]].copy()
                rename_clip(copy, clip)
                have.add(clip)
        for a in list(bpy.data.actions):
            if a.name not in CLIPS:
                bpy.data.actions.remove(a)
        if have:
            rigs[0].animation_data_create()
            rigs[0].animation_data.action = bpy.data.actions["Idle" if "Idle" in have else sorted(have)[0]]
    else:
        for a in list(bpy.data.actions):
            bpy.data.actions.remove(a)

    # Stood on the ground at its real size, facing the game's way: scale, turn and move the roots. Measured at rest,
    # not in a crouch of its idle (the game's loader measures the rest mesh).
    for rig in rigs:
        rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    lo, hi = bounds(meshes)
    tall = hi.z - lo.z
    if tall <= 0:
        sys.exit("The mesh has no height")
    scale = args.height / tall
    turn = {"-y": 0, "+y": math.pi, "+x": math.pi / 2, "-x": -math.pi / 2}[args.facing]
    fix = Matrix.Rotation(turn, 4, "Z") @ Matrix.Scale(scale, 4)
    for o in roots:
        o.matrix_world = fix @ o.matrix_world
    bpy.context.view_layer.update()
    lo, hi = bounds(meshes)
    centre = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    for o in roots:
        o.matrix_world = Matrix.Translation(-centre) @ o.matrix_world
    bpy.context.view_layer.update()

    tris = decimate(meshes, budget)
    shrink_textures(args.texture_size)
    for rig in rigs:
        rig.data.pose_position = "POSE"

    folder = args.out or os.path.join(ROOT, "game", "assets", "creatures" if args.kind == "creature" else os.path.join("props", "generated"))
    os.makedirs(folder, exist_ok=True)
    out = os.path.join(folder, args.name + ".glb")
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_apply=True, export_animations=args.kind == "creature",
                              export_animation_mode="ACTIONS", export_nla_strips=False, export_image_format="AUTO", export_yup=True)
    clips = [a.name for a in bpy.data.actions] if args.kind == "creature" else []
    print(f"{out}: {tris} triangles, {args.height} m tall" + (f", clips {', '.join(clips)}" if clips else ", no animation"))


if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""
Downloads Poly Haven (CC0) scanned models and cuts them down for a game that also runs on phones.
The scans run to hundreds of thousands of triangles; Blender's decimator brings each to a budget
while keeping its photo textures, and writes game/assets/polyhaven/models/<name>.glb.

Needs Blender as a Python module:  pip install bpy==5.2.3  (in a venv), then
  python tools/polyhaven/models.py [--cache DIR]
"""
import argparse
import json
import os
import sys
import time
import urllib.request

import bpy

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "game", "assets", "polyhaven", "models")

# name in the game -> (Poly Haven asset, triangle budget, texture size)
MODELS = {
    "boulder": ("boulder_01", 2500, 1024),
    "boulder_flat": ("namaqualand_boulder_02", 2500, 1024),
    "boulder_small": ("namaqualand_boulder_05", 1800, 1024),
    "rock": ("rock_07", 1200, 512),
    "dead_trunk": ("dead_tree_trunk_02", 3000, 1024),
    "stump": ("tree_stump_01", 1500, 1024),
    "barrel": ("Barrel_01", 3000, 512),
    "crate": ("wooden_crate_01", 3000, 512),
    "lantern": ("wooden_lantern_01", 3000, 512),
    "fire_pit": ("stone_fire_pit", 4000, 1024),
    "fern": ("fern_02", 6500, 512),
    "chest": ("treasure_chest", 4000, 512),
    # The detail pass: scanned trees, rock faces and cliffs, shrubs, and ships for Tristinwold's harbour. Godot makes
    # simpler stand-ins of each for a distance on its own; the Cinematic setting keeps them at full detail.
    # (Poly Haven's full-grown fir and pine are scans of near a gigabyte each: too much. The young fir, scaled up,
    # and the small broadleaf tree stand in for them.)
    "fir_sapling": ("fir_sapling_medium", 6000, 1024),
    "small_tree": ("tree_small_02", 12000, 1024),
    "rock_face": ("rock_face_01", 9000, 1024),
    "rock_face_b": ("rock_face_02", 9000, 1024),
    "cliff": ("namaqualand_cliff_01", 9000, 1024),
    "moss_rocks": ("rock_moss_set_01", 6000, 1024),
    "coast_rocks": ("coast_rocks_01", 6000, 1024),
    "shrub": ("shrub_01", 4000, 512),
    "shrub_b": ("shrub_02", 4000, 512),
    "shrub_c": ("shrub_03", 4000, 512),
    "nettle": ("nettle_plant", 3000, 512),
    "ship": ("dutch_ship_medium", 30000, 1024),
    "ship_large": ("dutch_ship_large_01", 40000, 1024),
    "pinnace": ("ship_pinnace", 15000, 1024),
    "cannon": ("cannon_01", 3000, 512),
}


def get(url, path, size=None):
    """Downloads a file once. Poly Haven's mirrors sometimes serve a different revision of a file than the listing
    describes; given the listing's size, a file that doesn't match it is fetched again."""
    if os.path.exists(path) and os.path.getsize(path) > 0 and (size is None or os.path.getsize(path) == size):
        return path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    for attempt in range(8):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "tristinwold-asset-fetch"})
            data = urllib.request.urlopen(req, timeout=120).read()
            if size is not None and len(data) != size:
                raise IOError(f"got {len(data)} bytes, the listing says {size}")
            with open(path, "wb") as f:
                f.write(data)
            return path
        except Exception as e:
            print(f"  retry {url}: {e}", file=sys.stderr)
            time.sleep(2 ** attempt)
    raise SystemExit(f"could not download {url}")


def download(asset, cache):
    """The 1k glTF and everything it includes, laid out as the .gltf expects."""
    folder = os.path.join(cache, "models", asset)
    listing = os.path.join(cache, asset + ".files.json")
    get("https://api.polyhaven.com/files/" + asset, listing)
    entry = json.load(open(listing))["gltf"]["1k"]["gltf"]
    for rel, inc in entry["include"].items():
        get(inc["url"], os.path.join(folder, rel), inc.get("size"))
    return get(entry["url"], os.path.join(folder, asset + ".gltf"), entry.get("size"))


def triangles(objects):
    return sum(len(p.vertices) - 2 for o in objects for p in o.data.polygons)


def build(name, asset, budget, texture, cache):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=download(asset, cache))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    before = triangles(meshes)
    for o in meshes:  # some scans come as loose triangles, which the decimator can't collapse
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.remove_doubles(threshold=0.0001)
        bpy.ops.object.mode_set(mode="OBJECT")
    for o in meshes:
        target = budget * triangles([o]) / max(before, 1)
        for _ in range(6):  # seams can stop one pass short, so go again until it fits
            ratio = target / max(triangles([o]), 1)
            if ratio > 0.95:
                break
            mod = o.modifiers.new("decimate", "DECIMATE")
            mod.ratio = ratio
            mod.use_collapse_triangulate = True
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.modifier_apply(modifier=mod.name)
    after = triangles(meshes)
    for image in bpy.data.images:  # small things don't need big textures
        if image.size[0] > texture:
            image.scale(texture, texture)
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(OUT, name + ".glb"), export_format="GLB",
                              export_image_format="JPEG", export_jpeg_quality=88, export_apply=True)
    print(f"{name}: {asset} {before} -> {after} triangles")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--cache", default=os.path.join(ROOT, ".polyhaven-cache"))
    p.add_argument("--only")
    args = p.parse_args()
    for name, (asset, budget, texture) in MODELS.items():
        if args.only and name != args.only:
            continue
        build(name, asset, budget, texture, args.cache)
    with open(os.path.join(OUT, "CREDITS.md"), "w") as f:
        f.write("# Poly Haven models\n\nFrom https://polyhaven.com, CC0 (public domain), reduced for the game:\n\n")
        for name, (asset, _, _) in MODELS.items():
            f.write(f"- {name}.glb: {asset}\n")


if __name__ == "__main__":
    main()

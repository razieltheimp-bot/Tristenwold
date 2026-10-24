#!/usr/bin/env python3
"""
Photographs Poly Haven's scanned grass clump (CC0) from the side, on a transparent background,
to make the card the game's grass tufts are drawn with. The scan itself is 1.6 million triangles;
the card is two triangles. Needs Blender as a Python module (pip install bpy==5.2.3 pillow):

  python tools/polyhaven/grass_card.py [--cache DIR]
"""
import argparse
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from models import ROOT, download  # noqa: E402

OUT = os.path.join(ROOT, "game", "assets", "polyhaven", "foliage", "grass.png")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--cache", default=os.path.join(ROOT, ".polyhaven-cache"))
    p.add_argument("--asset", default="grass_medium_02")
    args = p.parse_args()

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=download(args.asset, args.cache))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    lo = [min((o.matrix_world @ v.co)[i] for o in meshes for v in o.data.vertices) for i in range(3)]
    hi = [max((o.matrix_world @ v.co)[i] for o in meshes for v in o.data.vertices) for i in range(3)]
    width, height = hi[0] - lo[0], hi[2] - lo[2]
    centre = ((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, (lo[2] + hi[2]) / 2)

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 48
    scene.render.film_transparent = True
    scene.render.resolution_x = 1024
    scene.render.resolution_y = max(128, int(1024 * height / width))
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"

    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(width, height) * 1.02
    cam.location = (centre[0], lo[1] - 10, centre[2])
    cam.rotation_euler = (math.radians(90), 0, 0)
    scene.collection.objects.link(cam)
    scene.camera = cam

    # Soft daylight from the front and above, so the card isn't lit from any one side.
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3
    sun.rotation_euler = (math.radians(50), 0, math.radians(20))
    scene.collection.objects.link(sun)
    world = bpy.data.worlds.new("sky")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    scene.world = world

    raw = OUT.replace(".png", "_raw.png")
    scene.render.filepath = raw
    bpy.ops.render.render(write_still=True)
    tuft(raw, OUT)
    os.remove(raw)
    print("wrote", OUT)


def tuft(raw, out):
    """The scan is a row of sparse clumps: cut them apart and pile them into one thick tuft."""
    from PIL import Image
    import random
    shot = Image.open(raw).convert("RGBA")
    alpha = shot.getchannel("A")
    w, h = shot.size
    filled = [any(alpha.getpixel((x, y)) > 20 for y in range(0, h, 2)) for x in range(w)]
    clumps, start = [], None
    for x, f in enumerate(filled + [False]):
        if f and start is None:
            start = x
        elif not f and start is not None:
            if x - start > 20:
                clumps.append(shot.crop((start, 0, x, h)))
            start = None
    rng = random.Random(4)
    card = Image.new("RGBA", (1024, 512), (0, 0, 0, 0))
    for i in range(14):
        c = rng.choice(clumps)
        if rng.random() < 0.5:
            c = c.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        bbox = c.getchannel("A").getbbox()
        c = c.crop(bbox)
        scale = rng.uniform(0.7, 1.0) * 500 / c.height
        c = c.resize((max(1, int(c.width * scale)), max(1, int(c.height * scale))), Image.Resampling.LANCZOS)
        x = int(512 + rng.uniform(-300, 300) - c.width / 2)
        card.alpha_composite(c, (max(0, min(1024 - c.width, x)), 512 - c.height))
    # The scan photographs darker than grass reads in daylight: lift it and warm it a little.
    from PIL import ImageEnhance
    rgb = ImageEnhance.Color(ImageEnhance.Brightness(card.convert("RGB")).enhance(1.9)).enhance(1.25)
    rgb.putalpha(card.getchannel("A"))
    rgb.save(out, optimize=True)


if __name__ == "__main__":
    main()

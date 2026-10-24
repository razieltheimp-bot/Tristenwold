#!/usr/bin/env python3
"""
Downloads the Poly Haven (CC0) photo textures the game uses and packs them for Godot:

  game/assets/polyhaven/ground/{albedo,normal,arm}.jpg   eight ground layers stacked into one image each,
                                                          imported as texture arrays for the terrain shader
  game/assets/polyhaven/surfaces/<name>_{diff,nor,arm}.jpg   building materials (stone, plaster, roofs...)
  game/assets/polyhaven/foliage/*.png                    branch and leaf cards cut from Poly Haven's
                                                          fir and island-tree scans, plus bark

Run it from anywhere:  python3 tools/polyhaven/fetch.py [--cache DIR]
Everything it writes is already in the repository; run it again only to change the selection.
"""
import argparse
import io
import json
import math
import os
import random
import sys
import time
import urllib.request

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "game", "assets", "polyhaven")
API = "https://api.polyhaven.com/files/"
SIZE = 1024

# The ground layers, in the order the terrain shader numbers them. Sizes are the real width in metres.
GROUND = [
    ("forest_leaves_02", "autumn forest floor"),
    ("sparse_grass", "grass under the pines"),
    ("brown_mud_leaves_01", "marsh mud"),
    ("grass_path_2", "tundra"),
    ("snow_02", "snow"),
    ("mossy_rock", "cliffs and crags"),
    ("river_small_rocks", "shores"),
    ("dirt", "trodden ground in settlements"),
]

# Building surfaces: game name -> Poly Haven texture.
SURFACES = {
    "stone": "old_stone_wall",
    "dark_stone": "castle_wall_slates",
    "plaster": "plastered_wall_02",
    "timber": "rough_wood",
    "planks": "old_planks_02",
    "slate": "roof_slates_02",
    "clay_tile": "roof_09",
    "thatch": "thatch_roof_angled",
    "cobbles": "cobblestone_floor_01",
}


def fetch(url, cache):
    path = os.path.join(cache, url.rsplit("/", 1)[1])
    if os.path.exists(path) and os.path.getsize(path) > 0:
        return path
    for attempt in range(5):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": "tristinwold-asset-fetch"})
            data = urllib.request.urlopen(req, timeout=60).read()
            with open(path, "wb") as f:
                f.write(data)
            return path
        except Exception as e:  # network hiccup: back off and retry
            print(f"  retry {url}: {e}", file=sys.stderr)
            time.sleep(2 ** attempt)
    raise SystemExit(f"could not download {url}")


def files(asset, cache):
    """Poly Haven's list of downloads for an asset."""
    path = os.path.join(cache, asset + ".files.json")
    if not os.path.exists(path):
        req = urllib.request.Request(API + asset, headers={"User-Agent": "tristinwold-asset-fetch"})
        with open(path, "wb") as f:
            f.write(urllib.request.urlopen(req, timeout=60).read())
    return json.load(open(path))


def texture_maps(asset, cache):
    """Diffuse, OpenGL normal and AO/roughness/metal maps for a Poly Haven texture, at 1k."""
    d = files(asset, cache)
    pick = lambda key: Image.open(fetch(d[key]["1k"]["jpg"]["url"], cache)).convert("RGB").resize((SIZE, SIZE))
    return pick("Diffuse"), pick("nor_gl"), pick("arm")


def model_map(asset, key, cache):
    d = files(asset, cache)
    return fetch(d[key]["1k"]["png" if key.endswith("alpha") else "jpg"]["url"], cache)


def save_jpg(img, path, quality=90):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, quality=quality, optimize=True)


def import_file(path, importer, params):
    """Writes the Godot .import file that says how to import an image (Godot fills in the rest)."""
    with open(path + ".import", "w") as f:
        f.write(f'[remap]\n\nimporter="{importer}"\n\n[params]\n\n')
        for k, v in params.items():
            f.write(f"{k}={v}\n")


def ground(cache):
    albedo = Image.new("RGB", (SIZE, SIZE * len(GROUND)))
    normal = Image.new("RGB", (SIZE, SIZE * len(GROUND)))
    arm = Image.new("RGB", (SIZE, SIZE * len(GROUND)))
    for i, (asset, what) in enumerate(GROUND):
        print("ground", i, asset, "-", what)
        d, n, a = texture_maps(asset, cache)
        albedo.paste(d, (0, i * SIZE))
        normal.paste(n, (0, i * SIZE))
        arm.paste(a, (0, i * SIZE))
    for name, img in (("albedo", albedo), ("normal", normal), ("arm", arm)):
        path = os.path.join(OUT, "ground", name + ".jpg")
        save_jpg(img, path, 88)
        import_file(path, "2d_array_texture", {
            "compress/mode": 2,  # VRAM compressed: S3TC/BPTC on PCs, ETC2/ASTC on phones
            "compress/channel_pack": 0,
            "mipmaps/generate": "true",
            "slices/horizontal": 1,
            "slices/vertical": len(GROUND),
        })


def surfaces(cache):
    for name, asset in SURFACES.items():
        print("surface", name, asset)
        d, n, a = texture_maps(asset, cache)
        for img, kind in ((d, "diff"), (n, "nor"), (a, "arm")):
            path = os.path.join(OUT, "surfaces", f"{name}_{kind}.jpg")
            save_jpg(img, path)
            texture_import(path, normal=kind == "nor")


def texture_import(path, normal=False):
    """A texture for 3D: compressed for the graphics card, with mipmaps (and kept as a normal map)."""
    import_file(path, "texture", {
        "compress/mode": 2,
        "compress/normal_map": 1 if normal else 2,
        "mipmaps/generate": "true",
        "detect_3d/compress_to": 0,
    })


# ---- foliage cards ---------------------------------------------------------------------------

def cutout(diff_path, alpha_path, box):
    """One sprig or leaf from a scan's atlas, as an RGBA image pointing up."""
    diff = Image.open(diff_path).convert("RGB")
    alpha = Image.open(alpha_path).convert("L")
    if diff.size != alpha.size:
        alpha = alpha.resize(diff.size)
    piece = diff.crop(box)
    a = alpha.crop(box)
    # Drop the black background the scan's alpha slightly overlaps, then keep only the piece that
    # touches the bottom middle (its stem), not bits of neighbours that fall inside the box.
    a = ImageChops.multiply(a, piece.convert("L").point(lambda v: 255 if v > 14 else 0))
    grown = a.point(lambda v: 255 if v > 64 else 0).filter(ImageFilter.MaxFilter(5))
    w, h = grown.size
    seed = next(((x, y) for y in range(h - 1, h // 2, -1) for x in sorted(range(w), key=lambda x: abs(x - w // 2))
                 if grown.getpixel((x, y)) == 255), None)
    if seed:
        ImageDraw.floodfill(grown, seed, 128)
        a = ImageChops.multiply(a, grown.point(lambda v: 255 if v == 128 else 0))
    piece.putalpha(a)
    return piece


def bleed(img, passes=24):
    """Spreads the colours of a cut-out into its transparent surroundings, so mipmaps don't fringe dark."""
    rgb = img.convert("RGB")
    alpha = img.getchannel("A")
    mask = alpha.point(lambda v: 255 if v > 8 else 0)
    filled = Image.new("RGB", img.size, (0, 0, 0))
    filled.paste(rgb, mask=mask)
    have = mask
    for _ in range(passes):
        grown = filled.filter(ImageFilter.MaxFilter(3))
        grown_mask = have.filter(ImageFilter.MaxFilter(3))
        new = ImageChops.subtract(grown_mask, have)
        filled.paste(grown, mask=new)
        have = grown_mask
    background = Image.new("RGB", img.size, (60, 70, 45))
    background.paste(filled, mask=have)
    out = background.convert("RGBA")
    out.putalpha(alpha)
    return out


def paste_rotated(card, piece, base, angle_deg, length):
    """Places a cut-out with its base at `base`, pointing along `angle_deg` (0 = right), `length` px long."""
    w, h = piece.size
    scale = length / h
    piece = piece.resize((max(1, int(w * scale)), max(1, int(h * scale))), Image.Resampling.LANCZOS)
    w, h = piece.size
    # Pad so the base (bottom middle) sits at the centre, then rotate about it.
    padded = Image.new("RGBA", (max(w, 2 * h) + 4, 2 * h + 4), (0, 0, 0, 0))
    ox = (padded.width - w) // 2
    padded.paste(piece, (ox, padded.height // 2 - h), piece)
    rotated = padded.rotate(angle_deg - 90, resample=Image.Resampling.BICUBIC, expand=False)
    card.alpha_composite(rotated, (int(base[0] - rotated.width / 2), int(base[1] - rotated.height / 2)))


def fir_branch(cache):
    """A fir branch card: a twig with sprigs either side, thinning to the tip. Left edge meets the trunk."""
    diff = model_map("fir_tree_01", "twig_diff", cache)
    alpha = model_map("fir_tree_01", "twig_alpha", cache)
    boxes = [(670, 42, 958, 376), (314, 412, 652, 792), (652, 464, 976, 848), (194, 48, 436, 318), (508, 266, 644, 408)]
    sprigs = [cutout(diff, alpha, b) for b in boxes]
    rng = random.Random(3)
    card = Image.new("RGBA", (1024, 512), (0, 0, 0, 0))
    draw = ImageDraw.Draw(card)
    stem = lambda t: (20 + t * 960, 256 + 30 * t * t)  # droops a little toward the tip
    for i in range(40):
        t0, t1 = i / 40, (i + 1) / 40
        draw.line([stem(t0), stem(t1)], fill=(58, 44, 32, 255), width=max(2, int(12 * (1 - t0))))
    steps = 28
    for i in range(steps):
        t = 0.05 + 0.9 * i / steps
        side = 1 if i % 2 == 0 else -1
        length = 360 * (1 - t * 0.5) * (0.85 + rng.random() * 0.3)
        angle = side * (40 + rng.random() * 20)  # leaning toward the tip
        paste_rotated(card, rng.choice(sprigs[:3]), stem(t), -angle, length)
    paste_rotated(card, sprigs[0], stem(0.97), 4, 200)
    return bleed(card)


def leaf_cards(cache):
    diff = model_map("island_tree_02", "leaves_diff", cache)
    alpha = model_map("island_tree_02", "leaves_alpha", cache)
    boxes = [(18, 22, 146, 522), (170, 22, 344, 400), (712, 22, 834, 422), (370, 34, 492, 368),
             (530, 52, 662, 370), (222, 594, 362, 1022), (434, 618, 572, 1022), (18, 646, 184, 1022)]
    leaves = [cutout(diff, alpha, b) for b in boxes]

    def cluster(tint, seed):
        rng = random.Random(seed)
        card = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
        for _ in range(60):
            a = rng.random() * math.tau
            r = rng.random() ** 0.6 * 170
            base = (256 + math.cos(a) * r * 0.6, 256 + math.sin(a) * r * 0.6)
            leaf = rng.choice(leaves)
            if tint:
                leaf = recolour(leaf, rng.choice(tint), rng)
            spread = 50 if rng.random() < 0.6 else 180  # most point outward, some cross the middle
            paste_rotated(card, leaf, base, -math.degrees(a) + rng.uniform(-spread, spread), rng.uniform(80, 130))  # screen y runs down
        return bleed(card)

    autumn = [(214, 120, 40), (226, 168, 52), (180, 64, 34), (200, 150, 60), (150, 110, 40)]
    return cluster(None, 5), cluster(autumn, 6)


def recolour(leaf, rgb, rng):
    """Turns a green leaf autumn-coloured, keeping its veins and shading."""
    grey = leaf.convert("L")
    tinted = Image.merge("RGB", [grey.point(lambda v, c=c: min(255, int(v * c / 110 * rng.uniform(0.9, 1.1)))) for c in rgb])
    tinted.putalpha(leaf.getchannel("A"))
    return tinted


def foliage(cache):
    folder = os.path.join(OUT, "foliage")
    os.makedirs(folder, exist_ok=True)
    print("foliage: fir branch")
    fir_branch(cache).save(os.path.join(folder, "fir_branch.png"), optimize=True)
    print("foliage: leaves")
    green, autumn = leaf_cards(cache)
    green.save(os.path.join(folder, "leaves_green.png"), optimize=True)
    autumn.save(os.path.join(folder, "leaves_autumn.png"), optimize=True)
    for key, name in (("bark_diff", "bark_diff"), ("bark_nor_gl", "bark_nor"), ("bark_arm", "bark_arm")):
        img = Image.open(model_map("pine_tree_01", key, cache)).convert("RGB")
        save_jpg(img, os.path.join(folder, name + ".jpg"))
        texture_import(os.path.join(folder, name + ".jpg"), normal=name == "bark_nor")
    for name in ("fir_branch", "leaves_green", "leaves_autumn", "grass"):
        texture_import(os.path.join(folder, name + ".png"))


def credits():
    with open(os.path.join(OUT, "CREDITS.md"), "w") as f:
        f.write("# Poly Haven assets\n\nAll from https://polyhaven.com, released CC0 (public domain).\n\n")
        f.write("Ground layers: " + ", ".join(a for a, _ in GROUND) + ".\n\n")
        f.write("Building surfaces: " + ", ".join(SURFACES.values()) + ".\n\n")
        f.write("Foliage cards are cut from fir_tree_01 (sprigs) and island_tree_02 (leaves); bark from pine_tree_01.\n")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--cache", default=os.path.join(ROOT, ".polyhaven-cache"))
    p.add_argument("--only", choices=["ground", "surfaces", "foliage"])
    args = p.parse_args()
    os.makedirs(args.cache, exist_ok=True)
    if args.only in (None, "ground"):
        ground(args.cache)
    if args.only in (None, "surfaces"):
        surfaces(args.cache)
    if args.only in (None, "foliage"):
        foliage(args.cache)
    credits()


if __name__ == "__main__":
    main()

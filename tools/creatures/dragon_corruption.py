#!/usr/bin/env python3
"""
Corrupts the ten dragon coats (made by dragon_colours.py): each dragon's own colour bled into a vivid Rift violet,
with veins of molten gold running through the hide (darker where they cut in) and eyes burning violet-white.
Writes, for each colour, the coat (<colour>_corrupt.png) and what glows on it (<colour>_corrupt_glow.png, the
emission map: the gold veins and the eyes).

  python3 tools/creatures/dragon_corruption.py path/to/dragon_oga/dragon.png
"""
import colorsys
import os
import sys

import numpy as np
from PIL import Image

DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "game", "assets", "creatures", "dragons")
COLOURS = ["red", "blue", "green", "black", "white", "gold", "silver", "bronze", "copper", "brass"]
IMPORT = '[remap]\n\nimporter="texture"\n\n[params]\n\ncompress/mode=2\nmipmaps/generate=true\ndetect_3d/compress_to=0\n'


def ridged(w, h, seed, sharp=0.93):
    """
    Veins: the contour lines of a smooth random field, so they run long and curving like the gold streaks on a
    Rift-corrupted hide. Higher sharp gives thinner lines; lower, a wider band (for the darkened rim).
    """
    rng = np.random.default_rng(seed)
    field = np.zeros((h, w))
    for cells, weight in ((4, 1.0), (9, 0.45), (20, 0.15)):
        small = Image.fromarray((rng.random((cells, cells)) * 255).astype(np.uint8))
        field += np.asarray(small.resize((w, h), Image.Resampling.BICUBIC), dtype=float) / 255 * weight
    field /= 1.6
    lines = np.zeros((h, w))
    for level in (0.3, 0.4, 0.5, 0.6, 0.7):
        lines = np.maximum(lines, 1 - np.abs(field - level) * 14)
    return np.clip((lines - sharp) / (1 - sharp), 0, 1)


def eyes(src, size):
    """Where the eyes are, from the original texture (they're the only bright, saturated amber there)."""
    s = src.convert("RGB")
    mask = Image.new("L", s.size)
    ps, pm = s.load(), mask.load()
    for y in range(s.height):
        for x in range(s.width):
            r, g, b = ps[x, y]
            h, sat, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if sat > 0.6 and v > 0.6 and 0.08 < h < 0.2:
                pm[x, y] = 255
    return np.asarray(mask.resize(size, Image.Resampling.BILINEAR), dtype=float) / 255


def main():
    src = Image.open(sys.argv[1])
    for i, name in enumerate(COLOURS):
        coat = Image.open(os.path.join(DIR, f"{name}.png")).convert("RGBA")
        a = np.asarray(coat, dtype=float)
        rgb, alpha = a[..., :3] / 255, a[..., 3:]
        lum = rgb @ np.array([0.3, 0.59, 0.11])
        violet = np.array([0.48, 0.18, 0.78])
        base = (rgb * 0.4 + (np.maximum(lum, 0.3) ** 0.7)[..., None] * violet * 0.9) * 0.9  # its own colour, bled into violet
        veins = ridged(coat.width, coat.height, 17 + i)
        rim = np.clip(ridged(coat.width, coat.height, 17 + i, sharp=0.55) - veins, 0, 1)
        eye = eyes(src, coat.size)
        gold = np.array([1.0, 0.8, 0.45])
        base = base * (1 - rim[..., None] * 0.45)                       # the hide darkens where the veins cut in
        base = base * (1 - veins[..., None]) + veins[..., None] * gold * 0.85
        base = base * (1 - eye[..., None]) + eye[..., None] * np.array([0.9, 0.7, 1.0])
        glow = veins[..., None] * gold * 0.75 + eye[..., None] * np.array([0.85, 0.6, 1.0])
        out = Image.fromarray(np.concatenate([np.clip(base, 0, 1) * 255, alpha], axis=-1).astype(np.uint8), "RGBA")
        glow_img = Image.fromarray((np.clip(glow, 0, 1) * 255).astype(np.uint8), "RGB")
        for img, suffix in ((out, "_corrupt"), (glow_img, "_corrupt_glow")):
            path = os.path.join(DIR, f"{name}{suffix}.png")
            img.save(path, optimize=True)
            with open(path + ".import", "w") as f:
                f.write(IMPORT)
        print("wrote", name)


if __name__ == "__main__":
    main()

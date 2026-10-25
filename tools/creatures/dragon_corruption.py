#!/usr/bin/env python3
"""
Corrupts the ten dragon coats (made by dragon_colours.py): the hide darkened towards a bruised violet-black,
with veins of Rift-light running through it and eyes burning magenta. Writes, for each colour, the coat
(<colour>_corrupt.png) and what glows on it (<colour>_corrupt_glow.png, used as the emission map).

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


def ridged(w, h, seed):
    """Branching veins: ridged fractal noise, sharpened so only the creases show."""
    rng = np.random.default_rng(seed)
    total = np.zeros((h, w))
    amp = 1.0
    for cells in (6, 12, 24, 48):
        small = Image.fromarray((rng.random((cells, cells)) * 255).astype(np.uint8))
        layer = np.asarray(small.resize((w, h), Image.Resampling.BICUBIC), dtype=float) / 255
        total += (1 - np.abs(layer - 0.5) * 2) * amp
        amp *= 0.55
    total /= total.max()
    return np.clip((total - 0.62) / 0.38, 0, 1) ** 3


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
        bruise = np.array([0.30, 0.12, 0.40])
        base = rgb * 0.1 + lum[..., None] * bruise * 0.6  # a trace of its own colour, drowned in violet-black
        veins = ridged(coat.width, coat.height, 17 + i)
        eye = eyes(src, coat.size)
        light = np.array([0.85, 0.25, 1.0])
        base = base * (1 - veins[..., None] * 0.6) + veins[..., None] * light * 0.55
        base = base * (1 - eye[..., None]) + eye[..., None] * np.array([1.0, 0.3, 0.9])
        glow = veins[..., None] * light * 0.9 + eye[..., None] * np.array([1.0, 0.35, 1.0])
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

#!/usr/bin/env python3
"""
Coats for the bosses of the wild lairs, recoloured from the creatures' own textures:

  dragons/amethyst.png     the Crystal Basilisk: the pale dragon hide turned deep amethyst, its spines and ridges
                           lit lavender (glow: dragons/amethyst_glow.png).
  ice_wolf_shadow.png      the Duskprowler: fur gone black-violet, veined with violet light (glow: _glow).
  wolf_shadow.png          its shadow-wolves, the same.
  hawk_storm.png           the Thunderhawk: storm-blue feathers, white at the tips, forked with faint lightning (glow).

  python3 tools/creatures/lair_coats.py
"""
import os

import numpy as np
from PIL import Image

from dragon_corruption import IMPORT, ridged

DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "game", "assets", "creatures")


def ramp(lum, stops):
    """Maps brightness (0..1) along a colour ramp of (at, (r, g, b)) stops."""
    at = np.array([s[0] for s in stops])
    out = np.zeros(lum.shape + (3,))
    for c in range(3):
        out[..., c] = np.interp(lum, at, [s[1][c] for s in stops])
    return out


def load(name):
    img = Image.open(os.path.join(DIR, name)).convert("RGBA")
    a = np.asarray(img, dtype=float) / 255
    return img, a[..., :3], a[..., 3:]


def save(name, rgb, alpha=None):
    rgb = np.clip(rgb, 0, 1) * 255
    img = Image.fromarray(np.concatenate([rgb, alpha * 255], axis=-1).astype(np.uint8), "RGBA") if alpha is not None \
        else Image.fromarray(rgb.astype(np.uint8), "RGB")
    path = os.path.join(DIR, name)
    img.save(path, optimize=True)
    with open(path + ".import", "w") as f:
        f.write(IMPORT)
    print("wrote", name)


def lum_of(rgb):
    return rgb @ np.array([0.3, 0.59, 0.11])


def amethyst():
    img, rgb, alpha = load("dragons/white.png")
    lum = lum_of(rgb)
    coat = ramp(lum, [(0, (0.06, 0.02, 0.1)), (0.35, (0.2, 0.08, 0.32)), (0.65, (0.46, 0.26, 0.7)), (0.85, (0.72, 0.56, 0.95)), (1, (0.92, 0.85, 1))])
    # The brightest of the hide (its spines and plates) is crystal: it glows from within.
    crystal = np.clip((lum - 0.72) / 0.2, 0, 1)
    save("dragons/amethyst.png", coat, alpha)
    save("dragons/amethyst_glow.png", crystal[..., None] * np.array([0.6, 0.35, 0.95]) * 0.8)


def shadow(name, seed):
    img, rgb, alpha = load(f"{name}_texture.png")
    lum = lum_of(rgb)
    coat = ramp(lum, [(0, (0.02, 0.015, 0.03)), (0.5, (0.07, 0.05, 0.1)), (1, (0.2, 0.15, 0.28))])
    veins = ridged(img.width, img.height, seed, sharp=0.9)
    violet = np.array([0.7, 0.3, 1.0])
    coat = coat * (1 - veins[..., None]) + veins[..., None] * violet * 0.8
    save(f"{name}_shadow.png", coat, alpha)
    save(f"{name}_shadow_glow.png", veins[..., None] * violet)


def storm():
    img, rgb, alpha = load("hawk_texture.png")
    lum = lum_of(rgb)
    lo, hi = np.percentile(lum, 5), np.percentile(lum, 97)
    lum = np.clip((lum - lo) / (hi - lo), 0, 1)
    coat = ramp(lum, [(0, (0.06, 0.1, 0.26)), (0.4, (0.16, 0.3, 0.62)), (0.75, (0.45, 0.65, 0.95)), (1, (0.92, 0.96, 1))])
    bolts = ridged(img.width, img.height, 91, sharp=0.94)
    cyan = np.array([0.55, 0.85, 1.0])
    save("hawk_storm.png", coat * (1 - bolts[..., None] * 0.5) + bolts[..., None] * cyan * 0.6, alpha)
    save("hawk_storm_glow.png", bolts[..., None] * cyan * 0.7)


if __name__ == "__main__":
    amethyst()
    shadow("ice_wolf", 31)
    shadow("wolf", 37)
    storm()

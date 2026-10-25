#!/usr/bin/env python3
"""
Coats for the bosses of the wild lairs, recoloured from the creatures' own textures:

  dragons/amethyst.png     the Crystal Basilisk: the pale dragon hide turned deep amethyst, its spines and ridges
                           lit lavender (glow: dragons/amethyst_glow.png).
  ice_wolf_shadow.png      the Duskprowler: fur gone black-violet, veined with violet light (glow: _glow).
  wolf_shadow.png          its shadow-wolves, the same.
  hawk_storm.png           the Thunderhawk: storm-blue feathers, white at the tips, forked with faint lightning (glow).

And for the stranger things of the wilds:

  troll_deathcrest.png     deathcrest apes: pale grey hide banded with dark stripes, mottled.
  troll_wingedape.png      winged apes: black fur.
  goblin_direimp.png       dire imps: red-brown leathery hide; their eyes burn blue (glow: _glow).
  goblin_imp.png           imps: crimson; eyes the same.
  bear_kipine.png          the kipine: its owlbear fur darkened to a smoky brown-grey.

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


def striped():
    img, rgb, alpha = load("troll_troll_baseTexBaked.png")
    lum = lum_of(rgb)
    solid = alpha[..., 0] > 0.5
    lo, hi = np.percentile(lum[solid], 5), np.percentile(lum[solid], 95)
    lum = np.clip((lum - lo) / (hi - lo), 0, 1)
    hide = ramp(lum, [(0, (0.42, 0.4, 0.38)), (0.6, (0.7, 0.68, 0.64)), (1, (0.86, 0.84, 0.8))])
    bands = ridged(img.width, img.height, 53, sharp=0.55)
    spots = ridged(img.width, img.height, 59, sharp=0.8)
    dark = np.array([0.14, 0.11, 0.1])
    hide = hide * (1 - bands[..., None] * 0.85) + bands[..., None] * dark * 0.85
    save("troll_deathcrest.png", hide * (1 - spots[..., None] * 0.3), alpha)
    fur = ramp(lum, [(0, (0.03, 0.03, 0.03)), (0.6, (0.09, 0.085, 0.08)), (1, (0.2, 0.18, 0.16))])
    save("troll_wingedape.png", fur * (1 - bands[..., None] * 0.3), alpha)


def devils():
    img, rgb, alpha = load("goblin_goblin-tex.png")
    lum = lum_of(rgb)
    r, g = rgb[..., 0], rgb[..., 1]
    eyes = np.clip((r - g - 0.25) * 4, 0, 1) * np.clip((r - 0.5) * 3, 0, 1)  # the pink-red of the goblin's eyes
    blue = np.array([0.45, 0.85, 1.0])
    for name, stops in (("direimp", [(0, (0.12, 0.04, 0.03)), (0.5, (0.42, 0.15, 0.08)), (1, (0.78, 0.42, 0.26))]),
                        ("imp", [(0, (0.16, 0.02, 0.05)), (0.5, (0.52, 0.08, 0.16)), (1, (0.9, 0.35, 0.42))])):
        hide = ramp(lum, stops)
        save(f"goblin_{name}.png", hide * (1 - eyes[..., None]) + eyes[..., None] * blue, alpha)
        save(f"goblin_{name}_glow.png", eyes[..., None] * blue * 1.2)


def kipine():
    img, rgb, alpha = load("bear_texture.png")
    lum = lum_of(rgb)
    save("bear_kipine.png", ramp(lum, [(0, (0.04, 0.035, 0.035)), (0.4, (0.2, 0.16, 0.15)), (1, (0.62, 0.55, 0.52))]), alpha)


if __name__ == "__main__":
    amethyst()
    shadow("ice_wolf", 31)
    shadow("wolf", 37)
    storm()
    striped()
    devils()
    kipine()

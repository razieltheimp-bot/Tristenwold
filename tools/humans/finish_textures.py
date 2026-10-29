#!/usr/bin/env python3
"""Finishes the textures bake_humans.gd wrote into game/assets/humans/tex/: hair, eyebrow and eyelash textures are
turned grey (keeping their alpha), brightened so the game's hair colours tint them properly, and hair is brought
down to 1024 wide; skins are left as they are; hair textures no baked body wears are removed. Run after a bake:
    python3 tools/humans/finish_textures.py
"""
import json
import struct
import sys
from pathlib import Path

from PIL import Image, ImageOps

HUMANS = Path(__file__).resolve().parents[2] / "game" / "assets" / "humans"
TEX = HUMANS / "tex"
TINTED = ("hair-", "lefteyebrow-", "righteyebrow-", "lefteyelash", "righteyelash")
HAIR_WIDTH = 1024


def grey(path: Path) -> None:
    img = Image.open(path).convert("RGBA")
    if path.name.startswith("hair-") and img.width > HAIR_WIDTH:
        img = img.resize((HAIR_WIDTH, img.height * HAIR_WIDTH // img.width), Image.LANCZOS)
    alpha = img.getchannel("A")
    lum = ImageOps.grayscale(img)
    lo, hi = lum.getextrema()
    if hi > lo:
        # Stretch so the lightest strands are near white: the hair colour then comes through as it is.
        lum = lum.point(lambda v, lo=lo, hi=hi: int(40 + (v - lo) * 215 / (hi - lo)))
    Image.merge("LA", (lum, alpha)).save(path, optimize=True)


# The eyes people come in: the iris overlay coloured, laid on the eyeball.
EYES = {"brown": (92, 58, 34), "hazel": (128, 96, 48), "blue": (70, 110, 150), "green": (84, 120, 70), "grey": (120, 124, 130), "amber": (170, 110, 40), "red": (140, 40, 30)}


def eyes() -> int:
    base = TEX / "lefteye-lowpolyeyeball__eyeball.png"
    iris = TEX / "lefteye-lowpolyeyeball__iris.png"
    if not base.is_file() or not iris.is_file():
        return 0
    ball = Image.open(base).convert("RGBA")
    overlay = Image.open(iris).convert("RGBA").resize(ball.size, Image.LANCZOS)
    lum = ImageOps.grayscale(overlay)
    alpha = overlay.getchannel("A")
    for name, (r, g, b) in EYES.items():
        # The overlay's light is the iris's weave, its dark the pupil: the colour goes in by the light.
        coloured = Image.merge("RGBA", (lum.point(lambda v, c=r: c * v // 160), lum.point(lambda v, c=g: c * v // 160), lum.point(lambda v, c=b: c * v // 160), alpha))
        out = ball.copy()
        out.alpha_composite(coloured)
        out.convert("RGB").save(TEX / f"eyes__{name}.jpg", quality=92)
    iris.unlink()
    return len(EYES)


def worn() -> set[str]:
    """The texture files the baked bodies name in their surface tags ("Hair-Bob01|hair-bob01__gray.png")."""
    files: set[str] = set()
    for glb in HUMANS.glob("*_*_*.glb"):
        data = glb.read_bytes()
        length = struct.unpack("<I", data[12:16])[0]
        doc = json.loads(data[20:20 + length])
        for material in doc.get("materials", []):
            name = material.get("name", "")
            if "|" in name:
                file = name.split("|", 1)[1]
                if len(file) > 4 and file[-4] == "_":
                    file = file[:-4] + "." + file[-3:]  # the glTF export makes "eyeball.png" into "eyeball_png"
                files.add(file)
    return files


def main() -> int:
    if not TEX.is_dir():
        print(f"no textures at {TEX}", file=sys.stderr)
        return 1
    keyed = eyes()
    keep = worn()
    done = pruned = 0
    for path in sorted(TEX.iterdir()):
        if path.suffix == ".import":
            continue  # Godot's own; it makes them again
        if path.name.startswith("hair-") and keep and path.name not in keep:
            path.unlink()
            pruned += 1
            continue
        if path.suffix.lower() == ".png" and path.name.startswith(TINTED):
            grey(path)
            done += 1
    print(f"{done} hair, brow and lash textures made grey for tinting; {pruned} hair textures nobody wears removed; eyes in {keyed} colours")
    return 0


if __name__ == "__main__":
    sys.exit(main())

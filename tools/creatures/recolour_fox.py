#!/usr/bin/env python3
"""
Repaints the Khronos sample fox's coat (CC0 model by PixelMannen) as a grey wolf and an ice wolf.
The fox's texture has three paints: orange fur, white chest and tail tip, dark brown legs. Each
is swapped for the new animal's colour, keeping the original's light and shade.

  python3 tools/creatures/recolour_fox.py path/to/Fox/glTF/Texture.png
"""
import colorsys
import os
import sys

from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "game", "assets", "creatures")

COATS = {
    #            orange fur        white parts       dark legs
    "wolf": ((112, 106, 98), (196, 190, 178), (52, 48, 44)),
    "ice_wolf": ((214, 222, 230), (246, 248, 250), (150, 160, 170)),
}


def paint(src, coat):
    fur, white, dark = coat
    out = Image.new("RGB", src.size)
    px, po = src.load(), out.load()
    for y in range(src.height):
        for x in range(src.width):
            r, g, b = px[x, y]
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if s < 0.25 and v > 0.7:
                base, ref = white, 0.92
            elif v < 0.42:
                base, ref = dark, 0.3
            elif 0.03 < h < 0.14 and s > 0.4:
                base, ref = fur, 0.86
            else:
                po[x, y] = (r, g, b)  # the background between pieces
                continue
            k = min(1.25, v / ref)
            po[x, y] = tuple(min(255, int(c * k)) for c in base)
    return out


def main():
    src = Image.open(sys.argv[1]).convert("RGB")
    os.makedirs(OUT, exist_ok=True)
    for name, coat in COATS.items():
        paint(src, coat).save(os.path.join(OUT, f"{name}_coat.png"), optimize=True)
        print("wrote", name)


if __name__ == "__main__":
    main()

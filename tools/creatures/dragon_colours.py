#!/usr/bin/env python3
"""
Paints the dragon (Cethiel's design, CC0) in the ten dragon colours: red, blue, green, black and white
(chromatic) and gold, silver, bronze, copper and brass (metallic). The texture has three paints: the
scaled hide, the tan belly and horns, and the dark spines. Each is swapped for the colour's own, keeping
the original's light and shade; the eyes stay as they are.

  python3 tools/creatures/dragon_colours.py path/to/dragon_oga/dragon.png
"""
import colorsys
import os
import sys

from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "game", "assets", "creatures", "dragons")

#             hide                belly and horns      spines
COLOURS = {
    "red": ((150, 26, 18), (214, 160, 90), (40, 18, 16)),
    "blue": ((32, 70, 170), (150, 196, 230), (20, 26, 48)),
    "green": ((44, 124, 52), (236, 228, 204), (176, 66, 34)),  # emerald, a cream belly, red-orange spines
    "black": ((30, 28, 34), (96, 92, 80), (12, 12, 14)),
    "white": ((220, 230, 240), (240, 246, 252), (150, 176, 200)),
    "gold": ((214, 160, 48), (250, 224, 140), (130, 86, 20)),
    "silver": ((176, 182, 194), (236, 240, 246), (110, 116, 128)),
    "bronze": ((140, 88, 40), (200, 150, 90), (60, 70, 50)),
    "copper": ((184, 98, 52), (90, 170, 150), (90, 44, 24)),
    "brass": ((190, 152, 70), (240, 214, 150), (110, 84, 36)),
}


def paint(src, colours):
    hide, belly, spines = colours
    out = Image.new("RGBA", src.size)
    ps, po = src.load(), out.load()
    for y in range(src.height):
        for x in range(src.width):
            r, g, b, a = ps[x, y]
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if s > 0.6 and v > 0.6 and 0.08 < h < 0.2:
                po[x, y] = (r, g, b, a)  # the eyes
                continue
            if 0.06 < h < 0.16 and s > 0.22 and v > 0.38:
                base, ref = belly, 0.62
            elif s < 0.18 or (0.2 < h < 0.6):
                base, ref = spines, 0.35
            else:
                base, ref = hide, 0.42
            k = max(0.0, min(1.6, v / ref))
            po[x, y] = tuple(min(255, int(c * k)) for c in base) + (a,)
    return out.resize((src.width * 2, src.height * 2), Image.Resampling.BICUBIC)


def main():
    src = Image.open(sys.argv[1]).convert("RGBA")
    os.makedirs(OUT, exist_ok=True)
    for name, colours in COLOURS.items():
        path = os.path.join(OUT, f"{name}.png")
        paint(src, colours).save(path, optimize=True)
        with open(path + ".import", "w") as f:
            f.write('[remap]\n\nimporter="texture"\n\n[params]\n\ncompress/mode=2\nmipmaps/generate=true\ndetect_3d/compress_to=0\n')
        print("wrote", name)


if __name__ == "__main__":
    main()

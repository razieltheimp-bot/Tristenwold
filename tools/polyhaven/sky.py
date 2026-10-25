#!/usr/bin/env python3
"""
Finds where the sun is in a Poly Haven sky (a Radiance .hdr panorama), so the game's sunlight can come from the
same place as the bright spot in the sky, and writes a small preview. Prints the sun's elevation and azimuth.

  python3 tools/polyhaven/sky.py game/assets/polyhaven/sky/sky.hdr [preview.jpg]
"""
import math
import sys

import numpy as np
from PIL import Image


def read_hdr(path):
    data = open(path, "rb").read()
    header_end = data.index(b"\n\n") + 2
    line_end = data.index(b"\n", header_end)
    dims = data[header_end:line_end].split()
    h, w = int(dims[1]), int(dims[3])
    pos = line_end + 1
    out = np.zeros((h, w, 4), np.uint8)
    for y in range(h):
        assert data[pos] == 2 and data[pos + 1] == 2, "only new-style RLE is read"
        pos += 4
        for c in range(4):
            x = 0
            while x < w:
                n = data[pos]
                pos += 1
                if n > 128:
                    n -= 128
                    out[y, x:x + n, c] = data[pos]
                    pos += 1
                else:
                    out[y, x:x + n, c] = np.frombuffer(data[pos:pos + n], np.uint8)
                    pos += n
                x += n
    e = out[..., 3].astype(np.int32)
    scale = np.where(e > 0, np.ldexp(1.0, e - 136), 0)
    return out[..., :3] * scale[..., None]


def main():
    rgb = read_hdr(sys.argv[1])
    h, w, _ = rgb.shape
    lum = rgb @ np.array([0.2126, 0.7152, 0.0722])
    y, x = np.unravel_index(np.argmax(lum), lum.shape)
    elevation = 90 - (y + 0.5) / h * 180
    azimuth = (x + 0.5) / w * 360  # around the panorama from its left edge
    print(f"sun at pixel ({x}, {y}): elevation {elevation:.1f} degrees, azimuth {azimuth:.1f} degrees, peak {lum.max():.0f}")
    if len(sys.argv) > 2:
        ldr = (rgb / (1 + rgb)) ** (1 / 2.2)
        Image.fromarray((np.clip(ldr, 0, 1) * 255).astype(np.uint8)).resize((1024, 512)).save(sys.argv[2])


if __name__ == "__main__":
    main()

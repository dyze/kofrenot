# Regenerates src/kofrenot.App/Assets/kofrenot.ico (needs Python 3 and Pillow):
#   python build/make_icon.py src/kofrenot.App/Assets/kofrenot.ico preview.png
"""Draws kofrenot.ico: a white note with a folded corner and text lines on a rounded teal square.
Each size is drawn 8x larger and scaled down; small sizes get fewer, thicker lines so they stay readable."""
import sys
from PIL import Image, ImageDraw

TEAL = (13, 148, 136, 255)
WHITE = (255, 255, 255, 255)
FOLD = (204, 236, 232, 255)


def frame(size):
    s = size * 8
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pad = s * 0.04
    d.rounded_rectangle([pad, pad, s - pad, s - pad], radius=s * 0.22, fill=TEAL)

    left, top, right, bottom = s * 0.26, s * 0.18, s * 0.74, s * 0.82
    fold = s * 0.16
    d.polygon([(left, top), (right - fold, top), (right, top + fold), (right, bottom), (left, bottom)], fill=WHITE)
    d.polygon([(right - fold, top), (right - fold, top + fold), (right, top + fold)], fill=FOLD)

    lines = 2 if size <= 20 else 3
    stroke = s * (0.07 if size <= 20 else 0.05)
    for i in range(lines):
        y = top + s * 0.24 + i * s * (0.16 if size <= 20 else 0.12)
        end = right - s * 0.08 - (s * 0.10 if i == lines - 1 else 0)
        d.line([left + s * 0.08, y, end, y], fill=TEAL, width=round(stroke))
    return img.resize((size, size), Image.LANCZOS)


sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
frames = [frame(n) for n in sizes]
frames[-1].save(sys.argv[1], format="ICO", sizes=[(n, n) for n in sizes], append_images=frames[:-1])
frame(256).save(sys.argv[2])

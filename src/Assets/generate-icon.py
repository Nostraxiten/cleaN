#!/usr/bin/env python3
"""Generates Assets/cleaN.ico from the same geometry as logo.svg.

The icon is the brand mark: a plain white square with a black capital N.
Pure standard library, so it runs anywhere Python 3 does:

    python3 generate-icon.py
"""
import struct
import zlib
from pathlib import Path

# Same outline as logo.svg, in a 100x100 box.
GLYPH = [
    (20, 80), (20, 20), (34, 20), (66, 58), (66, 20),
    (80, 20), (80, 80), (66, 80), (34, 42), (34, 80),
]
SIZES = [16, 24, 32, 48, 64, 128, 256]
SUPERSAMPLE = 4
BACKGROUND = (255, 255, 255)
FOREGROUND = (0, 0, 0)


def inside(x, y, polygon):
    """Ray casting point-in-polygon test."""
    result = False
    count = len(polygon)
    j = count - 1
    for i in range(count):
        xi, yi = polygon[i]
        xj, yj = polygon[j]
        if (yi > y) != (yj > y):
            crossing = xi + (y - yi) * (xj - xi) / (yj - yi)
            if x < crossing:
                result = not result
        j = i
    return result


def render(size):
    """Returns rows of RGB tuples, top-down, with an antialiased glyph."""
    scale = 100.0 / size
    step = 1.0 / SUPERSAMPLE
    samples = SUPERSAMPLE * SUPERSAMPLE
    rows = []
    for py in range(size):
        row = []
        for px in range(size):
            hits = 0
            for sy in range(SUPERSAMPLE):
                y = (py + (sy + 0.5) * step) * scale
                for sx in range(SUPERSAMPLE):
                    x = (px + (sx + 0.5) * step) * scale
                    if inside(x, y, GLYPH):
                        hits += 1
            coverage = hits / samples
            row.append(tuple(
                round(BACKGROUND[c] * (1 - coverage) + FOREGROUND[c] * coverage) for c in range(3)
            ))
        rows.append(row)
    return rows


def as_bmp(rows):
    """32bpp BGRA DIB entry (BITMAPINFOHEADER + pixels bottom-up + AND mask)."""
    size = len(rows)
    header = struct.pack('<IiiHHIIiiII', 40, size, size * 2, 1, 32, 0, size * size * 4, 0, 0, 0, 0)
    pixels = bytearray()
    for row in reversed(rows):
        for red, green, blue in row:
            pixels += bytes((blue, green, red, 255))
    mask_stride = ((size + 31) // 32) * 4
    return header + bytes(pixels) + bytes(mask_stride * size)


def as_png(rows):
    """Minimal 8-bit RGB PNG."""
    size = len(rows)
    raw = bytearray()
    for row in rows:
        raw.append(0)  # no filter
        for red, green, blue in row:
            raw += bytes((red, green, blue))

    def chunk(tag, payload):
        return (struct.pack('>I', len(payload)) + tag + payload
                + struct.pack('>I', zlib.crc32(tag + payload) & 0xFFFFFFFF))

    return (b'\x89PNG\r\n\x1a\n'
            + chunk(b'IHDR', struct.pack('>IIBBBBB', size, size, 8, 2, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(bytes(raw), 9))
            + chunk(b'IEND', b''))


def main():
    images = []
    for size in SIZES:
        rows = render(size)
        # PNG compression is what keeps the 256px entry small; Windows accepts it since Vista.
        images.append((size, as_png(rows) if size >= 256 else as_bmp(rows)))

    output = bytearray(struct.pack('<HHH', 0, 1, len(images)))
    offset = 6 + 16 * len(images)
    for size, data in images:
        output += struct.pack('<BBBBHHII', size % 256, size % 256, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    for _, data in images:
        output += data

    target = Path(__file__).with_name('cleaN.ico')
    target.write_bytes(bytes(output))
    print(f'Wrote {target} ({len(output)} bytes, {len(images)} sizes)')


if __name__ == '__main__':
    main()

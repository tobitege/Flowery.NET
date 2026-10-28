"""Reads the pixels of a PNG screenshot with the Python standard library.

Used by the CI checks (check_browser_gallery.py, check_android_gallery.py), which compare pixels of screenshots and
should not need Pillow. Supports what Chromium and Android's screencap write: 8 bits per channel, RGB or RGBA, not
interlaced.
"""

import struct
import zlib


class Png:
    def __init__(self, width, height, channels, pixels):
        self.width = width
        self.height = height
        self._channels = channels
        self._pixels = pixels

    def pixel(self, x, y):
        """Returns (red, green, blue) at (x, y)."""
        offset = (y * self.width + x) * self._channels
        return tuple(self._pixels[offset:offset + 3])

    def changed_share(self, other, region=None, tolerance=24):
        """Returns the share of pixels in region (x, y, width, height) that differ by more than tolerance in a channel."""
        if (self.width, self.height) != (other.width, other.height):
            return 1.0
        x0, y0, width, height = region or (0, 0, self.width, self.height)
        changed = 0
        # Every second pixel in both directions is enough for a share and four times faster.
        samples = 0
        for y in range(y0, y0 + height, 2):
            for x in range(x0, x0 + width, 2):
                samples += 1
                if any(abs(a - b) > tolerance for a, b in zip(self.pixel(x, y), other.pixel(x, y))):
                    changed += 1
        return changed / samples if samples else 0.0


def read(data):
    """Decodes a PNG from bytes."""
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("Not a PNG")
    position = 8
    header = None
    compressed = bytearray()
    while position < len(data):
        length, kind = struct.unpack(">I4s", data[position:position + 8])
        chunk = data[position + 8:position + 8 + length]
        position += 12 + length
        if kind == b"IHDR":
            header = struct.unpack(">IIBBBBB", chunk)
        elif kind == b"IDAT":
            compressed += chunk
        elif kind == b"IEND":
            break
    if header is None:
        raise ValueError("PNG without header")
    width, height, depth, color_type, _, _, interlace = header
    channels = {2: 3, 6: 4}.get(color_type)
    if depth != 8 or channels is None or interlace:
        raise ValueError(f"Unsupported PNG: depth {depth}, color type {color_type}, interlace {interlace}")

    raw = zlib.decompress(bytes(compressed))
    stride = width * channels
    pixels = bytearray(height * stride)
    previous = bytearray(stride)
    for row in range(height):
        start = row * (stride + 1)
        kind = raw[start]
        line = bytearray(raw[start + 1:start + 1 + stride])
        if kind == 1:
            for i in range(channels, stride):
                line[i] = (line[i] + line[i - channels]) & 0xFF
        elif kind == 2:
            for i in range(stride):
                line[i] = (line[i] + previous[i]) & 0xFF
        elif kind == 3:
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                line[i] = (line[i] + ((left + previous[i]) >> 1)) & 0xFF
        elif kind == 4:
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                up = previous[i]
                up_left = previous[i - channels] if i >= channels else 0
                estimate = left + up - up_left
                distances = (abs(estimate - left), abs(estimate - up), abs(estimate - up_left))
                predictor = left if distances[0] <= distances[1] and distances[0] <= distances[2] \
                    else up if distances[1] <= distances[2] else up_left
                line[i] = (line[i] + predictor) & 0xFF
        elif kind != 0:
            raise ValueError(f"Unknown PNG filter {kind}")
        pixels[row * stride:(row + 1) * stride] = line
        previous = line
    return Png(width, height, channels, bytes(pixels))

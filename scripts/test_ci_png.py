"""Tests ci_png.py, the PNG reader of the CI checks.

    python3 -B -m unittest discover -s scripts -p "test_*.py"
"""

import random
import struct
import unittest
import zlib

import ci_png


def paeth(left, up, up_left):
    # The predictor as the PNG specification defines it.
    estimate = left + up - up_left
    distance_left, distance_up, distance_up_left = abs(estimate - left), abs(estimate - up), abs(estimate - up_left)
    if distance_left <= distance_up and distance_left <= distance_up_left:
        return left
    return up if distance_up <= distance_up_left else up_left


def encode(width, height, channels, rows, filters, depth=8, interlace=0):
    """Encodes rows of channel bytes as a PNG; row y uses the filter filters[y % len(filters)]."""
    stride = width * channels
    data = bytearray()
    previous = bytes(stride)
    for y, row in enumerate(rows):
        kind = filters[y % len(filters)]
        data.append(kind)
        for i in range(stride):
            left = row[i - channels] if i >= channels else 0
            up = previous[i]
            up_left = previous[i - channels] if i >= channels else 0
            predictor = (0, left, up, (left + up) // 2, paeth(left, up, up_left))[kind]
            data.append((row[i] - predictor) & 0xFF)
        previous = row

    def chunk(kind, payload):
        return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload))

    color_type = {3: 2, 4: 6}[channels]
    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, depth, color_type, 0, 0, interlace))
            + chunk(b"IDAT", zlib.compress(bytes(data))) + chunk(b"IEND", b""))


def random_rows(width, height, channels, seed, values=256, step=1):
    generator = random.Random(seed)
    return [bytes(generator.randrange(values) * step for _ in range(width * channels)) for _ in range(height)]


class Given_PngReader(unittest.TestCase):
    def test_every_row_filter_gives_back_the_pixels(self):
        width, height = 7, 6
        # Few distinct values, as in flat areas of a screenshot, make the distances of the Paeth predictor tie, such as
        # up-left 100, left 50 and up 200, where the predictor has to take up.
        for values, step in ((256, 1), (5, 50)):
            for channels in (3, 4):
                for filters in ([0], [1], [2], [3], [4], [0, 1, 2, 3, 4]):
                    with self.subTest(values=values, channels=channels, filters=filters):
                        rows = random_rows(width, height, channels, channels * 10 + len(filters), values, step)
                        self.assert_decodes(width, height, channels, rows, filters)

    def assert_decodes(self, width, height, channels, rows, filters):
        image = ci_png.read(encode(width, height, channels, rows, filters))
        self.assertEqual((image.width, image.height), (width, height))
        for y in range(height):
            for x in range(width):
                expected = tuple(rows[y][x * channels:x * channels + 3])
                self.assertEqual(image.pixel(x, y), expected, f"pixel {x}, {y}")

    def test_the_changed_share_counts_the_pixels_that_differ_in_the_region(self):
        width, height = 8, 8
        black = [bytes(width * 3) for _ in range(height)]
        # The right half is white.
        half = [bytes([0] * (width // 2 * 3) + [255] * (width // 2 * 3)) for _ in range(height)]
        first = ci_png.read(encode(width, height, 3, black, [0]))
        second = ci_png.read(encode(width, height, 3, half, [0]))
        self.assertEqual(first.changed_share(first), 0.0)
        self.assertEqual(first.changed_share(second), 0.5)
        self.assertEqual(first.changed_share(second, (0, 0, 4, 8)), 0.0)
        self.assertEqual(first.changed_share(second, (4, 0, 4, 8)), 1.0)

    def test_a_different_size_counts_as_changed_entirely(self):
        small = ci_png.read(encode(2, 2, 3, random_rows(2, 2, 3, seed=1), [0]))
        large = ci_png.read(encode(3, 2, 3, random_rows(3, 2, 3, seed=2), [0]))
        self.assertEqual(small.changed_share(large), 1.0)

    def test_what_the_checks_never_get_is_refused(self):
        rows = random_rows(2, 2, 3, seed=3)
        with self.assertRaisesRegex(ValueError, "Unsupported PNG"):
            ci_png.read(encode(2, 2, 3, rows, [0], interlace=1))
        with self.assertRaisesRegex(ValueError, "Not a PNG"):
            ci_png.read(b"GIF89a")


if __name__ == "__main__":
    unittest.main()

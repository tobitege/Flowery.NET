"""Writes the media files for the metadata tests with ffmpeg and ImageMagick.

Run it from this folder: python make_media.py. Each file is a few kilobytes and has known metadata that the tests
check: the size, the recording date, the camera and the GPS position 52.52 N, 13.405 E.
"""
import os
import struct
import subprocess
import tempfile

# Intermediate files go to a folder of their own, outside the repository.
WORK = tempfile.mkdtemp(prefix="flowery-media-")


def work(name):
    return os.path.join(WORK, name)


FFMPEG = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y"]
SOURCE = ["-f", "lavfi", "-i", "testsrc2=size=64x36:rate=10", "-t", "1", "-pix_fmt", "yuv420p"]


def run(*args):
    subprocess.run(list(args), check=True)


def exif():
    """A little-endian EXIF block with make, model, capture date and a GPS position."""
    entries0 = [(0x010F, 2, b"Flowery\0"), (0x0110, 2, b"Test Camera\0"), (0x8769, 4, None), (0x8825, 4, None)]
    date = b"2023:04:05 06:07:08\0"
    gps = [(1, 2, b"N\0"), (2, 5, [(52, 1), (31, 1), (12, 1)]), (3, 2, b"E\0"), (4, 5, [(13, 1), (24, 1), (18, 1)])]
    header = b"II*\0" + struct.pack("<I", 8)
    ifd0_size = 2 + 12 * len(entries0) + 4
    exif_ifd = 8 + ifd0_size
    exif_size = 2 + 12 + 4
    gps_ifd = exif_ifd + exif_size
    gps_size = 2 + 12 * len(gps) + 4
    data_start = gps_ifd + gps_size
    data = b""

    def place(value):
        nonlocal data
        offset = data_start + len(data)
        data += value
        return offset

    def entry(tag, kind, value):
        if kind == 2:
            raw = value if len(value) <= 4 else struct.pack("<I", place(value))
            return struct.pack("<HHI", tag, kind, len(value)) + raw.ljust(4, b"\0")
        if kind == 5:
            offset = place(b"".join(struct.pack("<II", n, d) for n, d in value))
            return struct.pack("<HHII", tag, kind, len(value), offset)
        return struct.pack("<HHII", tag, kind, 1, value)

    ifd0 = struct.pack("<H", len(entries0))
    for tag, kind, value in entries0:
        ifd0 += entry(tag, kind, exif_ifd if tag == 0x8769 else gps_ifd if tag == 0x8825 else value)
    ifd0 += b"\0\0\0\0"
    exif_dir = struct.pack("<H", 1) + entry(0x9003, 2, date) + b"\0\0\0\0"
    gps_dir = struct.pack("<H", len(gps)) + b"".join(entry(*item) for item in gps) + b"\0\0\0\0"
    return header + ifd0 + exif_dir + gps_dir + data


def jpeg_with_exif(path):
    run("magick", "-size", "64x36", "gradient:red-blue", work("plain.jpg"))
    with open(work("plain.jpg"), "rb") as handle:
        plain = handle.read()
    payload = b"Exif\0\0" + exif()
    segment = b"\xff\xe1" + struct.pack(">H", len(payload) + 2) + payload
    with open(path, "wb") as handle:
        handle.write(plain[:2] + segment + plain[2:])


jpeg_with_exif("exif.jpg")
# Three plain frames of a tenth of a second each, red, green and blue, repeated forever.
run("magick", "-delay", "10", "-size", "16x16", "xc:red", "xc:lime", "xc:blue", "-loop", "0", "animated.gif")
run("magick", "exif.jpg", "photo.avif")
run("magick", "exif.jpg", "photo.png")
run(*FFMPEG, *SOURCE, "-c:v", "libx264", "-metadata", "creation_time=2024-05-06T07:08:09Z",
    "-metadata", "location=+52.5200+013.4050/", "-movflags", "+use_metadata_tags", work("plain.mp4"))
run(*FFMPEG, "-display_rotation", "90", "-i", work("plain.mp4"), "-c", "copy",
    "-movflags", "+use_metadata_tags", "-map_metadata", "0", "clip.mp4")
run(*FFMPEG, *SOURCE, "-c:v", "libx264", "-metadata", "creation_time=2024-05-06T07:08:09Z", "clip.mkv")

using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SkiaSharp;

namespace Flowery.NET.Photos.Services
{
    /// <summary>
    /// Result of a fast metadata probe. Width/Height are already orientation-corrected.
    /// </summary>
    public readonly record struct ImageProbe(int Width, int Height, DateTime? DateTaken, int Orientation, string? CameraModel, PhotoDetails? Details = null);

    /// <summary>Optional details: photo exposure and location, and the duration, codec and frame rate of a video.</summary>
    public sealed record PhotoDetails(string? Lens, string? Exposure, double? Latitude, double? Longitude,
        double? DurationSeconds = null, string? Codec = null, double? FrameRate = null);

    /// <summary>
    /// Reads image dimensions, EXIF orientation, capture date and camera model by parsing file headers directly.
    /// This avoids decoding pixels and keeps library scans fast. Formats that cannot be parsed fall back to Skia.
    /// </summary>
    public static class ImageMetadataReader
    {
        public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".m4v", ".mov", ".mkv", ".webm"
        };

        public static readonly HashSet<string> SupportedExtensions = new(new[]
        {
            ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".heic", ".heif", ".avif", ".jxr", ".wdp", ".ico"
        }.Concat(VideoExtensions), StringComparer.OrdinalIgnoreCase);

        public static bool IsSupported(string path) => SupportedExtensions.Contains(System.IO.Path.GetExtension(path));

        public static bool IsVideo(string path) => VideoExtensions.Contains(System.IO.Path.GetExtension(path));

        public static async Task<ImageProbe?> ProbeAsync(string path)
        {
            if (IsVideo(path)) return VideoMetadataReader.Probe(path);
            try
            {
                var fast = ProbeFast(path);
                if (fast is { Width: > 0, Height: > 0 })
                    return fast;
            }
            catch
            {
                // Fall back to Skia below.
            }

            return await ProbeWithDecoderAsync(path).ConfigureAwait(false);
        }

        public static async Task<ImageProbe?> ReadDetailsAsync(string path)
        {
            // The shell's photo properties of a video are empty; its own header has more.
            if (IsVideo(path)) return await ProbeAsync(path).ConfigureAwait(false);
#if WINDOWS
            try { return await WindowsPhotoCodec.ReadMetadataAsync(path).ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException)
            { /* Optional shell metadata may be unavailable for an otherwise readable image. */ }
#endif
            return await ProbeAsync(path).ConfigureAwait(false);
        }

        private static async Task<ImageProbe?> ProbeWithDecoderAsync(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var codec = SKCodec.Create(stream);
                if (codec == null)
                {
#if WINDOWS
                    return await WindowsPhotoCodec.ProbeAsync(path).ConfigureAwait(false);
#else
                    return null;
#endif
                }
                var swap = (int)codec.EncodedOrigin >= 5;
                return new ImageProbe(
                    swap ? codec.Info.Height : codec.Info.Width,
                    swap ? codec.Info.Width : codec.Info.Height, null, (int)codec.EncodedOrigin, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException)
            {
                return null;
            }
        }

        public static ImageProbe? ProbeFast(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 16 * 1024);
            Span<byte> head = stackalloc byte[32];
            var read = fs.Read(head);
            if (read < 24)
                return null;

            // JPEG
            if (head[0] == 0xFF && head[1] == 0xD8)
            {
                fs.Position = 2;
                return ReadJpeg(fs);
            }

            // PNG
            if (head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47)
            {
                var w = BinaryPrimitives.ReadInt32BigEndian(head[16..]);
                var h = BinaryPrimitives.ReadInt32BigEndian(head[20..]);
                // Skia draws PNG upright as stored and leaves the EXIF orientation aside, so the size stays as it is.
                return WithExif(w, h, FindChunk(fs, 8, "eXIf", png: true), exifTurns: false);
            }

            // HEIC and AVIF: the image size, rotation and EXIF block are items of the meta box.
            if (head[4] == 'f' && head[5] == 't' && head[6] == 'y' && head[7] == 'p')
                return ReadHeif(fs);

            // GIF
            if (head[0] == 'G' && head[1] == 'I' && head[2] == 'F')
            {
                return new ImageProbe(BinaryPrimitives.ReadUInt16LittleEndian(head[6..]), BinaryPrimitives.ReadUInt16LittleEndian(head[8..]), null, 1, null);
            }

            // BMP
            if (head[0] == 'B' && head[1] == 'M')
            {
                var w = BinaryPrimitives.ReadInt32LittleEndian(head[18..]);
                var h = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(head[22..]));
                return new ImageProbe(w, h, null, 1, null);
            }

            // WebP
            if (head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F' && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P')
            {
                fs.Position = 0;
                Span<byte> buf = stackalloc byte[40];
                if (fs.Read(buf) < 30)
                    return null;
                var chunk = Encoding.ASCII.GetString(buf.Slice(12, 4));
                switch (chunk)
                {
                    case "VP8 ":
                        return new ImageProbe(BinaryPrimitives.ReadUInt16LittleEndian(buf[26..]) & 0x3FFF, BinaryPrimitives.ReadUInt16LittleEndian(buf[28..]) & 0x3FFF, null, 1, null);
                    case "VP8L":
                        {
                            var bits = BinaryPrimitives.ReadUInt32LittleEndian(buf[21..]);
                            return new ImageProbe((int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1, null, 1, null);
                        }
                    case "VP8X":
                        {
                            var w = (buf[24] | (buf[25] << 8) | (buf[26] << 16)) + 1;
                            var h = (buf[27] | (buf[28] << 8) | (buf[29] << 16)) + 1;
                            // Only the extended format can carry an EXIF chunk; Skia turns WebP by its orientation.
                            return WithExif(w, h, FindChunk(fs, 12, "EXIF", png: false), exifTurns: true);
                        }
                }
            }

            return null;
        }

        private static ImageProbe? ReadJpeg(FileStream fs)
        {
            int width = 0, height = 0, orientation = 1;
            DateTime? taken = null;
            string? camera = null;
            PhotoDetails? details = null;
            Span<byte> marker = stackalloc byte[4];
            Span<byte> sof = stackalloc byte[5];

            while (fs.Position < fs.Length)
            {
                if (fs.Read(marker[..2]) < 2)
                    break;
                if (marker[0] != 0xFF)
                    break;

                var code = marker[1];
                while (code == 0xFF)
                {
                    var next = fs.ReadByte();
                    if (next < 0)
                        return null;
                    code = (byte)next;
                }

                // Standalone markers without length.
                if (code is 0xD8 or 0x01 || code is >= 0xD0 and <= 0xD7)
                    continue;
                if (code is 0xD9 or 0xDA)
                    break;

                if (fs.Read(marker[..2]) < 2)
                    break;
                var length = BinaryPrimitives.ReadUInt16BigEndian(marker) - 2;
                if (length < 0)
                    break;
                var segmentStart = fs.Position;

                if (code == 0xE1 && length > 14 && taken == null)
                {
                    var data = new byte[Math.Min(length, 128 * 1024)];
                    var got = fs.Read(data, 0, data.Length);
                    if (got > 14 && data[0] == 'E' && data[1] == 'x' && data[2] == 'i' && data[3] == 'f')
                    {
                        details = ParseExif(data.AsSpan(6, got - 6), ref orientation, ref taken, ref camera);
                    }
                }
                else if (code is >= 0xC0 and <= 0xCF && code is not 0xC4 and not 0xC8 and not 0xCC)
                {
                    if (fs.Read(sof) == 5)
                    {
                        height = BinaryPrimitives.ReadUInt16BigEndian(sof[1..]);
                        width = BinaryPrimitives.ReadUInt16BigEndian(sof[3..]);
                    }
                    break;
                }

                fs.Position = segmentStart + length;
            }

            if (width <= 0 || height <= 0)
                return null;

            if (orientation is >= 5 and <= 8)
                (width, height) = (height, width);

            return new ImageProbe(width, height, taken, orientation, camera, details);
        }

        private const int MaxExifLength = 1024 * 1024;

        // A probe from a size and an EXIF block, which may start with JPEG's "Exif" marker. The size turns by the EXIF
        // orientation only where the decoder turns the picture too.
        private static ImageProbe WithExif(int width, int height, byte[]? exif, bool exifTurns)
        {
            int orientation = 1;
            DateTime? taken = null;
            string? camera = null;
            var tiff = exif.AsSpan();
            if (tiff.StartsWith("Exif\0\0"u8)) tiff = tiff[6..];
            var details = tiff.StartsWith("II*\0"u8) || tiff.StartsWith("MM\0*"u8)
                ? ParseExif(tiff, ref orientation, ref taken, ref camera) : null;
            if (!exifTurns) orientation = 1;
            if (orientation is >= 5 and <= 8) (width, height) = (height, width);
            return new ImageProbe(width, height, taken, orientation, camera, details);
        }

        // Finds a chunk by seeking over the others. PNG chunks carry a big-endian length before their type and a checksum
        // after their data, WebP chunks a little-endian length after their type and a pad byte to an even length. PNG
        // writes EXIF before its pixel data, so the search stops there; WebP writes it after the image data.
        private static byte[]? FindChunk(Stream stream, long position, string type, bool png)
        {
            Span<byte> header = stackalloc byte[8];
            stream.Position = position;
            while (stream.Read(header) == 8)
            {
                var name = Encoding.ASCII.GetString(png ? header[4..] : header[..4]);
                long length = png ? BinaryPrimitives.ReadUInt32BigEndian(header) : BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
                if (name == type)
                {
                    if (length > MaxExifLength) return null;
                    var data = new byte[length];
                    stream.ReadExactly(data);
                    return data;
                }
                if (png && name is "IDAT" or "IEND") return null;
                stream.Position += length + (png ? 4 : length & 1);
            }
            return null;
        }

        private static ImageProbe? ReadHeif(Stream stream)
        {
            if (IsoBoxes.ReadTopLevel(stream, "meta", MaxExifLength) is not { } meta) return null;
            var data = meta.AsSpan();
            var boxes = IsoBoxes.Children(data, 4, meta.Length);
            int width = 0, height = 0, turns = 0;
            uint exifItem = 0;
            IsoBox? locations = null;
            foreach (var box in boxes)
            {
                if (box.Type == "iprp" && IsoBoxes.Find(data, box, "ipco") is { } properties)
                    (width, height, turns) = ReadHeifSize(data, properties);
                else if (box.Type == "iinf") exifItem = FindExifItem(data, box);
                else if (box.Type == "iloc") locations = box;
            }
            if (width <= 0 || height <= 0) return null;
            if (turns % 2 == 1) (width, height) = (height, width);
            var item = exifItem > 0 && locations is { } iloc ? ReadItem(stream, data[iloc.Start..iloc.End], exifItem) : null;
            // The item starts with the offset of the TIFF header behind it.
            var exif = item is { Length: > 4 } && BinaryPrimitives.ReadUInt32BigEndian(item) is var offset && offset < item.Length - 4
                ? item[(4 + (int)offset)..] : null;
            return WithExif(width, height, exif, exifTurns: false);
        }

        // The largest image size property is the primary image; grid tiles and thumbnails are smaller.
        private static (int Width, int Height, int Turns) ReadHeifSize(ReadOnlySpan<byte> data, IsoBox properties)
        {
            int width = 0, height = 0, turns = 0;
            foreach (var property in IsoBoxes.Children(data, properties))
            {
                if (property.Type == "ispe" && property.Length >= 12)
                {
                    var w = (int)BinaryPrimitives.ReadUInt32BigEndian(data[(property.Start + 4)..]);
                    var h = (int)BinaryPrimitives.ReadUInt32BigEndian(data[(property.Start + 8)..]);
                    if ((long)w * h > (long)width * height) (width, height) = (w, h);
                }
                else if (property.Type == "irot" && property.Length >= 1) turns = data[property.Start] & 3;
            }
            return (width, height, turns);
        }

        // Item info entries of version 2 carry a 16-bit item ID, those of version 3 a 32-bit one, before the item type.
        private static uint FindExifItem(ReadOnlySpan<byte> data, IsoBox info)
        {
            foreach (var entry in IsoBoxes.Children(data, info, data[info.Start] == 0 ? 6 : 8))
            {
                if (entry.Type != "infe" || entry.Length < 12 || data[entry.Start] < 2) continue;
                var idLength = data[entry.Start] == 2 ? 2 : 4;
                if (entry.Length < 10 + idLength) continue;
                var id = IsoBoxes.ReadUnsigned(data.Slice(entry.Start + 4, idLength));
                if (IsoBoxes.TypeName(data.Slice(entry.Start + 6 + idLength, 4)) == "Exif") return (uint)id;
            }
            return 0;
        }

        // The item location box gives each item a base offset and extents in the file; field sizes come from its header.
        private static byte[]? ReadItem(Stream stream, ReadOnlySpan<byte> iloc, uint itemId)
        {
            var version = iloc[0];
            int offsetSize = iloc[4] >> 4, lengthSize = iloc[4] & 15, baseSize = iloc[5] >> 4;
            var indexSize = version > 0 ? iloc[5] & 15 : 0;
            var position = 6;
            ulong Next(ReadOnlySpan<byte> box, int size, ref int at)
            {
                var value = IsoBoxes.ReadUnsigned(box.Slice(at, size));
                at += size;
                return value;
            }
            var count = Next(iloc, version < 2 ? 2 : 4, ref position);
            for (ulong i = 0; i < count; i++)
            {
                var id = Next(iloc, version < 2 ? 2 : 4, ref position);
                var method = version > 0 ? Next(iloc, 2, ref position) & 15 : 0;
                position += 2;
                var baseOffset = Next(iloc, baseSize, ref position);
                var extents = Next(iloc, 2, ref position);
                for (ulong e = 0; e < extents; e++)
                {
                    position += indexSize;
                    var offset = Next(iloc, offsetSize, ref position);
                    var length = Next(iloc, lengthSize, ref position);
                    // Only items stored in the file itself, in one piece.
                    if (id != itemId) continue;
                    if (method != 0 || extents != 1 || length > MaxExifLength) return null;
                    stream.Position = (long)(baseOffset + offset);
                    var item = new byte[length];
                    stream.ReadExactly(item);
                    return item;
                }
            }
            return null;
        }

        private static PhotoDetails? ParseExif(ReadOnlySpan<byte> tiff, ref int orientation, ref DateTime? taken, ref string? camera)
        {
            if (tiff.Length < 8)
                return null;
            var little = tiff[0] == 'I';
            var ifd0 = ReadU32(tiff, 4, little);
            uint exifIfd = 0;
            uint gpsIfd = 0;
            string? make = null, model = null;

            foreach (var entry in EnumerateIfd(tiff, ifd0, little))
            {
                var raw = tiff.Slice(entry.EntryOffset + 8, 4);
                switch (entry.Tag)
                {
                    case 0x0112:
                        orientation = little ? raw[0] | (raw[1] << 8) : (raw[0] << 8) | raw[1];
                        break;
                    case 0x010F:
                        make = ReadAscii(tiff, entry.Count, entry.ValueOffset, raw);
                        break;
                    case 0x0110:
                        model = ReadAscii(tiff, entry.Count, entry.ValueOffset, raw);
                        break;
                    case 0x8769:
                        exifIfd = entry.ValueOffset;
                        break;
                    case 0x8825:
                        gpsIfd = entry.ValueOffset;
                        break;
                }
            }

            DateTime? date = null;
            string? lens = null;
            double? exposure = null, aperture = null, focalLength = null;
            ushort? iso = null;
            if (exifIfd > 0)
            {
                foreach (var entry in EnumerateIfd(tiff, exifIfd, little))
                {
                    if (entry.Tag == 0xA434)
                        lens = ReadAscii(tiff, entry.Count, entry.ValueOffset, tiff.Slice(entry.EntryOffset + 8, 4));
                    if (entry.Tag == 0x829A) exposure = ReadRational(tiff, entry.ValueOffset, little);
                    if (entry.Tag == 0x829D) aperture = ReadRational(tiff, entry.ValueOffset, little);
                    if (entry.Tag == 0x920A) focalLength = ReadRational(tiff, entry.ValueOffset, little);
                    if (entry.Tag == 0x8827 && entry.Count == 1) iso = ReadU16(tiff, entry.EntryOffset + 8, little);
                    if (entry.Tag is 0x9003 or 0x9004 && date == null)
                    {
                        var text = ReadAscii(tiff, entry.Count, entry.ValueOffset, tiff.Slice(entry.EntryOffset + 8, 4));
                        if (text != null && DateTime.TryParseExact(text.Trim(), "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                            date = parsed;
                    }
                }
            }

            taken = date;
            if (!string.IsNullOrWhiteSpace(model))
            {
                camera = !string.IsNullOrWhiteSpace(make) && !model.StartsWith(make, StringComparison.OrdinalIgnoreCase)
                    ? $"{make.Trim()} {model.Trim()}"
                    : model.Trim();
            }
            double? latitude = null, longitude = null;
            var south = false;
            var west = false;
            foreach (var entry in EnumerateIfd(tiff, gpsIfd, little))
            {
                if (entry.Tag == 1) south = tiff[entry.EntryOffset + 8] == 'S';
                if (entry.Tag == 3) west = tiff[entry.EntryOffset + 8] == 'W';
                if (entry.Tag is not (2 or 4) || entry.Count != 3) continue;
                var degrees = ReadRational(tiff, entry.ValueOffset, little);
                var minutes = ReadRational(tiff, (ulong)entry.ValueOffset + 8, little);
                var seconds = ReadRational(tiff, (ulong)entry.ValueOffset + 16, little);
                var value = degrees + minutes / 60 + seconds / 3600;
                if (entry.Tag == 2) latitude = value; else longitude = value;
            }
            if (latitude.HasValue) latitude = south ? -latitude : latitude;
            if (longitude.HasValue) longitude = west ? -longitude : longitude;
            List<string> exposureParts = [];
            if (aperture > 0) exposureParts.Add(string.Format(CultureInfo.InvariantCulture, "ƒ/{0:0.#}", aperture));
            if (exposure > 0) exposureParts.Add(exposure < 1
                ? string.Format(CultureInfo.InvariantCulture, "1/{0:0} s", 1 / exposure)
                : string.Format(CultureInfo.InvariantCulture, "{0:0.#} s", exposure));
            if (iso > 0) exposureParts.Add($"ISO {iso}");
            if (focalLength > 0) exposureParts.Add(string.Format(CultureInfo.InvariantCulture, "{0:0} mm", focalLength));
            return new PhotoDetails(lens, string.Join("  ·  ", exposureParts), latitude, longitude);
        }

        private static double? ReadRational(ReadOnlySpan<byte> data, ulong offset, bool little)
        {
            if (offset + 8 > (ulong)data.Length) return null;
            var numerator = ReadU32(data, (int)offset, little);
            var denominator = ReadU32(data, (int)offset + 4, little);
            return denominator == 0 ? null : (double)numerator / denominator;
        }

        private readonly record struct IfdEntry(ushort Tag, uint Count, uint ValueOffset, int EntryOffset);

        private static List<IfdEntry> EnumerateIfd(ReadOnlySpan<byte> tiff, uint offset, bool little)
        {
            List<IfdEntry> result = [];
            if (offset == 0 || (ulong)offset + 2 > (ulong)tiff.Length)
                return result;
            var entries = ReadU16(tiff, (int)offset, little);
            for (var i = 0; i < entries; i++)
            {
                var entry = (int)offset + 2 + i * 12;
                if (entry + 12 > tiff.Length)
                    break;
                result.Add(new IfdEntry(
                    ReadU16(tiff, entry, little),
                    ReadU32(tiff, entry + 4, little),
                    ReadU32(tiff, entry + 8, little),
                    entry));
            }
            return result;
        }

        private static string? ReadAscii(ReadOnlySpan<byte> tiff, uint count, uint valueOffset, ReadOnlySpan<byte> raw)
        {
            if (count == 0)
                return null;
            ReadOnlySpan<byte> bytes;
            if (count <= 4)
                bytes = raw[..(int)count];
            else if ((ulong)valueOffset + count <= (ulong)tiff.Length)
                bytes = tiff.Slice((int)valueOffset, (int)count);
            else
                return null;
            return Encoding.ASCII.GetString(bytes).TrimEnd('\0', ' ');
        }

        private static ushort ReadU16(ReadOnlySpan<byte> data, int offset, bool little) =>
            little ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]) : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);

        private static uint ReadU32(ReadOnlySpan<byte> data, int offset, bool little) =>
            little ? BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]) : BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
    }
}

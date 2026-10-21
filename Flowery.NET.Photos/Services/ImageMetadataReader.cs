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

    public sealed record PhotoDetails(string? Lens, string? Exposure, double? Latitude, double? Longitude);

    /// <summary>
    /// Reads image dimensions, EXIF orientation, capture date and camera model by parsing file headers directly.
    /// This avoids decoding pixels and keeps library scans fast. Formats that cannot be parsed fall back to Skia.
    /// </summary>
    public static class ImageMetadataReader
    {
        public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".heic", ".heif", ".avif", ".jxr", ".wdp", ".ico"
        };

        public static bool IsSupported(string path) => SupportedExtensions.Contains(System.IO.Path.GetExtension(path));

        public static async Task<ImageProbe?> ProbeAsync(string path)
        {
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
                return new ImageProbe(w, h, null, 1, null);
            }

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
                            return new ImageProbe(w, h, null, 1, null);
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

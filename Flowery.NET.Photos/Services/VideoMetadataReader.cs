using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace Flowery.NET.Photos.Services;

/// <summary>
/// Reads size, duration, frame rate, codec, recording date, camera and location of MP4, MOV, MKV and WebM videos from
/// their container headers, without decoding any frame.
/// </summary>
public static partial class VideoMetadataReader
{
    private const int MaxHeaderLength = 32 * 1024 * 1024;
    private static readonly DateTime QuickTimeEpoch = new(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MatroskaEpoch = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The video's metadata, or null for a file that is missing, unreadable or broken.</summary>
    public static ImageProbe? Probe(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 16 * 1024);
            return Probe(stream);
        }
        // Sizes in a broken header point outside the data.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or IndexOutOfRangeException)
        { return null; }
    }

    /// <summary>The metadata of a video in a seekable stream, which tests count the reads of.</summary>
    internal static ImageProbe? Probe(Stream stream)
    {
        Span<byte> head = stackalloc byte[4];
        if (stream.Read(head) < 4) return null;
        stream.Position = 0;
        return BinaryPrimitives.ReadUInt32BigEndian(head) == MatroskaHeader ? ReadMatroska(stream) : ReadMp4(stream);
    }

    // MP4 and MOV keep everything but the media data in the moov box, which may also come after it.
    private static ImageProbe? ReadMp4(Stream stream)
    {
        if (IsoBoxes.ReadTopLevel(stream, "moov", MaxHeaderLength) is not { } moov) return null;
        var data = moov.AsSpan();
        var root = new IsoBox("moov", 0, moov.Length);
        var info = new Mp4Info();
        foreach (var box in IsoBoxes.Children(data, root))
        {
            switch (box.Type)
            {
                case "mvhd": ReadMovieHeader(data[box.Start..box.End], info); break;
                case "trak": ReadTrack(data, box, info); break;
                case "meta": ReadMetadataKeys(data, box, info); break;
                case "udta": ReadUserData(data, box, info); break;
            }
        }
        if (info.Width <= 0 || info.Height <= 0) return null;
        var details = new PhotoDetails(null, null, info.Latitude, info.Longitude,
            info.Duration > 0 ? info.Duration : null, CodecName(info.Codec), info.FrameRate);
        return new ImageProbe(info.Width, info.Height, info.Created, 1, info.Camera, details);
    }

    private sealed class Mp4Info
    {
        public int Width, Height;
        public double Duration;
        public double? FrameRate, Latitude, Longitude;
        public DateTime? Created;
        public string? Codec, Camera, Make, Model;
    }

    private static void ReadMovieHeader(ReadOnlySpan<byte> box, Mp4Info info)
    {
        if (box.Length < 32) return;
        var wide = box[0] == 1;
        var created = wide ? BinaryPrimitives.ReadUInt64BigEndian(box[4..]) : BinaryPrimitives.ReadUInt32BigEndian(box[4..]);
        var scale = BinaryPrimitives.ReadUInt32BigEndian(box[(wide ? 20 : 12)..]);
        var duration = wide ? BinaryPrimitives.ReadUInt64BigEndian(box[24..]) : BinaryPrimitives.ReadUInt32BigEndian(box[16..]);
        if (scale > 0) info.Duration = (double)duration / scale;
        // Cameras without a clock write zero; QuickTime counts seconds from 1904.
        if (created > 0 && created < 200UL * 365 * 86400) info.Created ??= QuickTimeEpoch.AddSeconds(created).ToLocalTime();
    }

    // Only the first video track counts. Its header holds the size and the display matrix that turns phone videos
    // upright, its sample table the codec and the number of frames.
    private static void ReadTrack(ReadOnlySpan<byte> data, IsoBox track, Mp4Info info)
    {
        if (info.Width > 0 || IsoBoxes.Find(data, track, "mdia") is not { } media
            || IsoBoxes.Find(data, media, "hdlr") is not { } handler || handler.Length < 12
            || IsoBoxes.TypeName(data.Slice(handler.Start + 8, 4)) != "vide"
            || IsoBoxes.Find(data, track, "tkhd") is not { } header) return;
        var tkhd = data[header.Start..header.End];
        var wide = tkhd.Length > 0 && tkhd[0] == 1;
        var matrix = wide ? 52 : 40;
        if (tkhd.Length < matrix + 44) return;
        var width = (int)(BinaryPrimitives.ReadUInt32BigEndian(tkhd[(matrix + 36)..]) >> 16);
        var height = (int)(BinaryPrimitives.ReadUInt32BigEndian(tkhd[(matrix + 40)..]) >> 16);
        // A matrix of (0, ±1, ∓1, 0) rotates by a quarter turn.
        var turned = BinaryPrimitives.ReadInt32BigEndian(tkhd[matrix..]) == 0;
        (info.Width, info.Height) = turned ? (height, width) : (width, height);

        if (IsoBoxes.Find(data, media, "minf") is not { } minf || IsoBoxes.Find(data, minf, "stbl") is not { } stbl) return;
        if (IsoBoxes.Find(data, stbl, "stsd") is { Length: >= 16 } stsd)
            info.Codec = IsoBoxes.TypeName(data.Slice(stsd.Start + 12, 4));
        if (IsoBoxes.Find(data, media, "mdhd") is not { } mdhd || IsoBoxes.Find(data, stbl, "stts") is not { } stts) return;
        var mediaHeader = data[mdhd.Start..mdhd.End];
        if (mediaHeader.Length < 24) return;
        var mediaWide = mediaHeader[0] == 1;
        var scale = BinaryPrimitives.ReadUInt32BigEndian(mediaHeader[(mediaWide ? 20 : 12)..]);
        var duration = mediaWide ? BinaryPrimitives.ReadUInt64BigEndian(mediaHeader[24..]) : BinaryPrimitives.ReadUInt32BigEndian(mediaHeader[16..]);
        var frames = 0L;
        var times = data[stts.Start..stts.End];
        var entries = times.Length >= 8 ? BinaryPrimitives.ReadUInt32BigEndian(times[4..]) : 0;
        for (var i = 0; i < entries && 16 + i * 8 <= times.Length; i++)
            frames += BinaryPrimitives.ReadUInt32BigEndian(times[(8 + i * 8)..]);
        if (scale > 0 && duration > 0 && frames > 1) info.FrameRate = Math.Round(frames * (double)scale / duration, 2);
    }

    private static void ReadUserData(ReadOnlySpan<byte> data, IsoBox userData, Mp4Info info)
    {
        foreach (var box in IsoBoxes.Children(data, userData))
        {
            if (box.Type == "meta") ReadMetadataKeys(data, box, info);
            // Android and older QuickTime files: a 16-bit length and language, then an ISO 6709 location.
            else if (box.Type == "©xyz" && box.Length > 4)
                ReadLocation(Encoding.UTF8.GetString(data.Slice(box.Start + 4, Math.Min(box.Length - 4,
                    BinaryPrimitives.ReadUInt16BigEndian(data[box.Start..])))), info);
        }
    }

    // QuickTime metadata: the keys box names the entries, and the item list holds one value box per key index.
    private static void ReadMetadataKeys(ReadOnlySpan<byte> data, IsoBox meta, Mp4Info info)
    {
        var children = IsoBoxes.MetaChildren(data, meta);
        if (children.Find(box => box.Type == "keys") is not { Length: >= 8 } keys
            || children.Find(box => box.Type == "ilst") is not { Length: > 0 } items) return;
        List<string> names = [];
        for (var position = keys.Start + 8; position + 8 <= keys.End;)
        {
            var size = (int)BinaryPrimitives.ReadUInt32BigEndian(data[position..]);
            if (size < 8 || position + size > keys.End) break;
            names.Add(Encoding.UTF8.GetString(data.Slice(position + 8, size - 8)));
            position += size;
        }
        foreach (var item in IsoBoxes.Children(data, items))
        {
            var index = (int)BinaryPrimitives.ReadUInt32BigEndian(Encoding.Latin1.GetBytes(item.Type)) - 1;
            if (index < 0 || index >= names.Count || IsoBoxes.Find(data, item, "data") is not { Length: > 8 } value) continue;
            var text = Encoding.UTF8.GetString(data.Slice(value.Start + 8, value.Length - 8)).Trim('\0', ' ');
            SetMetadata(names[index], text, info);
        }
        if (info.Model is { } model) info.Camera = info.Make is { } make && !model.StartsWith(make, StringComparison.OrdinalIgnoreCase)
            ? $"{make} {model}" : model;
    }

    private static void SetMetadata(string key, string value, Mp4Info info)
    {
        switch (key)
        {
            case "com.apple.quicktime.location.ISO6709" or "location": ReadLocation(value, info); break;
            case "com.apple.quicktime.make": info.Make = value; break;
            case "com.apple.quicktime.model": info.Model = value; break;
            // The recording date with the camera's time zone is better than the header date in UTC. Like the EXIF date
            // of a photo it keeps the clock time where it was taken; a date in UTC becomes local time.
            case "com.apple.quicktime.creationdate" or "creation_time":
                if (DateTimeOffset.TryParse(ZoneWithColon().Replace(value, "$1:$2"), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var date))
                    info.Created = date.Offset == TimeSpan.Zero ? date.LocalDateTime : date.DateTime;
                break;
        }
    }

    // Apple writes the time zone as +0200, which DateTimeOffset reads only as +02:00.
    [GeneratedRegex(@"([+-]\d{2})(\d{2})$")]
    private static partial Regex ZoneWithColon();

    [GeneratedRegex(@"^([+-]\d+(?:\.\d+)?)([+-]\d+(?:\.\d+)?)")]
    private static partial Regex Iso6709();

    private static void ReadLocation(string value, Mp4Info info)
    {
        var match = Iso6709().Match(value);
        if (!match.Success) return;
        info.Latitude = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        info.Longitude = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
    }

    private static string? CodecName(string? codec) => codec switch
    {
        null => null,
        "avc1" or "avc3" or "V_MPEG4/ISO/AVC" => "H.264",
        "hvc1" or "hev1" or "V_MPEGH/ISO/HEVC" => "HEVC",
        "av01" or "V_AV1" => "AV1",
        "vp09" or "V_VP9" => "VP9",
        "vp08" or "V_VP8" => "VP8",
        "mp4v" => "MPEG-4",
        "apcn" or "apch" or "apcs" or "apco" or "ap4h" => "ProRes",
        _ => codec.Trim()
    };

    private const uint MatroskaHeader = 0x1A45DFA3;
    private const uint SegmentId = 0x18538067, InfoId = 0x1549A966, TracksId = 0x1654AE6B, ClusterId = 0x1F43B675;
    private const uint TrackEntryId = 0xAE, TrackTypeId = 0x83, CodecId = 0x86, DefaultDurationId = 0x23E383, VideoId = 0xE0;
    private const uint PixelWidthId = 0xB0, PixelHeightId = 0xBA, TimecodeScaleId = 0x2AD7B1, DurationId = 0x4489, DateId = 0x4461;

    // Matroska and WebM write their segment info and track list before the first cluster of frames, mostly within the
    // first few kilobytes. A gallery probes hundreds of videos, on a network drive too, so the reader starts with 64 KB
    // and reads on, up to 4 MB, only while the track list has not come.
    private static ImageProbe? ReadMatroska(Stream stream)
    {
        var buffer = Array.Empty<byte>();
        for (var length = 64 * 1024; ; length *= 4)
        {
            var read = buffer.Length;
            Array.Resize(ref buffer, (int)Math.Min(stream.Length, length));
            stream.ReadExactly(buffer, read, buffer.Length - read);
            if (ReadMatroska(buffer) is { } probe) return probe;
            if (buffer.Length == stream.Length || length >= 4 * 1024 * 1024) return null;
        }
    }

    private static ImageProbe? ReadMatroska(ReadOnlySpan<byte> data)
    {
        long width = 0, height = 0, scale = 1_000_000;
        double duration = 0, frameRate = 0;
        DateTime? created = null;
        string? codec = null;
        var position = 0;
        while (ReadElement(data, ref position, out var id, out var start, out var end))
        {
            if (id == MatroskaHeader) { position = end; continue; }
            if (id != SegmentId) break;
            foreach (var (childId, childStart, childEnd) in Elements(data, start, end))
            {
                if (childId == ClusterId) break;
                if (childId == InfoId)
                {
                    foreach (var (field, fieldStart, fieldEnd) in Elements(data, childStart, childEnd))
                    {
                        var value = data[fieldStart..fieldEnd];
                        if (field == TimecodeScaleId) scale = (long)IsoBoxes.ReadUnsigned(value);
                        else if (field == DurationId) duration = ReadFloat(value);
                        else if (field == DateId && value.Length == 8)
                            created = MatroskaEpoch.AddTicks(BinaryPrimitives.ReadInt64BigEndian(value) / 100).ToLocalTime();
                    }
                }
                else if (childId == TracksId && width == 0)
                    ReadMatroskaVideoTrack(data, childStart, childEnd, ref width, ref height, ref codec, ref frameRate);
            }
            break;
        }
        if (width <= 0 || height <= 0) return null;
        var seconds = duration * scale / 1e9;
        var details = new PhotoDetails(null, null, null, null, seconds > 0 ? seconds : null, CodecName(codec),
            frameRate > 0 ? frameRate : null);
        return new ImageProbe((int)width, (int)height, created, 1, null, details);
    }

    private static void ReadMatroskaVideoTrack(ReadOnlySpan<byte> data, int start, int end, ref long width, ref long height,
        ref string? codec, ref double frameRate)
    {
        foreach (var (entryId, entryStart, entryEnd) in Elements(data, start, end))
        {
            if (entryId != TrackEntryId) continue;
            ulong type = 0, frameDuration = 0;
            string? entryCodec = null;
            long entryWidth = 0, entryHeight = 0;
            foreach (var (field, fieldStart, fieldEnd) in Elements(data, entryStart, entryEnd))
            {
                var value = data[fieldStart..fieldEnd];
                if (field == TrackTypeId) type = IsoBoxes.ReadUnsigned(value);
                else if (field == CodecId) entryCodec = Encoding.ASCII.GetString(value).TrimEnd('\0');
                else if (field == DefaultDurationId) frameDuration = IsoBoxes.ReadUnsigned(value);
                else if (field == VideoId)
                    foreach (var (size, sizeStart, sizeEnd) in Elements(data, fieldStart, fieldEnd))
                    {
                        if (size == PixelWidthId) entryWidth = (long)IsoBoxes.ReadUnsigned(data[sizeStart..sizeEnd]);
                        else if (size == PixelHeightId) entryHeight = (long)IsoBoxes.ReadUnsigned(data[sizeStart..sizeEnd]);
                    }
            }
            if (type != 1) continue;
            (width, height, codec) = (entryWidth, entryHeight, entryCodec);
            if (frameDuration > 0) frameRate = Math.Round(1e9 / frameDuration, 2);
            return;
        }
    }

    private static List<(uint Id, int Start, int End)> Elements(ReadOnlySpan<byte> data, int start, int end)
    {
        List<(uint, int, int)> elements = [];
        var position = start;
        while (position < end && ReadElement(data[..end], ref position, out var id, out var childStart, out var childEnd))
        {
            elements.Add((id, childStart, childEnd));
            position = childEnd;
        }
        return elements;
    }

    // An EBML element: an ID that keeps its length marker, a size without it, and the content. An unknown size, such as
    // the segment of a live recording, extends to the end of the buffer.
    private static bool ReadElement(ReadOnlySpan<byte> data, ref int position, out uint id, out int start, out int end)
    {
        id = 0; start = end = position;
        if (!ReadVarInt(data, ref position, keepMarker: true, out var rawId, out _) || rawId > uint.MaxValue) return false;
        if (!ReadVarInt(data, ref position, keepMarker: false, out var size, out var unknown)) return false;
        id = (uint)rawId; start = position;
        end = unknown || size > (ulong)(data.Length - position) ? data.Length : position + (int)size;
        return true;
    }

    private static bool ReadVarInt(ReadOnlySpan<byte> data, ref int position, bool keepMarker, out ulong value, out bool allOnes)
    {
        value = 0; allOnes = false;
        if (position >= data.Length || data[position] == 0) return false;
        var length = BitOperations.LeadingZeroCount((uint)data[position]) - 23;
        if (position + length > data.Length) return false;
        value = keepMarker ? data[position] : (ulong)(data[position] & (0xFF >> length));
        allOnes = value == (ulong)(0xFF >> length);
        for (var i = 1; i < length; i++)
        {
            value = (value << 8) | data[position + i];
            allOnes &= data[position + i] == 0xFF;
        }
        position += length;
        return true;
    }

    private static double ReadFloat(ReadOnlySpan<byte> value) => value.Length switch
    {
        4 => BinaryPrimitives.ReadSingleBigEndian(value),
        8 => BinaryPrimitives.ReadDoubleBigEndian(value),
        _ => 0
    };
}

using System.Buffers.Binary;
using System.Text;

namespace Flowery.NET.Photos.Services;

/// <summary>A box of the ISO base media file format (MP4, MOV, HEIC, AVIF) inside a buffer: its type and content range.</summary>
internal readonly record struct IsoBox(string Type, int Start, int End)
{
    public int Length => End - Start;
}

/// <summary>Reads the box structure of MP4, MOV, HEIC and AVIF files without decoding any media.</summary>
internal static class IsoBoxes
{
    /// <summary>Reads the content of the first top-level box of a type, skipping media data by seeking.</summary>
    public static byte[]? ReadTopLevel(Stream stream, string type, int maxLength)
    {
        Span<byte> header = stackalloc byte[16];
        stream.Position = 0;
        while (stream.Position + 8 <= stream.Length)
        {
            var start = stream.Position;
            stream.ReadExactly(header[..8]);
            long size = BinaryPrimitives.ReadUInt32BigEndian(header);
            var headerLength = 8;
            if (size == 1)
            {
                stream.ReadExactly(header[8..16]);
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(header[8..]);
                headerLength = 16;
            }
            else if (size == 0) size = stream.Length - start;
            if (size < headerLength || start + size > stream.Length) return null;
            if (TypeName(header[4..8]) == type)
            {
                var length = size - headerLength;
                if (length > maxLength) return null;
                var content = new byte[length];
                stream.ReadExactly(content);
                return content;
            }
            stream.Position = start + size;
        }
        return null;
    }

    /// <summary>The boxes inside a range of a buffer; a broken size ends the list.</summary>
    public static List<IsoBox> Children(ReadOnlySpan<byte> data, int start, int end)
    {
        List<IsoBox> boxes = [];
        var position = start;
        while (position + 8 <= end)
        {
            long size = BinaryPrimitives.ReadUInt32BigEndian(data[position..]);
            var headerLength = 8;
            if (size == 1 && position + 16 <= end)
            {
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(data[(position + 8)..]);
                headerLength = 16;
            }
            else if (size == 0) size = end - position;
            if (size < headerLength || position + size > end) break;
            boxes.Add(new IsoBox(TypeName(data.Slice(position + 4, 4)), position + headerLength, position + (int)size));
            position += (int)size;
        }
        return boxes;
    }

    public static List<IsoBox> Children(ReadOnlySpan<byte> data, IsoBox parent, int skip = 0) =>
        parent.Length < skip ? [] : Children(data, parent.Start + skip, parent.End);

    /// <summary>The first child of a type, if any.</summary>
    public static IsoBox? Find(ReadOnlySpan<byte> data, IsoBox parent, string type, int skip = 0)
    {
        foreach (var box in Children(data, parent, skip))
            if (box.Type == type) return box;
        return null;
    }

    /// <summary>
    /// The children of a meta box. ISO files give it a version and flags before its children, QuickTime files do not;
    /// a QuickTime meta box starts directly with its handler box.
    /// </summary>
    public static List<IsoBox> MetaChildren(ReadOnlySpan<byte> data, IsoBox meta) =>
        Children(data, meta, meta.Length >= 8 && TypeName(data.Slice(meta.Start + 4, 4)) == "hdlr" ? 0 : 4);

    /// <summary>A big-endian unsigned number of up to eight bytes, as ISO boxes and Matroska elements store them.</summary>
    public static ulong ReadUnsigned(ReadOnlySpan<byte> value)
    {
        ulong result = 0;
        foreach (var part in value[..Math.Min(8, value.Length)]) result = (result << 8) | part;
        return result;
    }

    // Latin-1 keeps QuickTime's © types, such as ©xyz, as one character each.
    public static string TypeName(ReadOnlySpan<byte> type) => Encoding.Latin1.GetString(type);
}

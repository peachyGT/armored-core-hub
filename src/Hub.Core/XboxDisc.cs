using System.Buffers.Binary;
using System.Text;

namespace Hub.Core;

// Read only the root directory and XEX execution metadata. No executable loading,
// extraction, filename-based identity, or recursive traversal of disc contents.
internal static class XboxDisc
{
    public sealed record Identity(string? TitleId, string? Problem = null);
    private static readonly long[] PartitionOffsets = [0, 0xFB20, 0x20600, 0x2080000, 0xFD90000];

    public static Identity? Inspect(string path)
    {
        using var stream = File.OpenRead(path);
        var header = new byte[28];
        foreach (var partition in PartitionOffsets)
        {
            if (!Read(stream, partition + 32 * 2048, header) || !header.AsSpan(0, 20).SequenceEqual("MICROSOFT*XBOX*MEDIA"u8)) continue;
            try
            {
                var sector = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(20));
                var size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(24));
                if (size < 14 || size > 4 * 1024 * 1024) return new(null, "The Xbox disc directory has an invalid size.");
                var directory = new byte[size];
                if (!Read(stream, partition + sector * 2048L, directory)) return new(null, "The Xbox disc directory is incomplete.");
                var pending = new Stack<int>(); pending.Push(0);
                var visited = new HashSet<int>();
                while (pending.TryPop(out var offset))
                {
                    if (!visited.Add(offset) || offset < 0 || offset > directory.Length - 14)
                        return new(null, "The Xbox disc directory contains an invalid entry.");
                    var entry = directory.AsSpan(offset);
                    var nameLength = entry[13];
                    if (nameLength == 0 || nameLength > entry.Length - 14) return new(null, "The Xbox disc directory contains an invalid name.");
                    var left = BinaryPrimitives.ReadUInt16LittleEndian(entry) * 4;
                    var right = BinaryPrimitives.ReadUInt16LittleEndian(entry[2..]) * 4;
                    if (left != 0) pending.Push(left);
                    if (right != 0) pending.Push(right);
                    if ((entry[12] & 0x10) != 0 || !Encoding.ASCII.GetString(entry.Slice(14, nameLength)).Equals("default.xex", StringComparison.OrdinalIgnoreCase)) continue;
                    var fileOffset = partition + BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]) * 2048L;
                    var fileSize = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);
                    var id = ReadXexTitle(stream, fileOffset, fileSize);
                    return id == null ? new(null, "The Xbox game executable metadata is incomplete.") : new(id);
                }
                return new(null, "This Xbox disc has no default.xex game executable.");
            }
            catch (EndOfStreamException) { return new(null, "The Xbox disc metadata is incomplete."); }
        }
        return null;
    }

    public static string? ReadXexTitle(Stream stream, long start, long length)
    {
        if (start < 0 || length < 24 || length > stream.Length || start > stream.Length - length) return null;
        Span<byte> header = stackalloc byte[24];
        if (!Read(stream, start, header) || !header[..4].SequenceEqual("XEX2"u8)) return null;
        var count = BinaryPrimitives.ReadUInt32BigEndian(header[20..]);
        if (count > 4096 || 24 + count * 8L > length) return null;
        Span<byte> entry = stackalloc byte[8]; Span<byte> id = stackalloc byte[4];
        for (var i = 0; i < count; i++)
        {
            if (!Read(stream, start + 24 + i * 8L, entry)) return null;
            if (BinaryPrimitives.ReadUInt32BigEndian(entry) != 0x40006) continue;
            var offset = BinaryPrimitives.ReadUInt32BigEndian(entry[4..]);
            if (offset < 24 + count * 8L || offset + 24L > length) return null;
            return Read(stream, start + offset + 12, id) ? Convert.ToHexString(id) : null;
        }
        return null;
    }

    private static bool Read(Stream stream, long offset, Span<byte> bytes)
    {
        if (offset < 0 || offset > stream.Length - bytes.Length) return false;
        stream.Position = offset; stream.ReadExactly(bytes); return true;
    }
}

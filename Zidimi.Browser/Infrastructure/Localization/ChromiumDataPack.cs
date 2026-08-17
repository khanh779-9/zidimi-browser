using System.Buffers.Binary;
using System.Text;

namespace Zidimi.Browser.Infrastructure.Localization;

/// <summary>
/// Minimal managed reader/writer for Chromium GRIT DataPack v5 (.pak).
/// The layout mirrors tools/grit/grit/format/data_pack.py: a v5 header, a uint16/uint32
/// resource table with one sentinel entry, followed by uint16/uint16 aliases and payload bytes.
/// </summary>
internal sealed class ChromiumDataPack
{
    public const uint Version = 5;

    public byte Encoding { get; private set; }
    public Dictionary<ushort, byte[]> Resources { get; } = new();

    private ChromiumDataPack(byte encoding) => Encoding = encoding;

    public static ChromiumDataPack Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 12) throw new InvalidDataException("Chromium DataPack header is truncated.");

        var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, 4));
        if (version != Version)
            throw new InvalidDataException($"Unsupported Chromium DataPack version {version}; expected {Version}.");

        var encoding = bytes[4];
        var resourceCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2));
        var aliasCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(10, 2));
        var tableSize = checked((resourceCount + 1) * 6);
        var aliasSize = checked(aliasCount * 4);
        var headerAndTables = checked(12 + tableSize + aliasSize);
        if (headerAndTables > bytes.Length)
            throw new InvalidDataException("Chromium DataPack index is truncated.");

        var pack = new ChromiumDataPack(encoding);
        var ids = new ushort[resourceCount];
        var offsets = new uint[resourceCount + 1];

        var cursor = 12;
        for (var i = 0; i <= resourceCount; i++)
        {
            var id = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(cursor, 2));
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor + 2, 4));
            cursor += 6;
            if (i < resourceCount) ids[i] = id;
            offsets[i] = offset;
        }

        for (var i = 0; i < resourceCount; i++)
        {
            var start = checked((int)offsets[i]);
            var end = checked((int)offsets[i + 1]);
            if (start < headerAndTables || end < start || end > bytes.Length)
                throw new InvalidDataException("Chromium DataPack payload offset is invalid.");
            pack.Resources[ids[i]] = bytes.AsSpan(start, end - start).ToArray();
        }

        for (var i = 0; i < aliasCount; i++)
        {
            var aliasId = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(cursor, 2));
            var targetIndex = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(cursor + 2, 2));
            cursor += 4;
            if (targetIndex >= resourceCount)
                throw new InvalidDataException("Chromium DataPack alias index is invalid.");
            pack.Resources[aliasId] = pack.Resources[ids[targetIndex]];
        }

        return pack;
    }

    public bool TryGetUtf8(ushort id, out string value)
    {
        value = string.Empty;
        if (!Resources.TryGetValue(id, out var bytes)) return false;
        try
        {
            value = Encoding switch
            {
                2 => System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0'),
                _ => System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0'),
            };
            return true;
        }
        catch { return false; }
    }

    public void SetUtf8(ushort id, string value)
    {
        // Zidimi's private resource IDs are consumed only by the managed WPF shell. Keep their
        // payload explicitly UTF-8 so lookup does not depend on the stock pack's message encoding.
        Resources[id] = System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty);
    }

    public byte[] ToBytes()
    {
        // Chromium aliases identical payloads. Reproduce that behavior so augmenting locale packs
        // does not needlessly inflate them.
        var sorted = Resources.OrderBy(kv => kv.Key).ToArray();
        var canonicalByPayload = new Dictionary<string, ushort>(StringComparer.Ordinal);
        var canonical = new List<KeyValuePair<ushort, byte[]>>();
        var aliases = new List<(ushort AliasId, ushort TargetId)>();

        foreach (var kv in sorted)
        {
            var signature = Convert.ToBase64String(kv.Value);
            if (canonicalByPayload.TryGetValue(signature, out var target))
                aliases.Add((kv.Key, target));
            else
            {
                canonicalByPayload[signature] = kv.Key;
                canonical.Add(kv);
            }
        }

        if (canonical.Count > ushort.MaxValue || aliases.Count > ushort.MaxValue)
            throw new InvalidDataException("Chromium DataPack resource count exceeds v5 limits.");

        var targetIndex = canonical.Select((kv, index) => (kv.Key, index))
            .ToDictionary(x => x.Key, x => checked((ushort)x.index));
        var headerLength = 12;
        var indexLength = checked((canonical.Count + 1) * 6);
        var aliasLength = checked(aliases.Count * 4);
        var dataOffset = checked(headerLength + indexLength + aliasLength);
        var totalData = canonical.Sum(x => x.Value.Length);
        var output = new byte[checked(dataOffset + totalData)];

        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(0, 4), Version);
        output[4] = Encoding;
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(8, 2), checked((ushort)canonical.Count));
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(10, 2), checked((ushort)aliases.Count));

        var cursor = 12;
        var payloadCursor = dataOffset;
        foreach (var kv in canonical)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(cursor, 2), kv.Key);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(cursor + 2, 4), checked((uint)payloadCursor));
            cursor += 6;
            kv.Value.CopyTo(output, payloadCursor);
            payloadCursor += kv.Value.Length;
        }
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(cursor, 2), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(cursor + 2, 4), checked((uint)payloadCursor));
        cursor += 6;

        foreach (var alias in aliases.OrderBy(x => x.AliasId))
        {
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(cursor, 2), alias.AliasId);
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(cursor + 2, 2), targetIndex[alias.TargetId]);
            cursor += 4;
        }

        return output;
    }

    public static void WriteAtomic(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new InvalidOperationException("Chromium locales directory is missing; Zidimi will not create a parallel locale store.");
        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.zidimi-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }
}

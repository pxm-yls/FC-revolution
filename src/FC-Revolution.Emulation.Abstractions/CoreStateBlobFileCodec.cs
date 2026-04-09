using System.Collections.ObjectModel;
using System.Text;

namespace FCRevolution.Emulation.Abstractions;

public static class CoreStateBlobFileCodec
{
    private static readonly byte[] Magic = [0x46, 0x43, 0x53, 0x42, 0x31];
    private const int FormatVersion = 1;

    public static byte[] Serialize(CoreStateBlob state)
    {
        ArgumentNullException.ThrowIfNull(state);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(FormatVersion);
        writer.Write(state.Format);
        writer.Write(state.Metadata.Count);
        foreach (var entry in state.Metadata)
        {
            writer.Write(entry.Key);
            writer.Write(entry.Value);
        }

        writer.Write(state.Data.Length);
        writer.Write(state.Data);
        writer.Flush();
        return stream.ToArray();
    }

    public static CoreStateBlob Deserialize(byte[] payload, string legacyFormatFallback)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyFormatFallback);

        if (!HasHeader(payload))
        {
            return new CoreStateBlob
            {
                Format = legacyFormatFallback,
                Data = payload
            };
        }

        using var stream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var magic = reader.ReadBytes(Magic.Length);
        if (!magic.AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("Invalid core state blob header.");

        var version = reader.ReadInt32();
        if (version != FormatVersion)
            throw new InvalidDataException($"Unsupported core state blob version '{version}'.");

        var format = reader.ReadString();
        var metadataCount = reader.ReadInt32();
        var metadata = new Dictionary<string, string>(metadataCount, StringComparer.Ordinal);
        for (var index = 0; index < metadataCount; index++)
            metadata[reader.ReadString()] = reader.ReadString();

        var dataLength = reader.ReadInt32();
        if (dataLength < 0)
            throw new InvalidDataException("Core state blob data length must be non-negative.");

        var data = reader.ReadBytes(dataLength);
        if (data.Length != dataLength)
            throw new EndOfStreamException("Unexpected end of stream while reading core state blob data.");

        return new CoreStateBlob
        {
            Format = format,
            Data = data,
            Metadata = new ReadOnlyDictionary<string, string>(metadata)
        };
    }

    private static bool HasHeader(ReadOnlySpan<byte> payload) =>
        payload.Length >= Magic.Length &&
        payload[..Magic.Length].SequenceEqual(Magic);
}

using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Text;

namespace FCRevolution.Storage;

/// <summary>Reads compact per-frame replay records from disk.</summary>
public static class ReplayLogReader
{
    private static ReadOnlySpan<byte> Magic => "FCRL"u8;
    private const int HeaderSize = 5;
    private const byte LegacyVersion = 1;
    private const byte CurrentVersion = 2;

    public static List<FrameInputRecord> ReadAll(string path)
    {
        if (!File.Exists(path))
            return [];

        using var stream = File.OpenRead(path);
        return ReadAll(stream);
    }

    public static IEnumerable<FrameInputRecord> ReadRange(string path, long startExclusiveFrame, long endInclusiveFrame)
    {
        if (!File.Exists(path))
            yield break;

        using var stream = File.OpenRead(path);
        foreach (var record in ReadRange(stream, startExclusiveFrame, endInclusiveFrame))
            yield return record;
    }

    public static List<FrameInputRecord> ReadAll(Stream stream)
    {
        var header = ReadHeader(stream);

        var records = new List<FrameInputRecord>();
        foreach (var record in EnumerateRecords(stream, header))
            records.Add(record);

        return records;
    }

    public static IEnumerable<FrameInputRecord> ReadRange(Stream stream, long startExclusiveFrame, long endInclusiveFrame)
    {
        var header = ReadHeader(stream);
        foreach (var record in EnumerateRecords(stream, header))
        {
            if (record.Frame <= startExclusiveFrame)
                continue;
            if (record.Frame > endInclusiveFrame)
                yield break;

            yield return record;
        }
    }

    internal static ReplayLogHeader ReadHeader(Stream stream)
    {
        Span<byte> header = stackalloc byte[HeaderSize];
        if (stream.Read(header) != HeaderSize)
            throw new InvalidDataException("Replay log header is incomplete.");

        if (!header[..4].SequenceEqual(Magic))
            throw new InvalidDataException("Replay log magic mismatch.");

        return header[4] switch
        {
            LegacyVersion => new ReplayLogHeader(LegacyVersion, ["p1", "p2"]),
            CurrentVersion => ReadCurrentHeader(stream),
            _ => throw new InvalidDataException($"Unsupported replay log version: {header[4]}.")
        };
    }

    private static ReplayLogHeader ReadCurrentHeader(Stream stream)
    {
        var portCount = stream.ReadByte();
        if (portCount < 0)
            throw new InvalidDataException("Replay log header is incomplete.");

        var portIds = new List<string>(portCount);
        for (var index = 0; index < portCount; index++)
        {
            var length = stream.ReadByte();
            if (length <= 0)
                throw new InvalidDataException("Replay log port metadata is incomplete.");

            var portBytes = new byte[length];
            if (stream.Read(portBytes, 0, length) != length)
                throw new InvalidDataException("Replay log port metadata is incomplete.");

            var portId = Encoding.UTF8.GetString(portBytes).Trim();
            if (string.IsNullOrWhiteSpace(portId))
                throw new InvalidDataException("Replay log contains an empty port id.");

            portIds.Add(portId);
        }

        return new ReplayLogHeader(CurrentVersion, portIds);
    }

    private static IEnumerable<FrameInputRecord> EnumerateRecords(Stream stream, ReplayLogHeader header)
    {
        var recordSize = 8 + header.PortIds.Count;
        var buffer = new byte[recordSize];
        while (true)
        {
            var read = stream.Read(buffer, 0, recordSize);
            if (read == 0)
                yield break;
            if (read != recordSize)
                throw new InvalidDataException("Replay log record is truncated.");

            var buttonsByPort = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < header.PortIds.Count; index++)
                buttonsByPort[header.PortIds[index]] = buffer[8 + index];

            yield return new FrameInputRecord(
                BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(0, 8)),
                new ReadOnlyDictionary<string, byte>(buttonsByPort));
        }
    }

    internal sealed record ReplayLogHeader(byte Version, IReadOnlyList<string> PortIds);
}

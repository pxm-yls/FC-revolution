using System.Buffers.Binary;

namespace FCRevolution.Core.Replay;

/// <summary>Reads compact per-frame replay records from disk.</summary>
public static class ReplayLogReader
{
    private static ReadOnlySpan<byte> Magic => "FCRL"u8;
    private const byte CurrentVersion = 1;
    private const int HeaderSize = 5;
    private const int RecordSize = 10;

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
        ValidateHeader(stream);

        var records = new List<FrameInputRecord>();
        foreach (var record in EnumerateRecords(stream))
            records.Add(record);

        return records;
    }

    public static IEnumerable<FrameInputRecord> ReadRange(Stream stream, long startExclusiveFrame, long endInclusiveFrame)
    {
        ValidateHeader(stream);
        foreach (var record in EnumerateRecords(stream))
        {
            if (record.Frame <= startExclusiveFrame)
                continue;
            if (record.Frame > endInclusiveFrame)
                yield break;

            yield return record;
        }
    }

    private static void ValidateHeader(Stream stream)
    {
        Span<byte> header = stackalloc byte[HeaderSize];
        if (stream.Read(header) != HeaderSize)
            throw new InvalidDataException("Replay log header is incomplete.");

        if (!header[..4].SequenceEqual(Magic))
            throw new InvalidDataException("Replay log magic mismatch.");

        if (header[4] != CurrentVersion)
            throw new InvalidDataException($"Unsupported replay log version: {header[4]}.");
    }

    private static IEnumerable<FrameInputRecord> EnumerateRecords(Stream stream)
    {
        var buffer = new byte[RecordSize];
        while (true)
        {
            var read = stream.Read(buffer, 0, RecordSize);
            if (read == 0)
                yield break;
            if (read != RecordSize)
                throw new InvalidDataException("Replay log record is truncated.");

            yield return new FrameInputRecord(
                BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(0, 8)),
                buffer[8],
                buffer[9]);
        }
    }
}

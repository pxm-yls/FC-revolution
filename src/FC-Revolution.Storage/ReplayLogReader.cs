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
    private const byte ActionCatalogVersion = 3;

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
            LegacyVersion => new ReplayLogHeader(LegacyVersion, ReplayLogActionCatalog.CreateDefaultPortLayouts()),
            CurrentVersion => ReadCurrentHeader(stream),
            ActionCatalogVersion => ReadActionCatalogHeader(stream),
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

        return new ReplayLogHeader(CurrentVersion, ReplayLogActionCatalog.CreateDefaultPortLayouts(portIds));
    }

    private static ReplayLogHeader ReadActionCatalogHeader(Stream stream)
    {
        var portCount = stream.ReadByte();
        if (portCount < 0)
            throw new InvalidDataException("Replay log header is incomplete.");

        var layouts = new List<ReplayLogPortLayout>(portCount);
        for (var portIndex = 0; portIndex < portCount; portIndex++)
        {
            var portIdLength = stream.ReadByte();
            if (portIdLength <= 0)
                throw new InvalidDataException("Replay log port metadata is incomplete.");

            var portIdBytes = new byte[portIdLength];
            if (stream.Read(portIdBytes, 0, portIdLength) != portIdLength)
                throw new InvalidDataException("Replay log port metadata is incomplete.");

            var actionCount = stream.ReadByte();
            if (actionCount <= 0)
                throw new InvalidDataException("Replay log action metadata is incomplete.");

            var actionIds = new List<string>(actionCount);
            for (var actionIndex = 0; actionIndex < actionCount; actionIndex++)
            {
                var actionLength = stream.ReadByte();
                if (actionLength <= 0)
                    throw new InvalidDataException("Replay log action metadata is incomplete.");

                var actionBytes = new byte[actionLength];
                if (stream.Read(actionBytes, 0, actionLength) != actionLength)
                    throw new InvalidDataException("Replay log action metadata is incomplete.");

                var actionId = Encoding.UTF8.GetString(actionBytes).Trim();
                if (string.IsNullOrWhiteSpace(actionId))
                    throw new InvalidDataException("Replay log contains an empty action id.");

                actionIds.Add(actionId);
            }

            var portId = Encoding.UTF8.GetString(portIdBytes).Trim();
            if (string.IsNullOrWhiteSpace(portId))
                throw new InvalidDataException("Replay log contains an empty port id.");

            layouts.Add(new ReplayLogPortLayout(portId, actionIds));
        }

        return new ReplayLogHeader(ActionCatalogVersion, layouts);
    }

    private static IEnumerable<FrameInputRecord> EnumerateRecords(Stream stream, ReplayLogHeader header)
    {
        var recordSize = 8 + header.PortLayouts.Sum(static layout => layout.ByteLength);
        var buffer = new byte[recordSize];
        while (true)
        {
            var read = stream.Read(buffer, 0, recordSize);
            if (read == 0)
                yield break;
            if (read != recordSize)
                throw new InvalidDataException("Replay log record is truncated.");

            var actionsByPort = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
            var offset = 8;
            foreach (var portLayout in header.PortLayouts)
            {
                if (header.Version == ActionCatalogVersion)
                {
                    var actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (var actionIndex = 0; actionIndex < portLayout.ActionIds.Count; actionIndex++)
                    {
                        var byteIndex = actionIndex / 8;
                        var bitIndex = actionIndex % 8;
                        if ((buffer[offset + byteIndex] & (1 << bitIndex)) != 0)
                            actions.Add(portLayout.ActionIds[actionIndex]);
                    }

                    actionsByPort[portLayout.PortId] = actions;
                }
                else
                {
                    actionsByPort[portLayout.PortId] = ReplayLogActionCatalog.DecodeLegacyMask(buffer[offset]);
                }

                offset += portLayout.ByteLength;
            }

            yield return new FrameInputRecord(
                BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(0, 8)),
                new ReadOnlyDictionary<string, IReadOnlySet<string>>(actionsByPort));
        }
    }

    internal sealed record ReplayLogHeader(byte Version, IReadOnlyList<ReplayLogPortLayout> PortLayouts);
}

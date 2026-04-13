using StorageReplayLogReader = FCRevolution.Storage.ReplayLogReader;

namespace FCRevolution.Core.Replay;

/// <summary>Reads compact per-frame replay records from disk.</summary>
public static class ReplayLogReader
{
    public static List<FrameInputRecord> ReadAll(string path) => StorageReplayLogReader.ReadAll(path).Select(ToCoreRecord).ToList();

    public static IEnumerable<FrameInputRecord> ReadRange(string path, long startExclusiveFrame, long endInclusiveFrame)
        => StorageReplayLogReader.ReadRange(path, startExclusiveFrame, endInclusiveFrame).Select(ToCoreRecord);

    public static List<FrameInputRecord> ReadAll(Stream stream) => StorageReplayLogReader.ReadAll(stream).Select(ToCoreRecord).ToList();

    public static IEnumerable<FrameInputRecord> ReadRange(Stream stream, long startExclusiveFrame, long endInclusiveFrame)
        => StorageReplayLogReader.ReadRange(stream, startExclusiveFrame, endInclusiveFrame).Select(ToCoreRecord);

    private static FrameInputRecord ToCoreRecord(FCRevolution.Storage.FrameInputRecord record) =>
        new(record.Frame, record.Player1ButtonsMask, record.Player2ButtonsMask);
}

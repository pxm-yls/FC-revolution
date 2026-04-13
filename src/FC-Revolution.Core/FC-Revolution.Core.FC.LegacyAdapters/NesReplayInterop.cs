using FCRevolution.Core.Replay;
using FCRevolution.Core.State;

namespace FCRevolution.Core.FC.LegacyAdapters;

public readonly record struct LegacyReplayInputRecord(long Frame, byte Player1Mask, byte Player2Mask);

public readonly record struct LegacyReplayExportPlan(long BaseFrame, LegacyReplayInputRecord[] Records);

public sealed class NesReplayLogWriter : IDisposable
{
    private readonly ReplayLogWriter _writer = new();

    public bool IsOpen => _writer.IsOpen;

    public void Open(string path, bool resetFile) => _writer.Open(path, resetFile);

    public void Close() => _writer.Close();

    public void Append(long frame, byte player1Mask, byte player2Mask) =>
        _writer.Append(new FrameInputRecord(frame, player1Mask, player2Mask));

    public void Dispose() => _writer.Dispose();
}

public static class NesReplayInterop
{
    public static LegacyReplayExportPlan BuildReplayPlan(
        byte[] snapshotBytes,
        string inputLogPath,
        long startFrame,
        long endFrame)
    {
        if (endFrame < startFrame)
            throw new ArgumentOutOfRangeException(nameof(endFrame), "结束帧不能早于起始帧。");

        var baseFrame = StateSnapshotSerializer.HasHeader(snapshotBytes)
            ? StateSnapshotSerializer.Deserialize(snapshotBytes).Frame
            : startFrame;
        var records = ReplayLogReader.ReadRange(inputLogPath, baseFrame, endFrame)
            .Select(record => new LegacyReplayInputRecord(
                record.Frame,
                record.Player1ButtonsMask,
                record.Player2ButtonsMask))
            .ToArray();

        return new LegacyReplayExportPlan(baseFrame, records);
    }

    public static List<uint[]> RenderFrameRange(
        string romPath,
        byte[] snapshotBytes,
        string inputLogPath,
        long startFrame,
        long endFrame)
    {
        if (endFrame < startFrame)
            throw new ArgumentOutOfRangeException(nameof(endFrame), "结束帧不能早于起始帧。");

        var plan = BuildReplayPlan(snapshotBytes, inputLogPath, startFrame, endFrame);
        var legacyRecords = plan.Records
            .Select(record => new FrameInputRecord(record.Frame, record.Player1Mask, record.Player2Mask))
            .ToArray();
        var player = new ReplayPlayer(romPath, snapshotBytes, legacyRecords);
        return player.RenderFrameRange(startFrame, endFrame);
    }
}

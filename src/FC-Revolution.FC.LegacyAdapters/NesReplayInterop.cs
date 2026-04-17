using ReplayPlayer = FCRevolution.Core.Replay.ReplayPlayer;
using StorageReplayLogReader = FCRevolution.Storage.ReplayLogReader;
using StateSnapshotFrameReader = FCRevolution.Storage.StateSnapshotFrameReader;

namespace FCRevolution.FC.LegacyAdapters;

public static class NesReplayInterop
{
    public static List<uint[]> RenderFrameRange(
        string romPath,
        byte[] snapshotBytes,
        string inputLogPath,
        long startFrame,
        long endFrame)
    {
        if (endFrame < startFrame)
            throw new ArgumentOutOfRangeException(nameof(endFrame), "结束帧不能早于起始帧。");

        var baseFrame = StateSnapshotFrameReader.HasHeader(snapshotBytes)
            ? StateSnapshotFrameReader.ReadFrame(snapshotBytes)
            : startFrame;
        var records = StorageReplayLogReader.ReadRange(inputLogPath, baseFrame, endFrame).ToArray();
        var player = new ReplayPlayer(romPath, snapshotBytes, records);
        return player.RenderFrameRange(startFrame, endFrame);
    }
}

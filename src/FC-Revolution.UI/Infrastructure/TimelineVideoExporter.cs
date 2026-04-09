using FC_Revolution.UI.Adapters.Nes;

namespace FC_Revolution.UI.Infrastructure;

internal static class TimelineVideoExporter
{
    internal readonly record struct ReplayInputRecord(long Frame, byte Player1Mask, byte Player2Mask);

    internal readonly record struct ReplayExportPlan(long BaseFrame, ReplayInputRecord[] Records);

    public static string ExportMp4(
        string romPath,
        string snapshotPath,
        string inputLogPath,
        long startFrame,
        long endFrame,
        string outputPath)
    {
        return NesTimelineVideoExporter.ExportMp4(
            romPath,
            snapshotPath,
            inputLogPath,
            startFrame,
            endFrame,
            outputPath);
    }

    internal static ReplayExportPlan BuildReplayPlan(byte[] snapshotBytes, string inputLogPath, long startFrame, long endFrame) =>
        NesTimelineVideoExporter.BuildReplayPlan(snapshotBytes, inputLogPath, startFrame, endFrame);
}

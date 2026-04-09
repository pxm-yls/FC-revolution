using System;
using System.IO;
using System.Linq;
using FCRevolution.Core.Replay;
using FCRevolution.Core.State;

namespace FC_Revolution.UI.Infrastructure;

internal static class TimelineVideoExporter
{
    internal readonly record struct ReplayExportPlan(long BaseFrame, FrameInputRecord[] Records);

    public static string ExportMp4(
        string romPath,
        string snapshotPath,
        string inputLogPath,
        long startFrame,
        long endFrame,
        string outputPath)
    {
        if (endFrame < startFrame)
            throw new ArgumentOutOfRangeException(nameof(endFrame), "结束帧不能早于起始帧。");

        if (!File.Exists(snapshotPath))
            throw new FileNotFoundException("未找到导出起点快照。", snapshotPath);

        var snapshotBytes = File.ReadAllBytes(snapshotPath);
        var plan = BuildReplayPlan(snapshotBytes, inputLogPath, startFrame, endFrame);

        var player = new ReplayPlayer(romPath, snapshotBytes, plan.Records);
        var frames = player.RenderFrameRange(startFrame, endFrame);
        if (frames.Count == 0)
            throw new InvalidOperationException("导出区间没有生成任何画面帧。");

        FFmpegPreviewEncoder.EncodeMp4(outputPath, 256, 240, intervalMs: 1000 / 60, frames);
        return outputPath;
    }

    internal static ReplayExportPlan BuildReplayPlan(byte[] snapshotBytes, string inputLogPath, long startFrame, long endFrame)
    {
        if (endFrame < startFrame)
            throw new ArgumentOutOfRangeException(nameof(endFrame), "结束帧不能早于起始帧。");

        var baseFrame = StateSnapshotSerializer.HasHeader(snapshotBytes)
            ? StateSnapshotSerializer.Deserialize(snapshotBytes).Frame
            : startFrame;
        var records = ReplayLogReader.ReadRange(inputLogPath, baseFrame, endFrame).ToArray();

        return new ReplayExportPlan(baseFrame, records);
    }
}

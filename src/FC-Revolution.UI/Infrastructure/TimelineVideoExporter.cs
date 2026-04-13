using System;
using System.IO;
using System.Linq;
using FCRevolution.FC.LegacyAdapters;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Storage;

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
        if (endFrame < startFrame)
            throw new ArgumentOutOfRangeException(nameof(endFrame), "结束帧不能早于起始帧。");

        if (!File.Exists(snapshotPath))
            throw new FileNotFoundException("未找到导出起点快照。", snapshotPath);

        var snapshotBytes = File.ReadAllBytes(snapshotPath);
        var frames = NesReplayInterop.RenderFrameRange(
            romPath,
            snapshotBytes,
            inputLogPath,
            startFrame,
            endFrame);
        if (frames.Count == 0)
            throw new InvalidOperationException("导出区间没有生成任何画面帧。");

        FFmpegPreviewEncoder.EncodeMp4(
            outputPath,
            FrameRenderDefaults.Width,
            FrameRenderDefaults.Height,
            intervalMs: 1000 / 60,
            frames);
        return outputPath;
    }

    internal static ReplayExportPlan BuildReplayPlan(byte[] snapshotBytes, string inputLogPath, long startFrame, long endFrame)
    {
        if (endFrame < startFrame)
            throw new ArgumentOutOfRangeException(nameof(endFrame), "结束帧不能早于起始帧。");

        var baseFrame = StateSnapshotFrameReader.HasHeader(snapshotBytes)
            ? StateSnapshotFrameReader.ReadFrame(snapshotBytes)
            : startFrame;
        var records = ReplayLogReader.ReadRange(inputLogPath, baseFrame, endFrame)
            .Select(record => new ReplayInputRecord(record.Frame, record.Player1ButtonsMask, record.Player2ButtonsMask))
            .ToArray();

        return new ReplayExportPlan(
            baseFrame,
            records);
    }
}

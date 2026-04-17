using System;
using System.IO;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Rendering.Abstractions;

namespace FC_Revolution.UI.Infrastructure;

internal static class TimelineVideoExporter
{
    public static string ExportMp4(
        string romPath,
        string snapshotPath,
        string inputLogPath,
        long startFrame,
        long endFrame,
        string outputPath)
        => ExportMp4(
            romPath,
            snapshotPath,
            inputLogPath,
            startFrame,
            endFrame,
            outputPath,
            LegacyFeatureRuntime.Current);

    internal static string ExportMp4(
        string romPath,
        string snapshotPath,
        string inputLogPath,
        long startFrame,
        long endFrame,
        string outputPath,
        ILegacyFeatureRuntime legacyFeatureRuntime)
    {
        if (endFrame < startFrame)
            throw new ArgumentOutOfRangeException(nameof(endFrame), "结束帧不能早于起始帧。");

        if (!File.Exists(snapshotPath))
            throw new FileNotFoundException("未找到导出起点快照。", snapshotPath);

        ArgumentNullException.ThrowIfNull(legacyFeatureRuntime);
        if (!legacyFeatureRuntime.TryGetReplayFrameRenderer(out var replayFrameRenderer, out var errorMessage))
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(errorMessage)
                    ? "当前应用未提供导出分支视频所需的 legacy 渲染能力。"
                    : errorMessage);
        }

        var snapshotBytes = File.ReadAllBytes(snapshotPath);
        var frames = replayFrameRenderer.RenderFrameRange(
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
}

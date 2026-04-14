using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Storage;

namespace FC_Revolution.UI.Infrastructure;

internal static class TimelineVideoExporter
{
    internal readonly record struct ReplayInputRecord(long Frame, IReadOnlyDictionary<string, byte> MasksByPort)
    {
        public byte GetMask(string portId) =>
            MasksByPort.TryGetValue(portId, out var mask) ? mask : (byte)0;
    }

    internal readonly record struct ReplayExportPlan(long BaseFrame, ReplayInputRecord[] Records);

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

    internal static ReplayExportPlan BuildReplayPlan(byte[] snapshotBytes, string inputLogPath, long startFrame, long endFrame)
    {
        if (endFrame < startFrame)
            throw new ArgumentOutOfRangeException(nameof(endFrame), "结束帧不能早于起始帧。");

        var baseFrame = StateSnapshotFrameReader.HasHeader(snapshotBytes)
            ? StateSnapshotFrameReader.ReadFrame(snapshotBytes)
            : startFrame;
        var records = ReplayLogReader.ReadRange(inputLogPath, baseFrame, endFrame)
            .Select(record => new ReplayInputRecord(
                record.Frame,
                new Dictionary<string, byte>(record.ButtonsByPort, StringComparer.OrdinalIgnoreCase)))
            .ToArray();

        return new ReplayExportPlan(
            baseFrame,
            records);
    }
}

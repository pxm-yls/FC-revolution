using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Emulation.Host;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record PreviewFileData(int Width, int Height, int IntervalMs, List<uint[]> Frames);

internal sealed class MainWindowPreviewGenerationController
{
    private readonly CorePreviewFrameCaptureService _previewFrameCaptureService;
    private readonly int _previewSourceWidth;
    private readonly int _previewSourceHeight;
    private readonly int _previewDurationSeconds;
    private readonly int _previewPlaybackFps;
    private readonly int _previewSourceFps;
    private readonly int _previewAnimationFrameCount;
    private readonly int _previewCaptureStride;
    private readonly int _previewFrameIntervalMs;
    private readonly TimeSpan _previewBuildTimeout;
    private readonly string _previewMagicV1;
    private readonly string _previewMagicV2;
    private readonly string _legacyPreviewExtension;

    public MainWindowPreviewGenerationController(
        int previewSourceWidth,
        int previewSourceHeight,
        int previewDurationSeconds,
        int previewPlaybackFps,
        int previewSourceFps,
        int previewAnimationFrameCount,
        int previewCaptureStride,
        int previewFrameIntervalMs,
        TimeSpan previewBuildTimeout,
        string previewMagicV1,
        string previewMagicV2,
        string legacyPreviewExtension)
        : this(
            new CorePreviewFrameCaptureService(),
            previewSourceWidth,
            previewSourceHeight,
            previewDurationSeconds,
            previewPlaybackFps,
            previewSourceFps,
            previewAnimationFrameCount,
            previewCaptureStride,
            previewFrameIntervalMs,
            previewBuildTimeout,
            previewMagicV1,
            previewMagicV2,
            legacyPreviewExtension)
    {
    }

    public MainWindowPreviewGenerationController(
        Func<IEmulatorCoreSession> createCoreSession,
        int previewSourceWidth,
        int previewSourceHeight,
        int previewDurationSeconds,
        int previewPlaybackFps,
        int previewSourceFps,
        int previewAnimationFrameCount,
        int previewCaptureStride,
        int previewFrameIntervalMs,
        TimeSpan previewBuildTimeout,
        string previewMagicV1,
        string previewMagicV2,
        string legacyPreviewExtension)
        : this(
            new CorePreviewFrameCaptureService(createCoreSession),
            previewSourceWidth,
            previewSourceHeight,
            previewDurationSeconds,
            previewPlaybackFps,
            previewSourceFps,
            previewAnimationFrameCount,
            previewCaptureStride,
            previewFrameIntervalMs,
            previewBuildTimeout,
            previewMagicV1,
            previewMagicV2,
            legacyPreviewExtension)
    {
    }

    public MainWindowPreviewGenerationController(
        CorePreviewFrameCaptureService previewFrameCaptureService,
        int previewSourceWidth,
        int previewSourceHeight,
        int previewDurationSeconds,
        int previewPlaybackFps,
        int previewSourceFps,
        int previewAnimationFrameCount,
        int previewCaptureStride,
        int previewFrameIntervalMs,
        TimeSpan previewBuildTimeout,
        string previewMagicV1,
        string previewMagicV2,
        string legacyPreviewExtension)
    {
        _previewFrameCaptureService = previewFrameCaptureService;
        _previewSourceWidth = previewSourceWidth;
        _previewSourceHeight = previewSourceHeight;
        _previewDurationSeconds = previewDurationSeconds;
        _previewPlaybackFps = previewPlaybackFps;
        _previewSourceFps = previewSourceFps;
        _previewAnimationFrameCount = previewAnimationFrameCount;
        _previewCaptureStride = previewCaptureStride;
        _previewFrameIntervalMs = previewFrameIntervalMs;
        _previewBuildTimeout = previewBuildTimeout;
        _previewMagicV1 = previewMagicV1;
        _previewMagicV2 = previewMagicV2;
        _legacyPreviewExtension = legacyPreviewExtension;
    }

    public async Task GeneratePreviewVideoWithTimeoutAsync(
        string romPath,
        string previewPath,
        double previewResolutionScale,
        int previewGenerationSpeedMultiplier,
        PreviewEncodingMode selectedPreviewEncodingMode,
        Action<double>? progressCallback,
        Action<double, string>? uiProgressCallback)
    {
        using var cts = new CancellationTokenSource();
        var buildTask = Task.Run(
            () => GeneratePreviewVideo(
                romPath,
                previewPath,
                previewResolutionScale,
                previewGenerationSpeedMultiplier,
                selectedPreviewEncodingMode,
                cts.Token,
                progressCallback,
                uiProgressCallback),
            cts.Token);
        var timeoutTask = Task.Delay(_previewBuildTimeout, CancellationToken.None);
        var completed = await Task.WhenAny(buildTask, timeoutTask);
        if (completed != buildTask)
        {
            cts.Cancel();
            throw new TimeoutException($"生成超时（>{_previewBuildTimeout.TotalSeconds:F0}s）");
        }

        await buildTask;
    }

    public (int Width, int Height) GetPreviewOutputSize(double previewResolutionScale)
    {
        var width = Math.Clamp((int)Math.Round(_previewSourceWidth * previewResolutionScale), 1, _previewSourceWidth);
        var height = Math.Clamp((int)Math.Round(_previewSourceHeight * previewResolutionScale), 1, _previewSourceHeight);
        return (width, height);
    }

    public uint[] ResizePreviewFrame(uint[] source, int targetWidth, int targetHeight)
    {
        if (targetWidth == _previewSourceWidth && targetHeight == _previewSourceHeight)
            return [.. source];

        return DownscaleNearest(source, _previewSourceWidth, _previewSourceHeight, targetWidth, targetHeight);
    }

    public void WritePreviewFrame(uint[] source, int targetWidth, int targetHeight, uint[]? resizeBuffer, byte[] destination)
    {
        if (targetWidth == _previewSourceWidth && targetHeight == _previewSourceHeight)
        {
            Buffer.BlockCopy(source, 0, destination, 0, destination.Length);
            return;
        }

        if (resizeBuffer == null || resizeBuffer.Length != targetWidth * targetHeight)
            throw new InvalidOperationException("预览缩放缓冲区不可用。");

        DownscaleNearestInto(source, _previewSourceWidth, _previewSourceHeight, resizeBuffer, targetWidth, targetHeight);
        Buffer.BlockCopy(resizeBuffer, 0, destination, 0, destination.Length);
    }

    public void SavePreviewAsMp4(string previewPath, PreviewFileData preview)
    {
        FFmpegPreviewEncoder.EncodeMp4(previewPath, preview.Width, preview.Height, preview.IntervalMs, preview.Frames);
    }

    public void SavePreviewAsRawFrames(string previewPath, PreviewFileData preview)
    {
        var frameByteLength = checked(preview.Width * preview.Height * sizeof(uint));
        var frameBytes = new byte[frameByteLength];
        using var writer = new RawPreviewStreamWriter(
            previewPath,
            preview.Width,
            preview.Height,
            preview.IntervalMs,
            preview.Frames.Count,
            _previewMagicV2);
        foreach (var frame in preview.Frames)
        {
            Buffer.BlockCopy(frame, 0, frameBytes, 0, frameByteLength);
            writer.WriteFrame(frameBytes);
        }

        writer.Complete();
    }

    public void UpgradeLegacyPreview(
        string previewPath,
        string targetPreviewPath,
        Action<string, PreviewFileData>? persistPreview = null,
        Action<string>? deletePreviewFile = null,
        Action<string, string>? deleteSiblingLegacyPreview = null)
    {
        var preview = LoadLegacyPreviewData(previewPath);
        (persistPreview ?? SavePreviewAsMp4).Invoke(targetPreviewPath, preview);
        (deletePreviewFile ?? MainWindowPreviewAssetController.TryDeletePreviewFile).Invoke(previewPath);
        (deleteSiblingLegacyPreview ?? MainWindowPreviewAssetController.TryDeleteSiblingLegacyPreviewFile)
            .Invoke(targetPreviewPath, _legacyPreviewExtension);
    }

    public PreviewFileData LoadLegacyPreviewData(string previewPath)
    {
        using var file = File.OpenRead(previewPath);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new BinaryReader(gzip, Encoding.UTF8, leaveOpen: false);

        var magic = reader.ReadString();
        if (!string.Equals(magic, _previewMagicV1, StringComparison.Ordinal))
            throw new InvalidDataException("旧版预览格式不匹配");

        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        var intervalMs = reader.ReadInt32();
        var frameCount = reader.ReadInt32();
        var pixelCount = checked(width * height);

        var frames = new List<uint[]>(frameCount);
        for (var frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var frame = new uint[pixelCount];
            for (var pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
                frame[pixelIndex] = reader.ReadUInt32();

            frames.Add(frame);
        }

        return new PreviewFileData(width, height, intervalMs, frames);
    }

    private void GeneratePreviewVideo(
        string romPath,
        string previewPath,
        double previewResolutionScale,
        int previewGenerationSpeedMultiplier,
        PreviewEncodingMode selectedPreviewEncodingMode,
        CancellationToken cancellationToken,
        Action<double>? progressCallback,
        Action<double, string>? uiProgressCallback)
    {
        var previewSize = GetPreviewOutputSize(previewResolutionScale);
        var resizedFrame = previewSize.Width == _previewSourceWidth && previewSize.Height == _previewSourceHeight
            ? null
            : new uint[previewSize.Width * previewSize.Height];
        var frameBytes = new byte[previewSize.Width * previewSize.Height * sizeof(uint)];
        var totalFrames = _previewDurationSeconds * _previewSourceFps;
        FFmpegPreviewEncoder.EncodeMp4(previewPath, previewSize.Width, previewSize.Height, _previewFrameIntervalMs, stdin =>
        {
            var framesWritten = 0;
            var captureResult = _previewFrameCaptureService.Capture(
                new CorePreviewFrameCaptureRequest(
                    romPath,
                    TotalFrames: totalFrames,
                    CaptureStride: _previewCaptureStride,
                    MaxCapturedFrames: _previewAnimationFrameCount,
                    ExpectedWidth: _previewSourceWidth,
                    ExpectedHeight: _previewSourceHeight,
                    TargetRunFps: Math.Max(1, previewGenerationSpeedMultiplier) * _previewSourceFps),
                capturedFrame =>
                {
                    WritePreviewFrame(capturedFrame.Frame.Pixels, previewSize.Width, previewSize.Height, resizedFrame, frameBytes);
                    stdin.Write(frameBytes, 0, frameBytes.Length);
                    framesWritten++;
                },
                progress =>
                {
                    var completedFrames = Math.Max(0, progress.GeneratedFrames - 1);
                    var normalizedProgress = completedFrames / (double)Math.Max(1, totalFrames);
                    var generatedFps = completedFrames / Math.Max(0.001, progress.Elapsed.TotalSeconds);
                    progressCallback?.Invoke(normalizedProgress);
                    uiProgressCallback?.Invoke(
                        normalizedProgress,
                        $"正在离线生成 {_previewDurationSeconds} 秒 / {_previewAnimationFrameCount} 帧预览，输出 {previewSize.Width}x{previewSize.Height}，进度 {normalizedProgress:P0}，当前速度 {generatedFps:F0} fps");
                },
                cancellationToken);

            if (framesWritten == 0 || captureResult.CapturedFrames == 0)
                throw new InvalidOperationException("没有可编码的预览帧。");
        }, selectedPreviewEncodingMode);
    }

    private static uint[] DownscaleNearest(uint[] source, int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        var result = new uint[targetWidth * targetHeight];
        DownscaleNearestInto(source, sourceWidth, sourceHeight, result, targetWidth, targetHeight);
        return result;
    }

    private static void DownscaleNearestInto(uint[] source, int sourceWidth, int sourceHeight, uint[] destination, int targetWidth, int targetHeight)
    {
        for (var y = 0; y < targetHeight; y++)
        {
            var sourceY = y * sourceHeight / targetHeight;
            for (var x = 0; x < targetWidth; x++)
            {
                var sourceX = x * sourceWidth / targetWidth;
                destination[y * targetWidth + x] = source[sourceY * sourceWidth + sourceX];
            }
        }
    }

    private sealed class RawPreviewStreamWriter : IDisposable
    {
        private readonly FileStream _stream;
        private readonly BinaryWriter _writer;
        private readonly int _frameByteLength;
        private readonly int _expectedFrameCount;
        private int _framesWritten;

        public RawPreviewStreamWriter(string previewPath, int width, int height, int intervalMs, int frameCount, string previewMagicV2)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
            _stream = File.Create(previewPath);
            _writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);
            _frameByteLength = checked(width * height * sizeof(uint));
            _expectedFrameCount = frameCount;

            _writer.Write(Encoding.ASCII.GetBytes(previewMagicV2));
            _writer.Write(width);
            _writer.Write(height);
            _writer.Write(intervalMs);
            _writer.Write(frameCount);

            for (var index = 0; index < frameCount; index++)
                _writer.Write((long)index * _frameByteLength);

            _writer.Flush();
        }

        public void WriteFrame(byte[] frameBytes)
        {
            if (frameBytes.Length != _frameByteLength)
                throw new InvalidDataException("预览帧尺寸不匹配。");

            if (_framesWritten >= _expectedFrameCount)
                throw new InvalidOperationException("写入的预览帧数量超过声明值。");

            _stream.Write(frameBytes, 0, frameBytes.Length);
            _framesWritten++;
        }

        public void Complete()
        {
            if (_framesWritten != _expectedFrameCount)
                throw new InvalidOperationException($"预览帧数量不完整，期望 {_expectedFrameCount}，实际 {_framesWritten}。");

            _stream.Flush();
        }

        public void Dispose()
        {
            _writer.Dispose();
            _stream.Dispose();
        }
    }
}

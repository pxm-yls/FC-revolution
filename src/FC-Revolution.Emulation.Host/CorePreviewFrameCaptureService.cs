using System.Diagnostics;
using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.Emulation.Host;

public sealed record CorePreviewFrameCaptureRequest(
    string MediaPath,
    int TotalFrames,
    int CaptureStride,
    int MaxCapturedFrames,
    int ExpectedWidth,
    int ExpectedHeight,
    int TargetRunFps,
    int ProgressReportIntervalFrames = 120);

public sealed record CorePreviewCapturedFrame(
    int SourceFrameIndex,
    VideoFramePacket Frame);

public sealed record CorePreviewFrameCaptureProgress(
    int GeneratedFrames,
    int TotalFrames,
    int CapturedFrames,
    TimeSpan Elapsed);

public sealed record CorePreviewFrameCaptureResult(
    int GeneratedFrames,
    int CapturedFrames,
    TimeSpan Elapsed);

public sealed class CorePreviewFrameCaptureService
{
    private readonly Func<IEmulatorCoreSession> _createCoreSession;

    public CorePreviewFrameCaptureService()
        : this(CreateDefaultCoreSession)
    {
    }

    public CorePreviewFrameCaptureService(Func<IEmulatorCoreSession> createCoreSession)
    {
        ArgumentNullException.ThrowIfNull(createCoreSession);
        _createCoreSession = createCoreSession;
    }

    public CorePreviewFrameCaptureResult Capture(
        CorePreviewFrameCaptureRequest request,
        Action<CorePreviewCapturedFrame> onFrameCaptured,
        Action<CorePreviewFrameCaptureProgress>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onFrameCaptured);

        if (string.IsNullOrWhiteSpace(request.MediaPath))
            throw new ArgumentException("MediaPath is required.", nameof(request));
        if (request.TotalFrames < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "TotalFrames must be greater than or equal to zero.");
        if (request.CaptureStride <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "CaptureStride must be greater than zero.");
        if (request.MaxCapturedFrames < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "MaxCapturedFrames must be greater than or equal to zero.");
        if (request.ExpectedWidth <= 0 || request.ExpectedHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Expected frame dimensions must be greater than zero.");
        if (request.TargetRunFps <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "TargetRunFps must be greater than zero.");

        using var previewSession = _createCoreSession();
        var loadResult = previewSession.LoadMedia(new CoreMediaLoadRequest(request.MediaPath));
        if (!loadResult.Success)
            throw new InvalidOperationException(loadResult.ErrorMessage ?? "无法加载预览 ROM。");

        var generationWatch = Stopwatch.StartNew();
        VideoFramePacket? latestPacket = null;
        void HandleVideoFrame(VideoFramePacket packet) => latestPacket = packet;

        var generatedFrames = 0;
        var capturedFrames = 0;

        previewSession.VideoFrameReady += HandleVideoFrame;
        try
        {
            for (; generatedFrames < request.TotalFrames && capturedFrames < request.MaxCapturedFrames; generatedFrames++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var frameResult = previewSession.RunFrame();
                if (!frameResult.Success)
                    throw new InvalidOperationException(frameResult.ErrorMessage ?? "预览帧生成失败。");

                if ((generatedFrames + 1) % request.CaptureStride == 0)
                {
                    if (latestPacket == null)
                        throw new InvalidOperationException("预览核心未产出视频帧。");

                    if (latestPacket.Width != request.ExpectedWidth || latestPacket.Height != request.ExpectedHeight)
                    {
                        throw new InvalidOperationException(
                            $"预览源帧尺寸不匹配，期望 {request.ExpectedWidth}x{request.ExpectedHeight}，实际 {latestPacket.Width}x{latestPacket.Height}。");
                    }

                    onFrameCaptured(new CorePreviewCapturedFrame(
                        generatedFrames + 1,
                        ClonePacket(latestPacket)));
                    capturedFrames++;
                }

                Throttle(generatedFrames + 1, generationWatch.Elapsed, request.TargetRunFps, cancellationToken);

                if (onProgress != null &&
                    request.ProgressReportIntervalFrames > 0 &&
                    generatedFrames % request.ProgressReportIntervalFrames == 0)
                {
                    onProgress(new CorePreviewFrameCaptureProgress(
                        generatedFrames + 1,
                        request.TotalFrames,
                        capturedFrames,
                        generationWatch.Elapsed));
                }
            }
        }
        finally
        {
            previewSession.VideoFrameReady -= HandleVideoFrame;
        }

        return new CorePreviewFrameCaptureResult(
            generatedFrames,
            capturedFrames,
            generationWatch.Elapsed);
    }

    private static IEmulatorCoreSession CreateDefaultCoreSession() =>
        ManagedCoreRuntime.TryCreateSession(
            new CoreSessionLaunchRequest(),
            out var session,
            options: new ManagedCoreRuntimeOptions())
            ? session!
            : ManagedCoreRuntime.CreateUnavailableSession();

    private static VideoFramePacket ClonePacket(VideoFramePacket packet) => new()
    {
        Pixels = [.. packet.Pixels],
        Width = packet.Width,
        Height = packet.Height,
        PixelFormat = packet.PixelFormat,
        PresentationIndex = packet.PresentationIndex,
        TimestampSeconds = packet.TimestampSeconds
    };

    private static void Throttle(int generatedFrames, TimeSpan elapsed, int targetFps, CancellationToken cancellationToken)
    {
        if ((generatedFrames & 15) != 0)
            return;

        var expectedElapsed = TimeSpan.FromSeconds(generatedFrames / (double)targetFps);
        var delay = expectedElapsed - elapsed;
        if (delay <= TimeSpan.Zero)
            return;

        var sleepMs = (int)Math.Floor(delay.TotalMilliseconds);
        if (sleepMs > 0)
            Task.Delay(sleepMs, cancellationToken).GetAwaiter().GetResult();
    }
}

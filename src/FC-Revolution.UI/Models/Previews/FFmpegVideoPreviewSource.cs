using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;
using FFmpeg.AutoGen;

namespace FC_Revolution.UI.Models.Previews;

internal unsafe sealed class FFmpegVideoPreviewSource : IPreviewSource
{
    private static readonly SemaphoreSlim DecodeLimiter = new(1, 1);
    private static readonly int AvErrorAgain = ffmpeg.AVERROR(ffmpeg.EAGAIN);
    private static readonly VideoPreviewWindowEvictionPolicy WindowEvictionPolicy = new();

    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _decodeGate = new(1, 1);
    private readonly WriteableBitmap[] _bitmaps;
    private readonly byte[] _pixelBuffer;
    private readonly byte[] _decodeBuffer;
    private readonly string _previewPath;
    private AVFormatContext* _formatContext;
    private AVCodecContext* _codecContext;
    private AVStream* _stream;
    private int _streamIndex = -1;
    private SwsContext* _swsContext;
    private AVFrame* _frame;
    private AVPacket* _packet;
    private int _sourceWidth;
    private int _sourceHeight;
    private AVPixelFormat _sourcePixelFormat = AVPixelFormat.AV_PIX_FMT_NONE;
    private int _currentFrameIndex;
    private int _currentBitmapIndex;
    private bool _disposed;
    private WindowCache? _activeWindow;
    private WindowCache? _prefetchedWindow;
    private Task? _preloadTask;
    private CancellationTokenSource? _preloadCts;
    private int _lastRequestedFrameIndex = -1;
    private int _lastReturnedFrameIndex = -1;
    private int _lastDecodedWindowStart = -1;
    private int _lastDecodedWindowEnd = -1;
    private int _lastScheduledPreloadStart = -1;
    private int _lastScheduledPreloadLength;
    private string _lastFrameSource = "init";
    private string? _lastPreloadError;
    private int _decoderCleanupScheduled;

    public FFmpegVideoPreviewSource(string previewPath)
    {
        FFmpegRuntimeBootstrap.EnsureInitialized();
        _previewPath = previewPath;
        InitializeDecoder(previewPath);
        _bitmaps =
        [
            PreviewBitmapPool.Rent(Width, Height),
            PreviewBitmapPool.Rent(Width, Height)
        ];
        _pixelBuffer = new byte[checked(Width * Height * 4)];
        _decodeBuffer = new byte[_pixelBuffer.Length];
        GetFrame(0);
    }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public int IntervalMs { get; private set; }
    public int FrameCount { get; private set; }
    public bool IsAnimated => FrameCount > 1;
    public bool IsLegacyPreview => false;
    public bool IsMemoryBacked => _activeWindow is { Count: > 0 };
    public bool HasStreamHandle => false;
    public bool HasPrefetchedFrame => _prefetchedWindow is { Count: > 0 };
    public bool SupportsFullFrameCaching => false;
    public string DebugInfo =>
        $"video req={_lastRequestedFrameIndex} cur={_currentFrameIndex} ret={_lastReturnedFrameIndex} src={_lastFrameSource} frameCount={FrameCount} active=[{_activeWindow?.StartIndex ?? -1},{_activeWindow?.EndIndex ?? -1}] activeCount={_activeWindow?.Count ?? 0} prefetched=[{_prefetchedWindow?.StartIndex ?? -1},{_prefetchedWindow?.EndIndex ?? -1}] prefetchedCount={_prefetchedWindow?.Count ?? 0} decoded=[{_lastDecodedWindowStart},{_lastDecodedWindowEnd}] preloadStart={_lastScheduledPreloadStart} preloadLen={_lastScheduledPreloadLength} preloadRunning={_preloadTask is { IsCompleted: false }} preloadErr={_lastPreloadError ?? "-"}";
    public int CachedBitmapCount => 0;
    public int CachedFrameCount => (_activeWindow?.Count ?? 0) + (_prefetchedWindow?.Count ?? 0);
    public long EstimatedBitmapCacheBytes => (long)CachedBitmapCount * Width * Height * 4;
    public long EstimatedMemoryFrameBytes => (long)CachedFrameCount * Width * Height * 4;
    public WriteableBitmap Bitmap => _bitmaps[_currentBitmapIndex];

    public WriteableBitmap GetFrame(int index)
    {
        ThrowIfDisposed();

        var frameIndex = Math.Clamp(index, 0, Math.Max(0, FrameCount - 1));
        WindowCache? decodedWindow = null;

        try
        {
            // Cache hit path stays fully under _syncRoot. Cache miss decodes outside _syncRoot to avoid
            // lock inversion with background preload (decodeLock -> _syncRoot).
            lock (_syncRoot)
            {
                _lastRequestedFrameIndex = frameIndex;
                if (TryCopyCachedFrame(frameIndex))
                {
                    _currentFrameIndex = frameIndex;
                    SchedulePreload(frameIndex);
                    return CommitFrameBitmap(frameIndex);
                }
            }

            decodedWindow = DecodeWindow(frameIndex, GetWindowFrameCount(), CancellationToken.None);

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    decodedWindow.Dispose();
                    decodedWindow = null;
                    ThrowIfDisposed();
                }

                var windowToActivate = decodedWindow;
                decodedWindow = null;
                SetActiveWindow(windowToActivate!);

                if (!TryCopyCachedFrame(frameIndex))
                    throw new InvalidDataException($"视频预览解码失败: {Path.GetFileName(_previewPath)}");

                _currentFrameIndex = frameIndex;
                SchedulePreload(frameIndex);
                return CommitFrameBitmap(frameIndex);
            }
        }
        finally
        {
            decodedWindow?.Dispose();
        }
    }

    public WriteableBitmap AdvanceFrame()
    {
        if (!IsAnimated)
            return Bitmap;

        var nextIndex = (_currentFrameIndex + 1) % Math.Max(1, FrameCount);
        return GetFrame(nextIndex);
    }

    public IReadOnlyList<WriteableBitmap> LoadAllBitmaps()
    {
        ThrowIfDisposed();
        return [GetFrame(Math.Max(0, _currentFrameIndex))];
    }

    public void EnableMemoryPlayback()
    {
        ThrowIfDisposed();
    }

    public void DisableMemoryPlayback()
    {
        lock (_syncRoot)
        {
            ReleaseWindow(ref _activeWindow);
            ReleaseWindow(ref _prefetchedWindow);
        }
    }

    public void ReleaseBitmapCache() { }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        var preloadCts = Interlocked.Exchange(ref _preloadCts, null);
        preloadCts?.Cancel();
        preloadCts?.Dispose();
        _preloadTask = null;

        ReleaseWindow(ref _activeWindow);
        ReleaseWindow(ref _prefetchedWindow);
        foreach (var bitmap in _bitmaps)
            PreviewBitmapPool.Return(bitmap);

        ScheduleDecoderCleanup();
    }

    private WriteableBitmap CommitFrameBitmap(int frameIndex)
    {
        _currentBitmapIndex = (_currentBitmapIndex + 1) % _bitmaps.Length;
        PreviewBitmapHelpers.WriteBitmap(_bitmaps[_currentBitmapIndex], _pixelBuffer);
        _lastReturnedFrameIndex = frameIndex;
        return _bitmaps[_currentBitmapIndex];
    }

    private void LoadFrameIntoBuffer(int frameIndex)
    {
        if (TryCopyCachedFrame(frameIndex))
        {
            _currentFrameIndex = frameIndex;
            SchedulePreload(frameIndex);
            return;
        }

        var loadedWindow = DecodeWindow(frameIndex, GetWindowFrameCount(), CancellationToken.None);
        lock (_syncRoot)
        {
            SetActiveWindow(loadedWindow);
            if (!TryCopyCachedFrame(frameIndex))
                throw new InvalidDataException($"视频预览解码失败: {Path.GetFileName(_previewPath)}");

            _currentFrameIndex = frameIndex;
            SchedulePreload(frameIndex);
        }
    }

    private bool TryCopyCachedFrame(int frameIndex)
    {
        var lookupDecision = WindowEvictionPolicy.BuildFrameLookupDecision(
            frameIndex,
            DescribeWindow(_activeWindow),
            DescribeWindow(_prefetchedWindow));
        if (lookupDecision.Source == VideoPreviewFrameWindowSource.Active &&
            _activeWindow?.TryCopyFrame(frameIndex, _pixelBuffer) == true)
        {
            _lastFrameSource = "active";
            return true;
        }

        if (lookupDecision.Source == VideoPreviewFrameWindowSource.Prefetched &&
            _prefetchedWindow?.TryCopyFrame(frameIndex, _pixelBuffer) == true)
        {
            if (lookupDecision.PromotePrefetchedToActive)
            {
                ReleaseWindow(ref _activeWindow);
                _activeWindow = _prefetchedWindow;
                _prefetchedWindow = null;
            }

            _lastFrameSource = "prefetched";
            return true;
        }

        _lastFrameSource = "miss";
        return false;
    }

    private void SchedulePreload(int currentFrameIndex)
    {
        var prefetchFrameCount = WindowEvictionPolicy.ResolvePrefetchFrameWindowCount(
            IntervalMs,
            _decodeBuffer.Length,
            _activeWindow?.Count ?? 0);
        var decision = WindowEvictionPolicy.BuildPreloadWindowDecision(
            FrameCount,
            _disposed,
            prefetchFrameCount,
            DescribeWindow(_activeWindow),
            DescribeWindow(_prefetchedWindow),
            _preloadTask is { IsCompleted: false });
        if (!decision.ShouldSchedule)
            return;

        var nextStart = decision.StartIndex;
        prefetchFrameCount = decision.FrameCount;

        _lastScheduledPreloadStart = nextStart;
        _lastScheduledPreloadLength = prefetchFrameCount;
        _lastPreloadError = null;

        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _preloadCts, cts);
        previous?.Cancel();
        previous?.Dispose();

        var preloadTask = Task.Run(() =>
        {
            if (!TryDecodeWindowForPreload(nextStart, prefetchFrameCount, cts.Token, out var window) ||
                window == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_disposed || cts.IsCancellationRequested)
                {
                    window.Dispose();
                    return;
                }

                var replacementDecision = WindowEvictionPolicy.BuildPrefetchedWindowReplacementDecision();
                if (replacementDecision.EvictExistingPrefetchedWindow)
                    ReleaseWindow(ref _prefetchedWindow);

                _prefetchedWindow = window;
                _lastPreloadError = null;
            }
        }, cts.Token);
        _preloadTask = preloadTask;
        _ = ObservePreloadTaskAsync(preloadTask, cts);
    }

    private bool TryDecodeWindowForPreload(
        int startFrameIndex,
        int frameCount,
        CancellationToken cancellationToken,
        out WindowCache? window)
    {
        window = null;

        try
        {
            if (!VideoPreviewDecodeLockPolicy.TryAcquireNonBlocking(
                    DecodeLimiter,
                    _decodeGate,
                    cancellationToken,
                    out var decodeLock))
            {
                return false;
            }

            using (decodeLock)
            {
                window = DecodeWindowCore(startFrameIndex, frameCount, cancellationToken);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private int GetWindowFrameCount() =>
        VideoPreviewWindowPolicy.GetFrameWindowCount(IntervalMs, _decodeBuffer.Length);

    private void InitializeDecoder(string previewPath)
    {
        AVFormatContext* formatContext = null;
        ThrowOnError(ffmpeg.avformat_open_input(&formatContext, previewPath, null, null), "打开视频预览失败");
        _formatContext = formatContext;

        try
        {
            ThrowOnError(ffmpeg.avformat_find_stream_info(_formatContext, null), "读取视频流信息失败");
            _streamIndex = ffmpeg.av_find_best_stream(_formatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
            if (_streamIndex < 0)
                throw new InvalidDataException("预览文件中未找到视频流");

            _stream = _formatContext->streams[_streamIndex];
            var codec = ffmpeg.avcodec_find_decoder(_stream->codecpar->codec_id);
            if (codec == null)
                throw new InvalidOperationException("未找到可用的视频解码器");

            _codecContext = ffmpeg.avcodec_alloc_context3(codec);
            if (_codecContext == null)
                throw new InvalidOperationException("分配解码上下文失败");

            ThrowOnError(ffmpeg.avcodec_parameters_to_context(_codecContext, _stream->codecpar), "复制视频参数失败");
            ThrowOnError(ffmpeg.avcodec_open2(_codecContext, codec, null), "打开视频解码器失败");
            _frame = ffmpeg.av_frame_alloc();
            _packet = ffmpeg.av_packet_alloc();
            if (_frame == null || _packet == null)
                throw new InvalidOperationException("分配 FFmpeg 帧缓冲失败");

            Width = Math.Max(1, _codecContext->width);
            Height = Math.Max(1, _codecContext->height);
            IntervalMs = ResolveIntervalMs(_formatContext, _stream);
            FrameCount = ResolveFrameCount(_formatContext, _stream, IntervalMs);
            _currentFrameIndex = -1;
        }
        catch
        {
            FreeDecoder();
            throw;
        }
    }

    private WindowCache DecodeWindow(int startFrameIndex, int frameCount, CancellationToken cancellationToken)
    {
        using var decodeLock = VideoPreviewDecodeLockPolicy.AcquireBlocking(DecodeLimiter, _decodeGate, cancellationToken);
        return DecodeWindowCore(startFrameIndex, frameCount, cancellationToken);
    }

    private WindowCache DecodeWindowCore(int startFrameIndex, int frameCount, CancellationToken cancellationToken)
    {
        var frames = new Dictionary<int, byte[]>(frameCount);
        var fallbackFrameIndex = startFrameIndex - 1;
        var draining = false;
        var endFrameIndex = Math.Min(FrameCount - 1, startFrameIndex + frameCount - 1);
        var nextResolvedFrameIndex = startFrameIndex;
        var intervalMs = Math.Max(1, IntervalMs);
        var startTimeMs = startFrameIndex * (double)intervalMs;
        var prerollToleranceMs = intervalMs * 0.5d;
        SeekDecoder(startFrameIndex);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var receiveError = ffmpeg.avcodec_receive_frame(_codecContext, _frame);
            if (receiveError == 0)
            {
                fallbackFrameIndex++;
                var frameTimeMs = TryResolveFrameTimeMs(_frame, _stream);
                if (frameTimeMs.HasValue && frameTimeMs.Value + prerollToleranceMs < startTimeMs)
                {
                    ffmpeg.av_frame_unref(_frame);
                    continue;
                }

                var resolvedFrameIndex = frameTimeMs.HasValue
                    ? Math.Clamp(nextResolvedFrameIndex++, startFrameIndex, endFrameIndex)
                    : Math.Clamp(fallbackFrameIndex, startFrameIndex, endFrameIndex);

                ConvertFrameToBgra(_frame, _decodeBuffer);
                var copy = RentFrameBuffer(_decodeBuffer.Length);
                Buffer.BlockCopy(_decodeBuffer, 0, copy, 0, _decodeBuffer.Length);
                frames[resolvedFrameIndex] = copy;
                if (resolvedFrameIndex >= endFrameIndex)
                {
                    ffmpeg.av_frame_unref(_frame);
                    break;
                }

                ffmpeg.av_frame_unref(_frame);
                continue;
            }

            if (receiveError == ffmpeg.AVERROR_EOF)
                break;

            if (receiveError != AvErrorAgain)
                ThrowOnError(receiveError, "接收视频帧失败");

            if (!draining)
            {
                var readError = ffmpeg.av_read_frame(_formatContext, _packet);
                if (readError >= 0)
                {
                    try
                    {
                        if (_packet->stream_index == _streamIndex)
                            ThrowOnError(ffmpeg.avcodec_send_packet(_codecContext, _packet), "发送解码包失败");
                    }
                    finally
                    {
                        ffmpeg.av_packet_unref(_packet);
                    }

                    continue;
                }

                if (readError == ffmpeg.AVERROR_EOF)
                {
                    ThrowOnError(ffmpeg.avcodec_send_packet(_codecContext, null), "刷新解码器失败");
                    draining = true;
                    continue;
                }

                ThrowOnError(readError, "读取视频流失败");
            }

            break;
        }

        var normalizedFrames = VideoPreviewWindowFrameNormalizer.Normalize(
            frames,
            startFrameIndex,
            endFrameIndex);
        var normalizedEndIndex = normalizedFrames.Length == 0 ? startFrameIndex : endFrameIndex;
        return new WindowCache(startFrameIndex, normalizedEndIndex, normalizedFrames, _decodeBuffer.Length);
    }

    private void SeekDecoder(int startFrameIndex)
    {
        if (_formatContext == null || _codecContext == null || _stream == null)
            throw new InvalidOperationException("视频解码器尚未初始化。");

        ffmpeg.av_frame_unref(_frame);
        ffmpeg.av_packet_unref(_packet);

        var startTimeMs = startFrameIndex * (long)Math.Max(1, IntervalMs);
        var targetTimestamp = _stream->time_base.num > 0 && _stream->time_base.den > 0
            ? (long)Math.Floor(startTimeMs / (ffmpeg.av_q2d(_stream->time_base) * 1000d))
            : 0L;

        if (targetTimestamp > 0)
            ThrowOnError(ffmpeg.av_seek_frame(_formatContext, _streamIndex, targetTimestamp, ffmpeg.AVSEEK_FLAG_BACKWARD), "定位视频流失败");
        else
            ThrowOnError(ffmpeg.av_seek_frame(_formatContext, _streamIndex, 0, ffmpeg.AVSEEK_FLAG_BACKWARD), "重置视频流失败");

        ffmpeg.avcodec_flush_buffers(_codecContext);
        ffmpeg.avformat_flush(_formatContext);
    }

    private void SetActiveWindow(WindowCache window)
    {
        var assignmentDecision = WindowEvictionPolicy.BuildActiveWindowAssignmentDecision();
        if (assignmentDecision.EvictCurrentActiveWindow)
            ReleaseWindow(ref _activeWindow);

        _activeWindow = window;
        _lastDecodedWindowStart = window.StartIndex;
        _lastDecodedWindowEnd = window.EndIndex;
        if (assignmentDecision.EvictPrefetchedWindow)
            ReleaseWindow(ref _prefetchedWindow);
    }

    private void ConvertFrameToBgra(AVFrame* sourceFrame, byte[] destinationBuffer)
    {
        var frameFormat = (AVPixelFormat)sourceFrame->format;
        if (_swsContext == null ||
            _sourceWidth != sourceFrame->width ||
            _sourceHeight != sourceFrame->height ||
            _sourcePixelFormat != frameFormat)
        {
            if (_swsContext != null)
                ffmpeg.sws_freeContext(_swsContext);

            _sourceWidth = sourceFrame->width;
            _sourceHeight = sourceFrame->height;
            _sourcePixelFormat = frameFormat;
            _swsContext = ffmpeg.sws_getContext(
                _sourceWidth,
                _sourceHeight,
                _sourcePixelFormat,
                Width,
                Height,
                AVPixelFormat.AV_PIX_FMT_BGRA,
                ffmpeg.SWS_FAST_BILINEAR,
                null,
                null,
                null);

            if (_swsContext == null)
                throw new InvalidOperationException("初始化 swscale 失败");
        }

        fixed (byte* dst = destinationBuffer)
        {
            var srcData = new byte*[]
            {
                sourceFrame->data[0], sourceFrame->data[1], sourceFrame->data[2], sourceFrame->data[3],
                sourceFrame->data[4], sourceFrame->data[5], sourceFrame->data[6], sourceFrame->data[7]
            };
            var srcLineSize = new[]
            {
                sourceFrame->linesize[0], sourceFrame->linesize[1], sourceFrame->linesize[2], sourceFrame->linesize[3],
                sourceFrame->linesize[4], sourceFrame->linesize[5], sourceFrame->linesize[6], sourceFrame->linesize[7]
            };
            var dstData = new byte*[] { dst, null, null, null };
            var dstLineSize = new[] { Width * 4, 0, 0, 0 };

            var scaledHeight = ffmpeg.sws_scale(
                _swsContext,
                srcData,
                srcLineSize,
                0,
                sourceFrame->height,
                dstData,
                dstLineSize);

            if (scaledHeight <= 0)
                throw new InvalidOperationException("视频帧像素转换失败");
        }
    }

    private static int ResolveIntervalMs(AVFormatContext* formatContext, AVStream* stream)
    {
        var fps = ffmpeg.av_guess_frame_rate(formatContext, stream, null);
        if (fps.num <= 0 || fps.den <= 0)
        {
            fps = stream->avg_frame_rate.num > 0 && stream->avg_frame_rate.den > 0
                ? stream->avg_frame_rate
                : stream->r_frame_rate;
        }

        if (fps.num <= 0 || fps.den <= 0)
            return 33;

        return Math.Max(1, (int)Math.Round(1000d * fps.den / fps.num));
    }

    private static int ResolveFrameCount(AVFormatContext* formatContext, AVStream* stream, int intervalMs)
    {
        if (stream->nb_frames > 0)
            return (int)Math.Clamp(stream->nb_frames, 1, int.MaxValue);

        var duration = stream->duration > 0 && stream->time_base.den > 0
            ? stream->duration * ffmpeg.av_q2d(stream->time_base)
            : formatContext->duration > 0
                ? formatContext->duration / (double)ffmpeg.AV_TIME_BASE
                : 0d;

        if (duration <= 0)
            return 1;

        return Math.Max(1, (int)Math.Round(duration * 1000d / Math.Max(1, intervalMs)));
    }

    private static double? TryResolveFrameTimeMs(AVFrame* frame, AVStream* stream)
    {
        if (frame->best_effort_timestamp != ffmpeg.AV_NOPTS_VALUE &&
            stream->time_base.num > 0 &&
            stream->time_base.den > 0)
        {
            return frame->best_effort_timestamp * ffmpeg.av_q2d(stream->time_base) * 1000d;
        }

        return null;
    }

    private static void ThrowOnError(int errorCode, string message)
    {
        if (errorCode >= 0)
            return;

        const int errorBufferSize = 1024;
        var buffer = stackalloc byte[errorBufferSize];
        ffmpeg.av_strerror(errorCode, buffer, (ulong)errorBufferSize);
        var reason = Marshal.PtrToStringAnsi((nint)buffer) ?? $"FFmpeg 错误码 {errorCode}";
        throw new InvalidOperationException($"{message}: {reason}");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FFmpegVideoPreviewSource));
    }

    private void FreeDecoder()
    {
        if (_swsContext != null)
        {
            ffmpeg.sws_freeContext(_swsContext);
            _swsContext = null;
        }

        if (_frame != null)
        {
            var frameToFree = _frame;
            ffmpeg.av_frame_free(&frameToFree);
            _frame = null;
        }

        if (_packet != null)
        {
            var packetToFree = _packet;
            ffmpeg.av_packet_free(&packetToFree);
            _packet = null;
        }

        if (_codecContext != null)
        {
            var codecContextToFree = _codecContext;
            ffmpeg.avcodec_free_context(&codecContextToFree);
            _codecContext = null;
        }

        if (_formatContext != null)
        {
            var formatContext = _formatContext;
            ffmpeg.avformat_close_input(&formatContext);
            _formatContext = formatContext;
        }

        _stream = null;
        _streamIndex = -1;
        _sourceWidth = 0;
        _sourceHeight = 0;
        _sourcePixelFormat = AVPixelFormat.AV_PIX_FMT_NONE;
    }

    private static byte[] RentFrameBuffer(int length)
        => ArrayPool<byte>.Shared.Rent(length);

    private static void ReturnFrameBuffer(byte[]? buffer)
    {
        if (buffer != null)
            ArrayPool<byte>.Shared.Return(buffer);
    }

    private static void ReleaseWindow(ref WindowCache? window)
    {
        window?.Dispose();
        window = null;
    }

    private static VideoPreviewWindowRange? DescribeWindow(WindowCache? window) =>
        window is { Count: > 0 }
            ? new VideoPreviewWindowRange(window.StartIndex, window.EndIndex, window.Count)
            : null;

    private void ScheduleDecoderCleanup()
    {
        if (Interlocked.Exchange(ref _decoderCleanupScheduled, 1) != 0)
            return;

        if (_decodeGate.Wait(0))
        {
            try
            {
                FreeDecoder();
            }
            finally
            {
                ReleaseAndDisposeDecodeGate();
            }

            return;
        }

        Task gateWaitTask;
        try
        {
            gateWaitTask = _decodeGate.WaitAsync();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        _ = CompleteDecoderCleanupAsync(gateWaitTask);
    }

    private Task ObservePreloadTaskAsync(Task preloadTask, CancellationTokenSource cts)
    {
        return preloadTask.ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                var baseException = task.Exception?.GetBaseException();
                if (baseException is not OperationCanceledException &&
                    baseException is not ObjectDisposedException)
                {
                    lock (_syncRoot)
                    {
                        if (!_disposed && ReferenceEquals(_preloadCts, cts))
                            _lastPreloadError = baseException?.Message ?? "prefetch failed";
                    }
                }
            }

            lock (_syncRoot)
            {
                if (ReferenceEquals(_preloadTask, preloadTask))
                    _preloadTask = null;

                if (ReferenceEquals(_preloadCts, cts))
                    _preloadCts = null;
            }

            cts.Dispose();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private Task CompleteDecoderCleanupAsync(Task gateWaitTask)
    {
        return gateWaitTask.ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                if (task.Exception?.GetBaseException() is ObjectDisposedException)
                    return;

                task.Exception?.GetBaseException();
                return;
            }

            if (task.IsCanceled)
                return;

            try
            {
                FreeDecoder();
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                ReleaseAndDisposeDecodeGate();
            }
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void ReleaseAndDisposeDecodeGate()
    {
        try
        {
            _decodeGate.Release();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            _decodeGate.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private sealed class WindowCache : IDisposable
    {
        public WindowCache(int startIndex, int endIndex, byte[][] frames, int frameLength)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
            Frames = frames;
            FrameLength = frameLength;
        }

        public int StartIndex { get; }
        public int EndIndex { get; }
        public byte[][] Frames { get; }
        public int FrameLength { get; }
        public int Count => Frames.Length;

        public bool TryCopyFrame(int frameIndex, byte[] destination)
        {
            if (frameIndex < StartIndex || frameIndex > EndIndex)
                return false;

            var buffer = Frames[frameIndex - StartIndex];
            Buffer.BlockCopy(buffer, 0, destination, 0, FrameLength);
            return true;
        }

        public void Dispose()
        {
            var uniqueFrames = new HashSet<byte[]>(ReferenceEqualityComparer.Instance);
            foreach (var frame in Frames)
            {
                if (frame != null)
                    uniqueFrames.Add(frame);
            }

            foreach (var frame in uniqueFrames)
                ReturnFrameBuffer(frame);
        }
    }
}

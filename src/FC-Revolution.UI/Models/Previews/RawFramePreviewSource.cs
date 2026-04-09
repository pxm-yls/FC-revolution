using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.Models.Previews;

internal sealed class RawFramePreviewSource : IPreviewSource
{
    private readonly FileStream? _stream;
    private readonly long[] _frameOffsets;
    private readonly long _dataStart;
    private readonly byte[] _pixelBuffer;
    private readonly WriteableBitmap _bitmap;
    private readonly object _syncRoot = new();
    private List<WriteableBitmap>? _cachedBitmaps;
    private byte[][]? _cachedFrames;
    private byte[]? _prefetchedBuffer;
    private Task? _prefetchTask;
    private CancellationTokenSource? _prefetchCts;
    private int _prefetchGeneration;
    private int _prefetchedFrameIndex = -1;
    private int _currentFrameIndex;
    private bool _disposed;

    private RawFramePreviewSource(
        int width,
        int height,
        int intervalMs,
        int frameCount,
        FileStream? stream,
        long[] frameOffsets,
        long dataStart)
    {
        Width = width;
        Height = height;
        IntervalMs = intervalMs;
        FrameCount = frameCount;
        _stream = stream;
        _frameOffsets = frameOffsets;
        _dataStart = dataStart;
        _pixelBuffer = new byte[checked(width * height * 4)];
        _bitmap = PreviewBitmapPool.Rent(width, height);
    }

    public int Width { get; }
    public int Height { get; }
    public int IntervalMs { get; }
    public int FrameCount { get; }
    public bool IsAnimated => FrameCount > 1 && (_stream != null || _cachedFrames != null);
    public bool IsLegacyPreview { get; init; }
    public bool IsMemoryBacked => _cachedFrames != null;
    public bool HasStreamHandle => _stream != null;
    public bool HasPrefetchedFrame => _prefetchedBuffer != null;
    public bool SupportsFullFrameCaching => PreviewCachingPolicy.SupportsFullFrameCaching(Width, Height, FrameCount);
    public string DebugInfo => $"raw current={_currentFrameIndex} frames={FrameCount} prefetched={_prefetchedFrameIndex}";
    public int CachedBitmapCount => _cachedBitmaps?.Count ?? 0;
    public int CachedFrameCount => _cachedFrames?.Length ?? 0;
    public long EstimatedBitmapCacheBytes => (long)CachedBitmapCount * Width * Height * 4;
    public long EstimatedMemoryFrameBytes => (long)CachedFrameCount * Width * Height * 4;
    public WriteableBitmap Bitmap => _bitmap;

    public static RawFramePreviewSource Open(string previewPath, string previewMagicV1, string previewMagicV2)
    {
        using var probe = File.OpenRead(previewPath);
        var firstByte = probe.ReadByte();
        probe.Position = 0;

        return firstByte == (byte)'F'
            ? OpenV2(previewPath, previewMagicV2)
            : OpenLegacyStatic(previewPath, previewMagicV1);
    }

    public WriteableBitmap GetFrame(int index)
    {
        ThrowIfDisposed();

        if (_stream == null)
            return _bitmap;

        lock (_syncRoot)
        {
            var frameIndex = Math.Clamp(index, 0, FrameCount - 1);
            LoadFrameIntoBuffer(frameIndex);
            PreviewBitmapHelpers.WriteBitmap(_bitmap, _pixelBuffer);
            _currentFrameIndex = frameIndex;
            SchedulePrefetch((_currentFrameIndex + 1) % FrameCount);
            return _bitmap;
        }
    }

    public WriteableBitmap AdvanceFrame()
    {
        if (!IsAnimated)
            return _bitmap;

        var nextIndex = (_currentFrameIndex + 1) % FrameCount;
        return GetFrame(nextIndex);
    }

    public IReadOnlyList<WriteableBitmap> LoadAllBitmaps()
    {
        ThrowIfDisposed();

        if (!SupportsFullFrameCaching)
            return [GetFrame(Math.Max(0, _currentFrameIndex))];

        if (_cachedBitmaps != null)
            return _cachedBitmaps;

        lock (_syncRoot)
        {
            if (_cachedBitmaps != null)
                return _cachedBitmaps;

            var frames = new List<WriteableBitmap>(FrameCount);
            for (var i = 0; i < FrameCount; i++)
            {
                LoadFrameIntoBuffer(i);
                frames.Add(PreviewBitmapHelpers.CreateBitmapFromBytes(Width, Height, _pixelBuffer));
            }

            _cachedBitmaps = frames;
            return _cachedBitmaps;
        }
    }

    public void EnableMemoryPlayback()
    {
        ThrowIfDisposed();

        if (!SupportsFullFrameCaching || _cachedFrames != null || _stream == null || FrameCount <= 1)
            return;

        lock (_syncRoot)
        {
            if (!SupportsFullFrameCaching || _cachedFrames != null || _stream == null || FrameCount <= 1)
                return;

            var frames = new byte[FrameCount][];
            for (var i = 0; i < FrameCount; i++)
            {
                _stream.Seek(_dataStart + _frameOffsets[i], SeekOrigin.Begin);
                frames[i] = new byte[_pixelBuffer.Length];
                _stream.ReadExactly(frames[i]);
            }

            _cachedFrames = frames;
            CancelPendingPrefetchLocked();
        }
    }

    public void DisableMemoryPlayback()
    {
        lock (_syncRoot)
        {
            _cachedFrames = null;
            CancelPendingPrefetchLocked();
        }
    }

    public void ReleaseBitmapCache()
    {
        lock (_syncRoot)
        {
            _cachedBitmaps = null;
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
                return;

            _disposed = true;
            _cachedBitmaps = null;
            _cachedFrames = null;
            CancelPendingPrefetchLocked();
        }

        _stream?.Dispose();
        PreviewBitmapPool.Return(_bitmap);
    }

    private static RawFramePreviewSource OpenV2(string previewPath, string previewMagicV2)
    {
        var stream = File.Open(previewPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var magic = Encoding.ASCII.GetString(reader.ReadBytes(previewMagicV2.Length));
        if (!string.Equals(magic, previewMagicV2, StringComparison.Ordinal))
        {
            stream.Dispose();
            throw new InvalidDataException("预览格式不匹配");
        }

        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        var intervalMs = reader.ReadInt32();
        var frameCount = reader.ReadInt32();
        var offsets = new long[frameCount];
        for (var i = 0; i < frameCount; i++)
            offsets[i] = reader.ReadInt64();

        var dataStart = stream.Position;
        var preview = new RawFramePreviewSource(width, height, intervalMs, frameCount, stream, offsets, dataStart);
        preview.GetFrame(0);
        return preview;
    }

    private static RawFramePreviewSource OpenLegacyStatic(string previewPath, string previewMagicV1)
    {
        using var file = File.OpenRead(previewPath);
        using var gzip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Decompress);
        using var reader = new BinaryReader(gzip, Encoding.UTF8, leaveOpen: false);

        var magic = reader.ReadString();
        if (!string.Equals(magic, previewMagicV1, StringComparison.Ordinal))
            throw new InvalidDataException("预览格式不匹配");

        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        var intervalMs = reader.ReadInt32();
        var frameCount = reader.ReadInt32();
        var preview = new RawFramePreviewSource(width, height, intervalMs, Math.Max(1, frameCount), null, [], 0)
        {
            IsLegacyPreview = true
        };

        reader.ReadExactly(preview._pixelBuffer);
        PreviewBitmapHelpers.WriteBitmap(preview._bitmap, preview._pixelBuffer);
        return preview;
    }

    private void LoadFrameIntoBuffer(int frameIndex)
    {
        if (_cachedFrames != null)
        {
            Buffer.BlockCopy(_cachedFrames[frameIndex], 0, _pixelBuffer, 0, _pixelBuffer.Length);
            return;
        }

        if (_stream == null)
            return;

        if (_prefetchedBuffer != null && _prefetchedFrameIndex == frameIndex)
        {
            Buffer.BlockCopy(_prefetchedBuffer, 0, _pixelBuffer, 0, _pixelBuffer.Length);
            ClearPrefetchedFrameLocked();
            return;
        }

        _stream.Seek(_dataStart + _frameOffsets[frameIndex], SeekOrigin.Begin);
        _stream.ReadExactly(_pixelBuffer);
    }

    private void SchedulePrefetch(int frameIndex)
    {
        if (_stream == null || _cachedFrames != null || FrameCount <= 1)
            return;

        if (_prefetchedFrameIndex == frameIndex || _prefetchTask is { IsCompleted: false })
            return;

        var offset = _frameOffsets[frameIndex];
        var length = _pixelBuffer.Length;
        var path = _stream.Name;
        var generation = unchecked(++_prefetchGeneration);
        var cts = new CancellationTokenSource();
        var previousCts = _prefetchCts;
        _prefetchCts = cts;
        previousCts?.Cancel();
        previousCts?.Dispose();
        _prefetchedFrameIndex = frameIndex;

        _prefetchTask = PrefetchFrameAsync(path, _dataStart + offset, length, frameIndex, generation, cts);
    }

    private async Task PrefetchFrameAsync(
        string path,
        long fileOffset,
        int length,
        int frameIndex,
        int generation,
        CancellationTokenSource cts)
    {
        byte[]? buffer = null;
        try
        {
            cts.Token.ThrowIfCancellationRequested();
            using var prefetchStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            prefetchStream.Seek(fileOffset, SeekOrigin.Begin);
            buffer = new byte[length];
            await prefetchStream.ReadExactlyAsync(buffer.AsMemory(0, length), cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }

        lock (_syncRoot)
        {
            if (generation == _prefetchGeneration)
            {
                if (ReferenceEquals(_prefetchCts, cts))
                    _prefetchCts = null;

                _prefetchTask = null;
                if (_disposed || _cachedFrames != null || cts.IsCancellationRequested || buffer == null)
                {
                    _prefetchedBuffer = null;
                    _prefetchedFrameIndex = -1;
                }
                else
                {
                    _prefetchedBuffer = buffer;
                    _prefetchedFrameIndex = frameIndex;
                }
            }
        }

        cts.Dispose();
    }

    private void ClearPrefetchedFrameLocked()
    {
        _prefetchedBuffer = null;
        _prefetchedFrameIndex = -1;
        if (_prefetchTask is null || _prefetchTask.IsCompleted)
            _prefetchTask = null;
    }

    private void CancelPendingPrefetchLocked()
    {
        var cts = _prefetchCts;
        _prefetchCts = null;
        _prefetchGeneration = unchecked(_prefetchGeneration + 1);
        _prefetchTask = null;
        _prefetchedBuffer = null;
        _prefetchedFrameIndex = -1;
        cts?.Cancel();
        cts?.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RawFramePreviewSource));
    }
}

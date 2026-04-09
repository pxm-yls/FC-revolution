using System;
using System.Threading;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FCRevolution.Backend.Hosting;
using FCRevolution.Rendering.Abstractions;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowPendingFramePresentation(
    uint[] FrameBuffer,
    LayeredFrameData? LayeredFrame);

internal sealed class GameWindowFramePresenterController : IDisposable
{
    private readonly int _width;
    private readonly int _height;
    private readonly WriteableBitmap[] _displayBitmaps;
    private readonly uint[] _enhancedFrameBuffer;
    private int _displayBitmapIndex;
    private volatile uint[]? _pendingFrame;
    private volatile uint[]? _lastPresentedFrame;
    private volatile LayeredFrameData? _pendingLayeredFrame;
    private volatile LayeredFrameData? _lastPresentedLayeredFrame;
    private int _framePresentQueued;
    private bool _disposed;

    public GameWindowFramePresenterController(int width, int height)
    {
        _width = width;
        _height = height;
        _displayBitmaps =
        [
            CreateBitmap(width, height),
            CreateBitmap(width, height)
        ];
        _enhancedFrameBuffer = new uint[checked(width * height)];
        _displayBitmapIndex = 0;
        CurrentBitmap = _displayBitmaps[0];
    }

    public WriteableBitmap CurrentBitmap { get; private set; }

    public uint[]? LastPresentedFrame => _lastPresentedFrame;

    public LayeredFrameData? LastPresentedLayeredFrame => _lastPresentedLayeredFrame;

    public void EnqueueCoreFrame(uint[] frameBuffer, LayeredFrameData? layeredFrame)
    {
        ArgumentNullException.ThrowIfNull(frameBuffer);

        _pendingFrame = frameBuffer;
        _lastPresentedFrame = frameBuffer;
        _pendingLayeredFrame = layeredFrame;
        _lastPresentedLayeredFrame = layeredFrame;
    }

    public void SetPendingPreviewFrame(uint[] frameBuffer)
    {
        ArgumentNullException.ThrowIfNull(frameBuffer);
        _pendingFrame = frameBuffer;
        _pendingLayeredFrame = null;
    }

    public void ClearPendingFrame()
    {
        _pendingFrame = null;
        _pendingLayeredFrame = null;
    }

    public bool TryAcquirePresentSlot() =>
        Interlocked.Exchange(ref _framePresentQueued, 1) == 0;

    public bool ReleasePresentSlotAndCheckForPending()
    {
        Interlocked.Exchange(ref _framePresentQueued, 0);
        return !_disposed && _pendingFrame != null;
    }

    public bool TryTakePendingPresentation(out GameWindowPendingFramePresentation presentation)
    {
        var frameBuffer = _pendingFrame;
        if (frameBuffer == null)
        {
            presentation = default;
            return false;
        }

        _pendingFrame = null;
        var layeredFrame = _pendingLayeredFrame;
        _pendingLayeredFrame = null;
        presentation = new GameWindowPendingFramePresentation(frameBuffer, layeredFrame);
        return true;
    }

    public WriteableBitmap PresentFrame(uint[] frameBuffer, PixelEnhancementMode enhancementMode)
    {
        ArgumentNullException.ThrowIfNull(frameBuffer);
        ThrowIfDisposed();

        var nextIndex = (_displayBitmapIndex + 1) & 1;
        var bitmap = _displayBitmaps[nextIndex];
        using var locked = bitmap.Lock();

        unsafe
        {
            if (enhancementMode == PixelEnhancementMode.None)
            {
                fixed (uint* source = frameBuffer)
                {
                    Buffer.MemoryCopy(
                        source,
                        (void*)locked.Address,
                        (long)locked.RowBytes * _height,
                        (long)frameBuffer.Length * sizeof(uint));
                }
            }
            else
            {
                PixelEnhancer.Apply(frameBuffer, _enhancedFrameBuffer, _width, _height, enhancementMode);
                fixed (uint* source = _enhancedFrameBuffer)
                {
                    Buffer.MemoryCopy(
                        source,
                        (void*)locked.Address,
                        (long)locked.RowBytes * _height,
                        (long)_enhancedFrameBuffer.Length * sizeof(uint));
                }
            }
        }

        _displayBitmapIndex = nextIndex;
        CurrentBitmap = bitmap;
        return bitmap;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ClearPendingFrame();
        foreach (var bitmap in _displayBitmaps)
            bitmap.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(GameWindowFramePresenterController));
    }

    private static WriteableBitmap CreateBitmap(int width, int height) =>
        new(
            new Avalonia.PixelSize(width, height),
            new Avalonia.Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
}

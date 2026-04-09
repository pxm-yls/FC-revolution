using FCRevolution.Rendering.Abstractions;

namespace FCRevolution.Rendering.Metal;

public sealed class MacMetalPresenter : IDisposable
{
    public const int DefaultFrameWidth = 256;
    public const int DefaultFrameHeight = 240;

    private IntPtr _presenter;
    private MacMetalPresenterDiagnostics _diagnostics = MacMetalPresenterDiagnostics.Empty;

    public MacMetalPresenter(
        IntPtr parentViewHandle,
        int frameWidth = DefaultFrameWidth,
        int frameHeight = DefaultFrameHeight,
        MacUpscaleMode upscaleMode = MacUpscaleMode.None)
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("MacMetalPresenter 仅支持 macOS。");

        if (!MacMetalBridge.IsAvailable)
            throw new InvalidOperationException(MacMetalBridge.UnavailableReason ?? "FCRMetalBridge 不可用。");

        if (parentViewHandle == IntPtr.Zero)
            throw new ArgumentException("父级 NSView 句柄不能为空。", nameof(parentViewHandle));

        if (frameWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameWidth));

        if (frameHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameHeight));

        _presenter = MacMetalBridge.CreateMetalPresenter(parentViewHandle, (uint)frameWidth, (uint)frameHeight, upscaleMode);
        if (_presenter == IntPtr.Zero)
            throw new InvalidOperationException("创建 CAMetalLayer presenter 失败。");

        ViewHandle = MacMetalBridge.GetMetalPresenterViewHandle(_presenter);
        if (ViewHandle == IntPtr.Zero)
        {
            Dispose();
            throw new InvalidOperationException("获取 CAMetalLayer NSView 句柄失败。");
        }

        RefreshDiagnostics();
    }

    public static bool IsSupported => MacMetalBridge.IsAvailable;

    public static string? UnavailableReason => MacMetalBridge.UnavailableReason;

    public IntPtr ViewHandle { get; }

    public MacMetalPresenterDiagnostics Diagnostics => _diagnostics;

    public unsafe bool PresentFrame(ReadOnlySpan<uint> frameBuffer)
    {
        if (_presenter == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(MacMetalPresenter));

        if (frameBuffer.Length == 0)
            return false;

        fixed (uint* pixels = frameBuffer)
        {
            bool presented = MacMetalBridge.PresentFrame(_presenter, (IntPtr)pixels, (uint)frameBuffer.Length);
            RefreshDiagnostics();
            return presented;
        }
    }

    public unsafe bool PresentFrame(LayeredFrameData frameData)
    {
        ArgumentNullException.ThrowIfNull(frameData);

        if (_presenter == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(MacMetalPresenter));

        fixed (byte* chrAtlas = frameData.ChrAtlas)
        fixed (uint* palette = frameData.Palette)
        fixed (BackgroundTileRenderItem* backgroundTiles = frameData.BackgroundTiles)
        fixed (SpriteRenderItem* sprites = frameData.Sprites)
        {
            bool presented = MacMetalBridge.PresentLayeredFrame(
                _presenter,
                (IntPtr)chrAtlas,
                (uint)frameData.ChrAtlas.Length,
                (IntPtr)palette,
                (uint)frameData.Palette.Length,
                (IntPtr)backgroundTiles,
                (uint)frameData.BackgroundTiles.Length,
                (IntPtr)sprites,
                (uint)frameData.Sprites.Length,
                frameData.ShowBackground ? (byte)1 : (byte)0,
                frameData.ShowSprites ? (byte)1 : (byte)0,
                frameData.ShowBackgroundLeft8 ? (byte)1 : (byte)0,
                frameData.ShowSpritesLeft8 ? (byte)1 : (byte)0);
            RefreshDiagnostics();
            return presented;
        }
    }

    public void SetUpscaleMode(MacUpscaleMode upscaleMode)
    {
        if (_presenter == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(MacMetalPresenter));

        MacMetalBridge.SetMetalPresenterUpscaleMode(_presenter, upscaleMode);
        RefreshDiagnostics();
    }

    public void SetUpscaleOutputResolution(MacUpscaleOutputResolution outputResolution)
    {
        if (_presenter == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(MacMetalPresenter));

        MacMetalBridge.SetMetalPresenterUpscaleOutputResolution(_presenter, outputResolution);
        RefreshDiagnostics();
    }

    public void SetDisplaySize(double width, double height)
    {
        if (_presenter == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(MacMetalPresenter));

        if (width <= 0 || height <= 0)
            return;

        MacMetalBridge.SetMetalPresenterDisplaySize(_presenter, width, height);
        RefreshDiagnostics();
    }

    public void SetCornerRadius(double radius)
    {
        if (_presenter == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(MacMetalPresenter));

        MacMetalBridge.SetMetalPresenterCornerRadius(_presenter, Math.Max(0d, radius));
        RefreshDiagnostics();
    }

    public void RequestTemporalHistoryReset(MacMetalTemporalResetReason reason)
    {
        if (_presenter == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(MacMetalPresenter));

        if (reason == MacMetalTemporalResetReason.None)
            return;

        MacMetalBridge.RequestMetalPresenterTemporalHistoryReset(_presenter, reason);
        RefreshDiagnostics();
    }

    public void Dispose()
    {
        if (_presenter == IntPtr.Zero)
            return;

        MacMetalBridge.DestroyMetalPresenter(_presenter);
        _presenter = IntPtr.Zero;
    }

    private void RefreshDiagnostics()
    {
        if (_presenter == IntPtr.Zero)
        {
            _diagnostics = MacMetalPresenterDiagnostics.Empty;
            return;
        }

        if (MacMetalBridge.TryGetPresenterDiagnostics(_presenter, out MacMetalPresenterDiagnostics diagnostics))
            _diagnostics = diagnostics;
    }
}

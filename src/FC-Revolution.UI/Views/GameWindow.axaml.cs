using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.Controls;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Views;

public partial class GameWindow : Window
{
    private const double ViewportOuterBorderThickness = 2d;
    private const double ViewportInnerInset = 6d;
    private const double NativeViewportCornerRadius = 6d;
    private bool _isPanningBranchCanvas;
    private Point _lastBranchCanvasPoint;
    private BranchGalleryViewModel? _observedBranchGallery;
    private GameWindowViewModel? _observedGameWindowViewModel;
    private MacMetalViewHost? _macMetalViewHost;
    private bool _didAttemptMacMetalPresenter;
    private string? _lastViewportLayoutLog;
    private int _handledTemporalHistoryResetVersion;
    private MacMetalTemporalResetReason _pendingTemporalHistoryResetReason = MacMetalTemporalResetReason.None;

    public GameWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        Opened += OnOpened;
        Closed += OnClosed;
        Activated += OnActivated;
        DataContextChanged += OnDataContextChanged;
        ViewportLayoutRoot.SizeChanged += (_, _) => UpdateViewportPresenterLayout();
        ViewportPresenterSurface.SizeChanged += (_, _) => UpdateMacMetalSurfaceGeometry();
        OverlayTopPanel.SizeChanged += (_, _) => UpdateViewportPresenterLayout();
        OverlayBottomPanel.SizeChanged += (_, _) => UpdateViewportPresenterLayout();
        ViewportLayoutRoot.Focusable = true;
    }

    private async void OnOpened(object? sender, System.EventArgs e)
    {
        WriteWindowDiagnostics(
            "game-window",
            $"Opened event begin | visible={IsVisible} | state={WindowState} | bounds={Bounds.Width:0.##}x{Bounds.Height:0.##}");
        Focus();
        ConstrainToWorkingArea();
        if (DataContext is GameWindowViewModel vm)
        {
            TryEnableMacMetalPresenter(vm);
            await vm.EnsureProfileTrustAsync(this);
            CenterTimelineOnCurrentMarker();
            UpdateViewportPresenterLayout();
        }

        RequestForegroundActivation("opened");
        WriteWindowDiagnostics(
            "game-window",
            $"Opened event complete | visible={IsVisible} | state={WindowState} | bounds={Bounds.Width:0.##}x{Bounds.Height:0.##}");
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_observedGameWindowViewModel != null)
        {
            _observedGameWindowViewModel.PropertyChanged -= OnGameWindowViewModelPropertyChanged;
            _observedGameWindowViewModel.RawFramePresented -= OnRawFramePresented;
            _observedGameWindowViewModel.LayeredFramePresented -= OnLayeredFramePresented;
        }

        if (_observedBranchGallery != null)
            _observedBranchGallery.PropertyChanged -= OnBranchGalleryPropertyChanged;

        _observedGameWindowViewModel = DataContext as GameWindowViewModel;
        _handledTemporalHistoryResetVersion = 0;
        _pendingTemporalHistoryResetReason = MacMetalTemporalResetReason.None;
        _observedBranchGallery = _observedGameWindowViewModel?.BranchGallery;
        if (_observedBranchGallery != null)
            _observedBranchGallery.PropertyChanged += OnBranchGalleryPropertyChanged;

        if (_observedGameWindowViewModel != null)
        {
            _observedGameWindowViewModel.PropertyChanged += OnGameWindowViewModelPropertyChanged;
            _observedGameWindowViewModel.RawFramePresented += OnRawFramePresented;
            _observedGameWindowViewModel.LayeredFramePresented += OnLayeredFramePresented;
            ForwardTemporalHistoryResetRequestIfNeeded();
            if (IsVisible)
                TryEnableMacMetalPresenter(_observedGameWindowViewModel);
            if (_observedGameWindowViewModel.LastPresentedLayeredFrame != null)
                _macMetalViewHost?.PresentFrame(_observedGameWindowViewModel.LastPresentedLayeredFrame);
            else if (_observedGameWindowViewModel.LastPresentedFrame is { Length: > 0 } frame)
                _macMetalViewHost?.PresentFrame(frame);
        }

        UpdateViewportPresenterLayout();
    }

    private void OnBranchGalleryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BranchGalleryViewModel.CurrentMarkerX))
            CenterTimelineOnCurrentMarker();
    }

    private void OnGameWindowViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameWindowViewModel.ViewportAspectWidth) or nameof(GameWindowViewModel.ViewportAspectHeight))
            UpdateViewportPresenterLayout();
        else if (e.PropertyName == nameof(GameWindowViewModel.IsOverlayVisible))
        {
            UpdateViewportPresenterLayout();
            Dispatcher.UIThread.Post(
                () => ReclaimKeyboardFocus("overlay-visibility-changed"),
                DispatcherPriority.Input);
        }
        else if (e.PropertyName == nameof(GameWindowViewModel.UpscaleMode))
            _macMetalViewHost?.SetUpscaleMode(_observedGameWindowViewModel?.UpscaleMode ?? MacUpscaleMode.None);
        else if (e.PropertyName == nameof(GameWindowViewModel.UpscaleOutputResolution))
            _macMetalViewHost?.SetUpscaleOutputResolution(_observedGameWindowViewModel?.UpscaleOutputResolution ?? MacUpscaleOutputResolution.Hd1080);
        else if (e.PropertyName == nameof(GameWindowViewModel.TemporalHistoryResetVersion))
            ForwardTemporalHistoryResetRequestIfNeeded();
    }

    private void OnRawFramePresented(ReadOnlyMemory<uint> frameBuffer)
    {
        _macMetalViewHost?.PresentFrame(frameBuffer.Span);
    }

    private void OnLayeredFramePresented(LayeredFrameData frameData)
    {
        _macMetalViewHost?.PresentFrame(frameData);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_observedGameWindowViewModel != null)
        {
            _observedGameWindowViewModel.PropertyChanged -= OnGameWindowViewModelPropertyChanged;
            _observedGameWindowViewModel.RawFramePresented -= OnRawFramePresented;
            _observedGameWindowViewModel.LayeredFramePresented -= OnLayeredFramePresented;
            _observedGameWindowViewModel = null;
        }

        if (_observedBranchGallery != null)
        {
            _observedBranchGallery.PropertyChanged -= OnBranchGalleryPropertyChanged;
            _observedBranchGallery = null;
        }

        ReleaseMacMetalPresenter();
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(
            () => ReclaimKeyboardFocus("activated", requestActivation: false),
            DispatcherPriority.Input);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not GameWindowViewModel vm || !vm.ShouldHandleKey(e.Key, e.KeyModifiers))
            return;

        WriteWindowDiagnostics(
            "game-window",
            $"shortcut keydown | key={e.Key} | modifiers={e.KeyModifiers} | source={e.Source?.GetType().Name ?? "unknown"} | overlay={vm.IsOverlayVisible}");
        vm.OnKeyDown(e.Key, e.KeyModifiers);
        e.Handled = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is not GameWindowViewModel vm || !vm.ShouldHandleKey(e.Key, e.KeyModifiers))
            return;

        vm.OnKeyUp(e.Key, e.KeyModifiers);
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ReclaimKeyboardFocus("pointer-pressed");
    }

    private void OnBranchCanvasWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not GameWindowViewModel vm)
            return;

        vm.BranchGallery.AdjustZoom(e.Delta.Y > 0 ? 0.12 : -0.12);
        e.Handled = true;
    }

    private void OnBranchCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
            return;

        _isPanningBranchCanvas = true;
        _lastBranchCanvasPoint = e.GetPosition(BranchCanvasScrollViewer);
        e.Handled = true;
    }

    private void OnBranchCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanningBranchCanvas)
            return;

        var current = e.GetPosition(BranchCanvasScrollViewer);
        var delta = current - _lastBranchCanvasPoint;
        BranchCanvasScrollViewer.Offset = new Vector(
            Math.Max(0, BranchCanvasScrollViewer.Offset.X - delta.X),
            Math.Max(0, BranchCanvasScrollViewer.Offset.Y - delta.Y));
        _lastBranchCanvasPoint = current;
        e.Handled = true;
    }

    private void OnBranchCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPanningBranchCanvas = false;
        CenterTimelineOnCurrentMarker();
    }

    private void OnBranchNodeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is GameWindowViewModel vm && vm.BranchGallery.HasBranchSelection)
            vm.BranchGallery.LoadBranchCommand.Execute(null);

        e.Handled = true;
    }

    private void CenterTimelineOnCurrentMarker()
    {
        if (_isPanningBranchCanvas || DataContext is not GameWindowViewModel vm)
            return;

        var viewportWidth = Math.Max(0, BranchCanvasScrollViewer.Bounds.Width);
        if (viewportWidth <= 1)
            return;

        var targetX = Math.Max(0, vm.BranchGallery.CurrentMarkerX - viewportWidth / 2d);
        BranchCanvasScrollViewer.Offset = new Vector(targetX, Math.Max(0, BranchCanvasScrollViewer.Offset.Y));
    }

    private void TryEnableMacMetalPresenter(GameWindowViewModel vm)
    {
        if (_macMetalViewHost != null || _didAttemptMacMetalPresenter || !OperatingSystem.IsMacOS())
            return;

        _didAttemptMacMetalPresenter = true;
        if (!MacMetalViewHost.IsSupported)
        {
            WriteWindowDiagnostics("game-window", $"mac metal presenter unavailable: {MacMetalViewHost.UnavailableReason ?? "unknown"}");
            SoftwareViewportImage.IsVisible = true;
            vm.UpdateSoftwareRendererStatus(MacMetalViewHost.UnavailableReason ?? "macOS presenter 不可用");
            return;
        }

        try
        {
            _macMetalViewHost = new MacMetalViewHost
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                ClipToBounds = true,
                Focusable = false
            };
            _macMetalViewHost.DiagnosticsChanged += OnMacMetalDiagnosticsChanged;
            _macMetalViewHost.TemporalResetDiagnosticsChanged += OnMacMetalTemporalResetDiagnosticsChanged;
            _macMetalViewHost.PresentationFailed += OnMacMetalPresentationFailed;
            _macMetalViewHost.SetCornerRadius(NativeViewportCornerRadius);
            _macMetalViewHost.SetUpscaleMode(vm.UpscaleMode);
            _macMetalViewHost.SetUpscaleOutputResolution(vm.UpscaleOutputResolution);
            if (_pendingTemporalHistoryResetReason != MacMetalTemporalResetReason.None)
            {
                _macMetalViewHost.RequestTemporalHistoryReset(_pendingTemporalHistoryResetReason);
                _pendingTemporalHistoryResetReason = MacMetalTemporalResetReason.None;
            }
            ViewportPresenterSurface.Children.Add(_macMetalViewHost);
            SoftwareViewportImage.IsVisible = false;
            UpdateMacMetalSurfaceGeometry();
            ReclaimKeyboardFocus("enable-mac-metal");
            vm.UpdateTemporalHistoryResetStatus(_macMetalViewHost.TemporalResetDiagnostics);
            WriteWindowDiagnostics(
                "game-window",
                $"mac metal presenter enabled | upscale={vm.UpscaleMode} | output={vm.UpscaleOutputResolution} | window={Bounds.Width:0.##}x{Bounds.Height:0.##}");

            if (vm.LastPresentedLayeredFrame != null)
                _macMetalViewHost.PresentFrame(vm.LastPresentedLayeredFrame);
            else if (vm.LastPresentedFrame is { Length: > 0 } frame)
                _macMetalViewHost.PresentFrame(frame);
        }
        catch (Exception ex)
        {
            WriteWindowDiagnosticsException("game-window", "failed to enable mac metal presenter", ex);
            ReleaseMacMetalPresenter();
            SoftwareViewportImage.IsVisible = true;
            vm.UpdateSoftwareRendererStatus($"macOS presenter 启用失败: {ex.Message}");
        }
    }

    private void ReleaseMacMetalPresenter()
    {
        if (_macMetalViewHost == null)
            return;

        _macMetalViewHost.DiagnosticsChanged -= OnMacMetalDiagnosticsChanged;
        _macMetalViewHost.TemporalResetDiagnosticsChanged -= OnMacMetalTemporalResetDiagnosticsChanged;
        _macMetalViewHost.PresentationFailed -= OnMacMetalPresentationFailed;
        ViewportPresenterSurface.Children.Remove(_macMetalViewHost);
        _macMetalViewHost.Dispose();
        _macMetalViewHost = null;
        SoftwareViewportImage.IsVisible = true;
        _observedGameWindowViewModel?.UpdateSoftwareRendererStatus("已回到软件路径");
    }

    private void OnMacMetalDiagnosticsChanged(MacMetalPresenterDiagnostics diagnostics)
    {
        _observedGameWindowViewModel?.UpdateMetalPresenterDiagnostics(diagnostics);
        var temporalReset = _macMetalViewHost?.TemporalResetDiagnostics ?? "Temporal 重置: 未知";
        WriteWindowDiagnostics(
            "game-window",
            $"render path = {diagnostics.EffectiveUpscaleMode} (requested {diagnostics.RequestedUpscaleMode}) | internal={diagnostics.InternalWidth}x{diagnostics.InternalHeight} | render={diagnostics.OutputWidth}x{diagnostics.OutputHeight} | drawable={diagnostics.DrawableWidth}x{diagnostics.DrawableHeight} | display={diagnostics.TargetWidthPoints:0}x{diagnostics.TargetHeightPoints:0}@{diagnostics.DisplayScale:0.##}x | host={diagnostics.HostWidthPoints:0.##}x{diagnostics.HostHeightPoints:0.##} | layer={diagnostics.LayerWidthPoints:0.##}x{diagnostics.LayerHeightPoints:0.##} | fallback={diagnostics.FallbackReason} | {temporalReset}");
    }

    private void OnMacMetalTemporalResetDiagnosticsChanged(string diagnostics)
    {
        WriteWindowDiagnostics("game-window", diagnostics);
        _observedGameWindowViewModel?.UpdateTemporalHistoryResetStatus(diagnostics);
    }

    private void OnMacMetalPresentationFailed(string message)
    {
        WriteWindowDiagnostics("game-window", message);
        ReleaseMacMetalPresenter();
        _observedGameWindowViewModel?.UpdateSoftwareRendererStatus(message);
    }

    private void UpdateViewportPresenterLayout()
    {
        var aspectWidth = 256d;
        var aspectHeight = 240d;
        bool isOverlayVisible = false;

        if (DataContext is GameWindowViewModel vm)
        {
            aspectWidth = Math.Max(1d, vm.ViewportAspectWidth);
            aspectHeight = Math.Max(1d, vm.ViewportAspectHeight);
            isOverlayVisible = vm.IsOverlayVisible;
        }

        var available = CalculateViewportAvailableSize(
            ViewportLayoutRoot.Bounds.Width,
            ViewportLayoutRoot.Bounds.Height,
            isOverlayVisible,
            OverlayTopPanel.Bounds.Height,
            OverlayTopPanel.Margin,
            OverlayBottomPanel.Bounds.Height,
            OverlayBottomPanel.Margin);
        var availableWidth = available.AvailableWidth;
        var availableHeight = available.AvailableHeight;
        var overlayTopReserve = MeasureOverlayReserve(OverlayTopPanel, isOverlayVisible);
        var overlayBottomReserve = MeasureOverlayReserve(OverlayBottomPanel, isOverlayVisible);
        ViewportPresenterFrame.Margin = CalculateViewportFrameMargin(overlayTopReserve, overlayBottomReserve);
        if (availableWidth <= 1d || availableHeight <= 1d)
            return;

        var scale = Math.Min(availableWidth / aspectWidth, availableHeight / aspectHeight);
        ViewportPresenterFrame.Width = Math.Max(1d, Math.Floor(aspectWidth * scale));
        ViewportPresenterFrame.Height = Math.Max(1d, Math.Floor(aspectHeight * scale));
        var layoutLog =
            $"viewport layout | window={Bounds.Width:0.##}x{Bounds.Height:0.##} | available={availableWidth:0.##}x{availableHeight:0.##} | overlay-reserve={overlayTopReserve:0.##}+{overlayBottomReserve:0.##} | frame={ViewportPresenterFrame.Width:0.##}x{ViewportPresenterFrame.Height:0.##} | surface={ViewportPresenterSurface.Bounds.Width:0.##}x{ViewportPresenterSurface.Bounds.Height:0.##} | aspect={aspectWidth:0.###}:{aspectHeight:0.###} | scale={scale:0.####} | overlay={_observedGameWindowViewModel?.IsOverlayVisible ?? false} | branch={_observedGameWindowViewModel?.IsBranchGalleryVisible ?? false}";
        if (!string.Equals(_lastViewportLayoutLog, layoutLog, StringComparison.Ordinal))
        {
            _lastViewportLayoutLog = layoutLog;
            WriteWindowDiagnostics("game-window", layoutLog);
        }

        UpdateMacMetalSurfaceGeometry();
    }

    internal static (double Width, double Height) CalculateViewportSurfaceSize(double frameWidth, double frameHeight)
    {
        var insetWidth = (ViewportOuterBorderThickness * 2d) + (ViewportInnerInset * 2d);
        var insetHeight = (ViewportOuterBorderThickness * 2d) + (ViewportInnerInset * 2d);
        return (
            Width: Math.Max(1d, frameWidth - insetWidth),
            Height: Math.Max(1d, frameHeight - insetHeight));
    }

    internal static (double AvailableWidth, double AvailableHeight) CalculateViewportAvailableSize(
        double layoutWidth,
        double layoutHeight,
        bool isOverlayVisible,
        double overlayTopHeight,
        Thickness overlayTopMargin,
        double overlayBottomHeight,
        Thickness overlayBottomMargin)
    {
        var availableWidth = Math.Max(0d, layoutWidth);
        var overlayTopReserve = isOverlayVisible
            ? Math.Max(0d, overlayTopHeight) + Math.Max(0d, overlayTopMargin.Top) + Math.Max(0d, overlayTopMargin.Bottom)
            : 0d;
        var overlayBottomReserve = isOverlayVisible
            ? Math.Max(0d, overlayBottomHeight) + Math.Max(0d, overlayBottomMargin.Top) + Math.Max(0d, overlayBottomMargin.Bottom)
            : 0d;
        var availableHeight = Math.Max(0d, layoutHeight - overlayTopReserve - overlayBottomReserve);
        return (availableWidth, availableHeight);
    }

    internal static Thickness CalculateViewportFrameMargin(double overlayTopReserve, double overlayBottomReserve)
    {
        return new Thickness(
            left: 0d,
            top: Math.Max(0d, overlayTopReserve),
            right: 0d,
            bottom: Math.Max(0d, overlayBottomReserve));
    }

    private static double MeasureOverlayReserve(Control overlayPanel, bool isOverlayVisible)
    {
        if (!isOverlayVisible)
            return 0d;

        return Math.Max(0d, overlayPanel.Bounds.Height) +
               Math.Max(0d, overlayPanel.Margin.Top) +
               Math.Max(0d, overlayPanel.Margin.Bottom);
    }

    private static void WriteWindowDiagnostics(string stage, string message)
    {
        StartupDiagnostics.Write(stage, message);
        GeometryDiagnostics.Write(stage, message);
    }

    private void ForwardTemporalHistoryResetRequestIfNeeded()
    {
        if (_observedGameWindowViewModel == null)
            return;

        if (_observedGameWindowViewModel.TemporalHistoryResetVersion <= _handledTemporalHistoryResetVersion)
            return;

        _handledTemporalHistoryResetVersion = _observedGameWindowViewModel.TemporalHistoryResetVersion;
        var reason = _observedGameWindowViewModel.TemporalHistoryResetReason;
        if (reason == MacMetalTemporalResetReason.None)
            return;

        var reasonLabel = GameWindowViewportDiagnosticsController.GetTemporalResetReasonLabel(reason);

        if (_macMetalViewHost == null)
        {
            _pendingTemporalHistoryResetReason = reason;
            _observedGameWindowViewModel.UpdateTemporalHistoryResetStatus($"Temporal 重置: 已记录，等待 presenter | reason={reasonLabel}");
            WriteWindowDiagnostics("game-window", $"temporal reset deferred | reason={reason} ({reasonLabel})");
            return;
        }

        _macMetalViewHost.RequestTemporalHistoryReset(reason);
        _observedGameWindowViewModel.UpdateTemporalHistoryResetStatus(_macMetalViewHost.TemporalResetDiagnostics);
        WriteWindowDiagnostics("game-window", $"temporal reset forwarded | reason={reason} ({reasonLabel})");
    }

    private static void WriteWindowDiagnosticsException(string stage, string message, Exception exception)
    {
        StartupDiagnostics.WriteException(stage, message, exception);
        GeometryDiagnostics.WriteException(stage, message, exception);
    }

    internal static (double Width, double Height, double MinWidth, double MinHeight) ConstrainWindowSizeToWorkingArea(
        double requestedWidth,
        double requestedHeight,
        double minWidth,
        double minHeight,
        PixelRect workingArea)
    {
        var targetWidth = Math.Max(1d, Math.Min(requestedWidth, workingArea.Width * 0.94));
        var targetHeight = Math.Max(1d, Math.Min(requestedHeight, workingArea.Height * 0.92));
        return (
            Width: targetWidth,
            Height: targetHeight,
            MinWidth: Math.Min(minWidth, targetWidth),
            MinHeight: Math.Min(minHeight, targetHeight));
    }

    private void ConstrainToWorkingArea()
    {
        var screen = Screens.ScreenFromWindow(this);
        if (screen == null)
        {
            WriteWindowDiagnostics("game-window", "no screen found for GameWindow");
            return;
        }

        var constrained = ConstrainWindowSizeToWorkingArea(Width, Height, MinWidth, MinHeight, screen.WorkingArea);
        MinWidth = constrained.MinWidth;
        MinHeight = constrained.MinHeight;
        Width = constrained.Width;
        Height = constrained.Height;
        WriteWindowDiagnostics(
            "game-window",
            $"working area constraint | working={screen.WorkingArea.Width}x{screen.WorkingArea.Height} | window={Width:0.##}x{Height:0.##} | min={MinWidth:0.##}x{MinHeight:0.##}");
    }

    private void UpdateMacMetalSurfaceGeometry()
    {
        if (_macMetalViewHost == null)
            return;

        var surfaceWidth = ViewportPresenterSurface.Bounds.Width;
        var surfaceHeight = ViewportPresenterSurface.Bounds.Height;
        if (surfaceWidth <= 1d || surfaceHeight <= 1d)
        {
            var computedSurface = CalculateViewportSurfaceSize(ViewportPresenterFrame.Width, ViewportPresenterFrame.Height);
            surfaceWidth = computedSurface.Width;
            surfaceHeight = computedSurface.Height;
        }

        _macMetalViewHost.SetDisplaySize(surfaceWidth, surfaceHeight);
    }

    private void RequestForegroundActivation(string stage)
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
        Focus();
        WriteWindowDiagnostics(
            "game-window",
            $"foreground request {stage} | visible={IsVisible} | state={WindowState} | bounds={Bounds.Width:0.##}x{Bounds.Height:0.##}");

        if (!string.Equals(stage, "opened", StringComparison.Ordinal))
            return;

        Dispatcher.UIThread.Post(
            () => RequestForegroundActivation("loaded"),
            DispatcherPriority.Loaded);
        Dispatcher.UIThread.Post(
            () => RequestForegroundActivation("background"),
            DispatcherPriority.Background);
    }

    private void ReclaimKeyboardFocus(string reason, bool requestActivation = true)
    {
        if (requestActivation && !IsActive)
            Activate();

        var viewportFocused = ViewportLayoutRoot.Focus();
        var windowFocused = Focus();
        WriteWindowDiagnostics(
            "game-window",
            $"focus reclaim {reason} | active={IsActive} | visible={IsVisible} | state={WindowState} | viewportFocused={viewportFocused} | windowFocused={windowFocused}");
    }
}

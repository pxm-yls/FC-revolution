using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.Controls;

public sealed class MacMetalViewHost : NativeControlHost, IDisposable
{
    private sealed class ResetDisposable(Action reset) : IDisposable
    {
        private Action? _reset = reset;

        public void Dispose()
        {
            _reset?.Invoke();
            _reset = null;
        }
    }

    private static Func<IntPtr, MacUpscaleMode, IMacMetalPresenterHost> s_presenterFactory =
        static (parentHandle, upscaleMode) => new MacMetalPresenterHost(parentHandle, upscaleMode);

    private static bool? s_isSupportedOverride;
    private static string? s_unavailableReasonOverride;

    private IMacMetalPresenterHost? _presenter;
    private uint[]? _lastFrame;
    private LayeredFrameData? _lastLayeredFrame;
    private MacUpscaleMode _upscaleMode = MacUpscaleMode.None;
    private MacUpscaleOutputResolution _upscaleOutputResolution = MacUpscaleOutputResolution.Hd1080;
    private MacMetalPresenterDiagnostics? _lastDiagnostics;
    private double _targetDisplayWidth;
    private double _targetDisplayHeight;
    private double _cornerRadius;
    private string? _lastGeometryLog;
    private string _temporalResetDiagnostics = "Temporal 重置: 未请求";
    private MacMetalTemporalResetReason _pendingTemporalResetReason = MacMetalTemporalResetReason.None;

    public static bool IsSupported => s_isSupportedOverride ?? MacMetalPresenter.IsSupported;

    public static string? UnavailableReason => s_isSupportedOverride.HasValue
        ? s_unavailableReasonOverride
        : MacMetalPresenter.UnavailableReason;

    internal static IDisposable OverridePresenterFactoryForTests(Func<IntPtr, MacUpscaleMode, IMacMetalPresenterHost> presenterFactory)
    {
        ArgumentNullException.ThrowIfNull(presenterFactory);

        var previous = s_presenterFactory;
        s_presenterFactory = presenterFactory;
        return new ResetDisposable(() => s_presenterFactory = previous);
    }

    internal static IDisposable OverrideAvailabilityForTests(bool isSupported, string? unavailableReason = null)
    {
        bool? previousIsSupported = s_isSupportedOverride;
        string? previousReason = s_unavailableReasonOverride;
        s_isSupportedOverride = isSupported;
        s_unavailableReasonOverride = unavailableReason;
        return new ResetDisposable(() =>
        {
            s_isSupportedOverride = previousIsSupported;
            s_unavailableReasonOverride = previousReason;
        });
    }

    public event Action<MacMetalPresenterDiagnostics>? DiagnosticsChanged;
    public event Action<string>? TemporalResetDiagnosticsChanged;
    public event Action<string>? PresentationFailed;

    public string TemporalResetDiagnostics => _temporalResetDiagnostics;

    public void PresentFrame(ReadOnlySpan<uint> frameBuffer)
    {
        if (frameBuffer.Length == 0)
            return;

        if (_presenter != null)
        {
            if (TryPresentFrame(frameBuffer))
                PublishDiagnostics();
            return;
        }

        _lastFrame = frameBuffer.ToArray();
    }

    public void PresentFrame(LayeredFrameData frameData)
    {
        ArgumentNullException.ThrowIfNull(frameData);

        if (_presenter != null)
        {
            if (TryPresentFrame(frameData))
                PublishDiagnostics();
            return;
        }

        _lastLayeredFrame = frameData;
    }

    public void SetUpscaleMode(MacUpscaleMode upscaleMode)
    {
        _upscaleMode = upscaleMode;
        if (_presenter == null)
            return;

        _presenter.SetUpscaleMode(upscaleMode);
        if (_lastLayeredFrame != null)
            TryPresentFrame(_lastLayeredFrame);
        else if (_lastFrame is { Length: > 0 })
            TryPresentFrame(_lastFrame);

        PublishDiagnostics();
    }

    public void RequestTemporalHistoryReset(MacMetalTemporalResetReason reason)
    {
        if (reason == MacMetalTemporalResetReason.None)
            return;

        if (_presenter == null)
        {
            _pendingTemporalResetReason = reason;
            PublishTemporalResetDiagnostics(BuildQueuedTemporalResetDiagnostics(reason));
            return;
        }

        _presenter.RequestTemporalHistoryReset(reason);
        _pendingTemporalResetReason = MacMetalTemporalResetReason.None;
        PublishDiagnostics();
    }

    public void SetUpscaleOutputResolution(MacUpscaleOutputResolution outputResolution)
    {
        _upscaleOutputResolution = outputResolution;
        if (_presenter == null)
            return;

        _presenter.SetUpscaleOutputResolution(outputResolution);
        if (_lastLayeredFrame != null)
            TryPresentFrame(_lastLayeredFrame);
        else if (_lastFrame is { Length: > 0 })
            TryPresentFrame(_lastFrame);

        PublishDiagnostics();
    }

    public void SetDisplaySize(double width, double height)
    {
        _targetDisplayWidth = width;
        _targetDisplayHeight = height;
        if (width > 0 && height > 0)
        {
            Width = width;
            Height = height;
        }

        UpdatePresenterDisplaySize();
        LogGeometry("set-display-size");
        PublishDiagnostics();
    }

    public void SetCornerRadius(double radius)
    {
        _cornerRadius = Math.Max(0d, radius);
        _presenter?.SetCornerRadius(_cornerRadius);
        LogGeometry("set-corner-radius");
    }

    public void Dispose()
    {
        ReleasePresenter();
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _presenter = s_presenterFactory(parent.Handle, _upscaleMode);
        _presenter.SetUpscaleOutputResolution(_upscaleOutputResolution);
        _presenter.SetCornerRadius(_cornerRadius);
        if (_pendingTemporalResetReason == MacMetalTemporalResetReason.None && _upscaleMode == MacUpscaleMode.Temporal)
            _pendingTemporalResetReason = MacMetalTemporalResetReason.PresenterRecreated;
        ApplyPendingTemporalHistoryReset();
        UpdatePresenterDisplaySize();
        LogGeometry("create-native-control");
        PublishDiagnostics();
        if (_lastLayeredFrame != null)
            _presenter.PresentFrame(_lastLayeredFrame);
        else if (_lastFrame is { Length: > 0 })
            _presenter.PresentFrame(_lastFrame);
        PublishDiagnostics();

        return new PlatformHandle(_presenter.ViewHandle, "NSView");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty)
        {
            UpdatePresenterDisplaySize();
            LogGeometry("bounds-changed");
            PublishDiagnostics();
        }
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        ReleasePresenter();
        base.DestroyNativeControlCore(control);
    }

    private void ReleasePresenter()
    {
        if (_presenter != null &&
            _pendingTemporalResetReason == MacMetalTemporalResetReason.None &&
            _upscaleMode == MacUpscaleMode.Temporal)
        {
            _pendingTemporalResetReason = MacMetalTemporalResetReason.PresenterRecreated;
        }

        _presenter?.Dispose();
        _presenter = null;
        _lastDiagnostics = null;
    }

    private void PublishDiagnostics()
    {
        if (_presenter == null)
            return;

        MacMetalPresenterDiagnostics diagnostics = _presenter.Diagnostics;
        if (_lastDiagnostics.HasValue && _lastDiagnostics.Value == diagnostics)
            return;

        _lastDiagnostics = diagnostics;
        DiagnosticsChanged?.Invoke(diagnostics);
        PublishTemporalResetDiagnostics(BuildTemporalResetDiagnostics(diagnostics));
    }

    private void UpdatePresenterDisplaySize()
    {
        if (_presenter == null)
            return;

        var width = _targetDisplayWidth > 0 ? _targetDisplayWidth : Bounds.Width;
        var height = _targetDisplayHeight > 0 ? _targetDisplayHeight : Bounds.Height;
        width = Math.Max(1d, width);
        height = Math.Max(1d, height);
        _presenter.SetDisplaySize(width, height);
    }

    private void LogGeometry(string reason)
    {
        var geometry =
            $"reason={reason} | target={_targetDisplayWidth:0.##}x{_targetDisplayHeight:0.##} | bounds={Bounds.Width:0.##}x{Bounds.Height:0.##} | size={Width:0.##}x{Height:0.##} | corner={_cornerRadius:0.##} | visible={IsVisible}";
        if (string.Equals(_lastGeometryLog, geometry, StringComparison.Ordinal))
            return;

        _lastGeometryLog = geometry;
        StartupDiagnostics.Write("mac-metal-host", geometry);
        GeometryDiagnostics.Write("mac-metal-host", geometry);
    }

    private void ApplyPendingTemporalHistoryReset()
    {
        if (_presenter == null || _pendingTemporalResetReason == MacMetalTemporalResetReason.None)
            return;

        var reason = _pendingTemporalResetReason;
        _pendingTemporalResetReason = MacMetalTemporalResetReason.None;
        _presenter.RequestTemporalHistoryReset(reason);
        PublishDiagnostics();
    }

    private void PublishTemporalResetDiagnostics(string diagnostics)
    {
        if (string.Equals(_temporalResetDiagnostics, diagnostics, StringComparison.Ordinal))
            return;

        _temporalResetDiagnostics = diagnostics;
        StartupDiagnostics.Write("mac-metal-host", diagnostics);
        GeometryDiagnostics.Write("mac-metal-host", diagnostics);
        TemporalResetDiagnosticsChanged?.Invoke(diagnostics);
    }

    private static string BuildQueuedTemporalResetDiagnostics(MacMetalTemporalResetReason reason)
        => $"Temporal 重置: 已排队(等待 presenter) | reason={GetTemporalResetReasonLabel(reason)}";

    private static string BuildTemporalResetDiagnostics(MacMetalPresenterDiagnostics diagnostics)
    {
        if (!diagnostics.TemporalResetPending &&
            !diagnostics.TemporalResetApplied &&
            diagnostics.TemporalResetCount == 0 &&
            diagnostics.TemporalResetReason == MacMetalTemporalResetReason.None)
        {
            return "Temporal 重置: 未请求";
        }

        string state = diagnostics.TemporalResetPending
            ? "待应用"
            : diagnostics.TemporalResetApplied
                ? "已应用"
                : "未知";

        return $"Temporal 重置: {state} | reason={GetTemporalResetReasonLabel(diagnostics.TemporalResetReason)} | count={diagnostics.TemporalResetCount}";
    }

    private static string GetTemporalResetReasonLabel(MacMetalTemporalResetReason reason) => reason switch
    {
        MacMetalTemporalResetReason.PresenterRecreated => "窗口重开 / presenter 重建",
        MacMetalTemporalResetReason.RomLoaded => "ROM 载入",
        MacMetalTemporalResetReason.SaveStateLoaded => "快速读档",
        MacMetalTemporalResetReason.UpscaleModeChanged => "超分模式切换",
        MacMetalTemporalResetReason.RuntimeFallback => "运行时回退",
        MacMetalTemporalResetReason.TimelineJump => "时间线跳转 / 回溯",
        _ => "无",
    };

    private bool TryPresentFrame(ReadOnlySpan<uint> frameBuffer)
    {
        if (_presenter == null)
            return false;

        if (_presenter.PresentFrame(frameBuffer))
            return true;

        if (_upscaleMode != MacUpscaleMode.None)
        {
            _presenter.SetUpscaleMode(MacUpscaleMode.None);
            if (_presenter.PresentFrame(frameBuffer))
                return true;
        }

        PresentationFailed?.Invoke("macOS Metal presenter 失败，已回退到软件显示路径。");
        return false;
    }

    private bool TryPresentFrame(LayeredFrameData frameData)
    {
        if (_presenter == null)
            return false;

        if (_presenter.PresentFrame(frameData))
            return true;

        if (_upscaleMode != MacUpscaleMode.None)
        {
            _presenter.SetUpscaleMode(MacUpscaleMode.None);
            if (_presenter.PresentFrame(frameData))
                return true;
        }

        PresentationFailed?.Invoke("macOS Metal presenter 失败，已回退到软件显示路径。");
        return false;
    }
}

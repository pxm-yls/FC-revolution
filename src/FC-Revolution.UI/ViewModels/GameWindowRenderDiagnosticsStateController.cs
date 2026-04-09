using FCRevolution.Rendering.Metal;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowRenderDiagnosticsUpdate(
    string ViewportRendererLabel,
    string ViewportRenderDiagnostics,
    MacMetalTemporalResetReason TemporalResetReason,
    int TemporalResetVersion);

internal sealed class GameWindowRenderDiagnosticsStateController
{
    private string _softwareRendererReason = "软件显示路径";
    private string _temporalHistoryResetStatus = "Temporal 重置: 未请求";
    private MacMetalTemporalResetReason _temporalHistoryResetReason = MacMetalTemporalResetReason.None;
    private int _temporalHistoryResetVersion;
    private MacMetalPresenterDiagnostics? _lastMetalPresenterDiagnostics;

    public MacMetalTemporalResetReason TemporalResetReason => _temporalHistoryResetReason;

    public int TemporalResetVersion => _temporalHistoryResetVersion;

    public GameWindowRenderDiagnosticsUpdate UpdateMetalPresenterDiagnostics(
        MacMetalPresenterDiagnostics diagnostics,
        MacUpscaleOutputResolution configuredUpscaleOutputResolution)
    {
        _lastMetalPresenterDiagnostics = diagnostics;
        var viewState = GameWindowViewportDiagnosticsController.BuildMetalPresenterViewState(
            diagnostics,
            configuredUpscaleOutputResolution,
            _temporalHistoryResetStatus);
        return new GameWindowRenderDiagnosticsUpdate(
            viewState.ViewportRendererLabel,
            viewState.ViewportRenderDiagnostics,
            _temporalHistoryResetReason,
            _temporalHistoryResetVersion);
    }

    public GameWindowRenderDiagnosticsUpdate UpdateSoftwareRendererStatus(
        MacUpscaleMode configuredUpscaleMode,
        MacUpscaleOutputResolution configuredUpscaleOutputResolution,
        int screenWidth,
        int screenHeight,
        string reason)
    {
        _lastMetalPresenterDiagnostics = null;
        _softwareRendererReason = reason;
        return BuildCurrentState(
            configuredUpscaleMode,
            configuredUpscaleOutputResolution,
            screenWidth,
            screenHeight);
    }

    public GameWindowRenderDiagnosticsUpdate UpdateTemporalHistoryResetStatus(
        string status,
        MacUpscaleMode configuredUpscaleMode,
        MacUpscaleOutputResolution configuredUpscaleOutputResolution,
        int screenWidth,
        int screenHeight)
    {
        if (!string.IsNullOrWhiteSpace(status))
            _temporalHistoryResetStatus = status.Trim();

        return BuildCurrentState(
            configuredUpscaleMode,
            configuredUpscaleOutputResolution,
            screenWidth,
            screenHeight);
    }

    public GameWindowRenderDiagnosticsUpdate RequestTemporalReset(
        MacMetalTemporalResetReason reason,
        MacUpscaleMode configuredUpscaleMode,
        MacUpscaleOutputResolution configuredUpscaleOutputResolution,
        int screenWidth,
        int screenHeight)
    {
        if (reason != MacMetalTemporalResetReason.None)
        {
            _temporalHistoryResetReason = reason;
            _temporalHistoryResetVersion++;
            var label = GameWindowViewportDiagnosticsController.GetTemporalResetReasonLabel(reason);
            _temporalHistoryResetStatus = $"Temporal 重置: 已请求 | reason={label}";
        }

        return BuildCurrentState(
            configuredUpscaleMode,
            configuredUpscaleOutputResolution,
            screenWidth,
            screenHeight);
    }

    private GameWindowRenderDiagnosticsUpdate BuildCurrentState(
        MacUpscaleMode configuredUpscaleMode,
        MacUpscaleOutputResolution configuredUpscaleOutputResolution,
        int screenWidth,
        int screenHeight)
    {
        var viewState = _lastMetalPresenterDiagnostics.HasValue
            ? GameWindowViewportDiagnosticsController.BuildMetalPresenterViewState(
                _lastMetalPresenterDiagnostics.Value,
                configuredUpscaleOutputResolution,
                _temporalHistoryResetStatus)
            : GameWindowViewportDiagnosticsController.BuildSoftwareRendererViewState(
                configuredUpscaleMode,
                configuredUpscaleOutputResolution,
                screenWidth,
                screenHeight,
                _softwareRendererReason,
                _temporalHistoryResetStatus);

        return new GameWindowRenderDiagnosticsUpdate(
            viewState.ViewportRendererLabel,
            viewState.ViewportRenderDiagnostics,
            _temporalHistoryResetReason,
            _temporalHistoryResetVersion);
    }
}

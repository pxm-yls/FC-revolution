using FCRevolution.Rendering.Metal;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowViewportDiagnosticsViewState(
    string ViewportRendererLabel,
    string ViewportRenderDiagnostics);

internal static class GameWindowViewportDiagnosticsController
{
    public static GameWindowViewportDiagnosticsViewState BuildMetalPresenterViewState(
        MacMetalPresenterDiagnostics diagnostics,
        MacUpscaleOutputResolution configuredUpscaleOutputResolution,
        string? temporalResetStatus = null)
    {
        var effectiveLabel = GetUpscaleModeLabel(diagnostics.EffectiveUpscaleMode);
        var requestedLabel = GetUpscaleModeLabel(diagnostics.RequestedUpscaleMode);
        var outputResolutionLabel = diagnostics.RequestedUpscaleMode == MacUpscaleMode.None
            ? null
            : GetUpscaleOutputResolutionLabel(configuredUpscaleOutputResolution);
        var rendererLabel = diagnostics.RequestedUpscaleMode == diagnostics.EffectiveUpscaleMode
            ? outputResolutionLabel == null
                ? $"Metal / {effectiveLabel}"
                : $"Metal / {effectiveLabel} / {outputResolutionLabel}"
            : outputResolutionLabel == null
                ? $"Metal / {effectiveLabel} | 目标 {requestedLabel}"
                : $"Metal / {effectiveLabel} / {outputResolutionLabel} | 目标 {requestedLabel}";

        return new GameWindowViewportDiagnosticsViewState(
            rendererLabel,
            BuildViewportRenderDiagnostics(diagnostics, temporalResetStatus));
    }

    public static GameWindowViewportDiagnosticsViewState BuildSoftwareRendererViewState(
        MacUpscaleMode configuredUpscaleMode,
        MacUpscaleOutputResolution configuredUpscaleOutputResolution,
        int screenWidth,
        int screenHeight,
        string reason,
        string? temporalResetStatus = null)
    {
        var outputResolutionLabel = configuredUpscaleMode == MacUpscaleMode.None
            ? null
            : GetUpscaleOutputResolutionLabel(configuredUpscaleOutputResolution);
        var rendererLabel = outputResolutionLabel == null
            ? $"软件 / {GetUpscaleModeLabel(configuredUpscaleMode)}"
            : $"软件 / {GetUpscaleModeLabel(configuredUpscaleMode)} / {outputResolutionLabel}";
        var diagnostics = $"内部 {screenWidth}x{screenHeight} -> 渲染 {screenWidth}x{screenHeight} -> 显示 {screenWidth}x{screenHeight} | {reason}";
        if (!string.IsNullOrWhiteSpace(temporalResetStatus))
            diagnostics = $"{diagnostics} | {temporalResetStatus}";
        return new GameWindowViewportDiagnosticsViewState(rendererLabel, diagnostics);
    }

    public static string BuildViewportRenderDiagnostics(MacMetalPresenterDiagnostics diagnostics, string? temporalResetStatus = null)
    {
        var summary =
            $"内部 {diagnostics.InternalWidth}x{diagnostics.InternalHeight} -> 渲染 {diagnostics.OutputWidth}x{diagnostics.OutputHeight} -> 显示 {diagnostics.TargetWidthPoints:0}x{diagnostics.TargetHeightPoints:0} @{diagnostics.DisplayScale:0.##}x";

        var detailed = diagnostics.FallbackReason == MacMetalFallbackReason.None
            ? summary
            : $"{summary} | 回退原因: {GetFallbackReasonLabel(diagnostics.FallbackReason)}";

        if (string.IsNullOrWhiteSpace(temporalResetStatus))
            return detailed;

        return $"{detailed} | {temporalResetStatus}";
    }

    public static string GetUpscaleModeLabel(MacUpscaleMode mode) => mode switch
    {
        MacUpscaleMode.Spatial => "Spatial",
        MacUpscaleMode.Temporal => "Temporal",
        _ => "无超分",
    };

    public static string GetUpscaleOutputResolutionLabel(MacUpscaleOutputResolution outputResolution) => outputResolution switch
    {
        MacUpscaleOutputResolution.Qhd1440 => "1440p",
        MacUpscaleOutputResolution.Uhd2160 => "4K",
        _ => "1080p",
    };

    public static string GetFallbackReasonLabel(MacMetalFallbackReason reason) => reason switch
    {
        MacMetalFallbackReason.UnsupportedPlatform => "平台或 macOS 版本不满足",
        MacMetalFallbackReason.UnsupportedDevice => "当前 GPU 不支持所选超分模式",
        MacMetalFallbackReason.OutputSmallerThanInput => "输出分辨率低于内部渲染分辨率",
        MacMetalFallbackReason.ScalerCreationFailed => "超分 scaler 初始化失败",
        MacMetalFallbackReason.RuntimeCommandFailure => "超分命令缓冲运行失败，已自动回退",
        MacMetalFallbackReason.RequestedPathUnavailable => "所请求的超分路径当前不可用，已自动回退",
        _ => "无",
    };

    public static string GetTemporalResetReasonLabel(MacMetalTemporalResetReason reason) => reason switch
    {
        MacMetalTemporalResetReason.PresenterRecreated => "窗口重开 / presenter 重建",
        MacMetalTemporalResetReason.RomLoaded => "ROM 载入",
        MacMetalTemporalResetReason.SaveStateLoaded => "快速读档",
        MacMetalTemporalResetReason.UpscaleModeChanged => "超分模式切换",
        MacMetalTemporalResetReason.RuntimeFallback => "运行时回退",
        MacMetalTemporalResetReason.TimelineJump => "时间线跳转 / 回溯",
        _ => "无",
    };
}

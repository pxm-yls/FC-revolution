using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowRenderDiagnosticsStateControllerTests
{
    [Fact]
    public void RequestTemporalReset_RebuildsMetalDiagnostics_AndIncrementsVersion()
    {
        var controller = new GameWindowRenderDiagnosticsStateController();
        var diagnostics = CreateDiagnostics(
            requestedMode: MacUpscaleMode.Temporal,
            effectiveMode: MacUpscaleMode.Temporal,
            fallbackReason: MacMetalFallbackReason.None);

        _ = controller.UpdateMetalPresenterDiagnostics(diagnostics, MacUpscaleOutputResolution.Uhd2160);
        var update = controller.RequestTemporalReset(
            MacMetalTemporalResetReason.RomLoaded,
            configuredUpscaleMode: MacUpscaleMode.None,
            configuredUpscaleOutputResolution: MacUpscaleOutputResolution.Uhd2160,
            screenWidth: 256,
            screenHeight: 240);

        Assert.Equal(MacMetalTemporalResetReason.RomLoaded, update.TemporalResetReason);
        Assert.Equal(1, update.TemporalResetVersion);
        Assert.Equal("Metal / Temporal / 4K", update.ViewportRendererLabel);
        Assert.Contains("Temporal 重置: 已请求 | reason=ROM 载入", update.ViewportRenderDiagnostics);
    }

    [Fact]
    public void UpdateSoftwareRendererStatus_ClearsMetalContext_ForLaterTemporalReset()
    {
        var controller = new GameWindowRenderDiagnosticsStateController();
        var diagnostics = CreateDiagnostics(
            requestedMode: MacUpscaleMode.Temporal,
            effectiveMode: MacUpscaleMode.Temporal,
            fallbackReason: MacMetalFallbackReason.None);

        _ = controller.UpdateMetalPresenterDiagnostics(diagnostics, MacUpscaleOutputResolution.Uhd2160);
        _ = controller.UpdateSoftwareRendererStatus(
            configuredUpscaleMode: MacUpscaleMode.Spatial,
            configuredUpscaleOutputResolution: MacUpscaleOutputResolution.Qhd1440,
            screenWidth: 256,
            screenHeight: 240,
            reason: "等待 presenter 应用 Spatial");
        var update = controller.RequestTemporalReset(
            MacMetalTemporalResetReason.UpscaleModeChanged,
            configuredUpscaleMode: MacUpscaleMode.Spatial,
            configuredUpscaleOutputResolution: MacUpscaleOutputResolution.Qhd1440,
            screenWidth: 256,
            screenHeight: 240);

        Assert.Equal("软件 / Spatial / 1440p", update.ViewportRendererLabel);
        Assert.Contains("等待 presenter 应用 Spatial", update.ViewportRenderDiagnostics);
        Assert.Contains("Temporal 重置: 已请求 | reason=超分模式切换", update.ViewportRenderDiagnostics);
        Assert.Equal(MacMetalTemporalResetReason.UpscaleModeChanged, controller.TemporalResetReason);
        Assert.Equal(1, controller.TemporalResetVersion);
    }

    [Fact]
    public void UpdateTemporalHistoryResetStatus_RebuildsCurrentMetalDiagnostics()
    {
        var controller = new GameWindowRenderDiagnosticsStateController();
        var diagnostics = CreateDiagnostics(
            requestedMode: MacUpscaleMode.Temporal,
            effectiveMode: MacUpscaleMode.Temporal,
            fallbackReason: MacMetalFallbackReason.None);

        _ = controller.UpdateMetalPresenterDiagnostics(diagnostics, MacUpscaleOutputResolution.Uhd2160);
        var update = controller.UpdateTemporalHistoryResetStatus(
            "Temporal 重置: 已记录，等待 presenter | reason=ROM 载入",
            configuredUpscaleMode: MacUpscaleMode.None,
            configuredUpscaleOutputResolution: MacUpscaleOutputResolution.Uhd2160,
            screenWidth: 256,
            screenHeight: 240);

        Assert.Equal("Metal / Temporal / 4K", update.ViewportRendererLabel);
        Assert.Contains("Temporal 重置: 已记录，等待 presenter | reason=ROM 载入", update.ViewportRenderDiagnostics);
        Assert.Equal(MacMetalTemporalResetReason.None, update.TemporalResetReason);
        Assert.Equal(0, update.TemporalResetVersion);
    }

    private static MacMetalPresenterDiagnostics CreateDiagnostics(
        MacUpscaleMode requestedMode,
        MacUpscaleMode effectiveMode,
        MacMetalFallbackReason fallbackReason)
    {
        return new MacMetalPresenterDiagnostics(
            RequestedUpscaleMode: requestedMode,
            EffectiveUpscaleMode: effectiveMode,
            FallbackReason: fallbackReason,
            InternalWidth: 256,
            InternalHeight: 240,
            OutputWidth: 512,
            OutputHeight: 480,
            DrawableWidth: 512,
            DrawableHeight: 480,
            TargetWidthPoints: 640,
            TargetHeightPoints: 360,
            DisplayScale: 2,
            HostWidthPoints: 640,
            HostHeightPoints: 360,
            LayerWidthPoints: 640,
            LayerHeightPoints: 360,
            TemporalResetPending: false,
            TemporalResetApplied: false,
            TemporalResetCount: 0,
            TemporalResetReason: MacMetalTemporalResetReason.None);
    }
}

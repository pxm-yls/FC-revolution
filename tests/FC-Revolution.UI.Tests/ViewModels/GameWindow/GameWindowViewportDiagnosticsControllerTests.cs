using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowViewportDiagnosticsControllerTests
{
    [Fact]
    public void BuildMetalPresenterViewState_FormatsRendererLabel_WhenRequestedMatchesEffective()
    {
        var diagnostics = CreateDiagnostics(
            requestedMode: MacUpscaleMode.Temporal,
            effectiveMode: MacUpscaleMode.Temporal,
            fallbackReason: MacMetalFallbackReason.None);

        var state = GameWindowViewportDiagnosticsController.BuildMetalPresenterViewState(
            diagnostics,
            MacUpscaleOutputResolution.Uhd2160);

        Assert.Equal("Metal / Temporal / 4K", state.ViewportRendererLabel);
        Assert.Equal(
            "内部 256x240 -> 渲染 512x480 -> 显示 640x360 @2x",
            state.ViewportRenderDiagnostics);
    }

    [Fact]
    public void BuildMetalPresenterViewState_FormatsTargetAndFallbackReason_WhenModesDiffer()
    {
        var diagnostics = CreateDiagnostics(
            requestedMode: MacUpscaleMode.Spatial,
            effectiveMode: MacUpscaleMode.Temporal,
            fallbackReason: MacMetalFallbackReason.UnsupportedDevice);

        var state = GameWindowViewportDiagnosticsController.BuildMetalPresenterViewState(
            diagnostics,
            MacUpscaleOutputResolution.Qhd1440);

        Assert.Equal("Metal / Temporal / 1440p | 目标 Spatial", state.ViewportRendererLabel);
        Assert.Equal(
            "内部 256x240 -> 渲染 512x480 -> 显示 640x360 @2x | 回退原因: 当前 GPU 不支持所选超分模式",
            state.ViewportRenderDiagnostics);
    }

    [Fact]
    public void BuildMetalPresenterViewState_FormatsRequestedTemporal_WhenEffectiveFallsBackToNone()
    {
        var diagnostics = CreateDiagnostics(
            requestedMode: MacUpscaleMode.Temporal,
            effectiveMode: MacUpscaleMode.None,
            fallbackReason: MacMetalFallbackReason.RuntimeCommandFailure);

        var state = GameWindowViewportDiagnosticsController.BuildMetalPresenterViewState(
            diagnostics,
            MacUpscaleOutputResolution.Uhd2160);

        Assert.Equal("Metal / 无超分 / 4K | 目标 Temporal", state.ViewportRendererLabel);
        Assert.Equal(
            "内部 256x240 -> 渲染 512x480 -> 显示 640x360 @2x | 回退原因: 超分命令缓冲运行失败，已自动回退",
            state.ViewportRenderDiagnostics);
    }

    [Fact]
    public void BuildMetalPresenterViewState_FormatsRequestedTemporal_WhenEffectiveFallsBackToSpatial()
    {
        var diagnostics = CreateDiagnostics(
            requestedMode: MacUpscaleMode.Temporal,
            effectiveMode: MacUpscaleMode.Spatial,
            fallbackReason: MacMetalFallbackReason.RequestedPathUnavailable);

        var state = GameWindowViewportDiagnosticsController.BuildMetalPresenterViewState(
            diagnostics,
            MacUpscaleOutputResolution.Qhd1440);

        Assert.Equal("Metal / Spatial / 1440p | 目标 Temporal", state.ViewportRendererLabel);
        Assert.Equal(
            "内部 256x240 -> 渲染 512x480 -> 显示 640x360 @2x | 回退原因: 所请求的超分路径当前不可用，已自动回退",
            state.ViewportRenderDiagnostics);
    }

    [Fact]
    public void BuildMetalPresenterViewState_AppendsTemporalResetStatus_WhenProvided()
    {
        var diagnostics = CreateDiagnostics(
            requestedMode: MacUpscaleMode.Temporal,
            effectiveMode: MacUpscaleMode.Temporal,
            fallbackReason: MacMetalFallbackReason.None);

        var state = GameWindowViewportDiagnosticsController.BuildMetalPresenterViewState(
            diagnostics,
            MacUpscaleOutputResolution.Uhd2160,
            temporalResetStatus: "Temporal 重置: 已请求 | reason=ROM 载入");

        Assert.Equal("Metal / Temporal / 4K", state.ViewportRendererLabel);
        Assert.Equal(
            "内部 256x240 -> 渲染 512x480 -> 显示 640x360 @2x | Temporal 重置: 已请求 | reason=ROM 载入",
            state.ViewportRenderDiagnostics);
    }

    [Fact]
    public void BuildSoftwareRendererViewState_FormatsLabelAndDiagnostics()
    {
        var none = GameWindowViewportDiagnosticsController.BuildSoftwareRendererViewState(
            configuredUpscaleMode: MacUpscaleMode.None,
            configuredUpscaleOutputResolution: MacUpscaleOutputResolution.Uhd2160,
            screenWidth: 256,
            screenHeight: 240,
            reason: "当前平台不使用 macOS Metal presenter");

        Assert.Equal("软件 / 无超分", none.ViewportRendererLabel);
        Assert.Equal(
            "内部 256x240 -> 渲染 256x240 -> 显示 256x240 | 当前平台不使用 macOS Metal presenter",
            none.ViewportRenderDiagnostics);

        var spatial = GameWindowViewportDiagnosticsController.BuildSoftwareRendererViewState(
            configuredUpscaleMode: MacUpscaleMode.Spatial,
            configuredUpscaleOutputResolution: MacUpscaleOutputResolution.Qhd1440,
            screenWidth: 256,
            screenHeight: 240,
            reason: "等待 presenter 应用 Spatial");

        Assert.Equal("软件 / Spatial / 1440p", spatial.ViewportRendererLabel);
    }

    [Fact]
    public void GetFallbackReasonLabel_ReturnsDefaultText_ForUnknownReason()
    {
        var label = GameWindowViewportDiagnosticsController.GetFallbackReasonLabel((MacMetalFallbackReason)999);
        Assert.Equal("无", label);
    }

    [Theory]
    [InlineData(MacMetalTemporalResetReason.PresenterRecreated, "窗口重开 / presenter 重建")]
    [InlineData(MacMetalTemporalResetReason.RomLoaded, "ROM 载入")]
    [InlineData(MacMetalTemporalResetReason.SaveStateLoaded, "快速读档")]
    [InlineData(MacMetalTemporalResetReason.UpscaleModeChanged, "超分模式切换")]
    [InlineData(MacMetalTemporalResetReason.TimelineJump, "时间线跳转 / 回溯")]
    public void GetTemporalResetReasonLabel_ReturnsExpectedText_ForKnownReasons(
        MacMetalTemporalResetReason reason,
        string expected)
    {
        var label = GameWindowViewportDiagnosticsController.GetTemporalResetReasonLabel(reason);
        Assert.Equal(expected, label);
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

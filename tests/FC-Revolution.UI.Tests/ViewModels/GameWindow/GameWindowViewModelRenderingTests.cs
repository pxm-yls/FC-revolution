using FCRevolution.Rendering.Metal;
using System.Diagnostics;
using System.Threading;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class GameWindowViewModelRenderingTests
{
    [Fact]
    public void UpdateDisplay_RepeatedFrames_StillRaiseScreenBitmapUpdated()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;
        var notifications = 0;
        var bitmaps = new List<object?>();

        vm.ScreenBitmapUpdated += bitmap =>
        {
            notifications++;
            bitmaps.Add(bitmap);
        };

        host.InvokeUpdateDisplay(GameWindowViewModelTestHost.CreateSolidFrame(0xFF0000FFu));
        host.InvokeUpdateDisplay(GameWindowViewModelTestHost.CreateSolidFrame(0xFF00FF00u));

        Assert.True(notifications >= 2);
        Assert.NotNull(vm.ScreenBitmap);
        Assert.True(bitmaps.Count >= 2);
        Assert.Contains(bitmaps, bitmap => bitmap != null);
    }

    [Fact]
    public void RunningGame_RaisesScreenBitmapUpdated_FromFrameReady()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;
        var notifications = 0;

        vm.ScreenBitmapUpdated += _ => notifications++;

        var timeout = Stopwatch.StartNew();
        while (notifications == 0 && timeout.Elapsed < TimeSpan.FromSeconds(1))
        {
            AvaloniaThreadingTestHelper.DrainJobs();
            Thread.Sleep(10);
        }

        Assert.True(notifications > 0);
        Assert.NotNull(vm.ScreenBitmap);
    }

    [Fact]
    public void RunningGame_RaisesRawOrLayeredFramePresentationEvent()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;
        var rawPresented = 0;
        var layeredPresented = 0;

        vm.RawFramePresented += _ => rawPresented++;
        vm.LayeredFramePresented += _ => layeredPresented++;

        var timeout = Stopwatch.StartNew();
        while (rawPresented + layeredPresented == 0 && timeout.Elapsed < TimeSpan.FromSeconds(1))
        {
            AvaloniaThreadingTestHelper.DrainJobs();
            Thread.Sleep(10);
        }

        Assert.True(rawPresented + layeredPresented > 0);
    }

    [Fact]
    public void UpdateTemporalHistoryResetStatus_RebuildsViewportDiagnostics_FromMetalState()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;

        vm.UpdateMetalPresenterDiagnostics(CreateDiagnostics(
            requestedMode: MacUpscaleMode.Temporal,
            effectiveMode: MacUpscaleMode.Temporal,
            fallbackReason: MacMetalFallbackReason.None));
        vm.UpdateTemporalHistoryResetStatus("Temporal 重置: 已记录，等待 presenter | reason=ROM 载入");

        Assert.Equal("Metal / Temporal / 1080p", vm.ViewportRendererLabel);
        Assert.Contains("Temporal 重置: 已记录，等待 presenter | reason=ROM 载入", vm.ViewportRenderDiagnostics);
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

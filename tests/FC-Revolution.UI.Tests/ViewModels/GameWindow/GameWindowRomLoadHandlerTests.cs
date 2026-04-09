using System;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowRomLoadHandlerTests
{
    [Fact]
    public void Load_WhenSuccessful_AppliesStatusDiagnosticsAndTemporalReset()
    {
        var resetTemporalHistoryCalls = 0;
        var loadedPath = string.Empty;
        var statusText = string.Empty;
        var diagnostic = string.Empty;
        var resetReason = MacMetalTemporalResetReason.None;
        var handler = new GameWindowRomLoadHandler(
            () => resetTemporalHistoryCalls++,
            romPath =>
            {
                loadedPath = romPath;
                return CoreLoadResult.Ok();
            },
            () => "mapper 000",
            status => statusText = status,
            message => diagnostic = message,
            reason => resetReason = reason);

        handler.Load("/roms/contra.nes");

        Assert.Equal(1, resetTemporalHistoryCalls);
        Assert.Equal("/roms/contra.nes", loadedPath);
        Assert.Equal("运行中: contra.nes | 核心为 mapper 000", statusText);
        Assert.Equal("游戏窗口运行中 contra.nes，核心为 mapper 000", diagnostic);
        Assert.Equal(MacMetalTemporalResetReason.RomLoaded, resetReason);
    }

    [Fact]
    public void Load_WhenCoreLoadFails_ThrowsAndSkipsSuccessProjection()
    {
        var statusApplied = false;
        var diagnosticApplied = false;
        var resetApplied = false;
        var handler = new GameWindowRomLoadHandler(
            () => { },
            _ => CoreLoadResult.Fail("bad rom"),
            () => "mapper 000",
            _ => statusApplied = true,
            _ => diagnosticApplied = true,
            _ => resetApplied = true);

        var ex = Assert.Throws<InvalidOperationException>(() => handler.Load("/roms/bad.nes"));

        Assert.Equal("bad rom", ex.Message);
        Assert.False(statusApplied);
        Assert.False(diagnosticApplied);
        Assert.False(resetApplied);
    }
}

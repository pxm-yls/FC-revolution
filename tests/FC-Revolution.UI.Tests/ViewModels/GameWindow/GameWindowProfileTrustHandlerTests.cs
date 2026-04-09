using Avalonia.Controls;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class GameWindowProfileTrustHandlerTests
{
    [Fact]
    public async Task EnsureAsync_WhenProfileKindMismatch_UpdatesStatusAndSkipsApply()
    {
        var initialized = false;
        var applyCalls = 0;
        var statusText = string.Empty;
        var handler = new GameWindowProfileTrustHandler(
            () => initialized,
            () => initialized = true,
            "/roms/contra.nes",
            _ => new RomConfigLoadResult(new RomConfigProfile(), HasProfileKindMismatch: true, IsForeignMachineProfile: false, IsFutureVersionProfile: false),
            (_, _, _) => Task.FromResult(true),
            _ => { },
            _ => applyCalls++,
            status => statusText = status);

        await handler.EnsureAsync(new Window());

        Assert.True(initialized);
        Assert.Equal(0, applyCalls);
        Assert.Equal("运行中: contra.nes | 警告：.fcr 类型不匹配", statusText);
    }

    [Fact]
    public async Task EnsureAsync_WhenForeignProfileCancelled_UpdatesStatusAndSkipsApply()
    {
        var applyCalls = 0;
        var trustCalls = 0;
        var statusText = string.Empty;
        var handler = new GameWindowProfileTrustHandler(
            () => false,
            () => { },
            "/roms/contra.nes",
            _ => new RomConfigLoadResult(new RomConfigProfile(), HasProfileKindMismatch: false, IsForeignMachineProfile: true, IsFutureVersionProfile: false),
            (_, _, _) => Task.FromResult(false),
            _ => trustCalls++,
            _ => applyCalls++,
            status => statusText = status);

        await handler.EnsureAsync(new Window());

        Assert.Equal(0, trustCalls);
        Assert.Equal(0, applyCalls);
        Assert.Equal("运行中: contra.nes | 已取消使用外部 .fcr", statusText);
    }

    [Fact]
    public async Task EnsureAsync_WhenForeignProfileConfirmed_RetrustsReloadsAndApplies()
    {
        var loadCalls = 0;
        var trustCalls = 0;
        RomConfigLoadResult? appliedResult = null;
        var reloadedResult = new RomConfigLoadResult(new RomConfigProfile(), HasProfileKindMismatch: false, IsForeignMachineProfile: false, IsFutureVersionProfile: true);
        var handler = new GameWindowProfileTrustHandler(
            () => false,
            () => { },
            "/roms/contra.nes",
            _ =>
            {
                loadCalls++;
                return loadCalls == 1
                    ? new RomConfigLoadResult(new RomConfigProfile(), HasProfileKindMismatch: false, IsForeignMachineProfile: true, IsFutureVersionProfile: false)
                    : reloadedResult;
            },
            (_, _, _) => Task.FromResult(true),
            _ => trustCalls++,
            result => appliedResult = result,
            _ => { });

        await handler.EnsureAsync(new Window());

        Assert.Equal(2, loadCalls);
        Assert.Equal(1, trustCalls);
        Assert.Same(reloadedResult, appliedResult);
    }

    [Fact]
    public async Task EnsureAsync_WhenAlreadyInitialized_IsNoOp()
    {
        var markCalls = 0;
        var loadCalls = 0;
        var handler = new GameWindowProfileTrustHandler(
            () => true,
            () => markCalls++,
            "/roms/contra.nes",
            _ =>
            {
                loadCalls++;
                return new RomConfigLoadResult(new RomConfigProfile(), false, false, false);
            },
            (_, _, _) => Task.FromResult(true),
            _ => { },
            _ => { },
            _ => { });

        await handler.EnsureAsync(new Window());

        Assert.Equal(0, markCalls);
        Assert.Equal(0, loadCalls);
    }
}

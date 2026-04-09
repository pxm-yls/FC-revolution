using Avalonia.Input;
using FCRevolution.Rendering.Metal;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class GameWindowViewModelSessionTests
{
    [Fact]
    public void QuickSave_CreatesLocalSaveFile()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;
        var quickSavePath = Path.ChangeExtension(host.RomPath, ".fcs");

        vm.OnKeyDown(Key.F2);

        Assert.True(File.Exists(quickSavePath));
        Assert.Contains("快速存档成功", vm.StatusText);
    }

    [Fact]
    public void QuickLoad_WithoutSave_UpdatesStatus()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;

        vm.OnKeyDown(Key.F3);

        Assert.Equal("当前游戏还没有快速存档", vm.StatusText);
    }

    [Fact]
    public void QuickLoad_WithSave_UpdatesTemporalResetReason()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;

        vm.OnKeyDown(Key.F2);
        vm.OnKeyDown(Key.F3);

        Assert.Contains("快速读档成功", vm.StatusText);
        Assert.Equal(MacMetalTemporalResetReason.SaveStateLoaded, vm.TemporalHistoryResetReason);
    }

    [Fact]
    public void DebugHotkey_DoesNotThrowOrCrash()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;

        var ex = Record.Exception(() => vm.OnKeyDown(Key.F12));

        Assert.Null(ex);
        Assert.DoesNotContain("打开调试窗口失败", vm.StatusText);
    }

    [Fact]
    public void HandleSessionFailure_StopsCurrentSessionWithoutCrashingHost()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;

        var ex = Record.Exception(() => host.InvokeHandleSessionFailure("你修改的内存值有问题，导致游戏崩溃，请重新尝试。"));

        Assert.Null(ex);
        Assert.Contains("当前游戏会话已停止", vm.StatusText);
        Assert.Contains("你修改的内存值有问题", vm.StatusText);
        Assert.True(vm.HasTransientMessage);
        Assert.Contains("你修改的内存值有问题", vm.TransientMessage);
    }

    [Fact]
    public void TogglePause_HotkeyTwice_TransitionsBetweenPausedAndResumed()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;

        vm.OnKeyDown(Key.F5);
        Assert.Contains("游戏已暂停", vm.StatusText);

        vm.OnKeyDown(Key.F5);
        Assert.Contains("游戏已继续", vm.StatusText);
    }
}

using Avalonia.Input;
using FCRevolution.Core.Input;
using FC_Revolution.UI.Models;
using System.IO;
using System.Linq;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowViewModelInputAndShortcutsTests
{
    [Fact]
    public void ShouldHandleKey_RecognizesUnifiedHotkeys()
    {
        using var host = new MainWindowViewModelTestHost();
        var vm = host.ViewModel;

        Assert.True(vm.ShouldHandleKey(Key.F2));
        Assert.True(vm.ShouldHandleKey(Key.F3));
        Assert.True(vm.ShouldHandleKey(Key.F4));
        Assert.True(vm.ShouldHandleKey(Key.F5));
        Assert.True(vm.ShouldHandleKey(Key.F7));
    }

    [Fact]
    public void ShouldHandleKey_RecognizesGlobalExtraBindingKeys()
    {
        using var host = new MainWindowViewModelTestHost();
        var vm = host.ViewModel;

        var previousCount = vm.GlobalExtraInputBindingsPlayer1.Count;
        vm.AddGlobalTurboBindingCommand.Execute("0");
        Assert.Equal(previousCount + 1, vm.GlobalExtraInputBindingsPlayer1.Count);

        var entry = vm.GlobalExtraInputBindingsPlayer1.Last();
        Assert.True(entry.TrySetSelectedKey(Key.Q));
        Assert.True(vm.ShouldHandleKey(Key.Q));
    }

    [Fact]
    public void OpenRomInputOverrideEditor_UsesQuickOverlayInsteadOfSettings()
    {
        using var host = new MainWindowViewModelTestHost();
        var vm = host.ViewModel;
        var rom = new RomLibraryItem("Contra.nes", "/tmp/contra.nes", "", false, 1024, DateTime.UtcNow);

        vm.OpenRomInputOverrideEditorCommand.Execute(rom);

        Assert.True(vm.IsQuickRomInputEditorOpen);
        Assert.False(vm.IsSettingsOpen);
        Assert.Equal(rom.Path, vm.CurrentRom?.Path);
    }

    [Fact]
    public void InputKeyDownUp_UpdatesPlayer1CombinedMask()
    {
        using var host = new MainWindowViewModelTestHost();
        var vm = host.ViewModel;
        host.CreateAndLoadTestRom();

        vm.OnKeyDown(Key.Z);
        var pressedMask = host.ReadByteField("_player1InputMask");
        Assert.NotEqual((byte)0, pressedMask & (byte)NesButton.A);

        vm.OnKeyUp(Key.Z);
        var releasedMask = host.ReadByteField("_player1InputMask");
        Assert.Equal((byte)0, releasedMask & (byte)NesButton.A);
    }

    [Fact]
    public void TogglePauseHotkeyTwice_UpdatesStatusText()
    {
        using var host = new MainWindowViewModelTestHost();
        var vm = host.ViewModel;
        host.CreateAndLoadTestRom();

        vm.OnKeyDown(Key.F5);
        Assert.Equal("已暂停", vm.StatusText);

        vm.OnKeyDown(Key.F5);
        Assert.Equal("运行中", vm.StatusText);
    }

    [Fact]
    public void QuickSaveThenQuickLoad_Hotkeys_DoNotThrowAndUpdateStatus()
    {
        using var host = new MainWindowViewModelTestHost();
        var vm = host.ViewModel;
        host.CreateAndLoadTestRom();
        var quickSavePath = host.InvokeGetQuickSavePath();

        try
        {
            var quickSaveException = Record.Exception(() => vm.OnKeyDown(Key.F2));
            Assert.Null(quickSaveException);
            Assert.Contains("存档已保存", vm.StatusText);
            Assert.True(File.Exists(quickSavePath));

            var quickLoadException = Record.Exception(() => vm.OnKeyDown(Key.F3));
            Assert.Null(quickLoadException);
            Assert.Equal("存档已读取", vm.StatusText);
        }
        finally
        {
            if (File.Exists(quickSavePath))
                File.Delete(quickSavePath);
        }
    }
}

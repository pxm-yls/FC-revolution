using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Input;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowInputKeyboardWorkflowControllerTests
{
    [Fact]
    public void HandleKeyDown_WhenInputBindingMatched_PressesKeyAndRequestsRefresh()
    {
        var runtime = new MainWindowActiveInputRuntimeController();
        var keyboardWorkflow = new MainWindowInputKeyboardWorkflowController();
        var actionController = new MainWindowInputActionController();
        runtime.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/game.nes");

        var decision = keyboardWorkflow.HandleKeyDown(
            runtime,
            actionController,
            Key.Z,
            KeyModifiers.None,
            isRomLoaded: true,
            activeRomPath: "/tmp/game.nes",
            handledKeys: new HashSet<Key> { Key.Z },
            isShortcutMatch: (_, _, _) => false);

        Assert.True(decision.ShouldSyncLegacyMirror);
        Assert.True(decision.ShouldRefreshActiveInputState);
        Assert.Equal(MainWindowInputAction.None, decision.Action);
        Assert.Contains(Key.Z, runtime.PressedKeys);
    }

    [Fact]
    public void HandleKeyDown_WhenShortcutMatched_ReturnsActionWithoutInputRefresh()
    {
        var runtime = new MainWindowActiveInputRuntimeController();
        var keyboardWorkflow = new MainWindowInputKeyboardWorkflowController();
        var actionController = new MainWindowInputActionController();
        runtime.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/game.nes");

        var decision = keyboardWorkflow.HandleKeyDown(
            runtime,
            actionController,
            Key.F5,
            KeyModifiers.Control,
            isRomLoaded: true,
            activeRomPath: "/tmp/game.nes",
            handledKeys: new HashSet<Key>(),
            isShortcutMatch: (id, _, _) => id == ShortcutCatalog.GameQuickSave);

        Assert.False(decision.ShouldSyncLegacyMirror);
        Assert.False(decision.ShouldRefreshActiveInputState);
        Assert.Equal(MainWindowInputAction.QuickSave, decision.Action);
        Assert.Empty(runtime.PressedKeys);
    }

    [Fact]
    public void HandleKeyUp_WhenKeyWasPressed_ReturnsRefreshDecision()
    {
        var runtime = new MainWindowActiveInputRuntimeController();
        var keyboardWorkflow = new MainWindowInputKeyboardWorkflowController();
        runtime.RefreshContext(isRomLoaded: true, activeRomPath: "/tmp/game.nes");
        runtime.PressKey(Key.Z);

        var decision = keyboardWorkflow.HandleKeyUp(runtime, Key.Z);

        Assert.True(decision.ShouldSyncLegacyMirror);
        Assert.True(decision.ShouldRefreshActiveInputState);
        Assert.Empty(runtime.PressedKeys);
    }

    [Fact]
    public void ShouldHandleKey_UsesRoutingControllerDecision()
    {
        var dispatchController = new MainWindowInputDispatchController();
        var keyboardWorkflow = new MainWindowInputKeyboardWorkflowController();

        var shouldHandle = keyboardWorkflow.ShouldHandleKey(
            dispatchController,
            Key.Q,
            KeyModifiers.None,
            isSettingsOpen: false,
            isQuickRomInputEditorOpen: false,
            handledKeys: new HashSet<Key> { Key.Q },
            sharedShortcutIds: ShortcutCatalog.SharedGameShortcutIds,
            isShortcutRoutingMatch: (_, _, _) => false);

        Assert.True(shouldHandle);
    }

    [Fact]
    public void DispatchAction_InvokesMappedActionHandler()
    {
        var keyboardWorkflow = new MainWindowInputKeyboardWorkflowController();
        var invoked = new List<string>();

        keyboardWorkflow.DispatchAction(
            MainWindowInputAction.Rewind,
            selectPreviousAsync: () =>
            {
                invoked.Add("prev");
                return Task.CompletedTask;
            },
            selectNextAsync: () =>
            {
                invoked.Add("next");
                return Task.CompletedTask;
            },
            playSelectedRom: () => invoked.Add("play"),
            quickSave: () => invoked.Add("save"),
            quickLoad: () => invoked.Add("load"),
            openBranchGallery: () => invoked.Add("gallery"),
            togglePause: () => invoked.Add("pause"),
            rewind: () => invoked.Add("rewind"));

        Assert.Equal(["rewind"], invoked);
    }
}

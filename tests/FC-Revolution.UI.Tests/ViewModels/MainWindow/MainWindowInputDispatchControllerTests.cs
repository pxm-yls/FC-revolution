using Avalonia.Input;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowInputDispatchControllerTests
{
    [Fact]
    public void BuildRoutingDecision_MatchesHandledBinding()
    {
        var controller = new MainWindowInputDispatchController();
        var decision = controller.BuildRoutingDecision(
            Key.Z,
            KeyModifiers.None,
            isSettingsOpen: false,
            isQuickRomInputEditorOpen: false,
            handledKeys: new HashSet<Key> { Key.Z },
            sharedShortcutIds: ["shortcut.a"],
            shortcutMatch: (_, _, _) => false);

        Assert.True(decision.ShouldHandleKey);
        Assert.True(decision.MatchesHandledBinding);
        Assert.False(decision.MatchesNavigationKey);
        Assert.False(decision.MatchesSharedShortcut);
        Assert.False(decision.IsConfigEditingActive);
    }

    [Fact]
    public void BuildRoutingDecision_MatchesNavigationKey()
    {
        var controller = new MainWindowInputDispatchController();
        var decision = controller.BuildRoutingDecision(
            Key.Left,
            KeyModifiers.None,
            isSettingsOpen: false,
            isQuickRomInputEditorOpen: false,
            handledKeys: new HashSet<Key>(),
            sharedShortcutIds: [],
            shortcutMatch: (_, _, _) => false);

        Assert.True(decision.ShouldHandleKey);
        Assert.False(decision.MatchesHandledBinding);
        Assert.True(decision.MatchesNavigationKey);
        Assert.False(decision.MatchesSharedShortcut);
    }

    [Fact]
    public void BuildRoutingDecision_MatchesSharedShortcut()
    {
        var controller = new MainWindowInputDispatchController();
        var decision = controller.BuildRoutingDecision(
            Key.S,
            KeyModifiers.Control,
            isSettingsOpen: false,
            isQuickRomInputEditorOpen: false,
            handledKeys: new HashSet<Key>(),
            sharedShortcutIds: ["shortcut.a", "shortcut.b"],
            shortcutMatch: (id, key, modifiers) => id == "shortcut.b" && key == Key.S && modifiers == KeyModifiers.Control);

        Assert.True(decision.ShouldHandleKey);
        Assert.False(decision.MatchesHandledBinding);
        Assert.False(decision.MatchesNavigationKey);
        Assert.True(decision.MatchesSharedShortcut);
    }

    [Fact]
    public void BuildRoutingDecision_ExposesConfigEditingStatus()
    {
        var controller = new MainWindowInputDispatchController();
        var decision = controller.BuildRoutingDecision(
            Key.None,
            KeyModifiers.None,
            isSettingsOpen: true,
            isQuickRomInputEditorOpen: false,
            handledKeys: new HashSet<Key>(),
            sharedShortcutIds: [],
            shortcutMatch: (_, _, _) => false);

        Assert.False(decision.ShouldHandleKey);
        Assert.True(decision.IsConfigEditingActive);
    }
}

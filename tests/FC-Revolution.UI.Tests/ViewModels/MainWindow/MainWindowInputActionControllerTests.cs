using Avalonia.Input;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowInputActionControllerTests
{
    [Fact]
    public void ResolveAction_ReturnsNavigationActions_WhenRomNotLoaded()
    {
        var controller = new MainWindowInputActionController();

        var left = controller.ResolveAction(Key.Left, KeyModifiers.None, isRomLoaded: false, (_, _, _) => false);
        var right = controller.ResolveAction(Key.Right, KeyModifiers.None, isRomLoaded: false, (_, _, _) => false);

        Assert.Equal(MainWindowInputAction.SelectPrevious, left);
        Assert.Equal(MainWindowInputAction.SelectNext, right);
    }

    [Fact]
    public void ResolveAction_IgnoresNavigationActions_WhenRomLoaded()
    {
        var controller = new MainWindowInputActionController();

        var left = controller.ResolveAction(Key.Left, KeyModifiers.None, isRomLoaded: true, (_, _, _) => false);
        var right = controller.ResolveAction(Key.Right, KeyModifiers.None, isRomLoaded: true, (_, _, _) => false);

        Assert.Equal(MainWindowInputAction.None, left);
        Assert.Equal(MainWindowInputAction.None, right);
    }

    [Fact]
    public void ResolveAction_ReturnsPlaySelectedRom_ForEnter()
    {
        var controller = new MainWindowInputActionController();
        var action = controller.ResolveAction(Key.Enter, KeyModifiers.None, isRomLoaded: true, (_, _, _) => false);

        Assert.Equal(MainWindowInputAction.PlaySelectedRom, action);
    }

    [Fact]
    public void ResolveAction_ReturnsShortcutActions()
    {
        var controller = new MainWindowInputActionController();
        var shortcuts = new Dictionary<string, MainWindowInputAction>
        {
            [ShortcutCatalog.GameQuickSave] = MainWindowInputAction.QuickSave,
            [ShortcutCatalog.GameQuickLoad] = MainWindowInputAction.QuickLoad,
            [ShortcutCatalog.GameToggleBranchGallery] = MainWindowInputAction.OpenBranchGallery,
            [ShortcutCatalog.GameTogglePause] = MainWindowInputAction.TogglePause,
            [ShortcutCatalog.GameRewind] = MainWindowInputAction.Rewind
        };

        foreach (var (shortcutId, expectedAction) in shortcuts)
        {
            var action = controller.ResolveAction(
                Key.F5,
                KeyModifiers.Control,
                isRomLoaded: true,
                (id, _, _) => id == shortcutId);

            Assert.Equal(expectedAction, action);
        }
    }

    [Fact]
    public void ResolveAction_ReturnsNone_WhenNoRuleMatches()
    {
        var controller = new MainWindowInputActionController();
        var action = controller.ResolveAction(Key.F12, KeyModifiers.None, isRomLoaded: true, (_, _, _) => false);

        Assert.Equal(MainWindowInputAction.None, action);
    }
}

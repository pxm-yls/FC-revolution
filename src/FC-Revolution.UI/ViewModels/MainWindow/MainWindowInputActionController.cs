using System;
using Avalonia.Input;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal enum MainWindowInputAction
{
    None,
    SelectPrevious,
    SelectNext,
    PlaySelectedRom,
    QuickSave,
    QuickLoad,
    OpenBranchGallery,
    TogglePause,
    Rewind
}

internal sealed class MainWindowInputActionController
{
    public MainWindowInputAction ResolveAction(
        Key key,
        KeyModifiers modifiers,
        bool isRomLoaded,
        Func<string, Key, KeyModifiers, bool> isShortcutMatch)
    {
        if (key == Key.Left && !isRomLoaded)
            return MainWindowInputAction.SelectPrevious;

        if (key == Key.Right && !isRomLoaded)
            return MainWindowInputAction.SelectNext;

        if (key == Key.Enter)
            return MainWindowInputAction.PlaySelectedRom;

        if (isShortcutMatch(ShortcutCatalog.GameQuickSave, key, modifiers))
            return MainWindowInputAction.QuickSave;

        if (isShortcutMatch(ShortcutCatalog.GameQuickLoad, key, modifiers))
            return MainWindowInputAction.QuickLoad;

        if (isShortcutMatch(ShortcutCatalog.GameToggleBranchGallery, key, modifiers))
            return MainWindowInputAction.OpenBranchGallery;

        if (isShortcutMatch(ShortcutCatalog.GameTogglePause, key, modifiers))
            return MainWindowInputAction.TogglePause;

        if (isShortcutMatch(ShortcutCatalog.GameRewind, key, modifiers))
            return MainWindowInputAction.Rewind;

        return MainWindowInputAction.None;
    }
}

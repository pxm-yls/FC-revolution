using System;
using System.Collections.Generic;
using Avalonia.Input;

namespace FC_Revolution.UI.ViewModels;

internal sealed record MainWindowInputRoutingDecision(
    bool ShouldHandleKey,
    bool IsConfigEditingActive,
    bool MatchesHandledBinding,
    bool MatchesNavigationKey,
    bool MatchesSharedShortcut);

internal sealed class MainWindowInputDispatchController
{
    public MainWindowInputRoutingDecision BuildRoutingDecision(
        Key key,
        KeyModifiers modifiers,
        bool isSettingsOpen,
        bool isQuickRomInputEditorOpen,
        IReadOnlySet<Key> handledKeys,
        IReadOnlyList<string> sharedShortcutIds,
        Func<string, Key, KeyModifiers, bool> shortcutMatch)
    {
        var matchesHandledBinding = handledKeys.Contains(key);
        var matchesNavigationKey = key is Key.Left or Key.Right or Key.Enter;
        var matchesSharedShortcut = false;
        foreach (var id in sharedShortcutIds)
        {
            if (!shortcutMatch(id, key, modifiers))
                continue;

            matchesSharedShortcut = true;
            break;
        }

        return new MainWindowInputRoutingDecision(
            ShouldHandleKey: matchesHandledBinding || matchesNavigationKey || matchesSharedShortcut,
            IsConfigEditingActive: isSettingsOpen || isQuickRomInputEditorOpen,
            MatchesHandledBinding: matchesHandledBinding,
            MatchesNavigationKey: matchesNavigationKey,
            MatchesSharedShortcut: matchesSharedShortcut);
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Input;

namespace FC_Revolution.UI.ViewModels;

internal sealed record MainWindowInputKeyDownDecision(
    bool ShouldSyncLegacyMirror,
    bool ShouldRefreshActiveInputState,
    MainWindowInputAction Action);

internal sealed record MainWindowInputKeyUpDecision(
    bool ShouldSyncLegacyMirror,
    bool ShouldRefreshActiveInputState);

internal sealed class MainWindowInputKeyboardWorkflowController
{
    public MainWindowInputKeyDownDecision HandleKeyDown(
        MainWindowActiveInputRuntimeController activeInputRuntime,
        MainWindowInputActionController inputActionController,
        Key key,
        KeyModifiers modifiers,
        bool isRomLoaded,
        string? activeRomPath,
        IReadOnlySet<Key> handledKeys,
        Func<string, Key, KeyModifiers, bool> isShortcutMatch)
    {
        ArgumentNullException.ThrowIfNull(activeInputRuntime);
        ArgumentNullException.ThrowIfNull(inputActionController);
        ArgumentNullException.ThrowIfNull(handledKeys);
        ArgumentNullException.ThrowIfNull(isShortcutMatch);

        activeInputRuntime.RefreshContext(isRomLoaded, activeRomPath);
        var shouldRefreshActiveInputState = handledKeys.Contains(key);
        if (shouldRefreshActiveInputState)
            activeInputRuntime.PressKey(key);

        var action = inputActionController.ResolveAction(
            key,
            modifiers,
            isRomLoaded,
            isShortcutMatch);

        return new MainWindowInputKeyDownDecision(
            ShouldSyncLegacyMirror: shouldRefreshActiveInputState,
            ShouldRefreshActiveInputState: shouldRefreshActiveInputState,
            Action: action);
    }

    public MainWindowInputKeyUpDecision HandleKeyUp(
        MainWindowActiveInputRuntimeController activeInputRuntime,
        Key key)
    {
        ArgumentNullException.ThrowIfNull(activeInputRuntime);

        var shouldRefreshActiveInputState = activeInputRuntime.ReleaseKey(key);
        return new MainWindowInputKeyUpDecision(
            ShouldSyncLegacyMirror: shouldRefreshActiveInputState,
            ShouldRefreshActiveInputState: shouldRefreshActiveInputState);
    }

    public bool ShouldHandleKey(
        MainWindowInputDispatchController inputDispatchController,
        Key key,
        KeyModifiers modifiers,
        bool isSettingsOpen,
        bool isQuickRomInputEditorOpen,
        IReadOnlySet<Key> handledKeys,
        IReadOnlyList<string> sharedShortcutIds,
        Func<string, Key, KeyModifiers, bool> isShortcutRoutingMatch)
    {
        ArgumentNullException.ThrowIfNull(inputDispatchController);
        ArgumentNullException.ThrowIfNull(handledKeys);
        ArgumentNullException.ThrowIfNull(sharedShortcutIds);
        ArgumentNullException.ThrowIfNull(isShortcutRoutingMatch);

        var decision = inputDispatchController.BuildRoutingDecision(
            key,
            modifiers,
            isSettingsOpen,
            isQuickRomInputEditorOpen,
            handledKeys,
            sharedShortcutIds,
            isShortcutRoutingMatch);
        return decision.ShouldHandleKey;
    }

    public void DispatchAction(
        MainWindowInputAction action,
        Func<Task> selectPreviousAsync,
        Func<Task> selectNextAsync,
        Action playSelectedRom,
        Action quickSave,
        Action quickLoad,
        Action openBranchGallery,
        Action togglePause,
        Action rewind)
    {
        ArgumentNullException.ThrowIfNull(selectPreviousAsync);
        ArgumentNullException.ThrowIfNull(selectNextAsync);
        ArgumentNullException.ThrowIfNull(playSelectedRom);
        ArgumentNullException.ThrowIfNull(quickSave);
        ArgumentNullException.ThrowIfNull(quickLoad);
        ArgumentNullException.ThrowIfNull(openBranchGallery);
        ArgumentNullException.ThrowIfNull(togglePause);
        ArgumentNullException.ThrowIfNull(rewind);

        switch (action)
        {
            case MainWindowInputAction.SelectPrevious:
                _ = selectPreviousAsync();
                break;
            case MainWindowInputAction.SelectNext:
                _ = selectNextAsync();
                break;
            case MainWindowInputAction.PlaySelectedRom:
                playSelectedRom();
                break;
            case MainWindowInputAction.QuickSave:
                quickSave();
                break;
            case MainWindowInputAction.QuickLoad:
                quickLoad();
                break;
            case MainWindowInputAction.OpenBranchGallery:
                openBranchGallery();
                break;
            case MainWindowInputAction.TogglePause:
                togglePause();
                break;
            case MainWindowInputAction.Rewind:
                rewind();
                break;
        }
    }
}

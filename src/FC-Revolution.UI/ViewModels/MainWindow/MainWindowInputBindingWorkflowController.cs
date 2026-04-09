using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Core.Input;
using FCRevolution.Storage;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record MainWindowEffectiveInputBindingState(
    IReadOnlyDictionary<Key, (int Player, NesButton Button)> EffectiveKeyMap,
    IReadOnlyList<ResolvedExtraInputBinding> EffectiveExtraBindings,
    IReadOnlySet<Key> EffectiveHandledKeys);

internal sealed class MainWindowInputBindingWorkflowController
{
    public void BuildAndApplyGlobalInputBindingViewState(
        MainWindowInputBindingsController inputBindingsController,
        SystemConfigProfile? profile,
        IReadOnlyDictionary<int, IReadOnlyDictionary<NesButton, Key>> defaultKeyMaps,
        IReadOnlyList<Key> configurableKeys,
        InputBindingLayoutProfile inputBindingLayout,
        ObservableCollection<InputBindingEntry> globalInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindings,
        ObservableCollection<InputBindingEntry> globalInputBindingsPlayer1,
        ObservableCollection<InputBindingEntry> globalInputBindingsPlayer2,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindingsPlayer1,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindingsPlayer2)
    {
        ArgumentNullException.ThrowIfNull(inputBindingsController);
        ArgumentNullException.ThrowIfNull(defaultKeyMaps);
        ArgumentNullException.ThrowIfNull(configurableKeys);
        ArgumentNullException.ThrowIfNull(inputBindingLayout);
        ArgumentNullException.ThrowIfNull(globalInputBindings);
        ArgumentNullException.ThrowIfNull(globalExtraInputBindings);
        ArgumentNullException.ThrowIfNull(globalInputBindingsPlayer1);
        ArgumentNullException.ThrowIfNull(globalInputBindingsPlayer2);
        ArgumentNullException.ThrowIfNull(globalExtraInputBindingsPlayer1);
        ArgumentNullException.ThrowIfNull(globalExtraInputBindingsPlayer2);

        var viewState = inputBindingsController.BuildGlobalInputBindingViewState(
            profile,
            defaultKeyMaps,
            configurableKeys,
            inputBindingLayout);
        ApplyGlobalInputBindingViewState(
            viewState,
            globalInputBindings,
            globalExtraInputBindings,
            globalInputBindingsPlayer1,
            globalInputBindingsPlayer2,
            globalExtraInputBindingsPlayer1,
            globalExtraInputBindingsPlayer2);
    }

    public void RefreshRomInputBindings(
        MainWindowInputOverrideController inputOverrideController,
        RomLibraryItem? currentRom,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        ObservableCollection<InputBindingEntry> globalInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindings,
        ObservableCollection<InputBindingEntry> romInputBindings,
        ObservableCollection<InputBindingEntry> romInputBindingsPlayer1,
        ObservableCollection<InputBindingEntry> romInputBindingsPlayer2,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindings,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindingsPlayer1,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindingsPlayer2,
        IReadOnlyDictionary<int, IReadOnlyDictionary<NesButton, Key>> defaultKeyMaps,
        IReadOnlyList<Key> configurableKeys,
        InputBindingLayoutProfile inputBindingLayout,
        Action<bool> setRomInputOverrideEnabled,
        Action notifyRomInputOverrideSummaryChanged)
    {
        ArgumentNullException.ThrowIfNull(inputOverrideController);
        ArgumentNullException.ThrowIfNull(romInputOverrides);
        ArgumentNullException.ThrowIfNull(romExtraInputOverrides);
        ArgumentNullException.ThrowIfNull(globalInputBindings);
        ArgumentNullException.ThrowIfNull(globalExtraInputBindings);
        ArgumentNullException.ThrowIfNull(romInputBindings);
        ArgumentNullException.ThrowIfNull(romInputBindingsPlayer1);
        ArgumentNullException.ThrowIfNull(romInputBindingsPlayer2);
        ArgumentNullException.ThrowIfNull(romExtraInputBindings);
        ArgumentNullException.ThrowIfNull(romExtraInputBindingsPlayer1);
        ArgumentNullException.ThrowIfNull(romExtraInputBindingsPlayer2);
        ArgumentNullException.ThrowIfNull(defaultKeyMaps);
        ArgumentNullException.ThrowIfNull(configurableKeys);
        ArgumentNullException.ThrowIfNull(inputBindingLayout);
        ArgumentNullException.ThrowIfNull(setRomInputOverrideEnabled);
        ArgumentNullException.ThrowIfNull(notifyRomInputOverrideSummaryChanged);

        inputOverrideController.RefreshRomInputBindings(
            currentRom,
            romInputOverrides,
            romExtraInputOverrides,
            globalInputBindings,
            globalExtraInputBindings,
            romInputBindings,
            romInputBindingsPlayer1,
            romInputBindingsPlayer2,
            romExtraInputBindings,
            romExtraInputBindingsPlayer1,
            romExtraInputBindingsPlayer2,
            defaultKeyMaps,
            configurableKeys,
            inputBindingLayout,
            setRomInputOverrideEnabled,
            notifyRomInputOverrideSummaryChanged);
    }

    public MainWindowEffectiveInputBindingState BuildEffectiveInputBindingState(
        MainWindowInputBindingsController inputBindingsController,
        MainWindowInputStateController inputStateController,
        string? romPath,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        IReadOnlyDictionary<int, IReadOnlyDictionary<NesButton, Key>> defaultKeyMaps)
    {
        ArgumentNullException.ThrowIfNull(inputBindingsController);
        ArgumentNullException.ThrowIfNull(inputStateController);
        ArgumentNullException.ThrowIfNull(romInputOverrides);
        ArgumentNullException.ThrowIfNull(romExtraInputOverrides);
        ArgumentNullException.ThrowIfNull(globalInputBindings);
        ArgumentNullException.ThrowIfNull(globalExtraInputBindings);
        ArgumentNullException.ThrowIfNull(defaultKeyMaps);

        var playerMaps = inputBindingsController.GetEffectivePlayerInputMaps(
            romPath,
            romInputOverrides,
            globalInputBindings,
            defaultKeyMaps);
        var keyMap = BuildEffectiveKeyMap(playerMaps);

        var extraBindings = inputBindingsController
            .GetEffectiveExtraInputBindingProfiles(
                romPath,
                romExtraInputOverrides,
                globalExtraInputBindings)
            .Select(inputStateController.ResolveExtraInputBinding)
            .Where(static binding => binding != null)
            .Select(static binding => binding!)
            .ToList();

        var handledKeys = inputStateController.BuildEffectiveHandledKeys(keyMap, extraBindings);
        return new MainWindowEffectiveInputBindingState(keyMap, extraBindings, handledKeys);
    }

    private static void ApplyGlobalInputBindingViewState(
        GlobalInputBindingViewState viewState,
        ObservableCollection<InputBindingEntry> globalInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindings,
        ObservableCollection<InputBindingEntry> globalInputBindingsPlayer1,
        ObservableCollection<InputBindingEntry> globalInputBindingsPlayer2,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindingsPlayer1,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindingsPlayer2)
    {
        globalInputBindings.Clear();
        foreach (var entry in viewState.InputBindings)
            globalInputBindings.Add(entry);

        globalExtraInputBindings.Clear();
        foreach (var entry in viewState.ExtraBindings)
            globalExtraInputBindings.Add(entry);

        MainWindowInputOverrideController.RefreshPlayerBindingViews(
            globalInputBindings,
            globalInputBindingsPlayer1,
            globalInputBindingsPlayer2);
        MainWindowInputOverrideController.RefreshExtraBindingViews(
            globalExtraInputBindings,
            globalExtraInputBindingsPlayer1,
            globalExtraInputBindingsPlayer2);
    }

    private static Dictionary<Key, (int Player, NesButton Button)> BuildEffectiveKeyMap(
        IReadOnlyDictionary<int, Dictionary<NesButton, Key>> playerMaps)
    {
        var keyMap = new Dictionary<Key, (int Player, NesButton Button)>();
        foreach (var (player, bindings) in playerMaps)
        {
            foreach (var (button, key) in bindings)
                keyMap[key] = (player, button);
        }

        return keyMap;
    }
}

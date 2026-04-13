using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Storage;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record MainWindowEffectiveInputBindingState(
    IReadOnlyDictionary<Key, (int Player, string ActionId)> EffectiveKeyMap,
    IReadOnlyList<ResolvedExtraInputBinding> EffectiveExtraBindings,
    IReadOnlySet<Key> EffectiveHandledKeys);

internal sealed class MainWindowInputBindingWorkflowController
{
    public void BuildAndApplyGlobalInputBindingViewState(
        MainWindowInputBindingsController inputBindingsController,
        SystemConfigProfile? profile,
        CoreInputBindingSchema inputBindingSchema,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, Key>> defaultKeyMaps,
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
            inputBindingSchema,
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

    public void BuildAndApplyGlobalInputBindingViewState(
        MainWindowInputBindingsController inputBindingsController,
        SystemConfigProfile? profile,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, Key>> defaultKeyMaps,
        IReadOnlyList<Key> configurableKeys,
        InputBindingLayoutProfile inputBindingLayout,
        ObservableCollection<InputBindingEntry> globalInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindings,
        ObservableCollection<InputBindingEntry> globalInputBindingsPlayer1,
        ObservableCollection<InputBindingEntry> globalInputBindingsPlayer2,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindingsPlayer1,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindingsPlayer2) =>
        BuildAndApplyGlobalInputBindingViewState(
            inputBindingsController,
            profile,
            CoreInputBindingSchema.CreateFallback(),
            defaultKeyMaps,
            configurableKeys,
            inputBindingLayout,
            globalInputBindings,
            globalExtraInputBindings,
            globalInputBindingsPlayer1,
            globalInputBindingsPlayer2,
            globalExtraInputBindingsPlayer1,
            globalExtraInputBindingsPlayer2);

    public void RefreshRomInputBindings(
        MainWindowInputOverrideController inputOverrideController,
        RomLibraryItem? currentRom,
        Dictionary<string, Dictionary<int, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        ObservableCollection<InputBindingEntry> globalInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindings,
        ObservableCollection<InputBindingEntry> romInputBindings,
        ObservableCollection<InputBindingEntry> romInputBindingsPlayer1,
        ObservableCollection<InputBindingEntry> romInputBindingsPlayer2,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindings,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindingsPlayer1,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindingsPlayer2,
        CoreInputBindingSchema inputBindingSchema,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, Key>> defaultKeyMaps,
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
            inputBindingSchema,
            defaultKeyMaps,
            configurableKeys,
            inputBindingLayout,
            setRomInputOverrideEnabled,
            notifyRomInputOverrideSummaryChanged);
    }

    public void RefreshRomInputBindings(
        MainWindowInputOverrideController inputOverrideController,
        RomLibraryItem? currentRom,
        Dictionary<string, Dictionary<int, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        ObservableCollection<InputBindingEntry> globalInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindings,
        ObservableCollection<InputBindingEntry> romInputBindings,
        ObservableCollection<InputBindingEntry> romInputBindingsPlayer1,
        ObservableCollection<InputBindingEntry> romInputBindingsPlayer2,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindings,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindingsPlayer1,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindingsPlayer2,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, Key>> defaultKeyMaps,
        IReadOnlyList<Key> configurableKeys,
        InputBindingLayoutProfile inputBindingLayout,
        Action<bool> setRomInputOverrideEnabled,
        Action notifyRomInputOverrideSummaryChanged) =>
        RefreshRomInputBindings(
            inputOverrideController,
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
            CoreInputBindingSchema.CreateFallback(),
            defaultKeyMaps,
            configurableKeys,
            inputBindingLayout,
            setRomInputOverrideEnabled,
            notifyRomInputOverrideSummaryChanged);

    public MainWindowEffectiveInputBindingState BuildEffectiveInputBindingState(
        MainWindowInputBindingsController inputBindingsController,
        MainWindowInputStateController inputStateController,
        CoreInputBindingSchema inputBindingSchema,
        string? romPath,
        Dictionary<string, Dictionary<int, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, Key>> defaultKeyMaps)
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
            .Select(profile => inputStateController.ResolveExtraInputBinding(profile, inputBindingSchema))
            .Where(static binding => binding != null)
            .Select(static binding => binding!)
            .ToList();

        var handledKeys = inputStateController.BuildEffectiveHandledKeys(keyMap, extraBindings);
        return new MainWindowEffectiveInputBindingState(keyMap, extraBindings, handledKeys);
    }

    public MainWindowEffectiveInputBindingState BuildEffectiveInputBindingState(
        MainWindowInputBindingsController inputBindingsController,
        MainWindowInputStateController inputStateController,
        string? romPath,
        Dictionary<string, Dictionary<int, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, Key>> defaultKeyMaps) =>
        BuildEffectiveInputBindingState(
            inputBindingsController,
            inputStateController,
            CoreInputBindingSchema.CreateFallback(),
            romPath,
            romInputOverrides,
            romExtraInputOverrides,
            globalInputBindings,
            globalExtraInputBindings,
            defaultKeyMaps);

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

    private static Dictionary<Key, (int Player, string ActionId)> BuildEffectiveKeyMap(
        IReadOnlyDictionary<int, Dictionary<string, Key>> playerMaps)
    {
        var keyMap = new Dictionary<Key, (int Player, string ActionId)>();
        foreach (var (player, bindings) in playerMaps)
        {
            foreach (var (actionId, key) in bindings)
                keyMap[key] = (player, actionId);
        }

        return keyMap;
    }
}

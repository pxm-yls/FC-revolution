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
    IReadOnlyDictionary<Key, (string PortId, string ActionId)> EffectiveKeyMap,
    IReadOnlyList<ResolvedExtraInputBinding> EffectiveExtraBindings,
    IReadOnlySet<Key> EffectiveHandledKeys);

internal sealed class MainWindowInputBindingWorkflowController
{
    public void BuildAndApplyGlobalInputBindingViewState(
        MainWindowInputBindingsController inputBindingsController,
        SystemConfigProfile? profile,
        CoreInputBindingSchema inputBindingSchema,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, Key>> defaultKeyMaps,
        IReadOnlyList<Key> configurableKeys,
        InputBindingLayoutProfile inputBindingLayout,
        ObservableCollection<InputBindingEntry> globalInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindings,
        ObservableCollection<InputBindingPortGroup> globalInputPortGroups)
    {
        ArgumentNullException.ThrowIfNull(inputBindingsController);
        ArgumentNullException.ThrowIfNull(defaultKeyMaps);
        ArgumentNullException.ThrowIfNull(configurableKeys);
        ArgumentNullException.ThrowIfNull(inputBindingLayout);
        ArgumentNullException.ThrowIfNull(globalInputBindings);
        ArgumentNullException.ThrowIfNull(globalExtraInputBindings);
        ArgumentNullException.ThrowIfNull(globalInputPortGroups);

        var viewState = inputBindingsController.BuildGlobalInputBindingViewState(
            profile,
            inputBindingSchema,
            defaultKeyMaps,
            configurableKeys,
            inputBindingLayout);
        ApplyInputBindingViewState(
            viewState.InputBindings,
            viewState.ExtraBindings,
            inputBindingSchema,
            globalInputBindings,
            globalExtraInputBindings,
            globalInputPortGroups);
    }

    public void RefreshRomInputBindings(
        MainWindowInputOverrideController inputOverrideController,
        RomLibraryItem? currentRom,
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        ObservableCollection<InputBindingEntry> globalInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindings,
        ObservableCollection<InputBindingEntry> romInputBindings,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindings,
        ObservableCollection<InputBindingPortGroup> romInputPortGroups,
        CoreInputBindingSchema inputBindingSchema,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, Key>> defaultKeyMaps,
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
        ArgumentNullException.ThrowIfNull(romExtraInputBindings);
        ArgumentNullException.ThrowIfNull(romInputPortGroups);
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
            romExtraInputBindings,
            romInputPortGroups,
            inputBindingSchema,
            defaultKeyMaps,
            configurableKeys,
            inputBindingLayout,
            setRomInputOverrideEnabled,
            notifyRomInputOverrideSummaryChanged);
    }

    public MainWindowEffectiveInputBindingState BuildEffectiveInputBindingState(
        MainWindowInputBindingsController inputBindingsController,
        MainWindowInputStateController inputStateController,
        CoreInputBindingSchema inputBindingSchema,
        string? romPath,
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, Key>> defaultKeyMaps)
    {
        ArgumentNullException.ThrowIfNull(inputBindingsController);
        ArgumentNullException.ThrowIfNull(inputStateController);
        ArgumentNullException.ThrowIfNull(romInputOverrides);
        ArgumentNullException.ThrowIfNull(romExtraInputOverrides);
        ArgumentNullException.ThrowIfNull(globalInputBindings);
        ArgumentNullException.ThrowIfNull(globalExtraInputBindings);
        ArgumentNullException.ThrowIfNull(defaultKeyMaps);

        var portMaps = inputBindingsController.GetEffectiveInputMapsByPort(
            romPath,
            romInputOverrides,
            globalInputBindings,
            defaultKeyMaps,
            inputBindingSchema);
        var keyMap = BuildEffectiveKeyMap(portMaps, inputBindingSchema);

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

    private static void ApplyInputBindingViewState(
        IReadOnlyList<InputBindingEntry> inputBindings,
        IReadOnlyList<ExtraInputBindingEntry> extraBindings,
        CoreInputBindingSchema inputBindingSchema,
        ObservableCollection<InputBindingEntry> targetInputBindings,
        ObservableCollection<ExtraInputBindingEntry> targetExtraBindings,
        ObservableCollection<InputBindingPortGroup> targetPortGroups)
    {
        targetInputBindings.Clear();
        foreach (var entry in inputBindings)
            targetInputBindings.Add(entry);

        targetExtraBindings.Clear();
        foreach (var entry in extraBindings)
            targetExtraBindings.Add(entry);

        MainWindowInputOverrideController.RefreshPortBindingViews(
            targetInputBindings,
            targetExtraBindings,
            targetPortGroups,
            inputBindingSchema);
    }

    private static Dictionary<Key, (string PortId, string ActionId)> BuildEffectiveKeyMap(
        IReadOnlyDictionary<string, Dictionary<string, Key>> portMaps,
        CoreInputBindingSchema inputBindingSchema)
    {
        var keyMap = new Dictionary<Key, (string PortId, string ActionId)>();
        foreach (var portMap in portMaps)
        {
            if (!inputBindingSchema.TryNormalizePortId(portMap.Key, out var portId))
                continue;

            foreach (var (actionId, key) in portMap.Value)
                keyMap[key] = (portId, actionId);
        }

        return keyMap;
    }
}

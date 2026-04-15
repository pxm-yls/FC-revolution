using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Storage;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record ShortcutCommitResult(
    bool Handled,
    bool Accepted,
    bool RequiresSave,
    bool RequiresSessionApply,
    bool RequiresNotify,
    string? StatusText);

internal sealed record RomInputBindingViewState(
    bool IsOverrideEnabled,
    IReadOnlyList<InputBindingEntry> InputBindings,
    IReadOnlyList<ExtraInputBindingEntry> ExtraBindings);

internal sealed record GlobalInputBindingViewState(
    IReadOnlyList<InputBindingEntry> InputBindings,
    IReadOnlyList<ExtraInputBindingEntry> ExtraBindings);

internal sealed record GlobalInputConfigSaveState(
    Dictionary<string, Dictionary<string, string>> PortInputOverrides,
    List<ExtraInputBindingProfile> ExtraInputBindings,
    Dictionary<string, ShortcutBindingProfile> ShortcutBindings,
    InputBindingLayoutProfile InputBindingLayout);

internal sealed class MainWindowInputBindingsController
{
    public void InitializeShortcutBindings(
        Dictionary<string, ShortcutBindingEntry> shortcutBindings,
        ObservableCollection<ShortcutBindingEntry> mainWindowShortcutBindings,
        ObservableCollection<ShortcutBindingEntry> sharedGameShortcutBindings,
        ObservableCollection<ShortcutBindingEntry> gameWindowShortcutBindings)
    {
        shortcutBindings.Clear();
        PopulateShortcutBindings(shortcutBindings, mainWindowShortcutBindings, ShortcutCatalog.MainWindowShortcuts);
        PopulateShortcutBindings(shortcutBindings, sharedGameShortcutBindings, ShortcutCatalog.SharedGameShortcuts);
        PopulateShortcutBindings(shortcutBindings, gameWindowShortcutBindings, ShortcutCatalog.GameWindowOnlyShortcuts);
    }

    public void LoadShortcutBindings(
        SystemConfigProfile profile,
        IReadOnlyDictionary<string, ShortcutBindingEntry> shortcutBindings)
    {
        var resolved = ShortcutCatalog.ResolveGestureMap(profile.ShortcutBindings);
        foreach (var entry in shortcutBindings.Values)
        {
            if (!resolved.TryGetValue(entry.Id, out var gesture))
                gesture = entry.DefaultGesture;

            entry.ApplyGesture(gesture);
        }
    }

    public Dictionary<string, ShortcutBindingProfile> BuildShortcutProfiles(
        IReadOnlyDictionary<string, ShortcutBindingEntry> shortcutBindings) =>
        shortcutBindings.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.SelectedGesture.ToProfile(),
            StringComparer.Ordinal);

    public Dictionary<string, ShortcutGesture> BuildGameWindowShortcutMap(
        IReadOnlyDictionary<string, ShortcutBindingEntry> shortcutBindings) =>
        ShortcutCatalog.GameWindowShortcutIds.ToDictionary(
            id => id,
            id => GetShortcutGesture(id, shortcutBindings),
            StringComparer.Ordinal);

    public ShortcutGesture GetShortcutGesture(string id, IReadOnlyDictionary<string, ShortcutBindingEntry> shortcutBindings)
    {
        if (shortcutBindings.TryGetValue(id, out var entry))
            return entry.SelectedGesture;

        return ShortcutCatalog.ById.TryGetValue(id, out var descriptor)
            ? descriptor.DefaultGesture
            : ShortcutGesture.Empty;
    }

    public ShortcutCommitResult TryCommitShortcutBinding(
        ShortcutBindingEntry? entry,
        IReadOnlyCollection<ShortcutBindingEntry> allShortcutBindings,
        Key key,
        KeyModifiers modifiers)
    {
        if (entry == null)
            return new ShortcutCommitResult(false, false, false, false, false, null);

        if (!entry.TryBuildGesture(key, modifiers, out var gesture))
            return new ShortcutCommitResult(true, false, false, false, false, null);

        if (!gesture.IsComplete)
            return new ShortcutCommitResult(true, false, false, false, false, null);

        var conflict = allShortcutBindings.FirstOrDefault(binding =>
            !ReferenceEquals(binding, entry) &&
            binding.SelectedGesture == gesture);
        if (conflict != null)
        {
            entry.ContinueCapture(gesture.Modifiers);
            return new ShortcutCommitResult(
                true,
                false,
                false,
                false,
                false,
                $"{entry.ActionName} 与 {conflict.ActionName} 冲突：{gesture.ToDisplayString()}");
        }

        entry.ApplyGesture(gesture);
        return new ShortcutCommitResult(
            true,
            true,
            true,
            true,
            true,
            $"已设置 {entry.ActionName}: {gesture.ToDisplayString()}");
    }

    public void EnsureRomInputOverrideForEditing(
        string romPath,
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings)
    {
        if (romInputOverrides.ContainsKey(romPath) || romExtraInputOverrides.ContainsKey(romPath))
            return;

        romInputOverrides[romPath] = BuildInputMapsByPort(globalInputBindings);
        romExtraInputOverrides[romPath] = BuildExtraInputBindingProfiles(globalExtraInputBindings);
    }

    public void LoadRomProfileInputOverride(
        string romPath,
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        CoreInputBindingSchema inputBindingSchema)
    {
        var profile = RomConfigProfile.Load(romPath);
        var portMaps = BuildInputMapsByPort(profile, inputBindingSchema);
        if (portMaps.Count > 0)
            romInputOverrides[romPath] = portMaps;

        if (profile.ExtraInputBindings.Count > 0)
            romExtraInputOverrides[romPath] = profile.ExtraInputBindings.Select(CloneExtraInputBindingProfile).ToList();
    }

    public void LoadRomProfileInputOverride(
        string romPath,
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides)
        => LoadRomProfileInputOverride(romPath, romInputOverrides, romExtraInputOverrides, CoreInputBindingSchema.CreateFallback());

    public void SaveRomProfileInputOverride(
        string romPath,
        Dictionary<string, Dictionary<string, Key>>? inputOverride,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        CoreInputBindingSchema inputBindingSchema)
    {
        var profile = RomConfigProfile.Load(romPath);
        profile.PortInputOverrides = inputOverride == null
            ? new Dictionary<string, Dictionary<string, string>>()
            : BuildPortInputOverrideProfiles(inputOverride, inputBindingSchema);
        profile.ExtraInputBindings = romExtraInputOverrides.TryGetValue(romPath, out var extraBindings)
            ? extraBindings.Select(CloneExtraInputBindingProfile).ToList()
            : [];
        RomConfigProfile.Save(romPath, profile);
    }

    public void SaveRomProfileInputOverride(
        string romPath,
        Dictionary<string, Dictionary<string, Key>>? inputOverride,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides)
        => SaveRomProfileInputOverride(romPath, inputOverride, romExtraInputOverrides, CoreInputBindingSchema.CreateFallback());

    public GlobalInputConfigSaveState BuildGlobalInputConfigSaveState(
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        IReadOnlyDictionary<string, ShortcutBindingEntry> shortcutBindings,
        InputBindingLayoutProfile inputBindingLayout,
        CoreInputBindingSchema inputBindingSchema)
    {
        var portInputMaps = BuildInputMapsByPort(globalInputBindings);

        return new GlobalInputConfigSaveState(
            BuildPortInputOverrideProfiles(portInputMaps, inputBindingSchema),
            BuildExtraInputBindingProfiles(globalExtraInputBindings),
            BuildShortcutProfiles(shortcutBindings),
            inputBindingLayout.Clone());
    }

    public GlobalInputConfigSaveState BuildGlobalInputConfigSaveState(
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        IReadOnlyDictionary<string, ShortcutBindingEntry> shortcutBindings,
        InputBindingLayoutProfile inputBindingLayout)
        => BuildGlobalInputConfigSaveState(
            globalInputBindings,
            globalExtraInputBindings,
            shortcutBindings,
            inputBindingLayout,
            CoreInputBindingSchema.CreateFallback());

    public GlobalInputBindingViewState BuildGlobalInputBindingViewState(
        SystemConfigProfile? profile,
        CoreInputBindingSchema inputBindingSchema,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, Key>> defaultKeyMaps,
        IReadOnlyList<Key> configurableKeys,
        InputBindingLayoutProfile inputBindingLayout)
    {
        var portOverrides = profile == null
            ? new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase)
            : BuildInputMapsByPort(profile, inputBindingSchema);
        var inputBindings = BuildInputBindingEntries(
            portOverrides,
            inputBindingSchema,
            defaultKeyMaps,
            configurableKeys,
            inputBindingLayout);
        var extraBindings = BuildExtraInputBindingEntries(
            profile?.ExtraInputBindings ?? [],
            configurableKeys,
            inputBindingSchema.ExtraInputButtonOptions,
            inputBindingSchema);
        return new GlobalInputBindingViewState(inputBindings, extraBindings);
    }

    public GlobalInputBindingViewState BuildGlobalInputBindingViewState(
        SystemConfigProfile? profile,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, Key>> defaultKeyMaps,
        IReadOnlyList<Key> configurableKeys,
        InputBindingLayoutProfile inputBindingLayout) =>
        BuildGlobalInputBindingViewState(
            profile,
            CoreInputBindingSchema.CreateFallback(),
            defaultKeyMaps,
            configurableKeys,
            inputBindingLayout);

    public RomInputBindingViewState BuildRomInputBindingViewState(
        string? currentRomPath,
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        CoreInputBindingSchema inputBindingSchema,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, Key>> defaultKeyMaps,
        IReadOnlyList<Key> configurableKeys,
        InputBindingLayoutProfile inputBindingLayout)
    {
        if (string.IsNullOrWhiteSpace(currentRomPath))
            return new RomInputBindingViewState(false, [], []);

        var hasBaseOverride = romInputOverrides.TryGetValue(currentRomPath, out var overrideMap);
        var hasExtraOverride = romExtraInputOverrides.TryGetValue(currentRomPath, out var extraOverrideProfiles);
        if (!hasBaseOverride && !hasExtraOverride)
        {
            var clonedGlobalInputs = globalInputBindings
                .Select(entry =>
                {
                    var cloned = new InputBindingEntry(
                        entry.PortId,
                        entry.PortLabel,
                        entry.ActionId,
                        entry.ActionName,
                        entry.SelectedKey,
                        configurableKeys);
                    cloned.ApplyLayout(inputBindingLayout);
                    return cloned;
                })
                .ToList();
            var clonedGlobalExtra = globalExtraInputBindings.Select(entry => entry.Clone()).ToList();
            return new RomInputBindingViewState(false, clonedGlobalInputs, clonedGlobalExtra);
        }

        var romInputs = BuildInputBindingEntries(
            overrideMap ?? new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase),
            inputBindingSchema,
            defaultKeyMaps,
            configurableKeys,
            inputBindingLayout);
        var romExtra = BuildExtraInputBindingEntries(
            extraOverrideProfiles ?? [],
            configurableKeys,
            inputBindingSchema.ExtraInputButtonOptions,
            inputBindingSchema);
        return new RomInputBindingViewState(true, romInputs, romExtra);
    }

    public RomInputBindingViewState BuildRomInputBindingViewState(
        string? currentRomPath,
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, Key>> defaultKeyMaps,
        IReadOnlyList<Key> configurableKeys,
        InputBindingLayoutProfile inputBindingLayout) =>
        BuildRomInputBindingViewState(
            currentRomPath,
            romInputOverrides,
            romExtraInputOverrides,
            globalInputBindings,
            globalExtraInputBindings,
            CoreInputBindingSchema.CreateFallback(),
            defaultKeyMaps,
            configurableKeys,
            inputBindingLayout);

    public Dictionary<string, Dictionary<string, Key>> GetEffectiveInputMapsByPort(
        string? romPath,
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, Key>> defaultKeyMaps,
        CoreInputBindingSchema inputBindingSchema)
    {
        var source = romPath != null && romInputOverrides.TryGetValue(romPath, out var overrideMap)
            ? overrideMap
            : BuildInputMapsByPort(globalInputBindings);

        var result = new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase);
        foreach (var port in inputBindingSchema.GetSupportedPorts())
        {
            var portDefaults = defaultKeyMaps[port.PortId];
            var portMap = source.TryGetValue(port.PortId, out var map)
                ? map
                : BuildInputMap(globalInputBindings, port.PortId);
            result[port.PortId] = portDefaults.ToDictionary(
                pair => pair.Key,
                pair => portMap.TryGetValue(pair.Key, out var key) ? key : pair.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        return result;
    }

    public Dictionary<string, Dictionary<string, Key>> GetEffectiveInputMapsByPort(
        string? romPath,
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, Key>> defaultKeyMaps)
        => GetEffectiveInputMapsByPort(
            romPath,
            romInputOverrides,
            globalInputBindings,
            defaultKeyMaps,
            CoreInputBindingSchema.CreateFallback());

    public List<ExtraInputBindingProfile> GetEffectiveExtraInputBindingProfiles(
        string? romPath,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings)
    {
        var source = romPath != null && romExtraInputOverrides.TryGetValue(romPath, out var overrideBindings)
            ? overrideBindings
            : BuildExtraInputBindingProfiles(globalExtraInputBindings);

        return source.Select(CloneExtraInputBindingProfile).ToList();
    }

    public Dictionary<string, Dictionary<string, Key>> BuildInputMapsByPort(IEnumerable<InputBindingEntry> entries)
    {
        var maps = new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in entries.GroupBy(entry => entry.PortId, StringComparer.OrdinalIgnoreCase))
            maps[group.Key] = group.ToDictionary(entry => entry.ActionId, entry => entry.SelectedKey, StringComparer.OrdinalIgnoreCase);

        return maps;
    }

    public List<ExtraInputBindingProfile> BuildExtraInputBindingProfiles(IEnumerable<ExtraInputBindingEntry> entries) =>
        entries.Select(entry => entry.ToProfile()).ToList();

    public Dictionary<string, Dictionary<string, Key>> BuildInputMapsByPort(SystemConfigProfile profile, CoreInputBindingSchema inputBindingSchema)
    {
        var maps = new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase);
        foreach (var overrideEntry in profile.PortInputOverrides)
        {
            if (!TryResolveOverridePortId(overrideEntry.Key, inputBindingSchema, out var portId))
                continue;

            var map = BuildNormalizedActionMap(overrideEntry.Value, portId, inputBindingSchema);
            if (map.Count > 0)
                maps[portId] = map;
        }

        return maps;
    }

    public Dictionary<string, Dictionary<string, Key>> BuildInputMapsByPort(SystemConfigProfile profile) =>
        BuildInputMapsByPort(profile, CoreInputBindingSchema.CreateFallback());

    public Dictionary<string, Dictionary<string, Key>> BuildInputMapsByPort(RomConfigProfile profile, CoreInputBindingSchema inputBindingSchema)
    {
        var maps = new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase);
        foreach (var overrideEntry in profile.PortInputOverrides)
        {
            if (!TryResolveOverridePortId(overrideEntry.Key, inputBindingSchema, out var portId))
                continue;

            var map = BuildNormalizedActionMap(overrideEntry.Value, portId, inputBindingSchema);
            if (map.Count > 0)
                maps[portId] = map;
        }

        return maps;
    }

    public Dictionary<string, Dictionary<string, Key>> BuildInputMapsByPort(RomConfigProfile profile) =>
        BuildInputMapsByPort(profile, CoreInputBindingSchema.CreateFallback());

    public ExtraInputBindingProfile CloneExtraInputBindingProfile(ExtraInputBindingProfile profile) => new()
    {
        PortId = profile.PortId,
        LegacyPortOrdinal = profile.LegacyPortOrdinal,
        Kind = profile.Kind,
        Key = profile.Key,
        Buttons = [.. profile.Buttons],
        TurboHz = profile.TurboHz
    };

    private static void PopulateShortcutBindings(
        Dictionary<string, ShortcutBindingEntry> shortcutBindings,
        ObservableCollection<ShortcutBindingEntry> target,
        IReadOnlyList<ShortcutDescriptor> descriptors)
    {
        target.Clear();
        foreach (var descriptor in descriptors)
        {
            var entry = new ShortcutBindingEntry(descriptor, descriptor.DefaultGesture);
            target.Add(entry);
            shortcutBindings[descriptor.Id] = entry;
        }
    }

    private static List<InputBindingEntry> BuildInputBindingEntries(
        IReadOnlyDictionary<string, Dictionary<string, Key>> portOverrides,
        CoreInputBindingSchema inputBindingSchema,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, Key>> defaultKeyMaps,
        IReadOnlyList<Key> configurableKeys,
        InputBindingLayoutProfile inputBindingLayout)
    {
        var inputBindings = new List<InputBindingEntry>();
        foreach (var port in inputBindingSchema.GetSupportedPorts())
        {
            var portMap = portOverrides.TryGetValue(port.PortId, out var map) ? map : null;
            foreach (var actionId in defaultKeyMaps[port.PortId].Keys)
            {
                var selectedKey = portMap != null && portMap.TryGetValue(actionId, out var key)
                    ? key
                    : defaultKeyMaps[port.PortId][actionId];
                var entry = new InputBindingEntry(
                    port.PortId,
                    port.DisplayName,
                    actionId,
                    GetActionDisplayName(actionId, inputBindingSchema),
                    selectedKey,
                    configurableKeys);
                entry.ApplyLayout(inputBindingLayout);
                inputBindings.Add(entry);
            }
        }

        return inputBindings;
    }

    private static List<ExtraInputBindingEntry> BuildExtraInputBindingEntries(
        IEnumerable<ExtraInputBindingProfile> profiles,
        IReadOnlyList<Key> configurableKeys,
        IReadOnlyList<ExtraInputButtonOption> buttonOptions,
        CoreInputBindingSchema inputBindingSchema)
    {
        List<ExtraInputBindingEntry> entries = [];
        foreach (var profile in profiles)
        {
            if (!TryResolveExtraBindingPort(profile, inputBindingSchema, out var portId))
                continue;

            entries.Add(ExtraInputBindingEntry.FromProfile(
                profile,
                portId,
                inputBindingSchema.GetPortDisplayName(portId),
                configurableKeys,
                buttonOptions));
        }

        return entries;
    }

    private static Dictionary<string, Key> BuildInputMap(IEnumerable<InputBindingEntry> entries, string portId) =>
        entries
            .Where(entry => entry.PortId.Equals(portId, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(entry => entry.ActionId, entry => entry.SelectedKey, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, Dictionary<string, string>> BuildPortInputOverrideProfiles(
        IReadOnlyDictionary<string, Dictionary<string, Key>> portInputMaps,
        CoreInputBindingSchema inputBindingSchema)
    {
        var profiles = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var portMap in portInputMaps)
        {
            if (!inputBindingSchema.TryNormalizePortId(portMap.Key, out var portId))
                continue;

            profiles[portId] = portMap.Value.ToDictionary(
                button => button.Key,
                button => button.Value.ToString(),
                StringComparer.OrdinalIgnoreCase);
        }

        return profiles;
    }

    private static Dictionary<string, Key> BuildNormalizedActionMap(
        IReadOnlyDictionary<string, string> source,
        string portId,
        CoreInputBindingSchema inputBindingSchema)
    {
        var map = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source)
        {
            if (inputBindingSchema.TryNormalizeActionId(portId, pair.Key, out var normalizedActionId) &&
                Enum.TryParse<Key>(pair.Value, out var key))
            {
                map[normalizedActionId] = key;
            }
        }

        return map;
    }

    private static bool TryResolveOverridePortId(
        string? overrideKey,
        CoreInputBindingSchema inputBindingSchema,
        out string portId)
    {
        portId = string.Empty;
        if (string.IsNullOrWhiteSpace(overrideKey))
            return false;

        if (inputBindingSchema.TryNormalizePortId(overrideKey, out portId))
            return true;

        return overrideKey.Trim() switch
        {
            "Player1" => TryResolveLegacyOrdinalPortId(0, inputBindingSchema, out portId),
            "Player2" => TryResolveLegacyOrdinalPortId(1, inputBindingSchema, out portId),
            _ => false
        };
    }

    private static bool TryResolveExtraBindingPort(
        ExtraInputBindingProfile profile,
        CoreInputBindingSchema inputBindingSchema,
        out string portId)
    {
        if (inputBindingSchema.TryNormalizePortId(profile.PortId, out portId))
            return true;

        if (profile.LegacyPortOrdinal >= 0)
        {
            portId = inputBindingSchema.GetSupportedPorts()
                .FirstOrDefault(port => port.PlayerIndex == profile.LegacyPortOrdinal)
                ?.PortId
                ?? string.Empty;
            return !string.IsNullOrWhiteSpace(portId);
        }

        portId = string.Empty;
        return false;
    }

    private static bool TryResolveLegacyOrdinalPortId(
        int ordinal,
        CoreInputBindingSchema inputBindingSchema,
        out string portId)
    {
        portId = inputBindingSchema.GetSupportedPorts()
            .ElementAtOrDefault(ordinal)
            ?.PortId
            ?? string.Empty;
        return !string.IsNullOrWhiteSpace(portId);
    }

    private static string GetActionDisplayName(string actionId, CoreInputBindingSchema inputBindingSchema) =>
        inputBindingSchema.TryGetDisplayName(actionId, out var displayName) ? displayName : actionId;
}

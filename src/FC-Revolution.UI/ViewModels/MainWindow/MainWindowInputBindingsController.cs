using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Core.Input;
using FCRevolution.Storage;
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
    Dictionary<string, Dictionary<string, string>> PlayerInputOverrides,
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
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings)
    {
        if (romInputOverrides.ContainsKey(romPath) || romExtraInputOverrides.ContainsKey(romPath))
            return;

        romInputOverrides[romPath] = BuildPlayerInputMaps(globalInputBindings);
        romExtraInputOverrides[romPath] = BuildExtraInputBindingProfiles(globalExtraInputBindings);
    }

    public void LoadRomProfileInputOverride(
        string romPath,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides)
    {
        var profile = RomConfigProfile.Load(romPath);
        var playerMaps = BuildPlayerInputMaps(profile);
        if (playerMaps.Count > 0)
            romInputOverrides[romPath] = playerMaps;

        if (profile.ExtraInputBindings.Count > 0)
            romExtraInputOverrides[romPath] = profile.ExtraInputBindings.Select(CloneExtraInputBindingProfile).ToList();
    }

    public void SaveRomProfileInputOverride(
        string romPath,
        Dictionary<int, Dictionary<NesButton, Key>>? inputOverride,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides)
    {
        var profile = RomConfigProfile.Load(romPath);
        profile.PlayerInputOverrides = inputOverride == null
            ? new Dictionary<string, Dictionary<string, string>>()
            : BuildPlayerInputOverrideProfiles(inputOverride);
        profile.InputOverrides = profile.PlayerInputOverrides.TryGetValue(GetPlayerOverrideKey(0), out var player1Map)
            ? new Dictionary<string, string>(player1Map)
            : new Dictionary<string, string>();
        profile.ExtraInputBindings = romExtraInputOverrides.TryGetValue(romPath, out var extraBindings)
            ? extraBindings.Select(CloneExtraInputBindingProfile).ToList()
            : [];
        RomConfigProfile.Save(romPath, profile);
    }

    public GlobalInputConfigSaveState BuildGlobalInputConfigSaveState(
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        IReadOnlyDictionary<string, ShortcutBindingEntry> shortcutBindings,
        InputBindingLayoutProfile inputBindingLayout)
    {
        var playerInputMaps = BuildPlayerInputMaps(globalInputBindings);

        return new GlobalInputConfigSaveState(
            BuildPlayerInputOverrideProfiles(playerInputMaps),
            BuildExtraInputBindingProfiles(globalExtraInputBindings),
            BuildShortcutProfiles(shortcutBindings),
            inputBindingLayout.Clone());
    }

    public GlobalInputBindingViewState BuildGlobalInputBindingViewState(
        SystemConfigProfile? profile,
        IReadOnlyDictionary<int, IReadOnlyDictionary<NesButton, Key>> defaultKeyMaps,
        IReadOnlyList<Key> configurableKeys,
        InputBindingLayoutProfile inputBindingLayout)
    {
        var playerOverrides = profile == null
            ? new Dictionary<int, Dictionary<NesButton, Key>>()
            : BuildPlayerInputMaps(profile);
        var inputBindings = BuildInputBindingEntries(
            playerOverrides,
            defaultKeyMaps,
            configurableKeys,
            inputBindingLayout);
        var extraBindings = BuildExtraInputBindingEntries(
            profile?.ExtraInputBindings ?? [],
            configurableKeys);
        return new GlobalInputBindingViewState(inputBindings, extraBindings);
    }

    public RomInputBindingViewState BuildRomInputBindingViewState(
        string? currentRomPath,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        IReadOnlyDictionary<int, IReadOnlyDictionary<NesButton, Key>> defaultKeyMaps,
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
                    var cloned = new InputBindingEntry(entry.Player, entry.ActionName, entry.Button, entry.SelectedKey, configurableKeys);
                    cloned.ApplyLayout(inputBindingLayout);
                    return cloned;
                })
                .ToList();
            var clonedGlobalExtra = globalExtraInputBindings.Select(entry => entry.Clone()).ToList();
            return new RomInputBindingViewState(false, clonedGlobalInputs, clonedGlobalExtra);
        }

        var romInputs = BuildInputBindingEntries(
            overrideMap ?? new Dictionary<int, Dictionary<NesButton, Key>>(),
            defaultKeyMaps,
            configurableKeys,
            inputBindingLayout);
        var romExtra = BuildExtraInputBindingEntries(
            extraOverrideProfiles ?? [],
            configurableKeys);
        return new RomInputBindingViewState(true, romInputs, romExtra);
    }

    public Dictionary<int, Dictionary<NesButton, Key>> GetEffectivePlayerInputMaps(
        string? romPath,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IReadOnlyDictionary<int, IReadOnlyDictionary<NesButton, Key>> defaultKeyMaps)
    {
        var source = romPath != null && romInputOverrides.TryGetValue(romPath, out var overrideMap)
            ? overrideMap
            : BuildPlayerInputMaps(globalInputBindings);

        var result = new Dictionary<int, Dictionary<NesButton, Key>>();
        foreach (var player in GetSupportedPlayers())
        {
            var playerDefaults = defaultKeyMaps[player];
            var playerMap = source.TryGetValue(player, out var map) ? map : BuildInputMap(globalInputBindings, player);
            result[player] = playerDefaults.ToDictionary(
                pair => pair.Key,
                pair => playerMap.TryGetValue(pair.Key, out var key) ? key : pair.Value);
        }

        return result;
    }

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

    public Dictionary<int, Dictionary<NesButton, Key>> BuildPlayerInputMaps(IEnumerable<InputBindingEntry> entries)
    {
        var maps = new Dictionary<int, Dictionary<NesButton, Key>>();
        foreach (var player in GetSupportedPlayers())
            maps[player] = BuildInputMap(entries, player);

        return maps;
    }

    public List<ExtraInputBindingProfile> BuildExtraInputBindingProfiles(IEnumerable<ExtraInputBindingEntry> entries) =>
        entries.Select(entry => entry.ToProfile()).ToList();

    public Dictionary<int, Dictionary<NesButton, Key>> BuildPlayerInputMaps(SystemConfigProfile profile)
    {
        var maps = new Dictionary<int, Dictionary<NesButton, Key>>();

        foreach (var player in GetSupportedPlayers())
        {
            if (!profile.PlayerInputOverrides.TryGetValue(GetPlayerOverrideKey(player), out var playerMap))
                continue;

            var map = new Dictionary<NesButton, Key>();
            foreach (var pair in playerMap)
            {
                if (Enum.TryParse<NesButton>(pair.Key, out var button) &&
                    Enum.TryParse<Key>(pair.Value, out var key))
                {
                    map[button] = key;
                }
            }

            if (map.Count > 0)
                maps[player] = map;
        }

        return maps;
    }

    public Dictionary<int, Dictionary<NesButton, Key>> BuildPlayerInputMaps(RomConfigProfile profile)
    {
        var maps = new Dictionary<int, Dictionary<NesButton, Key>>();

        foreach (var player in GetSupportedPlayers())
        {
            if (!profile.PlayerInputOverrides.TryGetValue(GetPlayerOverrideKey(player), out var playerMap))
                continue;

            var map = new Dictionary<NesButton, Key>();
            foreach (var pair in playerMap)
            {
                if (Enum.TryParse<NesButton>(pair.Key, out var button) &&
                    Enum.TryParse<Key>(pair.Value, out var key))
                {
                    map[button] = key;
                }
            }

            if (map.Count > 0)
                maps[player] = map;
        }

        return maps;
    }

    public ExtraInputBindingProfile CloneExtraInputBindingProfile(ExtraInputBindingProfile profile) => new()
    {
        Player = profile.Player,
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

    private static IReadOnlyList<int> GetSupportedPlayers() => [0, 1];

    private static List<InputBindingEntry> BuildInputBindingEntries(
        IReadOnlyDictionary<int, Dictionary<NesButton, Key>> playerOverrides,
        IReadOnlyDictionary<int, IReadOnlyDictionary<NesButton, Key>> defaultKeyMaps,
        IReadOnlyList<Key> configurableKeys,
        InputBindingLayoutProfile inputBindingLayout)
    {
        var inputBindings = new List<InputBindingEntry>();
        foreach (var player in GetSupportedPlayers())
        {
            var playerMap = playerOverrides.TryGetValue(player, out var map) ? map : null;
            foreach (var button in defaultKeyMaps[player].Keys)
            {
                var selectedKey = playerMap != null && playerMap.TryGetValue(button, out var key)
                    ? key
                    : defaultKeyMaps[player][button];
                var entry = new InputBindingEntry(player, GetButtonDisplayName(button), button, selectedKey, configurableKeys);
                entry.ApplyLayout(inputBindingLayout);
                inputBindings.Add(entry);
            }
        }

        return inputBindings;
    }

    private static List<ExtraInputBindingEntry> BuildExtraInputBindingEntries(
        IEnumerable<ExtraInputBindingProfile> profiles,
        IReadOnlyList<Key> configurableKeys) =>
        profiles.Select(profile => ExtraInputBindingEntry.FromProfile(profile, configurableKeys)).ToList();

    private static Dictionary<NesButton, Key> BuildInputMap(IEnumerable<InputBindingEntry> entries, int player) =>
        entries.Where(entry => entry.Player == player).ToDictionary(entry => entry.Button, entry => entry.SelectedKey);

    private static Dictionary<string, Dictionary<string, string>> BuildPlayerInputOverrideProfiles(
        IReadOnlyDictionary<int, Dictionary<NesButton, Key>> playerInputMaps) =>
        playerInputMaps.ToDictionary(
            pair => GetPlayerOverrideKey(pair.Key),
            pair => pair.Value.ToDictionary(button => button.Key.ToString(), button => button.Value.ToString()),
            StringComparer.Ordinal);

    private static string GetPlayerOverrideKey(int player) => player == 0 ? "Player1" : "Player2";

    private static string GetButtonDisplayName(NesButton button) => button switch
    {
        NesButton.Select => "Select",
        NesButton.Start => "Start",
        _ => button.ToString()
    };
}

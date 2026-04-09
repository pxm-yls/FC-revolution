using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Core.Input;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowInputOverrideController
{
    private readonly MainWindowInputBindingsController _inputBindingsController;
    private readonly IReadOnlyList<Key> _configurableKeys;

    public MainWindowInputOverrideController(
        MainWindowInputBindingsController inputBindingsController,
        IReadOnlyList<Key> configurableKeys)
    {
        _inputBindingsController = inputBindingsController;
        _configurableKeys = configurableKeys;
    }

    public void ApplyGlobalInputBindings(
        Action saveSystemConfig,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus)
    {
        saveSystemConfig();
        refreshRomInputBindings();
        refreshActiveInputState();
        setStatus("已应用全局按键配置");
    }

    public void EnableRomInputOverride(
        RomLibraryItem? currentRom,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        Action<string, Dictionary<int, Dictionary<NesButton, Key>>?> saveRomProfileInputOverride,
        Action refreshRomInputBindings,
        Action<string> setStatus)
    {
        if (currentRom == null)
            return;

        var inputOverride = _inputBindingsController.BuildPlayerInputMaps(globalInputBindings);
        romInputOverrides[currentRom.Path] = inputOverride;
        romExtraInputOverrides[currentRom.Path] = _inputBindingsController.BuildExtraInputBindingProfiles(globalExtraInputBindings);
        saveRomProfileInputOverride(currentRom.Path, inputOverride);
        refreshRomInputBindings();
        setStatus($"已为 {currentRom.DisplayName} 启用独立按键配置");
    }

    public void ApplyRomInputOverride(
        RomLibraryItem? currentRom,
        bool isRomInputOverrideEnabled,
        IEnumerable<InputBindingEntry> romInputBindings,
        IEnumerable<ExtraInputBindingEntry> romExtraInputBindings,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        Action<string, Dictionary<int, Dictionary<NesButton, Key>>?> saveRomProfileInputOverride,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus)
    {
        if (currentRom == null || !isRomInputOverrideEnabled)
            return;

        PersistCurrentRomInputBindings(
            currentRom,
            romInputBindings,
            romExtraInputBindings,
            romInputOverrides,
            romExtraInputOverrides,
            saveRomProfileInputOverride,
            refreshRomInputBindings,
            refreshActiveInputState,
            setStatus,
            $"已保存 {currentRom.DisplayName} 的独立按键配置");
    }

    public void ClearRomInputOverride(
        RomLibraryItem? currentRom,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        Action<string, Dictionary<int, Dictionary<NesButton, Key>>?> saveRomProfileInputOverride,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus)
    {
        if (currentRom == null)
            return;

        romInputOverrides.Remove(currentRom.Path);
        romExtraInputOverrides.Remove(currentRom.Path);
        saveRomProfileInputOverride(currentRom.Path, null);
        refreshRomInputBindings();
        refreshActiveInputState();
        setStatus($"已清除 {currentRom.DisplayName} 的独立按键配置");
    }

    public void AddGlobalTurboBinding(
        string? playerToken,
        IEnumerable<InputBindingEntry> globalInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindingsPlayer1,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindingsPlayer2,
        Action saveSystemConfig,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus)
    {
        var player = ParsePlayerToken(playerToken);
        globalExtraInputBindings.Add(ExtraInputBindingEntry.CreateDefaultTurbo(
            player,
            GetSuggestedExtraKey(globalInputBindings, globalExtraInputBindings),
            _configurableKeys));
        RefreshExtraBindingViews(globalExtraInputBindings, globalExtraInputBindingsPlayer1, globalExtraInputBindingsPlayer2);
        saveSystemConfig();
        refreshRomInputBindings();
        refreshActiveInputState();
        setStatus($"已新增 {GetPlayerLabel(player)} 连发键");
    }

    public void AddGlobalComboBinding(
        string? playerToken,
        IEnumerable<InputBindingEntry> globalInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindingsPlayer1,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindingsPlayer2,
        Action saveSystemConfig,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus)
    {
        var player = ParsePlayerToken(playerToken);
        globalExtraInputBindings.Add(ExtraInputBindingEntry.CreateDefaultCombo(
            player,
            GetSuggestedExtraKey(globalInputBindings, globalExtraInputBindings),
            _configurableKeys));
        RefreshExtraBindingViews(globalExtraInputBindings, globalExtraInputBindingsPlayer1, globalExtraInputBindingsPlayer2);
        saveSystemConfig();
        refreshRomInputBindings();
        refreshActiveInputState();
        setStatus($"已新增 {GetPlayerLabel(player)} 组合键");
    }

    public void RemoveGlobalExtraBinding(
        ExtraInputBindingEntry? entry,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindingsPlayer1,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindingsPlayer2,
        Action saveSystemConfig,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus)
    {
        if (entry == null)
            return;

        globalExtraInputBindings.Remove(entry);
        RefreshExtraBindingViews(globalExtraInputBindings, globalExtraInputBindingsPlayer1, globalExtraInputBindingsPlayer2);
        saveSystemConfig();
        refreshRomInputBindings();
        refreshActiveInputState();
        setStatus($"已删除 {entry.PlayerLabel} {entry.KindLabel}");
    }

    public void AddRomTurboBinding(
        string? playerToken,
        RomLibraryItem? currentRom,
        bool isRomInputOverrideEnabled,
        ObservableCollection<InputBindingEntry> romInputBindings,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindings,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindingsPlayer1,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindingsPlayer2,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        Action<string, Dictionary<int, Dictionary<NesButton, Key>>?> saveRomProfileInputOverride,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus)
    {
        if (currentRom == null)
            return;

        EnsureRomInputOverrideForEditing(
            currentRom.Path,
            romInputOverrides,
            romExtraInputOverrides,
            globalInputBindings,
            globalExtraInputBindings,
            saveRomProfileInputOverride);
        if (!isRomInputOverrideEnabled)
            refreshRomInputBindings();

        var player = ParsePlayerToken(playerToken);
        romExtraInputBindings.Add(ExtraInputBindingEntry.CreateDefaultTurbo(
            player,
            GetSuggestedExtraKey(romInputBindings, romExtraInputBindings),
            _configurableKeys));
        RefreshExtraBindingViews(romExtraInputBindings, romExtraInputBindingsPlayer1, romExtraInputBindingsPlayer2);
        PersistCurrentRomInputBindings(
            currentRom,
            romInputBindings,
            romExtraInputBindings,
            romInputOverrides,
            romExtraInputOverrides,
            saveRomProfileInputOverride,
            refreshRomInputBindings,
            refreshActiveInputState,
            setStatus,
            $"已新增 {GetPlayerLabel(player)} 连发键");
    }

    public void AddRomComboBinding(
        string? playerToken,
        RomLibraryItem? currentRom,
        bool isRomInputOverrideEnabled,
        ObservableCollection<InputBindingEntry> romInputBindings,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindings,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindingsPlayer1,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindingsPlayer2,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        Action<string, Dictionary<int, Dictionary<NesButton, Key>>?> saveRomProfileInputOverride,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus)
    {
        if (currentRom == null)
            return;

        EnsureRomInputOverrideForEditing(
            currentRom.Path,
            romInputOverrides,
            romExtraInputOverrides,
            globalInputBindings,
            globalExtraInputBindings,
            saveRomProfileInputOverride);
        if (!isRomInputOverrideEnabled)
            refreshRomInputBindings();

        var player = ParsePlayerToken(playerToken);
        romExtraInputBindings.Add(ExtraInputBindingEntry.CreateDefaultCombo(
            player,
            GetSuggestedExtraKey(romInputBindings, romExtraInputBindings),
            _configurableKeys));
        RefreshExtraBindingViews(romExtraInputBindings, romExtraInputBindingsPlayer1, romExtraInputBindingsPlayer2);
        PersistCurrentRomInputBindings(
            currentRom,
            romInputBindings,
            romExtraInputBindings,
            romInputOverrides,
            romExtraInputOverrides,
            saveRomProfileInputOverride,
            refreshRomInputBindings,
            refreshActiveInputState,
            setStatus,
            $"已新增 {GetPlayerLabel(player)} 组合键");
    }

    public void RemoveRomExtraBinding(
        ExtraInputBindingEntry? entry,
        RomLibraryItem? currentRom,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindings,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindingsPlayer1,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindingsPlayer2,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        IEnumerable<InputBindingEntry> romInputBindings,
        Action<string, Dictionary<int, Dictionary<NesButton, Key>>?> saveRomProfileInputOverride,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus)
    {
        if (currentRom == null || entry == null)
            return;

        EnsureRomInputOverrideForEditing(
            currentRom.Path,
            romInputOverrides,
            romExtraInputOverrides,
            globalInputBindings,
            globalExtraInputBindings,
            saveRomProfileInputOverride);
        romExtraInputBindings.Remove(entry);
        RefreshExtraBindingViews(romExtraInputBindings, romExtraInputBindingsPlayer1, romExtraInputBindingsPlayer2);
        PersistCurrentRomInputBindings(
            currentRom,
            romInputBindings,
            romExtraInputBindings,
            romInputOverrides,
            romExtraInputOverrides,
            saveRomProfileInputOverride,
            refreshRomInputBindings,
            refreshActiveInputState,
            setStatus,
            $"已删除 {entry.PlayerLabel} {entry.KindLabel}");
    }

    public void IncrGlobalTurboHz(ExtraInputBindingEntry? entry, Action saveSystemConfig, Action refreshActiveInputState)
    {
        if (entry == null)
            return;

        entry.SetTurboHz(entry.TurboHz + 1);
        saveSystemConfig();
        refreshActiveInputState();
    }

    public void DecrGlobalTurboHz(ExtraInputBindingEntry? entry, Action saveSystemConfig, Action refreshActiveInputState)
    {
        if (entry == null)
            return;

        entry.SetTurboHz(entry.TurboHz - 1);
        saveSystemConfig();
        refreshActiveInputState();
    }

    public void IncrRomTurboHz(
        ExtraInputBindingEntry? entry,
        RomLibraryItem? currentRom,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        IEnumerable<InputBindingEntry> romInputBindings,
        IEnumerable<ExtraInputBindingEntry> romExtraInputBindings,
        Action<string, Dictionary<int, Dictionary<NesButton, Key>>?> saveRomProfileInputOverride,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus)
    {
        if (entry == null || currentRom == null)
            return;

        EnsureRomInputOverrideForEditing(
            currentRom.Path,
            romInputOverrides,
            romExtraInputOverrides,
            globalInputBindings,
            globalExtraInputBindings,
            saveRomProfileInputOverride);
        entry.SetTurboHz(entry.TurboHz + 1);
        PersistCurrentRomInputBindings(
            currentRom,
            romInputBindings,
            romExtraInputBindings,
            romInputOverrides,
            romExtraInputOverrides,
            saveRomProfileInputOverride,
            refreshRomInputBindings,
            refreshActiveInputState,
            setStatus,
            successStatus: null);
        refreshActiveInputState();
    }

    public void DecrRomTurboHz(
        ExtraInputBindingEntry? entry,
        RomLibraryItem? currentRom,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        IEnumerable<InputBindingEntry> romInputBindings,
        IEnumerable<ExtraInputBindingEntry> romExtraInputBindings,
        Action<string, Dictionary<int, Dictionary<NesButton, Key>>?> saveRomProfileInputOverride,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus)
    {
        if (entry == null || currentRom == null)
            return;

        EnsureRomInputOverrideForEditing(
            currentRom.Path,
            romInputOverrides,
            romExtraInputOverrides,
            globalInputBindings,
            globalExtraInputBindings,
            saveRomProfileInputOverride);
        entry.SetTurboHz(entry.TurboHz - 1);
        PersistCurrentRomInputBindings(
            currentRom,
            romInputBindings,
            romExtraInputBindings,
            romInputOverrides,
            romExtraInputOverrides,
            saveRomProfileInputOverride,
            refreshRomInputBindings,
            refreshActiveInputState,
            setStatus,
            successStatus: null);
        refreshActiveInputState();
    }

    public void OpenRomInputOverrideEditor(
        RomLibraryItem? rom,
        Action<RomLibraryItem> setCurrentRom,
        Action<bool> setQuickRomInputEditorOpen,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        Action<string, Dictionary<int, Dictionary<NesButton, Key>>?> saveRomProfileInputOverride,
        Action refreshRomInputBindings,
        Action<string> setStatus)
    {
        if (rom == null)
            return;

        setCurrentRom(rom);
        EnsureRomInputOverrideForEditing(
            rom.Path,
            romInputOverrides,
            romExtraInputOverrides,
            globalInputBindings,
            globalExtraInputBindings,
            saveRomProfileInputOverride);
        refreshRomInputBindings();
        setQuickRomInputEditorOpen(true);
        setStatus($"正在快速编辑 {rom.DisplayName} 的独立按键配置");
    }

    public void CloseQuickRomInputEditor(Action<bool> setQuickRomInputEditorOpen) =>
        setQuickRomInputEditorOpen(false);

    public void SaveQuickRomInputEditor(
        RomLibraryItem? currentRom,
        IEnumerable<InputBindingEntry> romInputBindings,
        IEnumerable<ExtraInputBindingEntry> romExtraInputBindings,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        Action<string, Dictionary<int, Dictionary<NesButton, Key>>?> saveRomProfileInputOverride,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus,
        Action<bool> setQuickRomInputEditorOpen)
    {
        if (currentRom == null)
            return;

        PersistCurrentRomInputBindings(
            currentRom,
            romInputBindings,
            romExtraInputBindings,
            romInputOverrides,
            romExtraInputOverrides,
            saveRomProfileInputOverride,
            refreshRomInputBindings,
            refreshActiveInputState,
            setStatus,
            $"已保存 {currentRom.DisplayName} 的独立按键配置");
        setQuickRomInputEditorOpen(false);
    }

    public void RemoveRomInputOverrideFromMenu(
        RomLibraryItem? rom,
        RomLibraryItem? currentRom,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        Action<string, Dictionary<int, Dictionary<NesButton, Key>>?> saveRomProfileInputOverride,
        Action refreshRomInputBindings,
        Action<string> setStatus)
    {
        if (rom == null)
            return;

        romInputOverrides.Remove(rom.Path);
        romExtraInputOverrides.Remove(rom.Path);
        saveRomProfileInputOverride(rom.Path, null);
        if (currentRom?.Path == rom.Path)
            refreshRomInputBindings();
        setStatus($"已删除 {rom.DisplayName} 的独立按键配置");
    }

    public void EnsureRomInputOverrideForEditing(
        string romPath,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        Action<string, Dictionary<int, Dictionary<NesButton, Key>>?> saveRomProfileInputOverride)
    {
        _inputBindingsController.EnsureRomInputOverrideForEditing(
            romPath,
            romInputOverrides,
            romExtraInputOverrides,
            globalInputBindings,
            globalExtraInputBindings);
        saveRomProfileInputOverride(romPath, romInputOverrides[romPath]);
    }

    public void LoadRomProfileInputOverride(
        string romPath,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides)
    {
        _inputBindingsController.LoadRomProfileInputOverride(romPath, romInputOverrides, romExtraInputOverrides);
    }

    public void SaveRomProfileInputOverride(
        string romPath,
        Dictionary<int, Dictionary<NesButton, Key>>? inputOverride,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides)
    {
        _inputBindingsController.SaveRomProfileInputOverride(romPath, inputOverride, romExtraInputOverrides);
    }

    public void PersistCurrentRomInputBindings(
        RomLibraryItem? currentRom,
        IEnumerable<InputBindingEntry> romInputBindings,
        IEnumerable<ExtraInputBindingEntry> romExtraInputBindings,
        Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        Action<string, Dictionary<int, Dictionary<NesButton, Key>>?> saveRomProfileInputOverride,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus,
        string? successStatus)
    {
        if (currentRom == null)
            return;

        romInputOverrides[currentRom.Path] = _inputBindingsController.BuildPlayerInputMaps(romInputBindings);
        romExtraInputOverrides[currentRom.Path] = _inputBindingsController.BuildExtraInputBindingProfiles(romExtraInputBindings);
        saveRomProfileInputOverride(currentRom.Path, romInputOverrides[currentRom.Path]);
        refreshRomInputBindings();
        refreshActiveInputState();
        if (successStatus != null)
            setStatus(successStatus);
    }

    public void RefreshRomInputBindings(
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
        romInputBindings.Clear();
        romInputBindingsPlayer1.Clear();
        romInputBindingsPlayer2.Clear();
        romExtraInputBindings.Clear();
        romExtraInputBindingsPlayer1.Clear();
        romExtraInputBindingsPlayer2.Clear();

        if (currentRom == null)
        {
            setRomInputOverrideEnabled(false);
            notifyRomInputOverrideSummaryChanged();
            return;
        }

        var viewState = _inputBindingsController.BuildRomInputBindingViewState(
            currentRom.Path,
            romInputOverrides,
            romExtraInputOverrides,
            globalInputBindings,
            globalExtraInputBindings,
            defaultKeyMaps,
            configurableKeys,
            inputBindingLayout);
        setRomInputOverrideEnabled(viewState.IsOverrideEnabled);
        foreach (var entry in viewState.InputBindings)
            romInputBindings.Add(entry);
        foreach (var entry in viewState.ExtraBindings)
            romExtraInputBindings.Add(entry);

        RefreshPlayerBindingViews(romInputBindings, romInputBindingsPlayer1, romInputBindingsPlayer2);
        RefreshExtraBindingViews(romExtraInputBindings, romExtraInputBindingsPlayer1, romExtraInputBindingsPlayer2);
        notifyRomInputOverrideSummaryChanged();
    }

    public static void RefreshPlayerBindingViews(
        IEnumerable<InputBindingEntry> source,
        ObservableCollection<InputBindingEntry> player1Bindings,
        ObservableCollection<InputBindingEntry> player2Bindings)
    {
        player1Bindings.Clear();
        player2Bindings.Clear();

        foreach (var entry in source)
        {
            if (entry.Player == 0)
                player1Bindings.Add(entry);
            else
                player2Bindings.Add(entry);
        }
    }

    public static void RefreshExtraBindingViews(
        IEnumerable<ExtraInputBindingEntry> source,
        ObservableCollection<ExtraInputBindingEntry> player1Bindings,
        ObservableCollection<ExtraInputBindingEntry> player2Bindings)
    {
        player1Bindings.Clear();
        player2Bindings.Clear();

        foreach (var entry in source)
        {
            if (entry.Player == 0)
                player1Bindings.Add(entry);
            else
                player2Bindings.Add(entry);
        }
    }

    private static string GetPlayerLabel(int player) => player == 0 ? "1P" : "2P";

    private static int ParsePlayerToken(string? playerToken) => playerToken == "1" ? 1 : 0;

    private Key GetSuggestedExtraKey(IEnumerable<InputBindingEntry> baseBindings, IEnumerable<ExtraInputBindingEntry> extraBindings)
    {
        var usedKeys = baseBindings
            .Select(entry => entry.SelectedKey)
            .Concat(extraBindings.Select(entry => entry.SelectedKey))
            .ToHashSet();

        return _configurableKeys.FirstOrDefault(key => !usedKeys.Contains(key), _configurableKeys[0]);
    }
}

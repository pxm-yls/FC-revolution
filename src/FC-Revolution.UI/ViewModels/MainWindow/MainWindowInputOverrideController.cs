using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowInputOverrideController
{
    private readonly MainWindowInputBindingsController _inputBindingsController;
    private readonly CoreInputBindingSchema _inputBindingSchema;
    private readonly IReadOnlyList<Key> _configurableKeys;

    public MainWindowInputOverrideController(
        MainWindowInputBindingsController inputBindingsController,
        CoreInputBindingSchema inputBindingSchema,
        IReadOnlyList<Key> configurableKeys)
    {
        _inputBindingsController = inputBindingsController;
        _inputBindingSchema = inputBindingSchema;
        _configurableKeys = configurableKeys;
    }

    public MainWindowInputOverrideController(
        MainWindowInputBindingsController inputBindingsController,
        IReadOnlyList<Key> configurableKeys)
        : this(inputBindingsController, CoreInputBindingSchema.CreateFallback(), configurableKeys)
    {
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
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        Action<string, Dictionary<string, Dictionary<string, Key>>?> saveRomProfileInputOverride,
        Action refreshRomInputBindings,
        Action<string> setStatus)
    {
        if (currentRom == null)
            return;

        var inputOverride = _inputBindingsController.BuildInputMapsByPort(globalInputBindings);
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
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        Action<string, Dictionary<string, Dictionary<string, Key>>?> saveRomProfileInputOverride,
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
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        Action<string, Dictionary<string, Dictionary<string, Key>>?> saveRomProfileInputOverride,
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
        string? portId,
        IEnumerable<InputBindingEntry> globalInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindings,
        ObservableCollection<InputBindingPortGroup> globalInputPortGroups,
        Action saveSystemConfig,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus)
    {
        var normalizedPortId = ResolveRequestedPortId(portId);
        globalExtraInputBindings.Add(ExtraInputBindingEntry.CreateDefaultTurbo(
            normalizedPortId,
            _inputBindingSchema.GetPortDisplayName(normalizedPortId),
            GetSuggestedExtraKey(globalInputBindings, globalExtraInputBindings),
            _configurableKeys,
            _inputBindingSchema.ExtraInputButtonOptions));
        RefreshPortBindingViews(globalInputBindings, globalExtraInputBindings, globalInputPortGroups, _inputBindingSchema);
        saveSystemConfig();
        refreshRomInputBindings();
        refreshActiveInputState();
        setStatus($"已新增 {GetPortLabel(normalizedPortId)} 连发键");
    }

    public void AddGlobalComboBinding(
        string? portId,
        IEnumerable<InputBindingEntry> globalInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindings,
        ObservableCollection<InputBindingPortGroup> globalInputPortGroups,
        Action saveSystemConfig,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus)
    {
        var normalizedPortId = ResolveRequestedPortId(portId);
        globalExtraInputBindings.Add(ExtraInputBindingEntry.CreateDefaultCombo(
            normalizedPortId,
            _inputBindingSchema.GetPortDisplayName(normalizedPortId),
            GetSuggestedExtraKey(globalInputBindings, globalExtraInputBindings),
            _configurableKeys,
            _inputBindingSchema.ExtraInputButtonOptions));
        RefreshPortBindingViews(globalInputBindings, globalExtraInputBindings, globalInputPortGroups, _inputBindingSchema);
        saveSystemConfig();
        refreshRomInputBindings();
        refreshActiveInputState();
        setStatus($"已新增 {GetPortLabel(normalizedPortId)} 组合键");
    }

    public void RemoveGlobalExtraBinding(
        ExtraInputBindingEntry? entry,
        IEnumerable<InputBindingEntry> globalInputBindings,
        ObservableCollection<ExtraInputBindingEntry> globalExtraInputBindings,
        ObservableCollection<InputBindingPortGroup> globalInputPortGroups,
        Action saveSystemConfig,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus)
    {
        if (entry == null)
            return;

        globalExtraInputBindings.Remove(entry);
        RefreshPortBindingViews(globalInputBindings, globalExtraInputBindings, globalInputPortGroups, _inputBindingSchema);
        saveSystemConfig();
        refreshRomInputBindings();
        refreshActiveInputState();
        setStatus($"已删除 {entry.PortLabel} {entry.KindLabel}");
    }

    public void AddRomTurboBinding(
        string? portId,
        RomLibraryItem? currentRom,
        bool isRomInputOverrideEnabled,
        ObservableCollection<InputBindingEntry> romInputBindings,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindings,
        ObservableCollection<InputBindingPortGroup> romInputPortGroups,
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        Action<string, Dictionary<string, Dictionary<string, Key>>?> saveRomProfileInputOverride,
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

        var normalizedPortId = ResolveRequestedPortId(portId);
        romExtraInputBindings.Add(ExtraInputBindingEntry.CreateDefaultTurbo(
            normalizedPortId,
            _inputBindingSchema.GetPortDisplayName(normalizedPortId),
            GetSuggestedExtraKey(romInputBindings, romExtraInputBindings),
            _configurableKeys,
            _inputBindingSchema.ExtraInputButtonOptions));
        RefreshPortBindingViews(romInputBindings, romExtraInputBindings, romInputPortGroups, _inputBindingSchema);
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
            $"已新增 {GetPortLabel(normalizedPortId)} 连发键");
    }

    public void AddRomComboBinding(
        string? portId,
        RomLibraryItem? currentRom,
        bool isRomInputOverrideEnabled,
        ObservableCollection<InputBindingEntry> romInputBindings,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindings,
        ObservableCollection<InputBindingPortGroup> romInputPortGroups,
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        Action<string, Dictionary<string, Dictionary<string, Key>>?> saveRomProfileInputOverride,
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

        var normalizedPortId = ResolveRequestedPortId(portId);
        romExtraInputBindings.Add(ExtraInputBindingEntry.CreateDefaultCombo(
            normalizedPortId,
            _inputBindingSchema.GetPortDisplayName(normalizedPortId),
            GetSuggestedExtraKey(romInputBindings, romExtraInputBindings),
            _configurableKeys,
            _inputBindingSchema.ExtraInputButtonOptions));
        RefreshPortBindingViews(romInputBindings, romExtraInputBindings, romInputPortGroups, _inputBindingSchema);
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
            $"已新增 {GetPortLabel(normalizedPortId)} 组合键");
    }

    public void RemoveRomExtraBinding(
        ExtraInputBindingEntry? entry,
        RomLibraryItem? currentRom,
        ObservableCollection<InputBindingEntry> romInputBindings,
        ObservableCollection<ExtraInputBindingEntry> romExtraInputBindings,
        ObservableCollection<InputBindingPortGroup> romInputPortGroups,
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        Action<string, Dictionary<string, Dictionary<string, Key>>?> saveRomProfileInputOverride,
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
        RefreshPortBindingViews(romInputBindings, romExtraInputBindings, romInputPortGroups, _inputBindingSchema);
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
            $"已删除 {entry.PortLabel} {entry.KindLabel}");
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
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        IEnumerable<InputBindingEntry> romInputBindings,
        IEnumerable<ExtraInputBindingEntry> romExtraInputBindings,
        Action<string, Dictionary<string, Dictionary<string, Key>>?> saveRomProfileInputOverride,
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
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        IEnumerable<InputBindingEntry> romInputBindings,
        IEnumerable<ExtraInputBindingEntry> romExtraInputBindings,
        Action<string, Dictionary<string, Dictionary<string, Key>>?> saveRomProfileInputOverride,
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
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        Action<string, Dictionary<string, Dictionary<string, Key>>?> saveRomProfileInputOverride,
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
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        Action<string, Dictionary<string, Dictionary<string, Key>>?> saveRomProfileInputOverride,
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
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        Action<string, Dictionary<string, Dictionary<string, Key>>?> saveRomProfileInputOverride,
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
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        IEnumerable<InputBindingEntry> globalInputBindings,
        IEnumerable<ExtraInputBindingEntry> globalExtraInputBindings,
        Action<string, Dictionary<string, Dictionary<string, Key>>?> saveRomProfileInputOverride)
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
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides)
    {
        _inputBindingsController.LoadRomProfileInputOverride(romPath, romInputOverrides, romExtraInputOverrides, _inputBindingSchema);
    }

    public void SaveRomProfileInputOverride(
        string romPath,
        Dictionary<string, Dictionary<string, Key>>? inputOverride,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides)
    {
        _inputBindingsController.SaveRomProfileInputOverride(romPath, inputOverride, romExtraInputOverrides, _inputBindingSchema);
    }

    public void PersistCurrentRomInputBindings(
        RomLibraryItem? currentRom,
        IEnumerable<InputBindingEntry> romInputBindings,
        IEnumerable<ExtraInputBindingEntry> romExtraInputBindings,
        Dictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        Dictionary<string, List<ExtraInputBindingProfile>> romExtraInputOverrides,
        Action<string, Dictionary<string, Dictionary<string, Key>>?> saveRomProfileInputOverride,
        Action refreshRomInputBindings,
        Action refreshActiveInputState,
        Action<string> setStatus,
        string? successStatus)
    {
        if (currentRom == null)
            return;

        romInputOverrides[currentRom.Path] = _inputBindingsController.BuildInputMapsByPort(romInputBindings);
        romExtraInputOverrides[currentRom.Path] = _inputBindingsController.BuildExtraInputBindingProfiles(romExtraInputBindings);
        saveRomProfileInputOverride(currentRom.Path, romInputOverrides[currentRom.Path]);
        refreshRomInputBindings();
        refreshActiveInputState();
        if (successStatus != null)
            setStatus(successStatus);
    }

    public void RefreshRomInputBindings(
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
        romInputBindings.Clear();
        romExtraInputBindings.Clear();
        romInputPortGroups.Clear();

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
            inputBindingSchema,
            defaultKeyMaps,
            configurableKeys,
            inputBindingLayout);
        setRomInputOverrideEnabled(viewState.IsOverrideEnabled);
        foreach (var entry in viewState.InputBindings)
            romInputBindings.Add(entry);
        foreach (var entry in viewState.ExtraBindings)
            romExtraInputBindings.Add(entry);

        RefreshPortBindingViews(romInputBindings, romExtraInputBindings, romInputPortGroups, inputBindingSchema);
        notifyRomInputOverrideSummaryChanged();
    }

    public static void RefreshPortBindingViews(
        IEnumerable<InputBindingEntry> inputBindings,
        IEnumerable<ExtraInputBindingEntry> extraBindings,
        ObservableCollection<InputBindingPortGroup> portGroups,
        CoreInputBindingSchema inputBindingSchema)
    {
        portGroups.Clear();

        foreach (var port in inputBindingSchema.GetSupportedPorts())
        {
            var portInputBindings = inputBindings
                .Where(entry => entry.PortId.Equals(port.PortId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var portExtraBindings = extraBindings
                .Where(entry => entry.PortId.Equals(port.PortId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            portGroups.Add(new InputBindingPortGroup(
                port.PortId,
                port.DisplayName,
                portInputBindings,
                portExtraBindings));
        }
    }

    private string ResolveRequestedPortId(string? requestedPortId)
    {
        if (_inputBindingSchema.TryNormalizePortId(requestedPortId, out var normalizedPortId))
            return normalizedPortId;

        return _inputBindingSchema.GetSupportedPorts().FirstOrDefault()?.PortId ?? _inputBindingSchema.GetPortId(0);
    }

    private string GetPortLabel(string portId) => _inputBindingSchema.GetPortDisplayName(portId);

    private Key GetSuggestedExtraKey(IEnumerable<InputBindingEntry> baseBindings, IEnumerable<ExtraInputBindingEntry> extraBindings)
    {
        var usedKeys = baseBindings
            .Select(entry => entry.SelectedKey)
            .Concat(extraBindings.Select(entry => entry.SelectedKey))
            .ToHashSet();

        return _configurableKeys.FirstOrDefault(key => !usedKeys.Contains(key), _configurableKeys[0]);
    }
}

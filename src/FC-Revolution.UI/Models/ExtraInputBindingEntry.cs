using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FC_Revolution.UI.Adapters.Nes;

namespace FC_Revolution.UI.Models;

public sealed class ExtraInputButtonOption
{
    public ExtraInputButtonOption(string actionId, string label)
    {
        ActionId = actionId;
        Label = label;
    }

    public string ActionId { get; }

    public string Label { get; }

    public override string ToString() => Label;
}

/// <summary>
/// 组合键中单个按钮的 Chip 视图项，自带移除命令。
/// </summary>
public sealed class ComboChipItem
{
    public ComboChipItem(string label, IRelayCommand removeCommand, bool canRemove)
    {
        Label = label;
        RemoveCommand = removeCommand;
        CanRemove = canRemove;
    }

    public string Label { get; }
    public IRelayCommand RemoveCommand { get; }
    public bool CanRemove { get; }
}

public sealed partial class ExtraInputButtonPickerItem : ObservableObject
{
    public ExtraInputButtonPickerItem(ExtraInputButtonOption option, IRelayCommand selectCommand)
    {
        Option = option;
        Label = option.Label;
        SelectCommand = selectCommand;
    }

    public ExtraInputButtonOption Option { get; }

    public string Label { get; }

    public IRelayCommand SelectCommand { get; }

    [ObservableProperty]
    private bool _isSelected;
}

public sealed partial class ExtraInputBindingEntry : ObservableObject, IKeyCaptureBinding
{
    private static readonly IReadOnlyList<ExtraInputButtonOption> SharedButtonOptions =
        NesInputAdapter.GetControllerActions()
            .Select(action => new ExtraInputButtonOption(action.ActionId, action.DisplayName))
            .ToArray();

    // 连发键使用的单个 FC 按钮
    [ObservableProperty]
    private ExtraInputButtonOption _primaryButtonOption;

    // 组合键使用的多按钮列表（内部存储）
    private readonly List<ExtraInputButtonOption> _comboButtonList = [];

    // 对外暴露的组合键 Chip 集合（可绑定）
    private readonly ObservableCollection<ComboChipItem> _comboChips = [];
    private readonly ObservableCollection<ExtraInputButtonPickerItem> _buttonPickerItems = [];

    public ExtraInputBindingEntry(
        int player,
        ExtraInputBindingKind kind,
        Key selectedKey,
        IReadOnlyList<Key> availableKeys,
        ExtraInputButtonOption? primaryButton = null,
        IReadOnlyList<ExtraInputButtonOption>? comboButtons = null,
        int turboHz = 10)
    {
        Player = player;
        Kind = kind;
        AvailableKeys = availableKeys;
        ButtonOptions = SharedButtonOptions;
        _selectedKey = selectedKey;
        _primaryButtonOption = primaryButton ?? SharedButtonOptions[0];
        _turboHz = Math.Clamp(turboHz, 1, 30);

        if (kind == ExtraInputBindingKind.Combo)
        {
            var buttons = comboButtons?.Distinct().Take(SharedButtonOptions.Count).ToList()
                ?? [SharedButtonOptions[0], SharedButtonOptions[1]];
            _comboButtonList.AddRange(buttons);
            RebuildComboChips();
        }

        RebuildButtonPickerItems();
    }

    public int Player { get; }

    public string PlayerLabel => Player == 0 ? "1P" : "2P";

    public ExtraInputBindingKind Kind { get; }

    public string KindLabel => Kind == ExtraInputBindingKind.Turbo ? "连发" : "组合";

    public bool IsTurbo => Kind == ExtraInputBindingKind.Turbo;

    public bool IsCombo => Kind == ExtraInputBindingKind.Combo;

    public IReadOnlyList<Key> AvailableKeys { get; }

    public IReadOnlyList<ExtraInputButtonOption> ButtonOptions { get; }

    /// <summary>组合键按钮 Chip 集合，用于绑定 UI。</summary>
    public ObservableCollection<ComboChipItem> ComboChips => _comboChips;

    /// <summary>弹窗按钮集合，用于绑定按钮平铺选择器。</summary>
    public ObservableCollection<ExtraInputButtonPickerItem> ButtonPickerItems => _buttonPickerItems;

    /// <summary>组合键始终允许打开按钮选择器，哪怕已经选满 8 个按钮。</summary>
    public bool CanAddMoreComboButtons => IsCombo;

    public string ButtonPickerHintText => Kind == ExtraInputBindingKind.Turbo
        ? "点击一个按钮后会立即替换当前连发键。"
        : "点击按钮可添加或取消选中；全部取消后该扩展键将暂时不生效。";

    [ObservableProperty]
    private Key _selectedKey;

    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    private bool _isButtonPickerOpen;

    [ObservableProperty]
    private int _turboHz = 10;

    public string SelectedKeyDisplay => IsCapturing ? "录入" : FormatKey(SelectedKey);

    public string TurboHzLabel => $"{TurboHz}/秒";

    public void SetTurboHz(int value)
    {
        TurboHz = Math.Clamp(value, 1, 30);
        OnPropertyChanged(nameof(TurboHzLabel));
    }

    public string SummaryText => Kind switch
    {
        ExtraInputBindingKind.Turbo => $"连发 {PrimaryButtonOption.Label}",
        _ => _comboButtonList.Count == 0
            ? "未选择"
            : string.Join("+", _comboButtonList.Select(o => o.Label))
    };

    public bool TrySetSelectedKey(Key key)
    {
        if (!AvailableKeys.Contains(key))
            return false;

        SelectedKey = key;
        IsCapturing = false;
        OnPropertyChanged(nameof(SelectedKeyDisplay));
        return true;
    }

    public IReadOnlyList<string> GetActionIds()
    {
        return Kind == ExtraInputBindingKind.Turbo
            ? [PrimaryButtonOption.ActionId]
            : _comboButtonList.Select(o => o.ActionId).ToList();
    }

    /// <summary>打开组合键的按钮选择器。</summary>
    [RelayCommand(CanExecute = nameof(CanAddMoreComboButtons))]
    private void AddComboButton()
    {
        IsButtonPickerOpen = true;
    }

    private void RemoveComboButtonByOption(ExtraInputButtonOption option)
    {
        _comboButtonList.RemoveAll(item => string.Equals(item.ActionId, option.ActionId, StringComparison.OrdinalIgnoreCase));
        RebuildComboChips();
        SyncButtonPickerSelection();
        OnPropertyChanged(nameof(SummaryText));
    }

    private void RebuildComboChips()
    {
        _comboChips.Clear();
        var canRemove = _comboButtonList.Count > 0;
        foreach (var opt in _comboButtonList)
        {
            var captured = opt;
            _comboChips.Add(new ComboChipItem(
                captured.Label,
                new RelayCommand(() => RemoveComboButtonByOption(captured), () => canRemove),
                canRemove));
        }
    }

    private void RebuildButtonPickerItems()
    {
        _buttonPickerItems.Clear();
        foreach (var option in SharedButtonOptions)
        {
            var captured = option;
            _buttonPickerItems.Add(new ExtraInputButtonPickerItem(
                captured,
                new RelayCommand(() => SelectButtonPickerOption(captured)))
            {
                IsSelected = IsOptionSelected(captured)
            });
        }
    }

    private void SelectButtonPickerOption(ExtraInputButtonOption option)
    {
        if (Kind == ExtraInputBindingKind.Turbo)
        {
            PrimaryButtonOption = option;
            IsButtonPickerOpen = false;
            SyncButtonPickerSelection();
            return;
        }

        var existingIndex = _comboButtonList.FindIndex(item =>
            string.Equals(item.ActionId, option.ActionId, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
            _comboButtonList.RemoveAt(existingIndex);
        else
            _comboButtonList.Add(option);

        RebuildComboChips();
        SyncButtonPickerSelection();
        OnPropertyChanged(nameof(SummaryText));
    }

    private void SyncButtonPickerSelection()
    {
        foreach (var item in _buttonPickerItems)
            item.IsSelected = IsOptionSelected(item.Option);
    }

    private bool IsOptionSelected(ExtraInputButtonOption option)
    {
        return Kind == ExtraInputBindingKind.Turbo
            ? string.Equals(option.ActionId, PrimaryButtonOption.ActionId, StringComparison.OrdinalIgnoreCase)
            : _comboButtonList.Any(item => string.Equals(item.ActionId, option.ActionId, StringComparison.OrdinalIgnoreCase));
    }

    public ExtraInputBindingProfile ToProfile() => new()
    {
        Player = Player,
        Kind = Kind.ToString(),
        Key = SelectedKey.ToString(),
        Buttons = GetActionIds().ToList(),
        TurboHz = TurboHz
    };

    public static ExtraInputBindingEntry CreateDefaultTurbo(int player, Key key, IReadOnlyList<Key> availableKeys) =>
        new(player, ExtraInputBindingKind.Turbo, key, availableKeys, SharedButtonOptions[0], turboHz: 10);

    public static ExtraInputBindingEntry CreateDefaultCombo(int player, Key key, IReadOnlyList<Key> availableKeys) =>
        new(player, ExtraInputBindingKind.Combo, key, availableKeys, null, [SharedButtonOptions[0], SharedButtonOptions[1]]);

    public static ExtraInputBindingEntry FromProfile(ExtraInputBindingProfile profile, IReadOnlyList<Key> availableKeys)
    {
        var kind = Enum.TryParse<ExtraInputBindingKind>(profile.Kind, out var parsedKind)
            ? parsedKind
            : ExtraInputBindingKind.Turbo;
        var key = Enum.TryParse<Key>(profile.Key, out var parsedKey) && availableKeys.Contains(parsedKey)
            ? parsedKey
            : availableKeys.FirstOrDefault();

        var buttons = (profile.Buttons ?? [])
            .Select(ParseButton)
            .Where(b => b != null)
            .Select(b => b!)
            .Distinct()
            .ToList();

        if (kind == ExtraInputBindingKind.Turbo)
        {
            var primary = buttons.Count > 0 ? buttons[0] : SharedButtonOptions[0];
            return new ExtraInputBindingEntry(player: profile.Player, kind: kind, selectedKey: key,
                availableKeys: availableKeys, primaryButton: primary, turboHz: Math.Clamp(profile.TurboHz <= 0 ? 10 : profile.TurboHz, 1, 30));
        }
        else
        {
            return new ExtraInputBindingEntry(player: profile.Player, kind: kind, selectedKey: key,
                availableKeys: availableKeys, comboButtons: buttons);
        }
    }

    public ExtraInputBindingEntry Clone()
    {
        if (Kind == ExtraInputBindingKind.Turbo)
            return new ExtraInputBindingEntry(Player, Kind, SelectedKey, AvailableKeys, PrimaryButtonOption, turboHz: TurboHz);
        else
            return new ExtraInputBindingEntry(Player, Kind, SelectedKey, AvailableKeys, comboButtons: [.. _comboButtonList]);
    }

    partial void OnSelectedKeyChanged(Key value) => OnPropertyChanged(nameof(SelectedKeyDisplay));

    partial void OnIsCapturingChanged(bool value) => OnPropertyChanged(nameof(SelectedKeyDisplay));

    partial void OnTurboHzChanged(int value) => OnPropertyChanged(nameof(TurboHzLabel));

    partial void OnPrimaryButtonOptionChanged(ExtraInputButtonOption value)
    {
        OnPropertyChanged(nameof(SummaryText));
        SyncButtonPickerSelection();
    }

    private static ExtraInputButtonOption? ParseButton(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return SharedButtonOptions.FirstOrDefault(option =>
            string.Equals(option.ActionId, value.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatKey(Key key) => key switch
    {
        Key.LeftCtrl => "LCtrl",
        Key.RightCtrl => "RCtrl",
        Key.LeftShift => "LShift",
        Key.RightShift => "RShift",
        Key.LeftAlt => "LAlt",
        Key.RightAlt => "RAlt",
        Key.Return => "Enter",
        Key.Back => "Back",
        Key.PageUp => "PgUp",
        Key.PageDown => "PgDn",
        Key.CapsLock => "Caps",
        Key.NumPad0 => "N0",
        Key.NumPad1 => "N1",
        Key.NumPad2 => "N2",
        Key.NumPad3 => "N3",
        Key.NumPad4 => "N4",
        Key.NumPad5 => "N5",
        Key.NumPad6 => "N6",
        Key.NumPad7 => "N7",
        Key.NumPad8 => "N8",
        Key.NumPad9 => "N9",
        _ => key.ToString()
    };
}

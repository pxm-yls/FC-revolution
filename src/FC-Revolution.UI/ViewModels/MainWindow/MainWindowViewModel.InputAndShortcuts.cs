using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using FCRevolution.Core.Input;
using FCRevolution.Storage;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

public partial class MainWindowViewModel
{
    private readonly MainWindowActiveInputRuntimeController _activeInputRuntime = new();
    private readonly MainWindowActiveInputWorkflowController _activeInputWorkflowController = new();
    private readonly MainWindowInputBindingWorkflowController _inputBindingWorkflowController = new();
    private readonly MainWindowInputKeyboardWorkflowController _inputKeyboardWorkflowController = new();

    private void InitializeShortcutBindings()
    {
        _inputBindingsController.InitializeShortcutBindings(
            _shortcutBindings,
            _mainWindowShortcutBindings,
            _sharedGameShortcutBindings,
            _gameWindowShortcutBindings);
        NotifyShortcutBindingsChanged();
    }

    private void LoadShortcutBindings(SystemConfigProfile profile)
    {
        _inputBindingsController.LoadShortcutBindings(profile, _shortcutBindings);
        ApplyShortcutBindingsToActiveSessions();
        NotifyShortcutBindingsChanged();
    }

    private Dictionary<string, ShortcutBindingProfile> BuildShortcutProfiles() =>
        _inputBindingsController.BuildShortcutProfiles(_shortcutBindings);

    private void ApplyShortcutBindingsToActiveSessions()
    {
        var gameWindowShortcuts = BuildGameWindowShortcutMap();

        foreach (var session in _gameSessionService.Sessions)
            session.ViewModel.ApplyShortcutBindings(gameWindowShortcuts);
    }

    private Dictionary<string, ShortcutGesture> BuildGameWindowShortcutMap() =>
        _inputBindingsController.BuildGameWindowShortcutMap(_shortcutBindings);

    private void NotifyShortcutBindingsChanged()
    {
        OnPropertyChanged(nameof(TaskMessageShortcutLabel));
        OnPropertyChanged(nameof(TaskMessagePanelTitle));
        OnPropertyChanged(nameof(MemoryDiagnosticsShortcutLabel));
    }

    private string GetShortcutDisplay(string id) => GetShortcutGesture(id).ToDisplayString();

    internal ShortcutGesture GetShortcutGesture(string id)
        => _inputBindingsController.GetShortcutGesture(id, _shortcutBindings);

    internal bool IsShortcutMatch(string id, Key key, KeyModifiers modifiers) =>
        GetShortcutGesture(id).Matches(key, modifiers);

    internal bool TryCommitShortcutBinding(ShortcutBindingEntry? entry, Key key, KeyModifiers modifiers)
    {
        var result = _inputBindingsController.TryCommitShortcutBinding(entry, _shortcutBindings.Values, key, modifiers);
        if (!result.Handled)
            return false;

        if (result.RequiresSave)
            SaveSystemConfig();
        if (result.RequiresSessionApply)
            ApplyShortcutBindingsToActiveSessions();
        if (result.RequiresNotify)
            NotifyShortcutBindingsChanged();
        if (result.StatusText != null)
            StatusText = result.StatusText;

        return true;
    }

    [RelayCommand]
    private void ApplyGlobalInputBindings() =>
        _inputOverrideController.ApplyGlobalInputBindings(
            SaveSystemConfig,
            RefreshRomInputBindings,
            RefreshActiveInputState,
            status => StatusText = status);

    [RelayCommand]
    private void EnableRomInputOverride() =>
        _inputOverrideController.EnableRomInputOverride(
            CurrentRom,
            _romInputOverrides,
            _romExtraInputOverrides,
            _globalInputBindings,
            _globalExtraInputBindings,
            (romPath, inputOverride) => _inputOverrideController.SaveRomProfileInputOverride(romPath, inputOverride, _romExtraInputOverrides),
            RefreshRomInputBindings,
            status => StatusText = status);

    [RelayCommand]
    private void ApplyRomInputOverride() =>
        _inputOverrideController.ApplyRomInputOverride(
            CurrentRom,
            IsRomInputOverrideEnabled,
            _romInputBindings,
            _romExtraInputBindings,
            _romInputOverrides,
            _romExtraInputOverrides,
            (romPath, inputOverride) => _inputOverrideController.SaveRomProfileInputOverride(romPath, inputOverride, _romExtraInputOverrides),
            RefreshRomInputBindings,
            RefreshActiveInputState,
            status => StatusText = status);

    [RelayCommand]
    private void ClearRomInputOverride() =>
        _inputOverrideController.ClearRomInputOverride(
            CurrentRom,
            _romInputOverrides,
            _romExtraInputOverrides,
            (romPath, inputOverride) => _inputOverrideController.SaveRomProfileInputOverride(romPath, inputOverride, _romExtraInputOverrides),
            RefreshRomInputBindings,
            RefreshActiveInputState,
            status => StatusText = status);

    [RelayCommand]
    private void AddGlobalTurboBinding(string? playerToken) =>
        _inputOverrideController.AddGlobalTurboBinding(
            playerToken,
            _globalInputBindings,
            _globalExtraInputBindings,
            _globalExtraInputBindingsPlayer1,
            _globalExtraInputBindingsPlayer2,
            SaveSystemConfig,
            RefreshRomInputBindings,
            RefreshActiveInputState,
            status => StatusText = status);

    [RelayCommand]
    private void AddGlobalComboBinding(string? playerToken) =>
        _inputOverrideController.AddGlobalComboBinding(
            playerToken,
            _globalInputBindings,
            _globalExtraInputBindings,
            _globalExtraInputBindingsPlayer1,
            _globalExtraInputBindingsPlayer2,
            SaveSystemConfig,
            RefreshRomInputBindings,
            RefreshActiveInputState,
            status => StatusText = status);

    [RelayCommand]
    private void RemoveGlobalExtraBinding(ExtraInputBindingEntry? entry) =>
        _inputOverrideController.RemoveGlobalExtraBinding(
            entry,
            _globalExtraInputBindings,
            _globalExtraInputBindingsPlayer1,
            _globalExtraInputBindingsPlayer2,
            SaveSystemConfig,
            RefreshRomInputBindings,
            RefreshActiveInputState,
            status => StatusText = status);

    [RelayCommand]
    private void AddRomTurboBinding(string? playerToken) =>
        _inputOverrideController.AddRomTurboBinding(
            playerToken,
            CurrentRom,
            IsRomInputOverrideEnabled,
            _romInputBindings,
            _romExtraInputBindings,
            _romExtraInputBindingsPlayer1,
            _romExtraInputBindingsPlayer2,
            _romInputOverrides,
            _romExtraInputOverrides,
            _globalInputBindings,
            _globalExtraInputBindings,
            (romPath, inputOverride) => _inputOverrideController.SaveRomProfileInputOverride(romPath, inputOverride, _romExtraInputOverrides),
            RefreshRomInputBindings,
            RefreshActiveInputState,
            status => StatusText = status);

    [RelayCommand]
    private void AddRomComboBinding(string? playerToken) =>
        _inputOverrideController.AddRomComboBinding(
            playerToken,
            CurrentRom,
            IsRomInputOverrideEnabled,
            _romInputBindings,
            _romExtraInputBindings,
            _romExtraInputBindingsPlayer1,
            _romExtraInputBindingsPlayer2,
            _romInputOverrides,
            _romExtraInputOverrides,
            _globalInputBindings,
            _globalExtraInputBindings,
            (romPath, inputOverride) => _inputOverrideController.SaveRomProfileInputOverride(romPath, inputOverride, _romExtraInputOverrides),
            RefreshRomInputBindings,
            RefreshActiveInputState,
            status => StatusText = status);

    [RelayCommand]
    private void RemoveRomExtraBinding(ExtraInputBindingEntry? entry) =>
        _inputOverrideController.RemoveRomExtraBinding(
            entry,
            CurrentRom,
            _romExtraInputBindings,
            _romExtraInputBindingsPlayer1,
            _romExtraInputBindingsPlayer2,
            _romInputOverrides,
            _romExtraInputOverrides,
            _globalInputBindings,
            _globalExtraInputBindings,
            _romInputBindings,
            (romPath, inputOverride) => _inputOverrideController.SaveRomProfileInputOverride(romPath, inputOverride, _romExtraInputOverrides),
            RefreshRomInputBindings,
            RefreshActiveInputState,
            status => StatusText = status);

    [RelayCommand]
    private void IncrGlobalTurboHz(ExtraInputBindingEntry? entry) =>
        _inputOverrideController.IncrGlobalTurboHz(entry, SaveSystemConfig, RefreshActiveInputState);

    [RelayCommand]
    private void DecrGlobalTurboHz(ExtraInputBindingEntry? entry) =>
        _inputOverrideController.DecrGlobalTurboHz(entry, SaveSystemConfig, RefreshActiveInputState);

    [RelayCommand]
    private void IncrRomTurboHz(ExtraInputBindingEntry? entry) =>
        _inputOverrideController.IncrRomTurboHz(
            entry,
            CurrentRom,
            _romInputOverrides,
            _romExtraInputOverrides,
            _globalInputBindings,
            _globalExtraInputBindings,
            _romInputBindings,
            _romExtraInputBindings,
            (romPath, inputOverride) => _inputOverrideController.SaveRomProfileInputOverride(romPath, inputOverride, _romExtraInputOverrides),
            RefreshRomInputBindings,
            RefreshActiveInputState,
            status => StatusText = status);

    [RelayCommand]
    private void DecrRomTurboHz(ExtraInputBindingEntry? entry) =>
        _inputOverrideController.DecrRomTurboHz(
            entry,
            CurrentRom,
            _romInputOverrides,
            _romExtraInputOverrides,
            _globalInputBindings,
            _globalExtraInputBindings,
            _romInputBindings,
            _romExtraInputBindings,
            (romPath, inputOverride) => _inputOverrideController.SaveRomProfileInputOverride(romPath, inputOverride, _romExtraInputOverrides),
            RefreshRomInputBindings,
            RefreshActiveInputState,
            status => StatusText = status);

    [RelayCommand]
    private void OpenRomInputOverrideEditor(RomLibraryItem? rom) =>
        _inputOverrideController.OpenRomInputOverrideEditor(
            rom,
            selectedRom => CurrentRom = selectedRom,
            isOpen => IsQuickRomInputEditorOpen = isOpen,
            _romInputOverrides,
            _romExtraInputOverrides,
            _globalInputBindings,
            _globalExtraInputBindings,
            (romPath, inputOverride) => _inputOverrideController.SaveRomProfileInputOverride(romPath, inputOverride, _romExtraInputOverrides),
            RefreshRomInputBindings,
            status => StatusText = status);

    [RelayCommand]
    private void CloseQuickRomInputEditor() =>
        _inputOverrideController.CloseQuickRomInputEditor(isOpen => IsQuickRomInputEditorOpen = isOpen);

    [RelayCommand]
    private void SaveQuickRomInputEditor() =>
        _inputOverrideController.SaveQuickRomInputEditor(
            CurrentRom,
            _romInputBindings,
            _romExtraInputBindings,
            _romInputOverrides,
            _romExtraInputOverrides,
            (romPath, inputOverride) => _inputOverrideController.SaveRomProfileInputOverride(romPath, inputOverride, _romExtraInputOverrides),
            RefreshRomInputBindings,
            RefreshActiveInputState,
            status => StatusText = status,
            isOpen => IsQuickRomInputEditorOpen = isOpen);

    [RelayCommand]
    private void RemoveRomInputOverrideFromMenu(RomLibraryItem? rom) =>
        _inputOverrideController.RemoveRomInputOverrideFromMenu(
            rom,
            CurrentRom,
            _romInputOverrides,
            _romExtraInputOverrides,
            (romPath, inputOverride) => _inputOverrideController.SaveRomProfileInputOverride(romPath, inputOverride, _romExtraInputOverrides),
            RefreshRomInputBindings,
            status => StatusText = status);

    private void InitializeInputBindings()
    {
        _inputBindingWorkflowController.BuildAndApplyGlobalInputBindingViewState(
            _inputBindingsController,
            profile: null,
            defaultKeyMaps: DefaultKeyMaps,
            configurableKeys: ConfigurableKeys,
            inputBindingLayout: _inputBindingLayout,
            globalInputBindings: _globalInputBindings,
            globalExtraInputBindings: _globalExtraInputBindings,
            globalInputBindingsPlayer1: _globalInputBindingsPlayer1,
            globalInputBindingsPlayer2: _globalInputBindingsPlayer2,
            globalExtraInputBindingsPlayer1: _globalExtraInputBindingsPlayer1,
            globalExtraInputBindingsPlayer2: _globalExtraInputBindingsPlayer2);
        RefreshRomInputBindings();
    }

    private void RefreshRomInputBindings() =>
        _inputBindingWorkflowController.RefreshRomInputBindings(
            _inputOverrideController,
            CurrentRom,
            _romInputOverrides,
            _romExtraInputOverrides,
            _globalInputBindings,
            _globalExtraInputBindings,
            _romInputBindings,
            _romInputBindingsPlayer1,
            _romInputBindingsPlayer2,
            _romExtraInputBindings,
            _romExtraInputBindingsPlayer1,
            _romExtraInputBindingsPlayer2,
            DefaultKeyMaps,
            ConfigurableKeys,
            _inputBindingLayout,
            enabled => IsRomInputOverrideEnabled = enabled,
            () => OnPropertyChanged(nameof(RomInputOverrideSummary)));

    public void MoveInputBindingLayoutSlot(NesButton button, double deltaX, double deltaY)
        => _inputLayoutController.MoveInputBindingLayoutSlot(
            _inputBindingLayout,
            button,
            deltaX,
            deltaY,
            _globalInputBindings,
            _romInputBindings,
            propertyName => OnPropertyChanged(propertyName));

    public void MoveInputLayoutDecoration(string decorationId, double deltaX, double deltaY)
        => _inputLayoutController.MoveInputLayoutDecoration(
            _inputBindingLayout,
            decorationId,
            deltaX,
            deltaY,
            _globalInputBindings,
            _romInputBindings,
            propertyName => OnPropertyChanged(propertyName));

    public void SaveInputBindingLayout() => _inputLayoutController.SaveInputBindingLayout(SaveSystemConfig);

    [RelayCommand]
    private void ResetInputBindingLayout() =>
        _inputBindingLayout = _inputLayoutController.ResetInputBindingLayout(
            _globalInputBindings,
            _romInputBindings,
            propertyName => OnPropertyChanged(propertyName),
            SaveSystemConfig);

    private void LoadGlobalInputConfig(SystemConfigProfile profile)
    {
        _inputBindingWorkflowController.BuildAndApplyGlobalInputBindingViewState(
            _inputBindingsController,
            profile,
            DefaultKeyMaps,
            ConfigurableKeys,
            _inputBindingLayout,
            _globalInputBindings,
            _globalExtraInputBindings,
            _globalInputBindingsPlayer1,
            _globalInputBindingsPlayer2,
            _globalExtraInputBindingsPlayer1,
            _globalExtraInputBindingsPlayer2);
    }

    private Dictionary<int, Dictionary<NesButton, Key>> GetEffectivePlayerInputMaps(string? romPath = null)
        => _inputBindingsController.GetEffectivePlayerInputMaps(
            romPath,
            _romInputOverrides,
            _globalInputBindings,
            DefaultKeyMaps);

    private List<ExtraInputBindingProfile> GetEffectiveExtraInputBindingProfiles(string? romPath = null)
        => _inputBindingsController.GetEffectiveExtraInputBindingProfiles(
            romPath,
            _romExtraInputOverrides,
            _globalExtraInputBindings);

    private MainWindowEffectiveInputBindingState GetEffectiveInputBindingState(string? romPath = null)
        => _inputBindingWorkflowController.BuildEffectiveInputBindingState(
            _inputBindingsController,
            _inputStateController,
            romPath,
            _romInputOverrides,
            _romExtraInputOverrides,
            _globalInputBindings,
            _globalExtraInputBindings,
            DefaultKeyMaps);

    private string? GetActiveInputRomPath()
        => _activeInputWorkflowController.GetActiveInputRomPath(IsRomLoaded, _romPath, CurrentRom);

    private void RefreshActiveInputState()
    {
        var activeRomPath = GetActiveInputRomPath();
        var effectiveInputBindingState = GetEffectiveInputBindingState(activeRomPath);
        var refreshResult = _activeInputWorkflowController.RefreshActiveInputState(
            _activeInputRuntime,
            _inputStateController,
            IsRomLoaded,
            activeRomPath,
            effectiveInputBindingState.EffectiveKeyMap,
            effectiveInputBindingState.EffectiveExtraBindings,
            _player1InputMask,
            _player2InputMask,
            GetControllerButtons(),
            GetInputPortId,
            GetInputActionId,
            _romLock,
            _inputStateWriter,
            UpdateInputMask);
        ApplyLegacyActiveInputRuntimeMirror(refreshResult.LegacyMirrorBeforeApply);
        ApplyLegacyActiveInputRuntimeMirror(refreshResult.LegacyMirrorAfterApply);
    }

    private void UpdateTurboPulse()
    {
        var activeRomPath = GetActiveInputRomPath();
        var effectiveInputBindingState = GetEffectiveInputBindingState(activeRomPath);
        var decision = _activeInputWorkflowController.UpdateTurboPulse(
            _activeInputRuntime,
            _inputStateController,
            effectiveInputBindingState.EffectiveExtraBindings);
        ApplyLegacyActiveInputRuntimeMirror(decision.LegacyMirror);
        if (decision.ShouldRefreshActiveInputState)
            RefreshActiveInputState();
    }

    private static string GetButtonDisplayName(NesButton button) => button switch
    {
        NesButton.Select => "Select",
        NesButton.Start => "Start",
        _ => button.ToString()
    };

    private static IReadOnlyList<NesButton> GetControllerButtons() =>
    [
        NesButton.A,
        NesButton.B,
        NesButton.Select,
        NesButton.Start,
        NesButton.Up,
        NesButton.Down,
        NesButton.Left,
        NesButton.Right
    ];

    private static string GetInputPortId(int player) => player == 0 ? "p1" : "p2";

    private static string GetInputActionId(NesButton button) => button switch
    {
        NesButton.A => "a",
        NesButton.B => "b",
        NesButton.Select => "select",
        NesButton.Start => "start",
        NesButton.Up => "up",
        NesButton.Down => "down",
        NesButton.Left => "left",
        NesButton.Right => "right",
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unsupported NES button mapping.")
    };

    public void OnKeyDown(Key key) => OnKeyDown(key, KeyModifiers.None);

    public void OnKeyDown(Key key, KeyModifiers modifiers)
    {
        var activeRomPath = GetActiveInputRomPath();
        var effectiveInputBindingState = GetEffectiveInputBindingState(activeRomPath);
        var keyDownDecision = _inputKeyboardWorkflowController.HandleKeyDown(
            _activeInputRuntime,
            _inputActionController,
            key,
            modifiers,
            IsRomLoaded,
            activeRomPath,
            effectiveInputBindingState.EffectiveHandledKeys,
            IsShortcutMatch);
        if (keyDownDecision.ShouldSyncLegacyMirror)
        {
            ApplyLegacyActiveInputRuntimeMirror(_activeInputWorkflowController.BuildLegacyMirror(_activeInputRuntime));
        }

        if (keyDownDecision.ShouldRefreshActiveInputState)
            RefreshActiveInputState();

        _inputKeyboardWorkflowController.DispatchAction(
            keyDownDecision.Action,
            SelectPreviousAsync,
            SelectNextAsync,
            PlaySelectedRom,
            QuickSave,
            QuickLoad,
            OpenBranchGallery,
            TogglePause,
            () => RewindRecentFrames(ShortRewindFrames));
    }

    public void OnKeyUp(Key key) => OnKeyUp(key, KeyModifiers.None);

    public void OnKeyUp(Key key, KeyModifiers modifiers)
    {
        var keyUpDecision = _inputKeyboardWorkflowController.HandleKeyUp(_activeInputRuntime, key);
        if (keyUpDecision.ShouldSyncLegacyMirror)
        {
            ApplyLegacyActiveInputRuntimeMirror(_activeInputWorkflowController.BuildLegacyMirror(_activeInputRuntime));
        }

        if (keyUpDecision.ShouldRefreshActiveInputState)
            RefreshActiveInputState();
    }

    public bool ShouldHandleKey(Key key) => ShouldHandleKey(key, KeyModifiers.None);

    public bool ShouldHandleKey(Key key, KeyModifiers modifiers)
    {
        var activeRomPath = GetActiveInputRomPath();
        var effectiveInputBindingState = GetEffectiveInputBindingState(activeRomPath);
        return _inputKeyboardWorkflowController.ShouldHandleKey(
            _inputDispatchController,
            key,
            modifiers,
            IsSettingsOpen,
            IsQuickRomInputEditorOpen,
            effectiveInputBindingState.EffectiveHandledKeys,
            ShortcutCatalog.SharedGameShortcutIds,
            IsShortcutRoutingMatch);
    }

    private bool IsShortcutRoutingMatch(string shortcutId, Key key, KeyModifiers modifiers)
    {
        var gesture = GetShortcutGesture(shortcutId);
        var normalizedModifiers = ShortcutGesture.NormalizeModifiers(modifiers);
        return gesture.Key == key &&
               (gesture.Modifiers == KeyModifiers.None || gesture.Matches(key, normalizedModifiers));
    }

    private void ApplyLegacyActiveInputRuntimeMirror(MainWindowActiveInputLegacyMirror mirror)
    {
        _pressedKeys.Clear();
        foreach (var key in mirror.PressedKeys)
            _pressedKeys.Add(key);
        _turboPulseActive = mirror.TurboPulseActive;
    }
}

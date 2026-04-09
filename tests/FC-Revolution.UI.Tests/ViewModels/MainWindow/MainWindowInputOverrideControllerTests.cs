using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Input;
using FCRevolution.Core.Input;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowInputOverrideControllerTests
{
    private static readonly IReadOnlyList<Key> ConfigurableKeys =
    [
        Key.Z, Key.X, Key.A, Key.S, Key.Q, Key.W, Key.I, Key.K, Key.Enter, Key.Space
    ];

    [Fact]
    public void EnableAndClearRomOverride_RunWorkflowAndPersist()
    {
        var inputBindingsController = new MainWindowInputBindingsController();
        var controller = new MainWindowInputOverrideController(inputBindingsController, ConfigurableKeys);
        var rom = CreateRom("Contra", "/tmp/contra.nes");
        var globalInput = new List<InputBindingEntry>
        {
            CreateInputBinding(0, NesButton.A, Key.Z),
            CreateInputBinding(1, NesButton.B, Key.I)
        };
        var globalExtra = new List<ExtraInputBindingEntry>
        {
            ExtraInputBindingEntry.CreateDefaultTurbo(0, Key.Q, ConfigurableKeys)
        };
        var romInputOverrides = new Dictionary<string, Dictionary<int, Dictionary<string, Key>>>(StringComparer.OrdinalIgnoreCase);
        var romExtraOverrides = new Dictionary<string, List<ExtraInputBindingProfile>>(StringComparer.OrdinalIgnoreCase);
        var saveCalls = 0;
        var refreshCalls = 0;
        string? status = null;

        controller.EnableRomInputOverride(
            rom,
            romInputOverrides,
            romExtraOverrides,
            globalInput,
            globalExtra,
            (_, _) => saveCalls++,
            () => refreshCalls++,
            text => status = text);

        Assert.True(romInputOverrides.ContainsKey(rom.Path));
        Assert.True(romExtraOverrides.ContainsKey(rom.Path));
        Assert.Equal(1, saveCalls);
        Assert.Equal(1, refreshCalls);
        Assert.Equal($"已为 {rom.DisplayName} 启用独立按键配置", status);

        var activeRefreshCalls = 0;
        controller.ClearRomInputOverride(
            rom,
            romInputOverrides,
            romExtraOverrides,
            (_, _) => saveCalls++,
            () => refreshCalls++,
            () => activeRefreshCalls++,
            text => status = text);

        Assert.False(romInputOverrides.ContainsKey(rom.Path));
        Assert.False(romExtraOverrides.ContainsKey(rom.Path));
        Assert.Equal(2, saveCalls);
        Assert.Equal(2, refreshCalls);
        Assert.Equal(1, activeRefreshCalls);
        Assert.Equal($"已清除 {rom.DisplayName} 的独立按键配置", status);
    }

    [Fact]
    public void GlobalExtraBindingAddAndRemove_RunExpectedSideEffects()
    {
        var inputBindingsController = new MainWindowInputBindingsController();
        var controller = new MainWindowInputOverrideController(inputBindingsController, ConfigurableKeys);
        var globalInput = new List<InputBindingEntry> { CreateInputBinding(0, NesButton.A, Key.Z) };
        var globalExtra = new ObservableCollection<ExtraInputBindingEntry>();
        var globalExtraP1 = new ObservableCollection<ExtraInputBindingEntry>();
        var globalExtraP2 = new ObservableCollection<ExtraInputBindingEntry>();
        var saveCalls = 0;
        var refreshRomCalls = 0;
        var refreshActiveCalls = 0;
        string? status = null;

        controller.AddGlobalTurboBinding(
            "1",
            globalInput,
            globalExtra,
            globalExtraP1,
            globalExtraP2,
            () => saveCalls++,
            () => refreshRomCalls++,
            () => refreshActiveCalls++,
            text => status = text);

        Assert.Single(globalExtra);
        Assert.Single(globalExtraP2);
        Assert.Equal(1, saveCalls);
        Assert.Equal(1, refreshRomCalls);
        Assert.Equal(1, refreshActiveCalls);
        Assert.Equal("已新增 2P 连发键", status);

        controller.RemoveGlobalExtraBinding(
            globalExtra[0],
            globalExtra,
            globalExtraP1,
            globalExtraP2,
            () => saveCalls++,
            () => refreshRomCalls++,
            () => refreshActiveCalls++,
            text => status = text);

        Assert.Empty(globalExtra);
        Assert.Empty(globalExtraP1);
        Assert.Empty(globalExtraP2);
        Assert.Equal(2, saveCalls);
        Assert.Equal(2, refreshRomCalls);
        Assert.Equal(2, refreshActiveCalls);
        Assert.Equal("已删除 2P 连发", status);
    }

    [Fact]
    public void AddRomComboBinding_WhenOverrideDisabled_EnsuresOverrideAndPersists()
    {
        var inputBindingsController = new MainWindowInputBindingsController();
        var controller = new MainWindowInputOverrideController(inputBindingsController, ConfigurableKeys);
        var rom = CreateRom("MegaMan", "/tmp/megaman.nes");
        var romInputBindings = new ObservableCollection<InputBindingEntry> { CreateInputBinding(0, NesButton.A, Key.Z) };
        var romExtraBindings = new ObservableCollection<ExtraInputBindingEntry>();
        var romExtraP1 = new ObservableCollection<ExtraInputBindingEntry>();
        var romExtraP2 = new ObservableCollection<ExtraInputBindingEntry>();
        var globalInput = new List<InputBindingEntry> { CreateInputBinding(0, NesButton.B, Key.X) };
        var globalExtra = new List<ExtraInputBindingEntry>();
        var romInputOverrides = new Dictionary<string, Dictionary<int, Dictionary<string, Key>>>(StringComparer.OrdinalIgnoreCase);
        var romExtraOverrides = new Dictionary<string, List<ExtraInputBindingProfile>>(StringComparer.OrdinalIgnoreCase);
        var saveCalls = 0;
        var refreshCalls = 0;
        var refreshActiveCalls = 0;
        string? status = null;

        controller.AddRomComboBinding(
            playerToken: null,
            currentRom: rom,
            isRomInputOverrideEnabled: false,
            romInputBindings,
            romExtraBindings,
            romExtraP1,
            romExtraP2,
            romInputOverrides,
            romExtraOverrides,
            globalInput,
            globalExtra,
            (_, _) => saveCalls++,
            () => refreshCalls++,
            () => refreshActiveCalls++,
            text => status = text);

        Assert.True(romInputOverrides.ContainsKey(rom.Path));
        Assert.True(romExtraOverrides.ContainsKey(rom.Path));
        Assert.Single(romExtraBindings);
        Assert.Single(romExtraP1);
        Assert.Equal("已新增 1P 组合键", status);
        Assert.True(saveCalls >= 2);
        Assert.True(refreshCalls >= 2);
        Assert.True(refreshActiveCalls >= 1);
    }

    [Fact]
    public void QuickEditorOpenSaveAndRemoveFlow_RunsExpectedWorkflow()
    {
        var inputBindingsController = new MainWindowInputBindingsController();
        var controller = new MainWindowInputOverrideController(inputBindingsController, ConfigurableKeys);
        var rom = CreateRom("NinjaGaiden", "/tmp/ng.nes");
        var globalInput = new List<InputBindingEntry> { CreateInputBinding(0, NesButton.A, Key.Z) };
        var globalExtra = new List<ExtraInputBindingEntry>();
        var romInputOverrides = new Dictionary<string, Dictionary<int, Dictionary<string, Key>>>(StringComparer.OrdinalIgnoreCase);
        var romExtraOverrides = new Dictionary<string, List<ExtraInputBindingProfile>>(StringComparer.OrdinalIgnoreCase);
        var romInput = new List<InputBindingEntry> { CreateInputBinding(0, NesButton.B, Key.X) };
        var romExtra = new List<ExtraInputBindingEntry> { ExtraInputBindingEntry.CreateDefaultTurbo(0, Key.Q, ConfigurableKeys) };
        var saveCalls = 0;
        var refreshCalls = 0;
        var refreshActiveCalls = 0;
        var quickEditorOpen = false;
        RomLibraryItem? currentRom = null;
        string? status = null;

        controller.OpenRomInputOverrideEditor(
            rom,
            setCurrentRom: selected => currentRom = selected,
            setQuickRomInputEditorOpen: isOpen => quickEditorOpen = isOpen,
            romInputOverrides,
            romExtraOverrides,
            globalInput,
            globalExtra,
            (_, _) => saveCalls++,
            () => refreshCalls++,
            text => status = text);

        Assert.Equal(rom.Path, currentRom?.Path);
        Assert.True(quickEditorOpen);
        Assert.Equal($"正在快速编辑 {rom.DisplayName} 的独立按键配置", status);

        controller.SaveQuickRomInputEditor(
            rom,
            romInput,
            romExtra,
            romInputOverrides,
            romExtraOverrides,
            (_, _) => saveCalls++,
            () => refreshCalls++,
            () => refreshActiveCalls++,
            text => status = text,
            isOpen => quickEditorOpen = isOpen);

        Assert.False(quickEditorOpen);
        Assert.Equal($"已保存 {rom.DisplayName} 的独立按键配置", status);

        controller.RemoveRomInputOverrideFromMenu(
            rom,
            currentRom: rom,
            romInputOverrides,
            romExtraOverrides,
            (_, _) => saveCalls++,
            () => refreshCalls++,
            text => status = text);

        Assert.False(romInputOverrides.ContainsKey(rom.Path));
        Assert.False(romExtraOverrides.ContainsKey(rom.Path));
        Assert.Equal($"已删除 {rom.DisplayName} 的独立按键配置", status);
    }

    [Fact]
    public void TurboHzAdjustments_RunPersistenceAndRefreshPath()
    {
        var inputBindingsController = new MainWindowInputBindingsController();
        var controller = new MainWindowInputOverrideController(inputBindingsController, ConfigurableKeys);
        var globalEntry = ExtraInputBindingEntry.CreateDefaultTurbo(0, Key.Q, ConfigurableKeys);
        var romEntry = ExtraInputBindingEntry.CreateDefaultTurbo(0, Key.W, ConfigurableKeys);
        var rom = CreateRom("Castlevania", "/tmp/cv.nes");
        var romInputOverrides = new Dictionary<string, Dictionary<int, Dictionary<string, Key>>>(StringComparer.OrdinalIgnoreCase);
        var romExtraOverrides = new Dictionary<string, List<ExtraInputBindingProfile>>(StringComparer.OrdinalIgnoreCase);
        var globalInput = new List<InputBindingEntry> { CreateInputBinding(0, NesButton.A, Key.Z) };
        var globalExtra = new List<ExtraInputBindingEntry>();
        var romInput = new List<InputBindingEntry> { CreateInputBinding(0, NesButton.B, Key.X) };
        var romExtra = new List<ExtraInputBindingEntry> { romEntry };
        var saveCalls = 0;
        var refreshCalls = 0;
        var refreshActiveCalls = 0;
        string? status = null;

        controller.IncrGlobalTurboHz(globalEntry, () => saveCalls++, () => refreshActiveCalls++);
        Assert.Equal(11, globalEntry.TurboHz);
        Assert.Equal(1, saveCalls);
        Assert.Equal(1, refreshActiveCalls);

        controller.DecrRomTurboHz(
            romEntry,
            rom,
            romInputOverrides,
            romExtraOverrides,
            globalInput,
            globalExtra,
            romInput,
            romExtra,
            (_, _) => saveCalls++,
            () => refreshCalls++,
            () => refreshActiveCalls++,
            text => status = text);

        Assert.Equal(9, romEntry.TurboHz);
        Assert.True(romInputOverrides.ContainsKey(rom.Path));
        Assert.True(refreshCalls >= 1);
        Assert.True(refreshActiveCalls >= 3);
        Assert.Null(status);
    }

    private static InputBindingEntry CreateInputBinding(int player, NesButton button, Key key) =>
        new(player, NesInputTestAdapter.ActionId(button), button.ToString(), key, ConfigurableKeys);

    private static RomLibraryItem CreateRom(string name, string path) =>
        new(
            name: $"{name}.nes",
            path: path,
            previewFilePath: string.Empty,
            hasPreview: false,
            fileSizeBytes: 1024,
            importedAtUtc: DateTime.UtcNow);
}

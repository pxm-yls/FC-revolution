using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowInputOverrideControllerTests
{
    private static readonly CoreInputBindingSchema InputSchema = CoreInputBindingSchema.CreateFallback();
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
            CreateInputBinding("p1", "a", "A", Key.Z),
            CreateInputBinding("p2", "b", "B", Key.I)
        };
        var globalExtra = new List<ExtraInputBindingEntry>
        {
            ExtraInputBindingEntry.CreateDefaultTurbo("p1", "1P", Key.Q, ConfigurableKeys)
        };
        var romInputOverrides = new Dictionary<string, Dictionary<string, Dictionary<string, Key>>>(StringComparer.OrdinalIgnoreCase);
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
        var globalInput = new List<InputBindingEntry> { CreateInputBinding("p1", "a", "A", Key.Z) };
        var globalExtra = new ObservableCollection<ExtraInputBindingEntry>();
        var globalPortGroups = new ObservableCollection<InputBindingPortGroup>();
        var saveCalls = 0;
        var refreshRomCalls = 0;
        var refreshActiveCalls = 0;
        string? status = null;

        controller.AddGlobalTurboBinding(
            "p2",
            globalInput,
            globalExtra,
            globalPortGroups,
            () => saveCalls++,
            () => refreshRomCalls++,
            () => refreshActiveCalls++,
            text => status = text);

        Assert.Single(globalExtra);
        Assert.Equal("p2", globalExtra[0].PortId);
        Assert.Single(globalPortGroups.Single(group => group.PortId == "p2").ExtraBindings);
        Assert.Empty(globalPortGroups.Single(group => group.PortId == "p1").ExtraBindings);
        Assert.Equal(1, saveCalls);
        Assert.Equal(1, refreshRomCalls);
        Assert.Equal(1, refreshActiveCalls);
        Assert.Equal($"已新增 {InputSchema.GetPortDisplayName("p2")} 连发键", status);

        controller.RemoveGlobalExtraBinding(
            globalExtra[0],
            globalInput,
            globalExtra,
            globalPortGroups,
            () => saveCalls++,
            () => refreshRomCalls++,
            () => refreshActiveCalls++,
            text => status = text);

        Assert.Empty(globalExtra);
        Assert.All(globalPortGroups, group => Assert.Empty(group.ExtraBindings));
        Assert.Equal(2, saveCalls);
        Assert.Equal(2, refreshRomCalls);
        Assert.Equal(2, refreshActiveCalls);
        Assert.Equal($"已删除 {InputSchema.GetPortDisplayName("p2")} 连发", status);
    }

    [Fact]
    public void AddRomComboBinding_WhenOverrideDisabled_EnsuresOverrideAndPersists()
    {
        var inputBindingsController = new MainWindowInputBindingsController();
        var controller = new MainWindowInputOverrideController(inputBindingsController, ConfigurableKeys);
        var rom = CreateRom("MegaMan", "/tmp/megaman.nes");
        var romInputBindings = new ObservableCollection<InputBindingEntry> { CreateInputBinding("p1", "a", "A", Key.Z) };
        var romExtraBindings = new ObservableCollection<ExtraInputBindingEntry>();
        var romInputPortGroups = new ObservableCollection<InputBindingPortGroup>();
        var globalInput = new List<InputBindingEntry> { CreateInputBinding("p1", "b", "B", Key.X) };
        var globalExtra = new List<ExtraInputBindingEntry>();
        var romInputOverrides = new Dictionary<string, Dictionary<string, Dictionary<string, Key>>>(StringComparer.OrdinalIgnoreCase);
        var romExtraOverrides = new Dictionary<string, List<ExtraInputBindingProfile>>(StringComparer.OrdinalIgnoreCase);
        var saveCalls = 0;
        var refreshCalls = 0;
        var refreshActiveCalls = 0;
        string? status = null;

        controller.AddRomComboBinding(
            portId: null,
            currentRom: rom,
            isRomInputOverrideEnabled: false,
            romInputBindings,
            romExtraBindings,
            romInputPortGroups,
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
        Assert.Equal("p1", romExtraBindings[0].PortId);
        Assert.Single(romInputPortGroups.Single(group => group.PortId == "p1").ExtraBindings);
        Assert.Equal($"已新增 {InputSchema.GetPortDisplayName("p1")} 组合键", status);
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
        var globalInput = new List<InputBindingEntry> { CreateInputBinding("p1", "a", "A", Key.Z) };
        var globalExtra = new List<ExtraInputBindingEntry>();
        var romInputOverrides = new Dictionary<string, Dictionary<string, Dictionary<string, Key>>>(StringComparer.OrdinalIgnoreCase);
        var romExtraOverrides = new Dictionary<string, List<ExtraInputBindingProfile>>(StringComparer.OrdinalIgnoreCase);
        var romInput = new List<InputBindingEntry> { CreateInputBinding("p1", "b", "B", Key.X) };
        var romExtra = new List<ExtraInputBindingEntry> { ExtraInputBindingEntry.CreateDefaultTurbo("p1", "1P", Key.Q, ConfigurableKeys) };
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
        var globalEntry = ExtraInputBindingEntry.CreateDefaultTurbo("p1", "1P", Key.Q, ConfigurableKeys);
        var romEntry = ExtraInputBindingEntry.CreateDefaultTurbo("p1", "1P", Key.W, ConfigurableKeys);
        var rom = CreateRom("Castlevania", "/tmp/cv.nes");
        var romInputOverrides = new Dictionary<string, Dictionary<string, Dictionary<string, Key>>>(StringComparer.OrdinalIgnoreCase);
        var romExtraOverrides = new Dictionary<string, List<ExtraInputBindingProfile>>(StringComparer.OrdinalIgnoreCase);
        var globalInput = new List<InputBindingEntry> { CreateInputBinding("p1", "a", "A", Key.Z) };
        var globalExtra = new List<ExtraInputBindingEntry>();
        var romInput = new List<InputBindingEntry> { CreateInputBinding("p1", "b", "B", Key.X) };
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

    private static InputBindingEntry CreateInputBinding(string portId, string actionId, string actionName, Key key) =>
        new(portId, GetPortLabel(portId), actionId, actionName, key, ConfigurableKeys);

    private static string GetPortLabel(string portId) =>
        string.Equals(portId, "p2", StringComparison.OrdinalIgnoreCase) ? "2P" : "1P";

    private static RomLibraryItem CreateRom(string name, string path) =>
        new(
            name: $"{name}.nes",
            path: path,
            previewFilePath: string.Empty,
            hasPreview: false,
            fileSizeBytes: 1024,
            importedAtUtc: DateTime.UtcNow);
}

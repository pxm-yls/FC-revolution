using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Input;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowInputBindingWorkflowControllerTests
{
    private static readonly IReadOnlyList<Key> ConfigurableKeys =
    [
        Key.Z, Key.X, Key.I, Key.K, Key.Q, Key.W, Key.F9, Key.Enter
    ];

    [Fact]
    public void BuildAndApplyGlobalInputBindingViewState_ProjectsBindingsAndPortViews()
    {
        var workflowController = new MainWindowInputBindingWorkflowController();
        var bindingsController = new MainWindowInputBindingsController();
        var globalInputBindings = new ObservableCollection<InputBindingEntry>();
        var globalExtraInputBindings = new ObservableCollection<ExtraInputBindingEntry>();
        var globalInputPortGroups = new ObservableCollection<InputBindingPortGroup>();

        workflowController.BuildAndApplyGlobalInputBindingViewState(
            bindingsController,
            profile: null,
            CoreInputBindingSchema.CreateFallback(),
            BuildDefaultKeyMaps(),
            ConfigurableKeys,
            InputBindingLayoutProfile.CreateDefault(),
            globalInputBindings,
            globalExtraInputBindings,
            globalInputPortGroups);

        Assert.Equal(4, globalInputBindings.Count);
        Assert.Empty(globalExtraInputBindings);
        Assert.Equal(2, globalInputPortGroups.Count);
        Assert.Equal(2, globalInputPortGroups.Single(group => group.PortId == "p1").InputBindings.Count);
        Assert.Equal(2, globalInputPortGroups.Single(group => group.PortId == "p2").InputBindings.Count);
        Assert.Empty(globalInputPortGroups.Single(group => group.PortId == "p1").ExtraBindings);
        Assert.Empty(globalInputPortGroups.Single(group => group.PortId == "p2").ExtraBindings);
    }

    [Fact]
    public void BuildEffectiveInputBindingState_UsesRomOverridesAndRomExtraBindings()
    {
        var workflowController = new MainWindowInputBindingWorkflowController();
        var bindingsController = new MainWindowInputBindingsController();
        var inputStateController = new MainWindowInputStateController();
        var inputBindingSchema = CoreInputBindingSchema.CreateFallback();
        const string romPath = "/tmp/contra.nes";
        var globalInputBindings = new List<InputBindingEntry>
        {
            CreateInputBinding("p1", "a", "A", Key.Z),
            CreateInputBinding("p1", "b", "B", Key.X),
            CreateInputBinding("p2", "a", "A", Key.I),
            CreateInputBinding("p2", "b", "B", Key.K)
        };
        var globalExtraBindings = new List<ExtraInputBindingEntry>
        {
            ExtraInputBindingEntry.CreateDefaultTurbo("p1", "1P", Key.W, ConfigurableKeys)
        };
        var romInputOverrides = new Dictionary<string, Dictionary<string, Dictionary<string, Key>>>(StringComparer.OrdinalIgnoreCase)
        {
            [romPath] = new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase)
            {
                ["p1"] = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase)
                {
                    ["a"] = Key.F9
                }
            }
        };
        var romExtraOverrides = new Dictionary<string, List<ExtraInputBindingProfile>>(StringComparer.OrdinalIgnoreCase)
        {
            [romPath] =
            [
                new ExtraInputBindingProfile
                {
                    PortId = "p1",
                    Kind = nameof(ExtraInputBindingKind.Turbo),
                    Key = nameof(Key.Q),
                    Buttons = ["a"]
                }
            ]
        };

        var state = workflowController.BuildEffectiveInputBindingState(
            bindingsController,
            inputStateController,
            inputBindingSchema,
            romPath,
            romInputOverrides,
            romExtraOverrides,
            globalInputBindings,
            globalExtraBindings,
            BuildDefaultKeyMaps());

        var romPrimaryBinding = state.EffectiveKeyMap[Key.F9];
        Assert.Equal("p1", romPrimaryBinding.PortId);
        Assert.Equal("a", romPrimaryBinding.ActionId);

        var globalPrimaryBinding = state.EffectiveKeyMap[Key.X];
        Assert.Equal("p1", globalPrimaryBinding.PortId);
        Assert.Equal("b", globalPrimaryBinding.ActionId);

        var secondaryPrimaryBinding = state.EffectiveKeyMap[Key.I];
        Assert.Equal("p2", secondaryPrimaryBinding.PortId);
        Assert.Equal("a", secondaryPrimaryBinding.ActionId);

        var secondarySecondaryBinding = state.EffectiveKeyMap[Key.K];
        Assert.Equal("p2", secondarySecondaryBinding.PortId);
        Assert.Equal("b", secondarySecondaryBinding.ActionId);

        var extra = Assert.Single(state.EffectiveExtraBindings);
        Assert.Equal(Key.Q, extra.Key);
        Assert.Equal("p1", extra.PortId);
        Assert.True(state.EffectiveHandledKeys.Contains(Key.F9));
        Assert.True(state.EffectiveHandledKeys.Contains(Key.Q));
        Assert.False(state.EffectiveHandledKeys.Contains(Key.W));
    }

    [Fact]
    public void RefreshRomInputBindings_WhenCurrentRomNull_DisablesOverrideAndClearsViews()
    {
        var workflowController = new MainWindowInputBindingWorkflowController();
        var inputBindingsController = new MainWindowInputBindingsController();
        var inputOverrideController = new MainWindowInputOverrideController(inputBindingsController, ConfigurableKeys);
        var inputBindingSchema = CoreInputBindingSchema.CreateFallback();
        var globalInputBindings = new ObservableCollection<InputBindingEntry>();
        var globalExtraInputBindings = new ObservableCollection<ExtraInputBindingEntry>();
        var romInputBindings = new ObservableCollection<InputBindingEntry>
        {
            CreateInputBinding("p1", "a", "A", Key.Z)
        };
        var romExtraInputBindings = new ObservableCollection<ExtraInputBindingEntry>
        {
            ExtraInputBindingEntry.CreateDefaultTurbo("p1", "1P", Key.Q, ConfigurableKeys)
        };
        var romInputPortGroups = new ObservableCollection<InputBindingPortGroup>
        {
            new("p1", "1P", romInputBindings, romExtraInputBindings)
        };
        var isOverrideEnabled = true;
        var summaryNotifyCount = 0;

        workflowController.RefreshRomInputBindings(
            inputOverrideController,
            currentRom: null,
            new Dictionary<string, Dictionary<string, Dictionary<string, Key>>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, List<ExtraInputBindingProfile>>(StringComparer.OrdinalIgnoreCase),
            globalInputBindings,
            globalExtraInputBindings,
            romInputBindings,
            romExtraInputBindings,
            romInputPortGroups,
            inputBindingSchema,
            BuildDefaultKeyMaps(),
            ConfigurableKeys,
            InputBindingLayoutProfile.CreateDefault(),
            enabled => isOverrideEnabled = enabled,
            () => summaryNotifyCount++);

        Assert.False(isOverrideEnabled);
        Assert.Equal(1, summaryNotifyCount);
        Assert.Empty(romInputBindings);
        Assert.Empty(romExtraInputBindings);
        Assert.Empty(romInputPortGroups);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, Key>> BuildDefaultKeyMaps() =>
        BuildReadOnlyMaps(
            new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase)
            {
                ["p1"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["a"] = Key.Z,
                    ["b"] = Key.X
                },
                ["p2"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["a"] = Key.I,
                    ["b"] = Key.K
                }
            });

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, Key>> BuildReadOnlyMaps(
        Dictionary<string, Dictionary<string, Key>> maps)
    {
        var readOnly = new Dictionary<string, IReadOnlyDictionary<string, Key>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (portId, bindings) in maps)
            readOnly[portId] = bindings;
        return readOnly;
    }

    private static InputBindingEntry CreateInputBinding(string portId, string actionId, string actionName, Key key) =>
        new(portId, GetPortLabel(portId), actionId, actionName, key, ConfigurableKeys);

    private static string GetPortLabel(string portId) =>
        string.Equals(portId, "p2", StringComparison.OrdinalIgnoreCase) ? "2P" : "1P";
}

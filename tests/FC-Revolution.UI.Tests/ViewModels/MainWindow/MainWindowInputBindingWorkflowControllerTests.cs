using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Input;
using FCRevolution.Core.Input;
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
    public void BuildAndApplyGlobalInputBindingViewState_ProjectsBindingsAndPlayerViews()
    {
        var workflowController = new MainWindowInputBindingWorkflowController();
        var bindingsController = new MainWindowInputBindingsController();
        var globalInputBindings = new ObservableCollection<InputBindingEntry>();
        var globalExtraInputBindings = new ObservableCollection<ExtraInputBindingEntry>();
        var globalInputBindingsPlayer1 = new ObservableCollection<InputBindingEntry>();
        var globalInputBindingsPlayer2 = new ObservableCollection<InputBindingEntry>();
        var globalExtraInputBindingsPlayer1 = new ObservableCollection<ExtraInputBindingEntry>();
        var globalExtraInputBindingsPlayer2 = new ObservableCollection<ExtraInputBindingEntry>();

        workflowController.BuildAndApplyGlobalInputBindingViewState(
            bindingsController,
            profile: null,
            BuildDefaultKeyMaps(),
            ConfigurableKeys,
            InputBindingLayoutProfile.CreateDefault(),
            globalInputBindings,
            globalExtraInputBindings,
            globalInputBindingsPlayer1,
            globalInputBindingsPlayer2,
            globalExtraInputBindingsPlayer1,
            globalExtraInputBindingsPlayer2);

        Assert.Equal(4, globalInputBindings.Count);
        Assert.Empty(globalExtraInputBindings);
        Assert.Equal(2, globalInputBindingsPlayer1.Count);
        Assert.Equal(2, globalInputBindingsPlayer2.Count);
        Assert.Empty(globalExtraInputBindingsPlayer1);
        Assert.Empty(globalExtraInputBindingsPlayer2);
    }

    [Fact]
    public void BuildEffectiveInputBindingState_UsesRomOverridesAndRomExtraBindings()
    {
        var workflowController = new MainWindowInputBindingWorkflowController();
        var bindingsController = new MainWindowInputBindingsController();
        var inputStateController = new MainWindowInputStateController();
        const string romPath = "/tmp/contra.nes";
        var globalInputBindings = new List<InputBindingEntry>
        {
            CreateInputBinding(0, NesButton.A, Key.Z),
            CreateInputBinding(0, NesButton.B, Key.X),
            CreateInputBinding(1, NesButton.A, Key.I),
            CreateInputBinding(1, NesButton.B, Key.K)
        };
        var globalExtraBindings = new List<ExtraInputBindingEntry>
        {
            ExtraInputBindingEntry.CreateDefaultTurbo(0, Key.W, ConfigurableKeys)
        };
        var romInputOverrides = new Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>>(StringComparer.OrdinalIgnoreCase)
        {
            [romPath] = new Dictionary<int, Dictionary<NesButton, Key>>
            {
                [0] = new Dictionary<NesButton, Key>
                {
                    [NesButton.A] = Key.F9
                }
            }
        };
        var romExtraOverrides = new Dictionary<string, List<ExtraInputBindingProfile>>(StringComparer.OrdinalIgnoreCase)
        {
            [romPath] =
            [
                new ExtraInputBindingProfile
                {
                    Player = 0,
                    Kind = nameof(ExtraInputBindingKind.Turbo),
                    Key = nameof(Key.Q),
                    Buttons = [nameof(NesButton.A)]
                }
            ]
        };

        var state = workflowController.BuildEffectiveInputBindingState(
            bindingsController,
            inputStateController,
            romPath,
            romInputOverrides,
            romExtraOverrides,
            globalInputBindings,
            globalExtraBindings,
            BuildDefaultKeyMaps());

        Assert.Equal((0, NesButton.A), state.EffectiveKeyMap[Key.F9]);
        Assert.Equal((0, NesButton.B), state.EffectiveKeyMap[Key.X]);
        Assert.Equal((1, NesButton.A), state.EffectiveKeyMap[Key.I]);
        Assert.Equal((1, NesButton.B), state.EffectiveKeyMap[Key.K]);

        var extra = Assert.Single(state.EffectiveExtraBindings);
        Assert.Equal(Key.Q, extra.Key);
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
        var globalInputBindings = new ObservableCollection<InputBindingEntry>();
        var globalExtraInputBindings = new ObservableCollection<ExtraInputBindingEntry>();
        var romInputBindings = new ObservableCollection<InputBindingEntry>
        {
            CreateInputBinding(0, NesButton.A, Key.Z)
        };
        var romInputBindingsPlayer1 = new ObservableCollection<InputBindingEntry>
        {
            romInputBindings[0]
        };
        var romInputBindingsPlayer2 = new ObservableCollection<InputBindingEntry>();
        var romExtraInputBindings = new ObservableCollection<ExtraInputBindingEntry>
        {
            ExtraInputBindingEntry.CreateDefaultTurbo(0, Key.Q, ConfigurableKeys)
        };
        var romExtraInputBindingsPlayer1 = new ObservableCollection<ExtraInputBindingEntry>
        {
            romExtraInputBindings[0]
        };
        var romExtraInputBindingsPlayer2 = new ObservableCollection<ExtraInputBindingEntry>();
        var isOverrideEnabled = true;
        var summaryNotifyCount = 0;

        workflowController.RefreshRomInputBindings(
            inputOverrideController,
            currentRom: null,
            new Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, List<ExtraInputBindingProfile>>(StringComparer.OrdinalIgnoreCase),
            globalInputBindings,
            globalExtraInputBindings,
            romInputBindings,
            romInputBindingsPlayer1,
            romInputBindingsPlayer2,
            romExtraInputBindings,
            romExtraInputBindingsPlayer1,
            romExtraInputBindingsPlayer2,
            BuildDefaultKeyMaps(),
            ConfigurableKeys,
            InputBindingLayoutProfile.CreateDefault(),
            enabled => isOverrideEnabled = enabled,
            () => summaryNotifyCount++);

        Assert.False(isOverrideEnabled);
        Assert.Equal(1, summaryNotifyCount);
        Assert.Empty(romInputBindings);
        Assert.Empty(romInputBindingsPlayer1);
        Assert.Empty(romInputBindingsPlayer2);
        Assert.Empty(romExtraInputBindings);
        Assert.Empty(romExtraInputBindingsPlayer1);
        Assert.Empty(romExtraInputBindingsPlayer2);
    }

    private static IReadOnlyDictionary<int, IReadOnlyDictionary<NesButton, Key>> BuildDefaultKeyMaps() =>
        new Dictionary<int, IReadOnlyDictionary<NesButton, Key>>
        {
            [0] = new Dictionary<NesButton, Key>
            {
                [NesButton.A] = Key.Z,
                [NesButton.B] = Key.X
            },
            [1] = new Dictionary<NesButton, Key>
            {
                [NesButton.A] = Key.I,
                [NesButton.B] = Key.K
            }
        };

    private static InputBindingEntry CreateInputBinding(int player, NesButton button, Key key) =>
        new(player, button.ToString(), button, key, ConfigurableKeys);
}

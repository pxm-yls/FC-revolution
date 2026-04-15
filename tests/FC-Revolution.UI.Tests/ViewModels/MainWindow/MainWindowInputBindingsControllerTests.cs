using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Input;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowInputBindingsControllerTests
{
    private static readonly IReadOnlyList<Key> TestConfigurableKeys =
    [
        Key.Z,
        Key.X,
        Key.A,
        Key.S,
        Key.F2,
        Key.F3,
        Key.F9,
        Key.Up,
        Key.Down,
        Key.Left,
        Key.Right,
        Key.Enter
    ];

    [Fact]
    public void TryCommitShortcutBinding_ReturnsConflict_WhenGestureAlreadyUsed()
    {
        var controller = new MainWindowInputBindingsController();
        var shortcuts = new Dictionary<string, ShortcutBindingEntry>(StringComparer.Ordinal);
        var main = new ObservableCollection<ShortcutBindingEntry>();
        var shared = new ObservableCollection<ShortcutBindingEntry>();
        var gameOnly = new ObservableCollection<ShortcutBindingEntry>();
        controller.InitializeShortcutBindings(shortcuts, main, shared, gameOnly);

        var entry = shortcuts[ShortcutCatalog.GameQuickLoad];
        var result = controller.TryCommitShortcutBinding(
            entry,
            shortcuts.Values,
            Key.F2,
            KeyModifiers.None);

        Assert.True(result.Handled);
        Assert.False(result.Accepted);
        Assert.False(result.RequiresSave);
        Assert.Contains("冲突", result.StatusText);
    }

    [Fact]
    public void TryCommitShortcutBinding_ReturnsApplyFlags_WhenGestureAccepted()
    {
        var controller = new MainWindowInputBindingsController();
        var shortcuts = new Dictionary<string, ShortcutBindingEntry>(StringComparer.Ordinal);
        var main = new ObservableCollection<ShortcutBindingEntry>();
        var shared = new ObservableCollection<ShortcutBindingEntry>();
        var gameOnly = new ObservableCollection<ShortcutBindingEntry>();
        controller.InitializeShortcutBindings(shortcuts, main, shared, gameOnly);

        var entry = shortcuts[ShortcutCatalog.GameQuickLoad];
        var result = controller.TryCommitShortcutBinding(
            entry,
            shortcuts.Values,
            Key.F9,
            KeyModifiers.None);

        Assert.True(result.Handled);
        Assert.True(result.Accepted);
        Assert.True(result.RequiresSave);
        Assert.True(result.RequiresSessionApply);
        Assert.True(result.RequiresNotify);
        Assert.Equal(Key.F9, entry.SelectedGesture.Key);
    }

    [Fact]
    public void BuildRomInputBindingViewState_UsesGlobalClone_WhenNoRomOverride()
    {
        var controller = new MainWindowInputBindingsController();
        var global = new List<InputBindingEntry>
        {
            CreateInputBinding("p1", "a", "A", Key.Z),
            CreateInputBinding("p1", "b", "B", Key.X),
            CreateInputBinding("p2", "a", "A", Key.Enter),
            CreateInputBinding("p2", "b", "B", Key.S)
        };
        var globalExtra = new List<ExtraInputBindingEntry>
        {
            ExtraInputBindingEntry.CreateDefaultTurbo("p1", "1P", Key.A, TestConfigurableKeys)
        };
        var defaultMaps = new Dictionary<string, IReadOnlyDictionary<string, Key>>(StringComparer.OrdinalIgnoreCase)
        {
            ["p1"] = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase)
            {
                ["a"] = Key.Z,
                ["b"] = Key.X
            },
            ["p2"] = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase)
            {
                ["a"] = Key.Enter,
                ["b"] = Key.S
            }
        };

        var state = controller.BuildRomInputBindingViewState(
            "/tmp/demo.nes",
            new Dictionary<string, Dictionary<string, Dictionary<string, Key>>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, List<ExtraInputBindingProfile>>(StringComparer.OrdinalIgnoreCase),
            global,
            globalExtra,
            defaultMaps,
            TestConfigurableKeys,
            InputBindingLayoutProfile.CreateDefault());

        Assert.False(state.IsOverrideEnabled);
        Assert.Equal(global.Count, state.InputBindings.Count);
        Assert.Equal(globalExtra.Count, state.ExtraBindings.Count);
        Assert.NotSame(global[0], state.InputBindings[0]);
    }

    [Fact]
    public void BuildGlobalInputBindingViewState_UsesDefaults_WhenProfileIsNull()
    {
        var controller = new MainWindowInputBindingsController();
        var layout = InputBindingLayoutProfile.CreateDefault();
        var state = controller.BuildGlobalInputBindingViewState(
            profile: null,
            BuildDefaultKeyMaps(),
            TestConfigurableKeys,
            layout);

        Assert.Equal(4, state.InputBindings.Count);
        Assert.Empty(state.ExtraBindings);

        var player1A = Assert.Single(state.InputBindings, entry => entry.PortId == "p1" && entry.ActionId == "a");
        Assert.Equal(Key.Z, player1A.SelectedKey);
        Assert.Equal(layout.GetSlot("a").CenterX, player1A.CenterX, precision: 6);
        Assert.Equal(layout.GetSlot("a").CenterY, player1A.CenterY, precision: 6);
    }

    [Fact]
    public void BuildGlobalInputBindingViewState_UsesOverridesWithDefaultFallback_AndProjectsExtraBindings()
    {
        var controller = new MainWindowInputBindingsController();
        var layout = InputBindingLayoutProfile.CreateDefault();
        var profile = new SystemConfigProfile
        {
            PortInputOverrides = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
            {
                ["p1"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["a"] = nameof(Key.F9)
                }
            },
            ExtraInputBindings =
            [
                new ExtraInputBindingProfile
                {
                    PortId = "p2",
                    Kind = nameof(ExtraInputBindingKind.Combo),
                    Key = nameof(Key.A),
                    Buttons = ["a", "b"]
                }
            ]
        };

        var state = controller.BuildGlobalInputBindingViewState(
            profile,
            BuildDefaultKeyMaps(),
            TestConfigurableKeys,
            layout);

        var player1A = Assert.Single(state.InputBindings, entry => entry.PortId == "p1" && entry.ActionId == "a");
        var player1B = Assert.Single(state.InputBindings, entry => entry.PortId == "p1" && entry.ActionId == "b");
        Assert.Equal(Key.F9, player1A.SelectedKey);
        Assert.Equal(Key.X, player1B.SelectedKey);

        var extra = Assert.Single(state.ExtraBindings);
        Assert.Equal("p2", extra.PortId);
        Assert.Equal(ExtraInputBindingKind.Combo, extra.Kind);
        Assert.Equal(Key.A, extra.SelectedKey);
        Assert.Equal("A+B", extra.SummaryText);
    }

    [Fact]
    public void BuildGlobalInputConfigSaveState_MapsBindingsAndClonesLayout()
    {
        var controller = new MainWindowInputBindingsController();
        var layout = InputBindingLayoutProfile.CreateDefault();
        layout.BridgeX += 9;
        layout.BridgeY -= 4;

        var inputBindings = new List<InputBindingEntry>
        {
            CreateInputBinding("p1", "a", "A", Key.F9),
            CreateInputBinding("p1", "b", "B", Key.X),
            CreateInputBinding("p2", "a", "A", Key.Enter),
            CreateInputBinding("p2", "b", "B", Key.S)
        };
        var extraBindings = new List<ExtraInputBindingEntry>
        {
            ExtraInputBindingEntry.CreateDefaultTurbo("p1", "1P", Key.A, TestConfigurableKeys)
        };
        extraBindings[0].SetTurboHz(12);

        var shortcuts = new Dictionary<string, ShortcutBindingEntry>(StringComparer.Ordinal)
        {
            [ShortcutCatalog.GameQuickLoad] = new(
                ShortcutCatalog.ById[ShortcutCatalog.GameQuickLoad],
                new ShortcutGesture(Key.F9, KeyModifiers.Control))
        };

        var state = controller.BuildGlobalInputConfigSaveState(
            inputBindings,
            extraBindings,
            shortcuts,
            layout);

        Assert.Equal(nameof(Key.F9), state.PortInputOverrides["p1"]["a"]);
        Assert.Equal(Key.Enter.ToString(), state.PortInputOverrides["p2"]["a"]);

        var extra = Assert.Single(state.ExtraInputBindings);
        Assert.Equal("p1", extra.PortId);
        Assert.Equal(nameof(Key.A), extra.Key);
        Assert.Equal(12, extra.TurboHz);
        Assert.Equal("a", Assert.Single(extra.Buttons));

        Assert.Equal(nameof(Key.F9), state.ShortcutBindings[ShortcutCatalog.GameQuickLoad].Key);
        Assert.Equal(nameof(KeyModifiers.Control), state.ShortcutBindings[ShortcutCatalog.GameQuickLoad].Modifiers);

        Assert.NotSame(layout, state.InputBindingLayout);
        Assert.Equal(layout.BridgeX, state.InputBindingLayout.BridgeX, precision: 6);
        Assert.Equal(layout.BridgeY, state.InputBindingLayout.BridgeY, precision: 6);
    }

    [Fact]
    public void BuildInputMapsByPort_MapsLegacyPlayerOrdinals_ToSchemaPortOrder()
    {
        var controller = new MainWindowInputBindingsController();
        var profile = new SystemConfigProfile
        {
            PortInputOverrides = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
            {
                ["Player1"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["fire"] = nameof(Key.Z)
                },
                ["Player2"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["jump"] = nameof(Key.X)
                }
            }
        };

        var maps = controller.BuildInputMapsByPort(profile, CoreInputBindingSchema.Create(new HighIndexInputSchema()));

        Assert.Equal(Key.Z, maps["pad-west"]["fire"]);
        Assert.Equal(Key.X, maps["pad-east"]["jump"]);
    }

    [Fact]
    public void SaveRomProfileInputOverride_WithSchemaWithoutPorts_DoesNotSynthesizeLegacyPrimaryPort()
    {
        var controller = new MainWindowInputBindingsController();
        var romPath = Path.GetTempFileName();

        try
        {
            controller.SaveRomProfileInputOverride(
                romPath,
                new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["pad-west"] = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["fire"] = Key.Z
                    }
                },
                new Dictionary<string, List<ExtraInputBindingProfile>>(StringComparer.OrdinalIgnoreCase),
                CoreInputBindingSchema.Create(new EmptyInputSchema()));

            var profile = RomConfigProfile.Load(romPath);
            Assert.Empty(profile.PortInputOverrides);
            Assert.Empty(profile.InputOverrides);
        }
        finally
        {
            var profilePath = RomConfigProfile.GetProfilePath(romPath);
            if (File.Exists(romPath))
                File.Delete(romPath);
            if (File.Exists(profilePath))
                File.Delete(profilePath);
        }
    }

    private static InputBindingEntry CreateInputBinding(string portId, string actionId, string actionName, Key key) =>
        new(portId, GetPortLabel(portId), actionId, actionName, key, TestConfigurableKeys);

    private static string GetPortLabel(string portId) =>
        string.Equals(portId, "p2", StringComparison.OrdinalIgnoreCase) ? "2P" : "1P";

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, Key>> BuildDefaultKeyMaps() =>
        new Dictionary<string, IReadOnlyDictionary<string, Key>>(StringComparer.OrdinalIgnoreCase)
        {
            ["p1"] = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase)
            {
                ["a"] = Key.Z,
                ["b"] = Key.X
            },
            ["p2"] = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase)
            {
                ["a"] = Key.Enter,
                ["b"] = Key.S
            }
        };

    private sealed class HighIndexInputSchema : IInputSchema
    {
        public IReadOnlyList<InputPortDescriptor> Ports { get; } =
        [
            new("pad-west", "West Pad", 4),
            new("pad-east", "East Pad", 7)
        ];

        public IReadOnlyList<InputActionDescriptor> Actions { get; } =
        [
            new("fire", "Fire", "pad-west", InputValueKind.Digital),
            new("jump", "Jump", "pad-east", InputValueKind.Digital)
        ];
    }

    private sealed class EmptyInputSchema : IInputSchema
    {
        public IReadOnlyList<InputPortDescriptor> Ports { get; } = [];

        public IReadOnlyList<InputActionDescriptor> Actions { get; } = [];
    }
}

using Avalonia.Input;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowInputBindingResolverTests
{
    [Fact]
    public void BuildKeyMap_MapsBindingsAndUsesLastAssignmentForDuplicateKey()
    {
        var inputMaps = new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase)
        {
            ["p1"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["a"] = Key.Z,
                ["b"] = Key.X
            },
            ["p2"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["start"] = Key.Z
            }
        };

        var keyMap = GameWindowInputBindingResolver.BuildKeyMap(inputMaps);

        var startBinding = keyMap[Key.Z];
        Assert.Equal("p2", startBinding.PortId);
        Assert.Equal("start", startBinding.ActionId);

        var secondaryBinding = keyMap[Key.X];
        Assert.Equal("p1", secondaryBinding.PortId);
        Assert.Equal("b", secondaryBinding.ActionId);
    }

    [Fact]
    public void BuildKeyMap_UsesSchemaPortDescriptors_InsteadOfHardcodedP1P2()
    {
        var keyMap = GameWindowInputBindingResolver.BuildKeyMap(
            new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase)
            {
                ["pad-west"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["fire"] = Key.Z
                },
                ["pad-east"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["jump"] = Key.X
                }
            },
            CoreInputBindingSchema.Create(new CustomInputSchema()));

        Assert.Equal("pad-west", keyMap[Key.Z].PortId);
        Assert.Equal("fire", keyMap[Key.Z].ActionId);
        Assert.Equal("pad-east", keyMap[Key.X].PortId);
        Assert.Equal("jump", keyMap[Key.X].ActionId);
    }

    [Fact]
    public void CoreInputBindingSchema_Create_UsesSchemaDeclaredPlayers_WithoutSynthesizingFallbackPorts()
    {
        var schema = CoreInputBindingSchema.Create(new HighIndexInputSchema());

        Assert.Collection(
            schema.GetSupportedPorts(),
            port =>
            {
                Assert.Equal("pad-west", port.PortId);
                Assert.Equal(4, port.PlayerIndex);
            },
            port =>
            {
                Assert.Equal("pad-east", port.PortId);
                Assert.Equal(7, port.PlayerIndex);
            });
        Assert.True(schema.TryGetPortId(4, out var westPortId));
        Assert.Equal("pad-west", westPortId);
        Assert.True(schema.TryGetPortId(7, out var eastPortId));
        Assert.Equal("pad-east", eastPortId);
        Assert.False(schema.TryGetPortId(0, out _));
        Assert.Equal(string.Empty, schema.GetPortId(0));
    }

    [Fact]
    public void ResolveExtraInputBindings_UsesSchemaPlayerIndexFallback_WhenPortIdMissing()
    {
        var bindings = GameWindowInputBindingResolver.ResolveExtraInputBindings(
            [
                new ExtraInputBindingProfile
                {
                    Player = 7,
                    Kind = ExtraInputBindingKind.Turbo.ToString(),
                    Key = Key.Q.ToString(),
                    Buttons = ["shield"]
                }
            ],
            CoreInputBindingSchema.Create(new HighIndexInputSchema()));

        var binding = Assert.Single(bindings);
        Assert.Equal("pad-east", binding.PortId);
        Assert.Equal(Key.Q, binding.Key);
        Assert.Equal("shield", Assert.Single(binding.ActionIds));
    }

    [Fact]
    public void ResolveExtraInputBindings_FiltersInvalidProfiles_AndNormalizesKindButtonsAndTurbo()
    {
        var profiles = new[]
        {
            new ExtraInputBindingProfile
            {
                PortId = "p1",
                Kind = "UnknownKind",
                Key = Key.Q.ToString(),
                Buttons = ["a", "BAD", "a"],
                TurboHz = 100
            },
            new ExtraInputBindingProfile
            {
                PortId = "p2",
                Kind = ExtraInputBindingKind.Combo.ToString(),
                Key = Key.W.ToString(),
                Buttons = ["b"]
            },
            new ExtraInputBindingProfile
            {
                PortId = "p1",
                Kind = ExtraInputBindingKind.Combo.ToString(),
                Key = Key.None.ToString(),
                Buttons = ["a", "b"]
            }
        };

        var bindings = GameWindowInputBindingResolver.ResolveExtraInputBindings(profiles);

        var binding = Assert.Single(bindings);
        Assert.Equal("p1", binding.PortId);
        Assert.Equal(Key.Q, binding.Key);
        Assert.Equal(ExtraInputBindingKind.Turbo, binding.Kind);
        Assert.Equal(30, binding.TurboHz);
        var actionId = Assert.Single(binding.ActionIds);
        Assert.Equal("a", actionId);
    }

    [Fact]
    public void BuildHandledKeys_MergesKeyMapKeysAndExtraBindingKeys()
    {
        var keyMap = new Dictionary<Key, (string PortId, string ActionId)>
        {
            [Key.Z] = ("p1", "a")
        };
        var extraBindings = new[]
        {
            new GameWindowResolvedExtraInputBinding(
                PortId: "p1",
                Key: Key.Q,
                Kind: ExtraInputBindingKind.Combo,
                ActionIds: ["a", "b"]),
            new GameWindowResolvedExtraInputBinding(
                PortId: "p2",
                Key: Key.Z,
                Kind: ExtraInputBindingKind.Turbo,
                ActionIds: ["start"])
        };

        var handledKeys = GameWindowInputBindingResolver.BuildHandledKeys(keyMap, extraBindings);

        Assert.Contains(Key.Z, handledKeys);
        Assert.Contains(Key.Q, handledKeys);
        Assert.Equal(2, handledKeys.Count);
    }

    private sealed class CustomInputSchema : IInputSchema
    {
        public IReadOnlyList<InputPortDescriptor> Ports { get; } =
        [
            new("pad-west", "West Pad", 0),
            new("pad-east", "East Pad", 1)
        ];

        public IReadOnlyList<InputActionDescriptor> Actions { get; } =
        [
            new("fire", "Fire", "pad-west", InputValueKind.Digital),
            new("jump", "Jump", "pad-east", InputValueKind.Digital)
        ];
    }

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
            new("shield", "Shield", "pad-east", InputValueKind.Digital)
        ];
    }
}

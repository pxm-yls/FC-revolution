using Avalonia.Input;
using FCRevolution.Core.Input;
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
        var inputMaps = new Dictionary<int, Dictionary<NesButton, Key>>
        {
            [0] = new()
            {
                [NesButton.A] = Key.Z,
                [NesButton.B] = Key.X
            },
            [1] = new()
            {
                [NesButton.Start] = Key.Z
            }
        };

        var keyMap = GameWindowInputBindingResolver.BuildKeyMap(NesInputTestAdapter.BuildBindingsByPort(inputMaps));

        var startBinding = keyMap[Key.Z];
        Assert.Equal("p2", startBinding.PortId);
        Assert.Equal(NesInputTestAdapter.ActionId(NesButton.Start), startBinding.ActionId);

        var secondaryBinding = keyMap[Key.X];
        Assert.Equal("p1", secondaryBinding.PortId);
        Assert.Equal(NesInputTestAdapter.ActionId(NesButton.B), secondaryBinding.ActionId);
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
    public void ResolveExtraInputBindings_FiltersInvalidProfiles_AndNormalizesKindButtonsAndTurbo()
    {
        var profiles = new[]
        {
            new ExtraInputBindingProfile
            {
                PortId = "p1",
                Kind = "UnknownKind",
                Key = Key.Q.ToString(),
                Buttons = [NesInputTestAdapter.ActionId(NesButton.A), "BAD", NesInputTestAdapter.ActionId(NesButton.A)],
                TurboHz = 100
            },
            new ExtraInputBindingProfile
            {
                PortId = "p2",
                Kind = ExtraInputBindingKind.Combo.ToString(),
                Key = Key.W.ToString(),
                Buttons = [NesInputTestAdapter.ActionId(NesButton.B)]
            },
            new ExtraInputBindingProfile
            {
                PortId = "p1",
                Kind = ExtraInputBindingKind.Combo.ToString(),
                Key = Key.None.ToString(),
                Buttons = [NesInputTestAdapter.ActionId(NesButton.A), NesInputTestAdapter.ActionId(NesButton.B)]
            }
        };

        var bindings = GameWindowInputBindingResolver.ResolveExtraInputBindings(profiles);

        var binding = Assert.Single(bindings);
        Assert.Equal("p1", binding.PortId);
        Assert.Equal(Key.Q, binding.Key);
        Assert.Equal(ExtraInputBindingKind.Turbo, binding.Kind);
        Assert.Equal(30, binding.TurboHz);
        var actionId = Assert.Single(binding.ActionIds);
        Assert.Equal(NesInputTestAdapter.ActionId(NesButton.A), actionId);
    }

    [Fact]
    public void BuildHandledKeys_MergesKeyMapKeysAndExtraBindingKeys()
    {
        var keyMap = new Dictionary<Key, (string PortId, string ActionId)>
        {
            [Key.Z] = ("p1", NesInputTestAdapter.ActionId(NesButton.A))
        };
        var extraBindings = new[]
        {
            new GameWindowResolvedExtraInputBinding(
                PortId: "p1",
                Key: Key.Q,
                Kind: ExtraInputBindingKind.Combo,
                ActionIds: NesInputTestAdapter.ActionIds(NesButton.A, NesButton.B)),
            new GameWindowResolvedExtraInputBinding(
                PortId: "p2",
                Key: Key.Z,
                Kind: ExtraInputBindingKind.Turbo,
                ActionIds: NesInputTestAdapter.ActionIds(NesButton.Start))
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
}

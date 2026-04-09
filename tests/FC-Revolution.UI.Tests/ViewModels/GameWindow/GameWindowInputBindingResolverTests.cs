using Avalonia.Input;
using FCRevolution.Core.Input;
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

        Assert.Equal((1, NesInputTestAdapter.ActionId(NesButton.Start)), keyMap[Key.Z]);
        Assert.Equal((0, NesInputTestAdapter.ActionId(NesButton.B)), keyMap[Key.X]);
    }

    [Fact]
    public void ResolveExtraInputBindings_FiltersInvalidProfiles_AndNormalizesKindButtonsAndTurbo()
    {
        var profiles = new[]
        {
            new ExtraInputBindingProfile
            {
                Player = 0,
                Kind = "UnknownKind",
                Key = Key.Q.ToString(),
                Buttons = [NesButton.A.ToString(), "BAD", NesButton.A.ToString()],
                TurboHz = 100
            },
            new ExtraInputBindingProfile
            {
                Player = 1,
                Kind = ExtraInputBindingKind.Combo.ToString(),
                Key = Key.W.ToString(),
                Buttons = [NesButton.B.ToString()]
            },
            new ExtraInputBindingProfile
            {
                Player = 0,
                Kind = ExtraInputBindingKind.Combo.ToString(),
                Key = Key.None.ToString(),
                Buttons = [NesButton.A.ToString(), NesButton.B.ToString()]
            }
        };

        var bindings = GameWindowInputBindingResolver.ResolveExtraInputBindings(profiles);

        var binding = Assert.Single(bindings);
        Assert.Equal(0, binding.Player);
        Assert.Equal(Key.Q, binding.Key);
        Assert.Equal(ExtraInputBindingKind.Turbo, binding.Kind);
        Assert.Equal(30, binding.TurboHz);
        var actionId = Assert.Single(binding.ActionIds);
        Assert.Equal(NesInputTestAdapter.ActionId(NesButton.A), actionId);
    }

    [Fact]
    public void BuildHandledKeys_MergesKeyMapKeysAndExtraBindingKeys()
    {
        var keyMap = new Dictionary<Key, (int Player, string ActionId)>
        {
            [Key.Z] = (0, NesInputTestAdapter.ActionId(NesButton.A))
        };
        var extraBindings = new[]
        {
            new GameWindowResolvedExtraInputBinding(
                Player: 0,
                Key: Key.Q,
                Kind: ExtraInputBindingKind.Combo,
                ActionIds: NesInputTestAdapter.ActionIds(NesButton.A, NesButton.B)),
            new GameWindowResolvedExtraInputBinding(
                Player: 1,
                Key: Key.Z,
                Kind: ExtraInputBindingKind.Turbo,
                ActionIds: NesInputTestAdapter.ActionIds(NesButton.Start))
        };

        var handledKeys = GameWindowInputBindingResolver.BuildHandledKeys(keyMap, extraBindings);

        Assert.Contains(Key.Z, handledKeys);
        Assert.Contains(Key.Q, handledKeys);
        Assert.Equal(2, handledKeys.Count);
    }
}

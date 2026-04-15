using Avalonia.Input;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowLocalInputProjectionControllerTests
{
    [Fact]
    public void BuildDesiredLocalInputMasks_ProjectsPressedMappedKeysForTwoPlayers()
    {
        var pressedKeys = new HashSet<Key> { Key.Z, Key.I };
        var keyMap = new Dictionary<Key, (string PortId, string ActionId)>
        {
            [Key.Z] = ("p1", FallbackInputTestData.ActionA),
            [Key.I] = ("p2", FallbackInputTestData.ActionUp)
        };

        var result = GameWindowLocalInputProjectionController.BuildDesiredLocalInputMasks(
            pressedKeys,
            keyMap,
            extraInputBindings: [],
            turboTickCounters: new Dictionary<Key, int>());

        Assert.Equal(FallbackInputTestData.MaskA, result.GetMask("p1"));
        Assert.Equal(FallbackInputTestData.MaskUp, result.GetMask("p2"));
    }

    [Fact]
    public void BuildDesiredLocalInputMasks_IncludesComboBindingsAndDeduplicatesByBitmask()
    {
        var pressedKeys = new HashSet<Key> { Key.Q };

        var result = GameWindowLocalInputProjectionController.BuildDesiredLocalInputMasks(
            pressedKeys,
            keyMap: new Dictionary<Key, (string PortId, string ActionId)>(),
            extraInputBindings:
            [
                new GameWindowResolvedExtraInputBinding(
                    PortId: "p1",
                    Key: Key.Q,
                    Kind: ExtraInputBindingKind.Combo,
                    ActionIds: FallbackInputTestData.ActionIds(
                        FallbackInputTestData.ActionA,
                        FallbackInputTestData.ActionB,
                        FallbackInputTestData.ActionA))
            ],
            turboTickCounters: new Dictionary<Key, int>());

        var expected = (byte)(FallbackInputTestData.MaskA | FallbackInputTestData.MaskB);
        Assert.Equal(expected, result.GetMask("p1"));
        Assert.Equal(0, result.GetMask("p2"));
    }

    [Fact]
    public void BuildDesiredLocalInputMasks_RespectsTurboWindowFromTickCounters()
    {
        var pressedKeys = new HashSet<Key> { Key.Q };
        var turboBinding = new GameWindowResolvedExtraInputBinding(
            PortId: "p1",
            Key: Key.Q,
            Kind: ExtraInputBindingKind.Turbo,
            ActionIds: FallbackInputTestData.ActionIds(FallbackInputTestData.ActionA),
            TurboHz: 10);

        var inOnWindow = GameWindowLocalInputProjectionController.BuildDesiredLocalInputMasks(
            pressedKeys,
            keyMap: new Dictionary<Key, (string PortId, string ActionId)>(),
            extraInputBindings: [turboBinding],
            turboTickCounters: new Dictionary<Key, int> { [Key.Q] = 0 });
        var inOffWindow = GameWindowLocalInputProjectionController.BuildDesiredLocalInputMasks(
            pressedKeys,
            keyMap: new Dictionary<Key, (string PortId, string ActionId)>(),
            extraInputBindings: [turboBinding],
            turboTickCounters: new Dictionary<Key, int> { [Key.Q] = 6 });

        Assert.Equal(FallbackInputTestData.MaskA, inOnWindow.GetMask("p1"));
        Assert.Equal(0, inOffWindow.GetMask("p1"));
    }
}

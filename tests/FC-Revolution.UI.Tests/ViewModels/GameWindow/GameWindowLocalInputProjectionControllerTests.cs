using Avalonia.Input;
using FCRevolution.Core.Input;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowLocalInputProjectionControllerTests
{
    [Fact]
    public void BuildDesiredLocalInputMasks_ProjectsPressedMappedKeysForTwoPlayers()
    {
        var pressedKeys = new HashSet<Key> { Key.Z, Key.I };
        var keyMap = new Dictionary<Key, (int Player, NesButton Button)>
        {
            [Key.Z] = (0, NesButton.A),
            [Key.I] = (1, NesButton.Up)
        };

        var result = GameWindowLocalInputProjectionController.BuildDesiredLocalInputMasks(
            pressedKeys,
            keyMap,
            extraInputBindings: [],
            turboTickCounters: new Dictionary<Key, int>());

        Assert.Equal((byte)NesButton.A, result.Player1Mask);
        Assert.Equal((byte)NesButton.Up, result.Player2Mask);
    }

    [Fact]
    public void BuildDesiredLocalInputMasks_IncludesComboBindingsAndDeduplicatesByBitmask()
    {
        var pressedKeys = new HashSet<Key> { Key.Q };

        var result = GameWindowLocalInputProjectionController.BuildDesiredLocalInputMasks(
            pressedKeys,
            keyMap: new Dictionary<Key, (int Player, NesButton Button)>(),
            extraInputBindings:
            [
                new GameWindowResolvedExtraInputBinding(
                    Player: 0,
                    Key: Key.Q,
                    Kind: ExtraInputBindingKind.Combo,
                    Buttons: [NesButton.A, NesButton.B, NesButton.A])
            ],
            turboTickCounters: new Dictionary<Key, int>());

        var expected = (byte)((byte)NesButton.A | (byte)NesButton.B);
        Assert.Equal(expected, result.Player1Mask);
        Assert.Equal(0, result.Player2Mask);
    }

    [Fact]
    public void BuildDesiredLocalInputMasks_RespectsTurboWindowFromTickCounters()
    {
        var pressedKeys = new HashSet<Key> { Key.Q };
        var turboBinding = new GameWindowResolvedExtraInputBinding(
            Player: 0,
            Key: Key.Q,
            Kind: ExtraInputBindingKind.Turbo,
            Buttons: [NesButton.A],
            TurboHz: 10);

        var inOnWindow = GameWindowLocalInputProjectionController.BuildDesiredLocalInputMasks(
            pressedKeys,
            keyMap: new Dictionary<Key, (int Player, NesButton Button)>(),
            extraInputBindings: [turboBinding],
            turboTickCounters: new Dictionary<Key, int> { [Key.Q] = 0 });
        var inOffWindow = GameWindowLocalInputProjectionController.BuildDesiredLocalInputMasks(
            pressedKeys,
            keyMap: new Dictionary<Key, (int Player, NesButton Button)>(),
            extraInputBindings: [turboBinding],
            turboTickCounters: new Dictionary<Key, int> { [Key.Q] = 6 });

        Assert.Equal((byte)NesButton.A, inOnWindow.Player1Mask);
        Assert.Equal(0, inOffWindow.Player1Mask);
    }
}

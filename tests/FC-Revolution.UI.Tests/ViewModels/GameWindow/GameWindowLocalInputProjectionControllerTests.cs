using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowLocalInputProjectionControllerTests
{
    [Fact]
    public void BuildDesiredLocalInputActions_ProjectsPressedMappedKeysForTwoPlayers()
    {
        var pressedKeys = new HashSet<Key> { Key.Z, Key.I };
        var keyMap = new Dictionary<Key, (string PortId, string ActionId)>
        {
            [Key.Z] = ("p1", FallbackInputTestData.ActionA),
            [Key.I] = ("p2", FallbackInputTestData.ActionUp)
        };

        var result = GameWindowLocalInputProjectionController.BuildDesiredLocalInputActions(
            pressedKeys,
            keyMap,
            extraInputBindings: [],
            turboTickCounters: new Dictionary<Key, int>());

        AssertActions(
            result.GetActions("p1"),
            FallbackInputTestData.ActionIds(FallbackInputTestData.ActionA));
        AssertActions(
            result.GetActions("p2"),
            FallbackInputTestData.ActionIds(FallbackInputTestData.ActionUp));
    }

    [Fact]
    public void BuildDesiredLocalInputActions_IncludesComboBindingsAndDeduplicatesActions()
    {
        var pressedKeys = new HashSet<Key> { Key.Q };

        var result = GameWindowLocalInputProjectionController.BuildDesiredLocalInputActions(
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

        AssertActions(
            result.GetActions("p1"),
            FallbackInputTestData.ActionIds(FallbackInputTestData.ActionA, FallbackInputTestData.ActionB));
        Assert.Empty(result.GetActions("p2"));
    }

    [Fact]
    public void BuildDesiredLocalInputActions_RespectsTurboWindowFromTickCounters()
    {
        var pressedKeys = new HashSet<Key> { Key.Q };
        var turboBinding = new GameWindowResolvedExtraInputBinding(
            PortId: "p1",
            Key: Key.Q,
            Kind: ExtraInputBindingKind.Turbo,
            ActionIds: FallbackInputTestData.ActionIds(FallbackInputTestData.ActionA),
            TurboHz: 10);

        var inOnWindow = GameWindowLocalInputProjectionController.BuildDesiredLocalInputActions(
            pressedKeys,
            keyMap: new Dictionary<Key, (string PortId, string ActionId)>(),
            extraInputBindings: [turboBinding],
            turboTickCounters: new Dictionary<Key, int> { [Key.Q] = 0 });
        var inOffWindow = GameWindowLocalInputProjectionController.BuildDesiredLocalInputActions(
            pressedKeys,
            keyMap: new Dictionary<Key, (string PortId, string ActionId)>(),
            extraInputBindings: [turboBinding],
            turboTickCounters: new Dictionary<Key, int> { [Key.Q] = 6 });

        AssertActions(
            inOnWindow.GetActions("p1"),
            FallbackInputTestData.ActionIds(FallbackInputTestData.ActionA));
        Assert.Empty(inOffWindow.GetActions("p1"));
    }

    private static void AssertActions(
        IReadOnlySet<string> actual,
        IReadOnlyCollection<string> expected)
    {
        Assert.Equal(
            expected.OrderBy(static actionId => actionId, StringComparer.OrdinalIgnoreCase),
            actual.OrderBy(static actionId => actionId, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }
}

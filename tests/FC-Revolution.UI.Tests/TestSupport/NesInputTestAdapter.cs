using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Core.Input;

namespace FC_Revolution.UI.Tests;

internal static class NesInputTestAdapter
{
    public static string ActionId(NesButton button) => button switch
    {
        NesButton.A => "a",
        NesButton.B => "b",
        NesButton.Select => "select",
        NesButton.Start => "start",
        NesButton.Up => "up",
        NesButton.Down => "down",
        NesButton.Left => "left",
        NesButton.Right => "right",
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, null)
    };

    public static Dictionary<int, Dictionary<string, Key>> BuildPlayerMaps(
        IReadOnlyDictionary<int, Dictionary<NesButton, Key>> source)
    {
        return source.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToDictionary(
                binding => ActionId(binding.Key),
                binding => binding.Value,
                StringComparer.OrdinalIgnoreCase));
    }

    public static Dictionary<string, Dictionary<string, Key>> BuildBindingsByPort(
        IReadOnlyDictionary<int, Dictionary<NesButton, Key>> source)
    {
        var bindingsByPort = new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase);
        foreach (var playerMap in source)
        {
            if (!TryGetPortId(playerMap.Key, out var portId))
                continue;

            bindingsByPort[portId] = playerMap.Value.ToDictionary(
                binding => ActionId(binding.Key),
                binding => binding.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        return bindingsByPort;
    }

    public static List<string> ActionIds(params NesButton[] buttons) =>
        buttons.Select(ActionId).ToList();

    private static bool TryGetPortId(int player, out string portId)
    {
        switch (player)
        {
            case 0:
                portId = "p1";
                return true;
            case 1:
                portId = "p2";
                return true;
            default:
                portId = string.Empty;
                return false;
        }
    }
}

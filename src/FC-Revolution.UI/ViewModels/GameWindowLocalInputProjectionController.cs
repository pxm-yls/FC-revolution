using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Core.Input;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowDesiredLocalInputMasks(
    byte Player1Mask,
    byte Player2Mask);

internal static class GameWindowLocalInputProjectionController
{
    public static GameWindowDesiredLocalInputMasks BuildDesiredLocalInputMasks(
        IReadOnlyCollection<Key> pressedKeys,
        IReadOnlyDictionary<Key, (int Player, NesButton Button)> keyMap,
        IReadOnlyList<GameWindowResolvedExtraInputBinding> extraInputBindings,
        IReadOnlyDictionary<Key, int> turboTickCounters)
    {
        byte player1Mask = 0;
        byte player2Mask = 0;

        foreach (var key in pressedKeys)
        {
            if (keyMap.TryGetValue(key, out var binding))
            {
                if (binding.Player == 0)
                    player1Mask |= (byte)binding.Button;
                else
                    player2Mask |= (byte)binding.Button;
            }
        }

        foreach (var binding in extraInputBindings)
        {
            if (!pressedKeys.Contains(binding.Key))
                continue;

            if (binding.Kind == ExtraInputBindingKind.Turbo)
            {
                var periodTicks = Math.Max(1, 60 / Math.Clamp(binding.TurboHz, 1, 30));
                var ticks = turboTickCounters.TryGetValue(binding.Key, out var t) ? t : 0;
                if ((ticks % (periodTicks * 2)) >= periodTicks)
                    continue;
            }

            foreach (var button in binding.Buttons)
            {
                if (binding.Player == 0)
                    player1Mask |= (byte)button;
                else
                    player2Mask |= (byte)button;
            }
        }

        return new GameWindowDesiredLocalInputMasks(player1Mask, player2Mask);
    }
}

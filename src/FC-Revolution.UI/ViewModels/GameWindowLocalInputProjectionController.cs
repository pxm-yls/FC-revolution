using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record GameWindowDesiredLocalInputActions(
    IReadOnlySet<string> Player1Actions,
    IReadOnlySet<string> Player2Actions);

internal readonly record struct GameWindowDesiredLocalInputMasks(
    byte Player1Mask,
    byte Player2Mask);

internal static class GameWindowLocalInputProjectionController
{
    public static GameWindowDesiredLocalInputActions BuildDesiredLocalInputActions(
        IReadOnlyCollection<Key> pressedKeys,
        IReadOnlyDictionary<Key, (int Player, string ActionId)> keyMap,
        IReadOnlyList<GameWindowResolvedExtraInputBinding> extraInputBindings,
        IReadOnlyDictionary<Key, int> turboTickCounters)
    {
        HashSet<string> player1Actions = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> player2Actions = new(StringComparer.OrdinalIgnoreCase);

        foreach (var key in pressedKeys)
        {
            if (!keyMap.TryGetValue(key, out var binding))
                continue;

            GetTargetActions(binding.Player, player1Actions, player2Actions).Add(binding.ActionId);
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

            var targetActions = GetTargetActions(binding.Player, player1Actions, player2Actions);
            foreach (var actionId in binding.ActionIds)
                targetActions.Add(actionId);
        }

        return new GameWindowDesiredLocalInputActions(player1Actions, player2Actions);
    }

    public static GameWindowDesiredLocalInputMasks BuildDesiredLocalInputMasks(
        IReadOnlyCollection<Key> pressedKeys,
        IReadOnlyDictionary<Key, (int Player, string ActionId)> keyMap,
        IReadOnlyList<GameWindowResolvedExtraInputBinding> extraInputBindings,
        IReadOnlyDictionary<Key, int> turboTickCounters)
    {
        var inputBindingSchema = CoreInputBindingSchema.CreateFallback();
        var desiredActions = BuildDesiredLocalInputActions(
            pressedKeys,
            keyMap,
            extraInputBindings,
            turboTickCounters);
        return new GameWindowDesiredLocalInputMasks(
            BuildLegacyMask(0, desiredActions.Player1Actions, inputBindingSchema),
            BuildLegacyMask(1, desiredActions.Player2Actions, inputBindingSchema));
    }

    private static HashSet<string> GetTargetActions(
        int player,
        HashSet<string> player1Actions,
        HashSet<string> player2Actions) =>
        player == 0 ? player1Actions : player2Actions;

    private static byte BuildLegacyMask(int player, IEnumerable<string> actionIds, CoreInputBindingSchema inputBindingSchema)
    {
        byte mask = 0;
        foreach (var actionId in actionIds)
        {
            if (inputBindingSchema.TryGetLegacyBitMask(player, actionId, out var bit))
                mask |= bit;
        }

        return mask;
    }
}

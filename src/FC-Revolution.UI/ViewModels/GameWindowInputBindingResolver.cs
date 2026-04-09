using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Contracts.RemoteControl;
using FC_Revolution.UI.Adapters.Nes;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record GameWindowResolvedExtraInputBinding(
    int Player,
    Key Key,
    ExtraInputBindingKind Kind,
    IReadOnlyList<string> ActionIds,
    int TurboHz = 10);

internal static class GameWindowInputBindingResolver
{
    public static Dictionary<Key, (int Player, string ActionId)> BuildKeyMap(
        IReadOnlyDictionary<string, Dictionary<string, Key>> inputBindingsByPort)
    {
        var map = new Dictionary<Key, (int Player, string ActionId)>();
        foreach (var (portId, bindings) in inputBindingsByPort)
        {
            var normalizedPortId = RemoteControlPorts.NormalizePortId(portId);
            if (normalizedPortId == null || !RemoteControlPorts.TryGetPlayer(normalizedPortId, out var player))
                continue;

            foreach (var (actionId, key) in bindings)
            {
                if (!NesInputAdapter.TryNormalizeControllerAction(actionId, out var normalizedActionId))
                    continue;

                map[key] = (player, normalizedActionId);
            }
        }

        return map;
    }

    public static IReadOnlyList<GameWindowResolvedExtraInputBinding> ResolveExtraInputBindings(
        IReadOnlyList<ExtraInputBindingProfile>? profiles)
    {
        return (profiles ?? Array.Empty<ExtraInputBindingProfile>())
            .Select(profile =>
            {
                if (!Enum.TryParse<Key>(profile.Key, out var key) || key == Key.None)
                    return null;

                var kind = Enum.TryParse<ExtraInputBindingKind>(profile.Kind, out var parsedKind)
                    ? parsedKind
                    : ExtraInputBindingKind.Turbo;
                var actionIds = (profile.Buttons ?? [])
                    .Select(actionId => NesInputAdapter.TryNormalizeControllerAction(actionId, out var normalizedActionId)
                        ? normalizedActionId
                        : null)
                    .Where(static actionId => actionId != null)
                    .Select(static actionId => actionId!)
                    .Distinct()
                    .ToList();
                if (actionIds.Count == 0 || (kind == ExtraInputBindingKind.Combo && actionIds.Count < 2))
                    return null;

                return new GameWindowResolvedExtraInputBinding(
                    profile.Player,
                    key,
                    kind,
                    actionIds,
                    Math.Clamp(profile.TurboHz <= 0 ? 10 : profile.TurboHz, 1, 30));
            })
            .Where(binding => binding != null)
            .Select(binding => binding!)
            .ToList();
    }

    public static HashSet<Key> BuildHandledKeys(
        IReadOnlyDictionary<Key, (int Player, string ActionId)> keyMap,
        IReadOnlyList<GameWindowResolvedExtraInputBinding> extraInputBindings)
    {
        var keys = keyMap.Keys.ToHashSet();
        foreach (var binding in extraInputBindings)
            keys.Add(binding.Key);

        return keys;
    }
}

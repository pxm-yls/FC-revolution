using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record GameWindowResolvedExtraInputBinding(
    string PortId,
    Key Key,
    ExtraInputBindingKind Kind,
    IReadOnlyList<string> ActionIds,
    int TurboHz = 10);

internal static class GameWindowInputBindingResolver
{
    public static Dictionary<Key, (string PortId, string ActionId)> BuildKeyMap(
        IReadOnlyDictionary<string, Dictionary<string, Key>> inputBindingsByPort,
        CoreInputBindingSchema inputBindingSchema)
    {
        ArgumentNullException.ThrowIfNull(inputBindingsByPort);
        ArgumentNullException.ThrowIfNull(inputBindingSchema);

        var map = new Dictionary<Key, (string PortId, string ActionId)>();
        foreach (var (portId, bindings) in inputBindingsByPort)
        {
            if (!inputBindingSchema.TryNormalizePortId(portId, out var normalizedPortId))
                continue;

            foreach (var (actionId, key) in bindings)
            {
                if (!inputBindingSchema.TryNormalizeActionId(normalizedPortId, actionId, out var normalizedActionId))
                    continue;

                map[key] = (normalizedPortId, normalizedActionId);
            }
        }

        return map;
    }

    public static Dictionary<Key, (string PortId, string ActionId)> BuildKeyMap(
        IReadOnlyDictionary<string, Dictionary<string, Key>> inputBindingsByPort) =>
        BuildKeyMap(inputBindingsByPort, CoreInputBindingSchema.CreateFallback());

    public static IReadOnlyList<GameWindowResolvedExtraInputBinding> ResolveExtraInputBindings(
        IReadOnlyList<ExtraInputBindingProfile>? profiles,
        CoreInputBindingSchema inputBindingSchema)
    {
        ArgumentNullException.ThrowIfNull(inputBindingSchema);

        return (profiles ?? Array.Empty<ExtraInputBindingProfile>())
            .Select(profile =>
            {
                if (!Enum.TryParse<Key>(profile.Key, out var key) || key == Key.None)
                    return null;

                if (!TryResolveProfilePort(profile, inputBindingSchema, out var portId))
                    return null;

                var kind = Enum.TryParse<ExtraInputBindingKind>(profile.Kind, out var parsedKind)
                    ? parsedKind
                    : ExtraInputBindingKind.Turbo;
                var actionIds = (profile.Buttons ?? [])
                    .Select(actionId => inputBindingSchema.TryNormalizeActionId(portId, actionId, out var normalizedActionId)
                        ? normalizedActionId
                        : null)
                    .Where(static actionId => actionId != null)
                    .Select(static actionId => actionId!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (actionIds.Count == 0 || (kind == ExtraInputBindingKind.Combo && actionIds.Count < 2))
                    return null;

                return new GameWindowResolvedExtraInputBinding(
                    portId,
                    key,
                    kind,
                    actionIds,
                    Math.Clamp(profile.TurboHz <= 0 ? 10 : profile.TurboHz, 1, 30));
            })
            .Where(binding => binding != null)
            .Select(binding => binding!)
            .ToList();
    }

    public static IReadOnlyList<GameWindowResolvedExtraInputBinding> ResolveExtraInputBindings(
        IReadOnlyList<ExtraInputBindingProfile>? profiles) =>
        ResolveExtraInputBindings(profiles, CoreInputBindingSchema.CreateFallback());

    public static HashSet<Key> BuildHandledKeys(
        IReadOnlyDictionary<Key, (string PortId, string ActionId)> keyMap,
        IReadOnlyList<GameWindowResolvedExtraInputBinding> extraInputBindings)
    {
        var keys = keyMap.Keys.ToHashSet();
        foreach (var binding in extraInputBindings)
            keys.Add(binding.Key);

        return keys;
    }

    private static bool TryResolveProfilePort(
        ExtraInputBindingProfile profile,
        CoreInputBindingSchema inputBindingSchema,
        out string portId)
    {
        if (inputBindingSchema.TryNormalizePortId(profile.PortId, out portId))
            return true;

        if (profile.Player is 0 or 1)
            return inputBindingSchema.TryNormalizePortId(inputBindingSchema.GetPortId(profile.Player), out portId);

        portId = string.Empty;
        return false;
    }
}

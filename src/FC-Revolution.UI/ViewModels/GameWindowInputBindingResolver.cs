using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Core.Input;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record GameWindowResolvedExtraInputBinding(
    int Player,
    Key Key,
    ExtraInputBindingKind Kind,
    IReadOnlyList<NesButton> Buttons,
    int TurboHz = 10);

internal static class GameWindowInputBindingResolver
{
    public static Dictionary<Key, (int Player, NesButton Button)> BuildKeyMap(
        IReadOnlyDictionary<int, Dictionary<NesButton, Key>> inputMaps)
    {
        var map = new Dictionary<Key, (int Player, NesButton Button)>();
        foreach (var (player, bindings) in inputMaps)
        {
            foreach (var (button, key) in bindings)
                map[key] = (player, button);
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
                var buttons = (profile.Buttons ?? [])
                    .Select(name => Enum.TryParse<NesButton>(name, out var parsedButton) ? parsedButton : (NesButton?)null)
                    .Where(button => button != null)
                    .Select(button => button!.Value)
                    .Distinct()
                    .ToList();
                if (buttons.Count == 0 || (kind == ExtraInputBindingKind.Combo && buttons.Count < 2))
                    return null;

                return new GameWindowResolvedExtraInputBinding(
                    profile.Player,
                    key,
                    kind,
                    buttons,
                    Math.Clamp(profile.TurboHz <= 0 ? 10 : profile.TurboHz, 1, 30));
            })
            .Where(binding => binding != null)
            .Select(binding => binding!)
            .ToList();
    }

    public static HashSet<Key> BuildHandledKeys(
        IReadOnlyDictionary<Key, (int Player, NesButton Button)> keyMap,
        IReadOnlyList<GameWindowResolvedExtraInputBinding> extraInputBindings)
    {
        var keys = keyMap.Keys.ToHashSet();
        foreach (var binding in extraInputBindings)
            keys.Add(binding.Key);

        return keys;
    }
}

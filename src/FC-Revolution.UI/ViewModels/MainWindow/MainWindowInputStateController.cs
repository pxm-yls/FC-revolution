using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FC_Revolution.UI.Adapters.Nes;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record ResolvedExtraInputBinding(
    int Player,
    Key Key,
    ExtraInputBindingKind Kind,
    IReadOnlyList<string> ActionIds);

internal sealed record InputDesiredMasks(byte Player1Mask, byte Player2Mask);

internal sealed record InputMaskTransition(string ActionId, bool DesiredPressed);

internal sealed record TurboPulseDecision(bool NextTurboPulseActive, bool ShouldRefreshActiveInputState);

internal sealed class MainWindowInputStateController
{
    public ResolvedExtraInputBinding? ResolveExtraInputBinding(ExtraInputBindingProfile profile)
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
        if (actionIds.Count == 0)
            return null;

        return new ResolvedExtraInputBinding(profile.Player, key, kind, actionIds);
    }

    public HashSet<Key> BuildEffectiveHandledKeys(
        IReadOnlyDictionary<Key, (int Player, string ActionId)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings)
    {
        var keys = effectiveKeyMap.Keys.ToHashSet();
        foreach (var binding in extraBindings)
            keys.Add(binding.Key);
        return keys;
    }

    public InputDesiredMasks BuildDesiredMasks(
        IReadOnlySet<Key> pressedKeys,
        IReadOnlyDictionary<Key, (int Player, string ActionId)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings,
        bool turboPulseActive)
    {
        byte player1Mask = 0;
        byte player2Mask = 0;
        foreach (var key in pressedKeys)
        {
            if (effectiveKeyMap.TryGetValue(key, out var binding))
            {
                if (!NesInputAdapter.TryGetBitMask(binding.ActionId, out var bitMask))
                    continue;

                if (binding.Player == 0)
                    player1Mask |= bitMask;
                else
                    player2Mask |= bitMask;
            }
        }

        foreach (var binding in extraBindings)
        {
            if (!pressedKeys.Contains(binding.Key))
                continue;

            if (binding.Kind == ExtraInputBindingKind.Turbo && !turboPulseActive)
                continue;

            foreach (var actionId in binding.ActionIds)
            {
                if (!NesInputAdapter.TryGetBitMask(actionId, out var bitMask))
                    continue;

                if (binding.Player == 0)
                    player1Mask |= bitMask;
                else
                    player2Mask |= bitMask;
            }
        }

        return new InputDesiredMasks(player1Mask, player2Mask);
    }

    public IReadOnlyList<InputMaskTransition> BuildMaskTransitions(
        byte desiredMask,
        byte currentMask,
        IReadOnlyList<string> controllerActionIds)
    {
        var transitions = new List<InputMaskTransition>();
        foreach (var actionId in controllerActionIds)
        {
            if (!NesInputAdapter.TryGetBitMask(actionId, out var bit))
                continue;

            var desired = (desiredMask & bit) != 0;
            var current = (currentMask & bit) != 0;
            if (desired == current)
                continue;

            transitions.Add(new InputMaskTransition(actionId, desired));
        }

        return transitions;
    }

    public TurboPulseDecision BuildTurboPulseDecision(
        bool turboPulseActive,
        IReadOnlySet<Key> pressedKeys,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings)
    {
        var hasActiveTurbo = extraBindings.Any(binding =>
            binding.Kind == ExtraInputBindingKind.Turbo &&
            pressedKeys.Contains(binding.Key));
        if (!hasActiveTurbo)
        {
            return turboPulseActive
                ? new TurboPulseDecision(NextTurboPulseActive: false, ShouldRefreshActiveInputState: true)
                : new TurboPulseDecision(NextTurboPulseActive: false, ShouldRefreshActiveInputState: false);
        }

        return new TurboPulseDecision(
            NextTurboPulseActive: !turboPulseActive,
            ShouldRefreshActiveInputState: true);
    }
}

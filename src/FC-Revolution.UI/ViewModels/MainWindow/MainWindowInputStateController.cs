using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record ResolvedExtraInputBinding(
    int Player,
    Key Key,
    ExtraInputBindingKind Kind,
    IReadOnlyList<string> ActionIds);

internal sealed record InputDesiredActions(
    IReadOnlySet<string> Player1Actions,
    IReadOnlySet<string> Player2Actions);

internal sealed record InputDesiredMasks(byte Player1Mask, byte Player2Mask);

internal sealed record InputActionTransition(string ActionId, bool DesiredPressed);

internal sealed record TurboPulseDecision(bool NextTurboPulseActive, bool ShouldRefreshActiveInputState);

internal sealed class MainWindowInputStateController
{
    public ResolvedExtraInputBinding? ResolveExtraInputBinding(
        ExtraInputBindingProfile profile,
        CoreInputBindingSchema inputBindingSchema)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(inputBindingSchema);

        if (!Enum.TryParse<Key>(profile.Key, out var key) || key == Key.None)
            return null;

        var kind = Enum.TryParse<ExtraInputBindingKind>(profile.Kind, out var parsedKind)
            ? parsedKind
            : ExtraInputBindingKind.Turbo;
        var actionIds = (profile.Buttons ?? [])
            .Select(actionId => inputBindingSchema.TryNormalizeActionId(profile.Player, actionId, out var normalizedActionId)
                ? normalizedActionId
                : null)
            .Where(static actionId => actionId != null)
            .Select(static actionId => actionId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (actionIds.Count == 0)
            return null;

        return new ResolvedExtraInputBinding(profile.Player, key, kind, actionIds);
    }

    public ResolvedExtraInputBinding? ResolveExtraInputBinding(ExtraInputBindingProfile profile) =>
        ResolveExtraInputBinding(profile, CoreInputBindingSchema.CreateFallback());

    public HashSet<Key> BuildEffectiveHandledKeys(
        IReadOnlyDictionary<Key, (int Player, string ActionId)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings)
    {
        var keys = effectiveKeyMap.Keys.ToHashSet();
        foreach (var binding in extraBindings)
            keys.Add(binding.Key);
        return keys;
    }

    public InputDesiredActions BuildDesiredActions(
        IReadOnlySet<Key> pressedKeys,
        IReadOnlyDictionary<Key, (int Player, string ActionId)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings,
        bool turboPulseActive)
    {
        HashSet<string> player1Actions = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> player2Actions = new(StringComparer.OrdinalIgnoreCase);
        foreach (var key in pressedKeys)
        {
            if (!effectiveKeyMap.TryGetValue(key, out var binding))
                continue;

            GetTargetActions(binding.Player, player1Actions, player2Actions).Add(binding.ActionId);
        }

        foreach (var binding in extraBindings)
        {
            if (!pressedKeys.Contains(binding.Key))
                continue;

            if (binding.Kind == ExtraInputBindingKind.Turbo && !turboPulseActive)
                continue;

            var targetActions = GetTargetActions(binding.Player, player1Actions, player2Actions);
            foreach (var actionId in binding.ActionIds)
                targetActions.Add(actionId);
        }

        return new InputDesiredActions(player1Actions, player2Actions);
    }

    public InputDesiredMasks BuildDesiredMasks(
        IReadOnlySet<Key> pressedKeys,
        IReadOnlyDictionary<Key, (int Player, string ActionId)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings,
        bool turboPulseActive)
    {
        var inputBindingSchema = CoreInputBindingSchema.CreateFallback();
        var desiredActions = BuildDesiredActions(pressedKeys, effectiveKeyMap, extraBindings, turboPulseActive);
        return new InputDesiredMasks(
            BuildLegacyMask(0, desiredActions.Player1Actions, inputBindingSchema),
            BuildLegacyMask(1, desiredActions.Player2Actions, inputBindingSchema));
    }

    public IReadOnlyList<InputActionTransition> BuildActionTransitions(
        IReadOnlySet<string> desiredActions,
        IReadOnlySet<string> currentActions,
        IReadOnlyList<string> controllerActionIds)
    {
        ArgumentNullException.ThrowIfNull(desiredActions);
        ArgumentNullException.ThrowIfNull(currentActions);
        ArgumentNullException.ThrowIfNull(controllerActionIds);

        var transitions = new List<InputActionTransition>();
        foreach (var actionId in controllerActionIds)
        {
            var desired = desiredActions.Contains(actionId);
            var current = currentActions.Contains(actionId);
            if (desired == current)
                continue;

            transitions.Add(new InputActionTransition(actionId, desired));
        }

        return transitions;
    }

    public IReadOnlyList<InputActionTransition> BuildMaskTransitions(
        byte desiredMask,
        byte currentMask,
        IReadOnlyList<string> controllerActionIds)
    {
        var inputBindingSchema = CoreInputBindingSchema.CreateFallback();
        var desiredActions = BuildActionsFromMask(0, desiredMask, controllerActionIds, inputBindingSchema);
        var currentActions = BuildActionsFromMask(0, currentMask, controllerActionIds, inputBindingSchema);
        return BuildActionTransitions(desiredActions, currentActions, controllerActionIds);
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

    private static IReadOnlySet<string> BuildActionsFromMask(
        int player,
        byte mask,
        IEnumerable<string> actionIds,
        CoreInputBindingSchema inputBindingSchema)
    {
        HashSet<string> actions = new(StringComparer.OrdinalIgnoreCase);
        foreach (var actionId in actionIds)
        {
            if (inputBindingSchema.TryGetLegacyBitMask(player, actionId, out var bit) &&
                (mask & bit) != 0)
            {
                actions.Add(actionId);
            }
        }

        return actions;
    }
}

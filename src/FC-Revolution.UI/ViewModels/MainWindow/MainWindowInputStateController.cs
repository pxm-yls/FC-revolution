using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record ResolvedExtraInputBinding(
    string PortId,
    Key Key,
    ExtraInputBindingKind Kind,
    IReadOnlyList<string> ActionIds);

internal sealed record InputDesiredActions(
    IReadOnlyDictionary<string, IReadOnlySet<string>> ActionsByPort)
{
    public IReadOnlySet<string> GetActions(string portId) =>
        ActionsByPort.TryGetValue(portId, out var actions)
            ? actions
            : EmptyActions;

    private static IReadOnlySet<string> EmptyActions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

internal sealed record InputDesiredMasks(
    IReadOnlyDictionary<string, byte> MasksByPort)
{
    public byte GetMask(string portId) =>
        MasksByPort.TryGetValue(portId, out var mask) ? mask : (byte)0;
}

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
        if (actionIds.Count == 0)
            return null;

        return new ResolvedExtraInputBinding(portId, key, kind, actionIds);
    }

    public ResolvedExtraInputBinding? ResolveExtraInputBinding(ExtraInputBindingProfile profile) =>
        ResolveExtraInputBinding(profile, CoreInputBindingSchema.CreateFallback());

    public HashSet<Key> BuildEffectiveHandledKeys(
        IReadOnlyDictionary<Key, (string PortId, string ActionId)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings)
    {
        var keys = effectiveKeyMap.Keys.ToHashSet();
        foreach (var binding in extraBindings)
            keys.Add(binding.Key);
        return keys;
    }

    public InputDesiredActions BuildDesiredActions(
        IReadOnlySet<Key> pressedKeys,
        IReadOnlyDictionary<Key, (string PortId, string ActionId)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings,
        bool turboPulseActive)
    {
        var actionsByPort = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in pressedKeys)
        {
            if (!effectiveKeyMap.TryGetValue(key, out var binding))
                continue;

            GetTargetActions(binding.PortId, actionsByPort).Add(binding.ActionId);
        }

        foreach (var binding in extraBindings)
        {
            if (!pressedKeys.Contains(binding.Key))
                continue;

            if (binding.Kind == ExtraInputBindingKind.Turbo && !turboPulseActive)
                continue;

            var targetActions = GetTargetActions(binding.PortId, actionsByPort);
            foreach (var actionId in binding.ActionIds)
                targetActions.Add(actionId);
        }

        return new InputDesiredActions(actionsByPort.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlySet<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase));
    }

    public InputDesiredMasks BuildDesiredMasks(
        IReadOnlySet<Key> pressedKeys,
        IReadOnlyDictionary<Key, (string PortId, string ActionId)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings,
        bool turboPulseActive,
        CoreInputBindingSchema inputBindingSchema)
    {
        var desiredActions = BuildDesiredActions(pressedKeys, effectiveKeyMap, extraBindings, turboPulseActive);
        return new InputDesiredMasks(
            inputBindingSchema.GetSupportedPorts().ToDictionary(
                port => port.PortId,
                port => BuildLegacyMask(port.PortId, desiredActions.GetActions(port.PortId), inputBindingSchema),
                StringComparer.OrdinalIgnoreCase));
    }

    public InputDesiredMasks BuildDesiredMasks(
        IReadOnlySet<Key> pressedKeys,
        IReadOnlyDictionary<Key, (string PortId, string ActionId)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings,
        bool turboPulseActive) =>
        BuildDesiredMasks(
            pressedKeys,
            effectiveKeyMap,
            extraBindings,
            turboPulseActive,
            CoreInputBindingSchema.CreateFallback());

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
        string portId,
        IDictionary<string, HashSet<string>> actionsByPort)
    {
        if (!actionsByPort.TryGetValue(portId, out var actions))
        {
            actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            actionsByPort[portId] = actions;
        }

        return actions;
    }

    private static byte BuildLegacyMask(string portId, IEnumerable<string> actionIds, CoreInputBindingSchema inputBindingSchema)
    {
        byte mask = 0;
        foreach (var actionId in actionIds)
        {
            if (inputBindingSchema.TryGetLegacyBitMask(portId, actionId, out var bit))
                mask |= bit;
        }

        return mask;
    }

    private static bool TryResolveProfilePort(
        ExtraInputBindingProfile profile,
        CoreInputBindingSchema inputBindingSchema,
        out string portId)
    {
        if (inputBindingSchema.TryNormalizePortId(profile.PortId, out portId))
            return true;

        if (profile.Player >= 0)
            return inputBindingSchema.TryGetPortId(profile.Player, out portId);

        portId = string.Empty;
        return false;
    }
}

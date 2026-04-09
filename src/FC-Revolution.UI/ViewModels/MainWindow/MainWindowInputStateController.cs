using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Core.Input;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record ResolvedExtraInputBinding(
    int Player,
    Key Key,
    ExtraInputBindingKind Kind,
    IReadOnlyList<NesButton> Buttons);

internal sealed record InputDesiredMasks(byte Player1Mask, byte Player2Mask);

internal sealed record InputMaskTransition(NesButton Button, bool DesiredPressed);

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
        var buttons = (profile.Buttons ?? [])
            .Select(buttonName => Enum.TryParse<NesButton>(buttonName, out var button) ? button : (NesButton?)null)
            .Where(button => button != null)
            .Select(button => button!.Value)
            .Distinct()
            .ToList();
        if (buttons.Count == 0)
            return null;

        return new ResolvedExtraInputBinding(profile.Player, key, kind, buttons);
    }

    public HashSet<Key> BuildEffectiveHandledKeys(
        IReadOnlyDictionary<Key, (int Player, NesButton Button)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings)
    {
        var keys = effectiveKeyMap.Keys.ToHashSet();
        foreach (var binding in extraBindings)
            keys.Add(binding.Key);
        return keys;
    }

    public InputDesiredMasks BuildDesiredMasks(
        IReadOnlySet<Key> pressedKeys,
        IReadOnlyDictionary<Key, (int Player, NesButton Button)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> extraBindings,
        bool turboPulseActive)
    {
        byte player1Mask = 0;
        byte player2Mask = 0;
        foreach (var key in pressedKeys)
        {
            if (effectiveKeyMap.TryGetValue(key, out var binding))
            {
                if (binding.Player == 0)
                    player1Mask |= (byte)binding.Button;
                else
                    player2Mask |= (byte)binding.Button;
            }
        }

        foreach (var binding in extraBindings)
        {
            if (!pressedKeys.Contains(binding.Key))
                continue;

            if (binding.Kind == ExtraInputBindingKind.Turbo && !turboPulseActive)
                continue;

            foreach (var button in binding.Buttons)
            {
                if (binding.Player == 0)
                    player1Mask |= (byte)button;
                else
                    player2Mask |= (byte)button;
            }
        }

        return new InputDesiredMasks(player1Mask, player2Mask);
    }

    public IReadOnlyList<InputMaskTransition> BuildMaskTransitions(
        byte desiredMask,
        byte currentMask,
        IReadOnlyList<NesButton> controllerButtons)
    {
        var transitions = new List<InputMaskTransition>();
        foreach (var button in controllerButtons)
        {
            var bit = (byte)button;
            var desired = (desiredMask & bit) != 0;
            var current = (currentMask & bit) != 0;
            if (desired == current)
                continue;

            transitions.Add(new InputMaskTransition(button, desired));
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

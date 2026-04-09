using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Core.Input;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record MainWindowActiveInputLegacyMirror(
    IReadOnlyList<Key> PressedKeys,
    bool TurboPulseActive);

internal sealed record MainWindowActiveInputRefreshDecision(
    MainWindowActiveInputPlan ApplyPlan,
    MainWindowActiveInputLegacyMirror LegacyMirror);

internal sealed record MainWindowActiveInputRefreshResult(
    MainWindowActiveInputLegacyMirror LegacyMirrorBeforeApply,
    MainWindowActiveInputLegacyMirror LegacyMirrorAfterApply);

internal sealed record MainWindowActiveInputTurboPulseDecision(
    bool ShouldRefreshActiveInputState,
    MainWindowActiveInputLegacyMirror LegacyMirror);

internal sealed class MainWindowActiveInputWorkflowController
{
    private static readonly IReadOnlyDictionary<Key, (int Player, NesButton Button)> EmptyKeyMap =
        new Dictionary<Key, (int Player, NesButton Button)>();
    private static readonly IReadOnlyList<ResolvedExtraInputBinding> EmptyExtraBindings = [];

    public string? GetActiveInputRomPath(bool isRomLoaded, string? loadedRomPath, RomLibraryItem? currentRom)
        => isRomLoaded ? loadedRomPath : currentRom?.Path;

    public MainWindowActiveInputRefreshDecision BuildRefreshDecision(
        MainWindowActiveInputRuntimeController runtimeController,
        MainWindowInputStateController inputStateController,
        bool isRomLoaded,
        string? activeRomPath,
        IReadOnlyDictionary<Key, (int Player, NesButton Button)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> effectiveExtraBindings,
        byte player1CurrentMask,
        byte player2CurrentMask,
        IReadOnlyList<NesButton> controllerButtons,
        Func<int, string> getInputPortId,
        Func<NesButton, string> getInputActionId)
    {
        ArgumentNullException.ThrowIfNull(runtimeController);
        ArgumentNullException.ThrowIfNull(inputStateController);
        ArgumentNullException.ThrowIfNull(effectiveKeyMap);
        ArgumentNullException.ThrowIfNull(effectiveExtraBindings);
        ArgumentNullException.ThrowIfNull(controllerButtons);
        ArgumentNullException.ThrowIfNull(getInputPortId);
        ArgumentNullException.ThrowIfNull(getInputActionId);

        runtimeController.RefreshContext(isRomLoaded, activeRomPath);
        var applyPlan = runtimeController.BuildApplyPlan(
            inputStateController,
            isRomLoaded ? effectiveKeyMap : EmptyKeyMap,
            isRomLoaded ? effectiveExtraBindings : EmptyExtraBindings,
            player1CurrentMask,
            player2CurrentMask,
            controllerButtons,
            getInputPortId,
            getInputActionId);

        return new MainWindowActiveInputRefreshDecision(
            applyPlan,
            BuildLegacyMirror(runtimeController));
    }

    public MainWindowActiveInputLegacyMirror ApplyRefreshDecision(
        MainWindowActiveInputRuntimeController runtimeController,
        MainWindowActiveInputRefreshDecision decision,
        object inputSyncRoot,
        ICoreInputStateWriter inputStateWriter,
        Action<int, NesButton, bool> updateInputMask)
    {
        ArgumentNullException.ThrowIfNull(runtimeController);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(inputSyncRoot);
        ArgumentNullException.ThrowIfNull(inputStateWriter);
        ArgumentNullException.ThrowIfNull(updateInputMask);

        runtimeController.ApplyPlan(
            decision.ApplyPlan,
            inputSyncRoot,
            inputStateWriter,
            updateInputMask);
        return BuildLegacyMirror(runtimeController);
    }

    public MainWindowActiveInputRefreshResult RefreshActiveInputState(
        MainWindowActiveInputRuntimeController runtimeController,
        MainWindowInputStateController inputStateController,
        bool isRomLoaded,
        string? activeRomPath,
        IReadOnlyDictionary<Key, (int Player, NesButton Button)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> effectiveExtraBindings,
        byte player1CurrentMask,
        byte player2CurrentMask,
        IReadOnlyList<NesButton> controllerButtons,
        Func<int, string> getInputPortId,
        Func<NesButton, string> getInputActionId,
        object inputSyncRoot,
        ICoreInputStateWriter inputStateWriter,
        Action<int, NesButton, bool> updateInputMask)
    {
        var refreshDecision = BuildRefreshDecision(
            runtimeController,
            inputStateController,
            isRomLoaded,
            activeRomPath,
            effectiveKeyMap,
            effectiveExtraBindings,
            player1CurrentMask,
            player2CurrentMask,
            controllerButtons,
            getInputPortId,
            getInputActionId);
        var updatedMirror = ApplyRefreshDecision(
            runtimeController,
            refreshDecision,
            inputSyncRoot,
            inputStateWriter,
            updateInputMask);
        return new MainWindowActiveInputRefreshResult(refreshDecision.LegacyMirror, updatedMirror);
    }

    public MainWindowActiveInputTurboPulseDecision UpdateTurboPulse(
        MainWindowActiveInputRuntimeController runtimeController,
        MainWindowInputStateController inputStateController,
        IReadOnlyList<ResolvedExtraInputBinding> effectiveExtraBindings)
    {
        ArgumentNullException.ThrowIfNull(runtimeController);
        ArgumentNullException.ThrowIfNull(inputStateController);
        ArgumentNullException.ThrowIfNull(effectiveExtraBindings);

        var shouldRefreshActiveInputState = runtimeController.UpdateTurboPulse(
            inputStateController,
            effectiveExtraBindings);
        return new MainWindowActiveInputTurboPulseDecision(
            shouldRefreshActiveInputState,
            BuildLegacyMirror(runtimeController));
    }

    public MainWindowActiveInputLegacyMirror BuildLegacyMirror(MainWindowActiveInputRuntimeController runtimeController)
    {
        ArgumentNullException.ThrowIfNull(runtimeController);

        return new MainWindowActiveInputLegacyMirror(
            runtimeController.PressedKeys.ToList(),
            runtimeController.TurboPulseActive);
    }
}

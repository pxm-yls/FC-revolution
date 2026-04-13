using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Infrastructure;
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
    private static readonly IReadOnlyDictionary<Key, (int Player, string ActionId)> EmptyKeyMap =
        new Dictionary<Key, (int Player, string ActionId)>();
    private static readonly IReadOnlyList<ResolvedExtraInputBinding> EmptyExtraBindings = [];

    public string? GetActiveInputRomPath(bool isRomLoaded, string? loadedRomPath, RomLibraryItem? currentRom)
        => isRomLoaded ? loadedRomPath : currentRom?.Path;

    public MainWindowActiveInputRefreshDecision BuildRefreshDecision(
        MainWindowActiveInputRuntimeController runtimeController,
        MainWindowInputStateController inputStateController,
        bool isRomLoaded,
        string? activeRomPath,
        IReadOnlyDictionary<Key, (int Player, string ActionId)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> effectiveExtraBindings,
        CoreInputBindingSchema inputBindingSchema)
    {
        ArgumentNullException.ThrowIfNull(runtimeController);
        ArgumentNullException.ThrowIfNull(inputStateController);
        ArgumentNullException.ThrowIfNull(effectiveKeyMap);
        ArgumentNullException.ThrowIfNull(effectiveExtraBindings);
        ArgumentNullException.ThrowIfNull(inputBindingSchema);

        runtimeController.RefreshContext(isRomLoaded, activeRomPath);
        var applyPlan = runtimeController.BuildApplyPlan(
            inputStateController,
            isRomLoaded ? effectiveKeyMap : EmptyKeyMap,
            isRomLoaded ? effectiveExtraBindings : EmptyExtraBindings,
            inputBindingSchema);

        return new MainWindowActiveInputRefreshDecision(
            applyPlan,
            BuildLegacyMirror(runtimeController));
    }

    public MainWindowActiveInputRefreshDecision BuildRefreshDecision(
        MainWindowActiveInputRuntimeController runtimeController,
        MainWindowInputStateController inputStateController,
        bool isRomLoaded,
        string? activeRomPath,
        IReadOnlyDictionary<Key, (int Player, string ActionId)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> effectiveExtraBindings,
        byte player1CurrentMask,
        byte player2CurrentMask,
        IReadOnlyList<string> controllerActionIds,
        Func<int, string> getInputPortId)
    {
        ArgumentNullException.ThrowIfNull(runtimeController);
        ArgumentNullException.ThrowIfNull(inputStateController);
        ArgumentNullException.ThrowIfNull(effectiveKeyMap);
        ArgumentNullException.ThrowIfNull(effectiveExtraBindings);
        ArgumentNullException.ThrowIfNull(controllerActionIds);
        ArgumentNullException.ThrowIfNull(getInputPortId);

        runtimeController.RefreshContext(isRomLoaded, activeRomPath);
        var applyPlan = runtimeController.BuildApplyPlan(
            inputStateController,
            isRomLoaded ? effectiveKeyMap : EmptyKeyMap,
            isRomLoaded ? effectiveExtraBindings : EmptyExtraBindings,
            player1CurrentMask,
            player2CurrentMask,
            controllerActionIds,
            getInputPortId);
        return new MainWindowActiveInputRefreshDecision(
            applyPlan,
            BuildLegacyMirror(runtimeController));
    }

    public MainWindowActiveInputLegacyMirror ApplyRefreshDecision(
        MainWindowActiveInputRuntimeController runtimeController,
        MainWindowActiveInputRefreshDecision decision,
        object inputSyncRoot,
        ICoreInputStateWriter inputStateWriter,
        Action<int, string, bool> updateInputMask)
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
        IReadOnlyDictionary<Key, (int Player, string ActionId)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> effectiveExtraBindings,
        CoreInputBindingSchema inputBindingSchema,
        object inputSyncRoot,
        ICoreInputStateWriter inputStateWriter,
        Action<int, string, bool> updateInputMask)
    {
        var refreshDecision = BuildRefreshDecision(
            runtimeController,
            inputStateController,
            isRomLoaded,
            activeRomPath,
            effectiveKeyMap,
            effectiveExtraBindings,
            inputBindingSchema);
        var updatedMirror = ApplyRefreshDecision(
            runtimeController,
            refreshDecision,
            inputSyncRoot,
            inputStateWriter,
            updateInputMask);
        return new MainWindowActiveInputRefreshResult(refreshDecision.LegacyMirror, updatedMirror);
    }

    public MainWindowActiveInputRefreshResult RefreshActiveInputState(
        MainWindowActiveInputRuntimeController runtimeController,
        MainWindowInputStateController inputStateController,
        bool isRomLoaded,
        string? activeRomPath,
        IReadOnlyDictionary<Key, (int Player, string ActionId)> effectiveKeyMap,
        IReadOnlyList<ResolvedExtraInputBinding> effectiveExtraBindings,
        byte player1CurrentMask,
        byte player2CurrentMask,
        IReadOnlyList<string> controllerActionIds,
        Func<int, string> getInputPortId,
        object inputSyncRoot,
        ICoreInputStateWriter inputStateWriter,
        Action<int, string, bool> updateInputMask)
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
            controllerActionIds,
            getInputPortId);
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

using System;
using System.Collections.Generic;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowInputStateChange(
    int Player,
    string ActionId,
    bool Pressed);

internal readonly record struct GameWindowInputStateSnapshot(
    byte Player1CombinedMask,
    byte Player2CombinedMask,
    byte Player1LocalMask,
    byte Player2LocalMask,
    byte Player1RemoteMask,
    byte Player2RemoteMask);

internal sealed class GameWindowInputStateController
{
    private readonly CoreInputBindingSchema _inputBindingSchema;
    private readonly HashSet<string> _player1CombinedActions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _player2CombinedActions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _player1LocalActions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _player2LocalActions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _player1RemoteActions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _player2RemoteActions = new(StringComparer.OrdinalIgnoreCase);

    public GameWindowInputStateController(CoreInputBindingSchema inputBindingSchema)
    {
        _inputBindingSchema = inputBindingSchema;
    }

    public GameWindowInputStateController()
        : this(CoreInputBindingSchema.CreateFallback())
    {
    }

    public GameWindowInputStateSnapshot Snapshot => new(
        GetCombinedMask(0),
        GetCombinedMask(1),
        GetLocalMask(0),
        GetLocalMask(1),
        GetRemoteMask(0),
        GetRemoteMask(1));

    public byte GetCombinedMask(int player) => BuildLegacyMask(player, GetCombinedActions(player));

    public IReadOnlyList<GameWindowInputStateChange> ApplyDesiredLocalInputActionsForPlayer(
        int player,
        IReadOnlySet<string> desiredActions,
        bool allowLocalInput)
    {
        if (!IsSupportedPlayer(player))
            return Array.Empty<GameWindowInputStateChange>();

        List<GameWindowInputStateChange> changes = [];
        var localActions = GetLocalActions(player);
        foreach (var actionId in _inputBindingSchema.GetBindableActionIds(player))
        {
            var desired = allowLocalInput && desiredActions.Contains(actionId);
            if (desired)
                localActions.Add(actionId);
            else
                localActions.Remove(actionId);

            ApplyCombinedActionState(player, actionId, allowLocalInput, changes);
        }

        return changes;
    }

    public IReadOnlyList<GameWindowInputStateChange> ApplyDesiredLocalInputMaskForPlayer(
        int player,
        byte desiredMask,
        bool allowLocalInput)
    {
        HashSet<string> desiredActions = new(StringComparer.OrdinalIgnoreCase);
        foreach (var actionId in _inputBindingSchema.GetBindableActionIds(player))
        {
            if (_inputBindingSchema.TryGetLegacyBitMask(player, actionId, out var bit) &&
                (desiredMask & bit) != 0)
            {
                desiredActions.Add(actionId);
            }
        }

        return ApplyDesiredLocalInputActionsForPlayer(player, desiredActions, allowLocalInput);
    }

    public IReadOnlyList<GameWindowInputStateChange> SetRemoteActionState(
        int player,
        string actionId,
        bool pressed,
        bool allowLocalInput)
    {
        if (!IsSupportedPlayer(player))
            return Array.Empty<GameWindowInputStateChange>();

        List<GameWindowInputStateChange> changes = [];
        var remoteActions = GetRemoteActions(player);
        if (pressed)
            remoteActions.Add(actionId);
        else
            remoteActions.Remove(actionId);

        ApplyCombinedActionState(player, actionId, allowLocalInput, changes);
        return changes;
    }

    public IReadOnlyList<GameWindowInputStateChange> ClearRemoteButtons(int player, bool allowLocalInput)
    {
        if (!IsSupportedPlayer(player))
            return Array.Empty<GameWindowInputStateChange>();

        List<GameWindowInputStateChange> changes = [];
        var remoteActions = GetRemoteActions(player);
        remoteActions.Clear();
        foreach (var actionId in _inputBindingSchema.GetBindableActionIds(player))
            ApplyCombinedActionState(player, actionId, allowLocalInput, changes);

        return changes;
    }

    public IReadOnlyList<GameWindowInputStateChange> RebuildCombinedStateForPlayer(int player, bool allowLocalInput)
    {
        if (!IsSupportedPlayer(player))
            return Array.Empty<GameWindowInputStateChange>();

        List<GameWindowInputStateChange> changes = [];
        foreach (var actionId in _inputBindingSchema.GetBindableActionIds(player))
            ApplyCombinedActionState(player, actionId, allowLocalInput, changes);
        return changes;
    }

    private void ApplyCombinedActionState(
        int player,
        string actionId,
        bool allowLocalInput,
        List<GameWindowInputStateChange> changes)
    {
        var localActions = GetLocalActions(player);
        var remoteActions = GetRemoteActions(player);
        var combinedActions = GetCombinedActions(player);
        var desired = (allowLocalInput && localActions.Contains(actionId)) || remoteActions.Contains(actionId);
        var current = combinedActions.Contains(actionId);
        if (desired == current)
            return;

        if (desired)
            combinedActions.Add(actionId);
        else
            combinedActions.Remove(actionId);

        changes.Add(new GameWindowInputStateChange(player, actionId, desired));
    }

    private static bool IsSupportedPlayer(int player) => player is 0 or 1;

    private byte GetLocalMask(int player) => BuildLegacyMask(player, GetLocalActions(player));

    private byte GetRemoteMask(int player) => BuildLegacyMask(player, GetRemoteActions(player));

    private byte BuildLegacyMask(int player, IEnumerable<string> actionIds)
    {
        byte mask = 0;
        foreach (var actionId in actionIds)
        {
            if (_inputBindingSchema.TryGetLegacyBitMask(player, actionId, out var bit))
                mask |= bit;
        }

        return mask;
    }

    private HashSet<string> GetCombinedActions(int player) => player == 0 ? _player1CombinedActions : _player2CombinedActions;

    private HashSet<string> GetLocalActions(int player) => player == 0 ? _player1LocalActions : _player2LocalActions;

    private HashSet<string> GetRemoteActions(int player) => player == 0 ? _player1RemoteActions : _player2RemoteActions;
}

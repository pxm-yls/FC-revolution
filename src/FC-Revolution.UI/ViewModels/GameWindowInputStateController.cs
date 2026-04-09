using System;
using System.Collections.Generic;
using System.Linq;
using FC_Revolution.UI.Adapters.Nes;

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
    private static readonly IReadOnlyList<string> ControllerActionIds =
        NesInputAdapter.GetControllerActions()
            .Select(action => action.ActionId)
            .ToArray();

    private byte _player1CombinedMask;
    private byte _player2CombinedMask;
    private byte _player1LocalMask;
    private byte _player2LocalMask;
    private byte _player1RemoteMask;
    private byte _player2RemoteMask;

    public GameWindowInputStateSnapshot Snapshot => new(
        _player1CombinedMask,
        _player2CombinedMask,
        _player1LocalMask,
        _player2LocalMask,
        _player1RemoteMask,
        _player2RemoteMask);

    public byte GetCombinedMask(int player) => player == 0 ? _player1CombinedMask : _player2CombinedMask;

    public IReadOnlyList<GameWindowInputStateChange> ApplyDesiredLocalInputMaskForPlayer(
        int player,
        byte desiredMask,
        bool allowLocalInput)
    {
        if (!IsSupportedPlayer(player))
            return Array.Empty<GameWindowInputStateChange>();

        List<GameWindowInputStateChange> changes = [];
        foreach (var actionId in ControllerActionIds)
        {
            if (!NesInputAdapter.TryGetBitMask(actionId, out var bit))
                continue;

            var currentMask = GetLocalMask(player);
            var desired = allowLocalInput && (desiredMask & bit) != 0;
            var current = (currentMask & bit) != 0;
            if (desired != current)
                SetLocalMask(player, bit, desired);

            ApplyCombinedActionState(player, actionId, allowLocalInput, changes);
        }

        return changes;
    }

    public IReadOnlyList<GameWindowInputStateChange> SetRemoteActionState(
        int player,
        string actionId,
        bool pressed,
        bool allowLocalInput)
    {
        if (!IsSupportedPlayer(player))
            return Array.Empty<GameWindowInputStateChange>();

        if (!NesInputAdapter.TryGetBitMask(actionId, out var bit))
            return Array.Empty<GameWindowInputStateChange>();

        List<GameWindowInputStateChange> changes = [];
        SetRemoteMask(player, bit, pressed);
        ApplyCombinedActionState(player, actionId, allowLocalInput, changes);
        return changes;
    }

    public IReadOnlyList<GameWindowInputStateChange> ClearRemoteButtons(int player, bool allowLocalInput)
    {
        if (!IsSupportedPlayer(player))
            return Array.Empty<GameWindowInputStateChange>();

        List<GameWindowInputStateChange> changes = [];
        foreach (var actionId in ControllerActionIds)
        {
            if (!NesInputAdapter.TryGetBitMask(actionId, out var bit))
                continue;

            SetRemoteMask(player, bit, pressed: false);
            ApplyCombinedActionState(player, actionId, allowLocalInput, changes);
        }

        return changes;
    }

    public IReadOnlyList<GameWindowInputStateChange> RebuildCombinedStateForPlayer(int player, bool allowLocalInput)
    {
        if (!IsSupportedPlayer(player))
            return Array.Empty<GameWindowInputStateChange>();

        List<GameWindowInputStateChange> changes = [];
        foreach (var actionId in ControllerActionIds)
            ApplyCombinedActionState(player, actionId, allowLocalInput, changes);
        return changes;
    }

    private void ApplyCombinedActionState(
        int player,
        string actionId,
        bool allowLocalInput,
        List<GameWindowInputStateChange> changes)
    {
        if (!NesInputAdapter.TryGetBitMask(actionId, out var bit))
            return;

        var localMask = GetLocalMask(player);
        var remoteMask = GetRemoteMask(player);
        var effectiveLocalMask = allowLocalInput ? localMask : (byte)0;
        var desired = ((effectiveLocalMask | remoteMask) & bit) != 0;
        var currentMask = GetCombinedMask(player);
        var current = (currentMask & bit) != 0;
        if (desired == current)
            return;

        SetCombinedMask(player, bit, desired);
        changes.Add(new GameWindowInputStateChange(player, actionId, desired));
    }

    private static bool IsSupportedPlayer(int player) => player is 0 or 1;

    private byte GetLocalMask(int player) => player == 0 ? _player1LocalMask : _player2LocalMask;

    private byte GetRemoteMask(int player) => player == 0 ? _player1RemoteMask : _player2RemoteMask;

    private void SetLocalMask(int player, byte bit, bool pressed)
    {
        if (player == 0)
            _player1LocalMask = pressed ? (byte)(_player1LocalMask | bit) : (byte)(_player1LocalMask & ~bit);
        else
            _player2LocalMask = pressed ? (byte)(_player2LocalMask | bit) : (byte)(_player2LocalMask & ~bit);
    }

    private void SetRemoteMask(int player, byte bit, bool pressed)
    {
        if (player == 0)
            _player1RemoteMask = pressed ? (byte)(_player1RemoteMask | bit) : (byte)(_player1RemoteMask & ~bit);
        else
            _player2RemoteMask = pressed ? (byte)(_player2RemoteMask | bit) : (byte)(_player2RemoteMask & ~bit);
    }

    private void SetCombinedMask(int player, byte bit, bool pressed)
    {
        if (player == 0)
            _player1CombinedMask = pressed ? (byte)(_player1CombinedMask | bit) : (byte)(_player1CombinedMask & ~bit);
        else
            _player2CombinedMask = pressed ? (byte)(_player2CombinedMask | bit) : (byte)(_player2CombinedMask & ~bit);
    }
}

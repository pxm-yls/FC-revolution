using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class GameWindowRemoteControlRuntimeSlotStateController
{
    private readonly IReadOnlyList<string> _supportedPortIds;
    private readonly Dictionary<string, GameWindowRemoteControlRuntimeSlot> _slotsByPortId;

    public GameWindowRemoteControlRuntimeSlotStateController(IReadOnlyList<InputPortDescriptor> supportedPorts)
    {
        _supportedPortIds = supportedPorts
            .Select(static port => port.PortId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _slotsByPortId = _supportedPortIds.ToDictionary(
            static portId => portId,
            static _ => new GameWindowRemoteControlRuntimeSlot(GamePlayerControlSource.Local, null, null, null),
            StringComparer.OrdinalIgnoreCase);
    }

    public GamePlayerControlSource GetPortControlSource(string portId) =>
        _slotsByPortId.TryGetValue(portId, out var slot) ? slot.ControlSource : GamePlayerControlSource.Local;

    public GameWindowRemoteControlRuntimeViewState BuildViewState(GameWindowRemoteControlStateController stateController)
    {
        var slotStates = new ReadOnlyDictionary<string, GameWindowRemoteControlSlotState>(
            _supportedPortIds.ToDictionary(
                static portId => portId,
                portId => ToSlotState(_slotsByPortId[portId]),
                StringComparer.OrdinalIgnoreCase));

        return new GameWindowRemoteControlRuntimeViewState(
            stateController.BuildRemoteControlStatusText(slotStates));
    }

    public bool TryGetSlotState(string portId, out GameWindowRemoteControlSlotState slotState) =>
        TryGetSlot(portId, out var slot, out slotState);

    public void SetRemoteOwner(string portId, string clientIp, string? clientName, DateTime heartbeatUtc) =>
        SetSlot(portId, GamePlayerControlSource.Remote, clientIp, clientName, heartbeatUtc);

    public bool TryReleaseToLocal(string portId, out bool hadRemoteControl)
    {
        if (!TryGetSlot(portId, out var slot, out _))
        {
            hadRemoteControl = false;
            return false;
        }

        hadRemoteControl = slot.ControlSource == GamePlayerControlSource.Remote;
        SetSlot(portId, GamePlayerControlSource.Local, null, null, null);
        return true;
    }

    public bool TrySetHeartbeat(string portId, DateTime heartbeatUtc)
    {
        if (!_slotsByPortId.TryGetValue(portId, out var slot))
            return false;

        _slotsByPortId[portId] = slot with { HeartbeatUtc = heartbeatUtc };
        return true;
    }

    private void SetSlot(
        string portId,
        GamePlayerControlSource controlSource,
        string? clientIp,
        string? clientName,
        DateTime? heartbeatUtc)
    {
        _slotsByPortId[portId] = new GameWindowRemoteControlRuntimeSlot(controlSource, clientIp, clientName, heartbeatUtc);
    }

    private bool TryGetSlot(
        string portId,
        out GameWindowRemoteControlRuntimeSlot slot,
        out GameWindowRemoteControlSlotState slotState)
    {
        if (!_slotsByPortId.TryGetValue(portId, out slot))
        {
            slotState = default;
            return false;
        }

        slotState = ToSlotState(slot);
        return true;
    }

    private static GameWindowRemoteControlSlotState ToSlotState(GameWindowRemoteControlRuntimeSlot slot) =>
        new(slot.ControlSource, slot.ClientIp, slot.ClientName);
}

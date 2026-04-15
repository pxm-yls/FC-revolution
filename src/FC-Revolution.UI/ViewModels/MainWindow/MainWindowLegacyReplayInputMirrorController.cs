using System;
using System.Collections.Generic;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowLegacyReplayInputMirrorController
{
    private readonly Dictionary<string, byte> _masksByPort = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, byte> MasksByPort => _masksByPort;

    public byte GetMask(string portId) =>
        _masksByPort.TryGetValue(portId, out var mask) ? mask : (byte)0;

    public void Reset() => _masksByPort.Clear();

    public void UpdateActionState(
        CoreInputBindingSchema inputBindingSchema,
        string portId,
        string actionId,
        bool pressed)
    {
        ArgumentNullException.ThrowIfNull(inputBindingSchema);

        if (!inputBindingSchema.TryGetLegacyBitMask(portId, actionId, out var bit))
            return;

        var currentMask = _masksByPort.TryGetValue(portId, out var mask) ? mask : (byte)0;
        _masksByPort[portId] = pressed
            ? (byte)(currentMask | bit)
            : (byte)(currentMask & ~bit);
    }
}

namespace FCRevolution.Storage;

/// <summary>Compact per-frame input log for deterministic replay/export compatibility.</summary>
public readonly record struct FrameInputRecord(long Frame, IReadOnlyDictionary<string, byte> ButtonsByPort)
{
    public byte GetButtonsMask(string portId) =>
        ButtonsByPort.TryGetValue(portId, out var mask) ? mask : (byte)0;
}

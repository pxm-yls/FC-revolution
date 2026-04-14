using System.Collections.ObjectModel;

namespace FCRevolution.Storage;

/// <summary>Compact per-frame input log for deterministic replay/export compatibility.</summary>
public readonly record struct FrameInputRecord(long Frame, IReadOnlyDictionary<string, byte> ButtonsByPort)
{
    public FrameInputRecord(long frame, byte player1ButtonsMask, byte player2ButtonsMask)
        : this(frame, CreateLegacyButtonsByPort(player1ButtonsMask, player2ButtonsMask))
    {
    }

    public byte Player1ButtonsMask => GetButtonsMask("p1");

    public byte Player2ButtonsMask => GetButtonsMask("p2");

    public byte GetButtonsMask(string portId) =>
        ButtonsByPort.TryGetValue(portId, out var mask) ? mask : (byte)0;

    private static IReadOnlyDictionary<string, byte> CreateLegacyButtonsByPort(byte player1ButtonsMask, byte player2ButtonsMask) =>
        new ReadOnlyDictionary<string, byte>(
            new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
            {
                ["p1"] = player1ButtonsMask,
                ["p2"] = player2ButtonsMask
            });
}

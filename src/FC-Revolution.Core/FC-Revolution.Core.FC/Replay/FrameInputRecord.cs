namespace FCRevolution.Core.Replay;

/// <summary>Compact per-frame input log for deterministic replay/export.</summary>
public readonly record struct FrameInputRecord(long Frame, byte Player1ButtonsMask, byte Player2ButtonsMask)
{
    public byte GetButtonsMask(string portId) => portId switch
    {
        "p1" => Player1ButtonsMask,
        "p2" => Player2ButtonsMask,
        _ => 0
    };
}

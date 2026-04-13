namespace FCRevolution.Storage;

/// <summary>Compact per-frame input log for deterministic replay/export compatibility.</summary>
public readonly record struct FrameInputRecord(long Frame, byte Player1ButtonsMask, byte Player2ButtonsMask);

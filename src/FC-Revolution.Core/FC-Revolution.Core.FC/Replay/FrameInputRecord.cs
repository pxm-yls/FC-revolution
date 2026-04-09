namespace FCRevolution.Core.Replay;

/// <summary>Compact per-frame input log for deterministic replay/export.</summary>
public readonly record struct FrameInputRecord(long Frame, byte Player1ButtonsMask, byte Player2ButtonsMask);

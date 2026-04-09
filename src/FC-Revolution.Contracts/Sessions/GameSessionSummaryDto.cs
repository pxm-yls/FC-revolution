namespace FCRevolution.Contracts.Sessions;

public sealed record GameSessionSummaryDto(
    Guid SessionId,
    string DisplayName,
    string RomPath,
    string ControlSummary,
    PlayerControlSourceDto Player1ControlSource,
    PlayerControlSourceDto Player2ControlSource);

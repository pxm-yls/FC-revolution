namespace FCRevolution.Contracts.Sessions;

public sealed record GameSessionSummaryDto(
    Guid SessionId,
    string DisplayName,
    string RomPath,
    string ControlSummary,
    IReadOnlyList<GameSessionControlPortDto> ControlPorts);

namespace FCRevolution.Contracts.Sessions;

public sealed record GameSessionControlPortDto(
    string PortId,
    string DisplayName,
    ControlPortSourceDto ControlSource);

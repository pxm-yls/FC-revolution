namespace FCRevolution.Contracts.RemoteControl;

public sealed record StartSessionRequest(string RomPath, string? CoreId = null);

public sealed record StartSessionResponse(Guid SessionId);

public sealed record ClaimControlRequest(
    int? Player = null,
    string ClientIp = "",
    string? ClientName = null,
    string? PortId = null);

public sealed record ReleaseControlRequest(
    int? Player = null,
    string? Reason = null,
    string? PortId = null);

public sealed record RefreshHeartbeatRequest(
    int? Player = null,
    string? PortId = null);

public sealed record ButtonStateRequest(
    int Player = 0,
    NesButtonDto? Button = null,
    bool Pressed = false,
    string? PortId = null,
    string? ActionId = null);

public sealed record InputActionValueDto(string PortId, string DeviceType, string ActionId, float Value);

public sealed record SetInputStateRequest(IReadOnlyList<InputActionValueDto> Actions);

namespace FCRevolution.Contracts.RemoteControl;

public sealed record StartSessionRequest(string RomPath, string? CoreId = null);

public sealed record StartSessionResponse(Guid SessionId);

public sealed record ClaimControlRequest(
    string ClientIp = "",
    string? ClientName = null,
    string? PortId = null);

public sealed record ReleaseControlRequest(
    string? Reason = null,
    string? PortId = null);

public sealed record RefreshHeartbeatRequest(
    string? PortId = null);

public sealed record InputActionValueDto(string PortId, string DeviceType, string ActionId, float Value);

public sealed record SetInputStateRequest(IReadOnlyList<InputActionValueDto> Actions);

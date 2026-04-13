using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Contracts.Services;

namespace FCRevolution.Backend.Hosting.WebSockets;

internal sealed class RemoteControlLeaseCoordinator
{
    private readonly IRemoteControlContract _remoteControlContract;
    private readonly ControlMessageCodec _codec;
    private readonly string _clientIp;
    private readonly CancellationToken _cancellationToken;

    internal RemoteControlLeaseCoordinator(
        IRemoteControlContract remoteControlContract,
        ControlMessageCodec codec,
        string clientIp,
        CancellationToken cancellationToken)
    {
        _remoteControlContract = remoteControlContract;
        _codec = codec;
        _clientIp = clientIp;
        _cancellationToken = cancellationToken;
    }

    internal Guid? ClaimedSessionId { get; private set; }

    internal string? ClaimedPortId { get; private set; }

    internal string? ClientName { get; private set; }

    internal void UpdateClientName(string? clientName)
    {
        if (!string.IsNullOrWhiteSpace(clientName))
            ClientName = clientName;
    }

    internal async Task<(bool Success, bool Changed)> EnsureClaimAsync(Guid sessionId, string portId, string failureMessage)
    {
        var claimChanged =
            !ClaimedSessionId.HasValue ||
            !string.Equals(ClaimedPortId, portId, StringComparison.Ordinal) ||
            ClaimedSessionId.Value != sessionId;

        if (!claimChanged)
            return (true, false);

        if (ClaimedSessionId.HasValue && !string.IsNullOrWhiteSpace(ClaimedPortId))
            await ReleaseClaimAsync("网页控制目标已切换", notifyClient: false);

        var claimOk = await _remoteControlContract.ClaimControlAsync(
            sessionId,
            new ClaimControlRequest(ClientIp: _clientIp, ClientName: ClientName, PortId: portId),
            _cancellationToken);
        if (!claimOk)
        {
            await _codec.SendSocketMessageAsync(
                ControlMessageCodec.CreateSocketMessage("error", failureMessage, sessionId.ToString(), portId));
            return (false, false);
        }

        ClaimedSessionId = sessionId;
        ClaimedPortId = portId;
        return (true, true);
    }

    internal async Task ReleaseClaimAsync(string reason, bool notifyClient)
    {
        if (!ClaimedSessionId.HasValue || string.IsNullOrWhiteSpace(ClaimedPortId))
            return;

        var releaseSessionId = ClaimedSessionId.Value;
        var releasePortId = ClaimedPortId;
        await _remoteControlContract.ReleaseControlAsync(
            releaseSessionId,
            new ReleaseControlRequest(Reason: reason, PortId: releasePortId),
            _cancellationToken);
        ClaimedSessionId = null;
        ClaimedPortId = null;

        if (notifyClient && _codec.IsSocketOpen)
        {
            await _codec.SendSocketMessageAsync(
                ControlMessageCodec.CreateSocketMessage("released", reason, releaseSessionId.ToString(), releasePortId));
        }
    }

    internal async Task ReleaseSpecifiedAsync(Guid sessionId, string portId, string reason)
    {
        await _remoteControlContract.ReleaseControlAsync(
            sessionId,
            new ReleaseControlRequest(Reason: reason, PortId: portId),
            _cancellationToken);

        if (ClaimedSessionId == sessionId &&
            string.Equals(ClaimedPortId, portId, StringComparison.Ordinal))
        {
            ClaimedSessionId = null;
            ClaimedPortId = null;
        }
    }

    internal void InvalidateClaim()
    {
        ClaimedSessionId = null;
        ClaimedPortId = null;
    }

    internal async Task ReleaseOnDisconnectAsync()
    {
        if (!ClaimedSessionId.HasValue || string.IsNullOrWhiteSpace(ClaimedPortId))
            return;

        await _remoteControlContract.ReleaseControlAsync(
            ClaimedSessionId.Value,
            new ReleaseControlRequest(
                Reason: "网页连接已断开，已恢复本地控制",
                PortId: ClaimedPortId),
            CancellationToken.None);
    }
}

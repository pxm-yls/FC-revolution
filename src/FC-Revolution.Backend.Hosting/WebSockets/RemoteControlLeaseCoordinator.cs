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

    internal int? ClaimedPlayer { get; private set; }

    internal string? ClaimedPortId { get; private set; }

    internal string? ClientName { get; private set; }

    internal void UpdateClientName(string? clientName)
    {
        if (!string.IsNullOrWhiteSpace(clientName))
            ClientName = clientName;
    }

    internal async Task<(bool Success, bool Changed)> EnsureClaimAsync(Guid sessionId, string portId, int player, string failureMessage)
    {
        var claimChanged =
            !ClaimedSessionId.HasValue ||
            !ClaimedPlayer.HasValue ||
            !string.Equals(ClaimedPortId, portId, StringComparison.Ordinal) ||
            ClaimedSessionId.Value != sessionId ||
            ClaimedPlayer.Value != player;

        if (!claimChanged)
            return (true, false);

        if (ClaimedSessionId.HasValue && ClaimedPlayer.HasValue)
            await ReleaseClaimAsync("网页控制目标已切换", notifyClient: false);

        var claimOk = await _remoteControlContract.ClaimControlAsync(
            sessionId,
            new ClaimControlRequest(Player: player, ClientIp: _clientIp, ClientName: ClientName, PortId: portId),
            _cancellationToken);
        if (!claimOk)
        {
            await _codec.SendSocketMessageAsync(
                new SocketMessage("error", failureMessage, sessionId.ToString(), player, portId));
            return (false, false);
        }

        ClaimedSessionId = sessionId;
        ClaimedPlayer = player;
        ClaimedPortId = portId;
        return (true, true);
    }

    internal async Task ReleaseClaimAsync(string reason, bool notifyClient)
    {
        if (!ClaimedSessionId.HasValue || !ClaimedPlayer.HasValue)
            return;

        var releaseSessionId = ClaimedSessionId.Value;
        var releasePlayer = ClaimedPlayer.Value;
        var releasePortId = ClaimedPortId;
        await _remoteControlContract.ReleaseControlAsync(
            releaseSessionId,
            new ReleaseControlRequest(Player: releasePlayer, Reason: reason, PortId: releasePortId),
            _cancellationToken);
        ClaimedSessionId = null;
        ClaimedPlayer = null;
        ClaimedPortId = null;

        if (notifyClient && _codec.IsSocketOpen)
        {
            await _codec.SendSocketMessageAsync(
                new SocketMessage("released", reason, releaseSessionId.ToString(), releasePlayer, releasePortId));
        }
    }

    internal async Task ReleaseSpecifiedAsync(Guid sessionId, string portId, int player, string reason)
    {
        await _remoteControlContract.ReleaseControlAsync(
            sessionId,
            new ReleaseControlRequest(Player: player, Reason: reason, PortId: portId),
            _cancellationToken);

        if (ClaimedSessionId == sessionId &&
            ClaimedPlayer == player &&
            string.Equals(ClaimedPortId, portId, StringComparison.Ordinal))
        {
            ClaimedSessionId = null;
            ClaimedPlayer = null;
            ClaimedPortId = null;
        }
    }

    internal void InvalidateClaim()
    {
        ClaimedSessionId = null;
        ClaimedPlayer = null;
        ClaimedPortId = null;
    }

    internal async Task ReleaseOnDisconnectAsync()
    {
        if (!ClaimedSessionId.HasValue || !ClaimedPlayer.HasValue)
            return;

        await _remoteControlContract.ReleaseControlAsync(
            ClaimedSessionId.Value,
            new ReleaseControlRequest(
                Player: ClaimedPlayer.Value,
                Reason: "网页连接已断开，已恢复本地控制",
                PortId: ClaimedPortId),
            CancellationToken.None);
    }
}

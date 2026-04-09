using FCRevolution.Backend.Abstractions;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Services;
using FCRevolution.Contracts.Sessions;

namespace FCRevolution.Backend.Hosting;

public sealed class BackendContractFacade :
    IRomCatalogContract,
    IGameSessionContract,
    IRemoteControlContract,
    IBackendStateSyncContract
{
    private readonly BackendRuntimeState _state;
    private readonly IBackendSessionControlBridge _sessionControlBridge;
    private readonly IBackendRemoteControlBridge _remoteControlBridge;

    public BackendContractFacade(
        BackendRuntimeState state,
        IBackendSessionControlBridge sessionControlBridge,
        IBackendRemoteControlBridge remoteControlBridge)
    {
        _state = state;
        _sessionControlBridge = sessionControlBridge;
        _remoteControlBridge = remoteControlBridge;
    }

    public Task<IReadOnlyList<RomSummaryDto>> GetRomsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_state.Roms);

    public Task<IReadOnlyList<GameSessionSummaryDto>> GetSessionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_state.Sessions);

    public Task<StartSessionResponse?> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default) =>
        _sessionControlBridge.StartSessionAsync(request, cancellationToken);

    public Task<bool> CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _sessionControlBridge.CloseSessionAsync(sessionId, cancellationToken);

    public Task<bool> ClaimControlAsync(Guid sessionId, ClaimControlRequest request, CancellationToken cancellationToken = default) =>
        _remoteControlBridge.ClaimControlAsync(sessionId, request, cancellationToken);

    public Task ReleaseControlAsync(Guid sessionId, ReleaseControlRequest request, CancellationToken cancellationToken = default) =>
        _remoteControlBridge.ReleaseControlAsync(sessionId, request, cancellationToken);

    public Task RefreshHeartbeatAsync(Guid sessionId, RefreshHeartbeatRequest request, CancellationToken cancellationToken = default) =>
        _remoteControlBridge.RefreshHeartbeatAsync(sessionId, request, cancellationToken);

    public Task<bool> SetInputStateAsync(Guid sessionId, SetInputStateRequest request, CancellationToken cancellationToken = default) =>
        _remoteControlBridge.SetInputStateAsync(sessionId, request, cancellationToken);

    public Task ReplaceRomsAsync(IReadOnlyList<RomSummaryDto> roms, CancellationToken cancellationToken = default)
    {
        _state.ReplaceRoms(roms);
        return Task.CompletedTask;
    }

    public Task ReplaceSessionsAsync(IReadOnlyList<GameSessionSummaryDto> sessions, CancellationToken cancellationToken = default)
    {
        _state.ReplaceSessions(sessions);
        return Task.CompletedTask;
    }
}

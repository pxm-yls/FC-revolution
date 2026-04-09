using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FCRevolution.Backend.Abstractions;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Services;
using FCRevolution.Contracts.Sessions;

namespace FC_Revolution.UI.AppServices;

public sealed class EmbeddedBackendRuntimeBridge :
    IBackendRuntimeBridge,
    IRomCatalogContract,
    IGameSessionContract,
    IRemoteControlContract
{
    private readonly IArcadeRuntimeContractAdapter _runtimeContractAdapter;

    public EmbeddedBackendRuntimeBridge(IArcadeRuntimeContractAdapter runtimeContractAdapter)
    {
        _runtimeContractAdapter = runtimeContractAdapter;
    }

    public Task<StartSessionResponse?> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default) =>
        _runtimeContractAdapter.StartSessionAsync(request, cancellationToken);

    public Task<BackendMediaAsset?> GetRomPreviewAssetAsync(string romPath, CancellationToken cancellationToken = default) =>
        _runtimeContractAdapter.GetRomPreviewAssetAsync(romPath, cancellationToken);

    public Task<IReadOnlyList<RomSummaryDto>> GetRomsAsync(CancellationToken cancellationToken = default) =>
        _runtimeContractAdapter.GetRomsAsync(cancellationToken);

    public Task<IReadOnlyList<GameSessionSummaryDto>> GetSessionsAsync(CancellationToken cancellationToken = default) =>
        _runtimeContractAdapter.GetSessionsAsync(cancellationToken);

    public Task<bool> CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _runtimeContractAdapter.CloseSessionAsync(sessionId, cancellationToken);

    public Task<byte[]?> GetSessionPreviewAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        _runtimeContractAdapter.GetSessionPreviewAsync(sessionId, cancellationToken);

    public Task<bool> ClaimControlAsync(Guid sessionId, ClaimControlRequest request, CancellationToken cancellationToken = default) =>
        _runtimeContractAdapter.ClaimControlAsync(sessionId, request, cancellationToken);

    public Task ReleaseControlAsync(Guid sessionId, ReleaseControlRequest request, CancellationToken cancellationToken = default) =>
        _runtimeContractAdapter.ReleaseControlAsync(sessionId, request, cancellationToken);

    public Task RefreshHeartbeatAsync(Guid sessionId, RefreshHeartbeatRequest request, CancellationToken cancellationToken = default) =>
        _runtimeContractAdapter.RefreshHeartbeatAsync(sessionId, request, cancellationToken);

    public Task<bool> SetButtonStateAsync(Guid sessionId, ButtonStateRequest request, CancellationToken cancellationToken = default)
    {
        if (RemoteControlRequestCompatibility.TryBuildGenericInputRequest(request, "embedded-runtime-bridge", out var genericRequest))
            return SetInputStateAsync(sessionId, genericRequest, cancellationToken);

        return _runtimeContractAdapter.SetButtonStateAsync(sessionId, request, cancellationToken);
    }

    public Task<bool> SetInputStateAsync(Guid sessionId, SetInputStateRequest request, CancellationToken cancellationToken = default) =>
        _runtimeContractAdapter.SetInputStateAsync(sessionId, request, cancellationToken);

    public Task<BackendStreamSubscription?> SubscribeStreamAsync(Guid sessionId, int audioChunkSize = 882, CancellationToken cancellationToken = default) =>
        _runtimeContractAdapter.SubscribeStreamAsync(sessionId, audioChunkSize, cancellationToken);
}

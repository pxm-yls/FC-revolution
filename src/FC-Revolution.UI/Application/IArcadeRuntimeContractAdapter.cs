using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FCRevolution.Backend.Abstractions;
using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Services;
using FCRevolution.Contracts.Sessions;

namespace FC_Revolution.UI.AppServices;

public interface IArcadeRuntimeContractAdapter :
    IRomCatalogContract,
    IGameSessionContract,
    IRemoteControlContract
{
    IReadOnlyList<RomSummaryDto> GetRomSummaries();
    IReadOnlyList<GameSessionSummaryDto> GetSessionSummaries();
    Task<BackendMediaAsset?> GetRomPreviewAssetAsync(string romPath, CancellationToken cancellationToken = default);
    Task<byte[]?> GetSessionPreviewAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<BackendStreamSubscription?> SubscribeStreamAsync(Guid sessionId, int audioChunkSize = 882, CancellationToken cancellationToken = default);
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Sessions;

namespace FC_Revolution.UI.AppServices;

public interface IBackendStateSyncClient
{
    Task PushRomsAsync(IReadOnlyList<RomSummaryDto> roms, CancellationToken cancellationToken = default);
    Task PushSessionsAsync(IReadOnlyList<GameSessionSummaryDto> sessions, CancellationToken cancellationToken = default);
}

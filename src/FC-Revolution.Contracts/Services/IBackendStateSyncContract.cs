using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Sessions;

namespace FCRevolution.Contracts.Services;

public interface IBackendStateSyncContract
{
    Task ReplaceRomsAsync(IReadOnlyList<RomSummaryDto> roms, CancellationToken cancellationToken = default);
    Task ReplaceSessionsAsync(IReadOnlyList<GameSessionSummaryDto> sessions, CancellationToken cancellationToken = default);
}

using FCRevolution.Contracts.Roms;

namespace FCRevolution.Contracts.Services;

public interface IRomCatalogContract
{
    Task<IReadOnlyList<RomSummaryDto>> GetRomsAsync(CancellationToken cancellationToken = default);
}

using System.Collections.Generic;
using System.Threading.Tasks;

namespace FC_Revolution.UI.AppServices;

public interface ILanArcadeDiagnosticsService
{
    Task<string> BuildDiagnosticsAsync(int port, string entryUrl, ILanArcadeService lanArcadeService, IReadOnlyList<string>? lanCandidates = null);
}

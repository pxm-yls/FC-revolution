using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Contracts.Sessions;

namespace FCRevolution.Contracts.Services;

public interface IGameSessionContract
{
    Task<IReadOnlyList<GameSessionSummaryDto>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task<StartSessionResponse?> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default);
    Task<bool> CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

using FCRevolution.Backend.Abstractions;
using FCRevolution.Contracts.RemoteControl;
namespace FCRevolution.Backend.Services;

public sealed class NullBackendRuntimeBridge : IBackendRuntimeBridge
{
    public Task<StartSessionResponse?> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult<StartSessionResponse?>(null);

    public Task<BackendMediaAsset?> GetRomPreviewAssetAsync(string romPath, CancellationToken cancellationToken = default) =>
        Task.FromResult<BackendMediaAsset?>(null);

    public Task<bool> CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<byte[]?> GetSessionPreviewAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult<byte[]?>(null);

    public Task<bool> ClaimControlAsync(Guid sessionId, ClaimControlRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task ReleaseControlAsync(Guid sessionId, ReleaseControlRequest request, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RefreshHeartbeatAsync(Guid sessionId, RefreshHeartbeatRequest request, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> SetButtonStateAsync(Guid sessionId, ButtonStateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> SetInputStateAsync(Guid sessionId, SetInputStateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<BackendStreamSubscription?> SubscribeStreamAsync(Guid sessionId, int audioChunkSize = 882, CancellationToken cancellationToken = default) =>
        Task.FromResult<BackendStreamSubscription?>(null);
}

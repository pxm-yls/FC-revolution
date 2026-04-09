using System.Threading.Channels;
using FCRevolution.Backend.Abstractions;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Sessions;

namespace FC_Revolution.Backend.Hosting.Tests;

internal sealed class RecordingRuntimeBridge : IBackendRuntimeBridge
{
    public StartSessionResponse? StartSessionResult { get; set; }

    public BackendMediaAsset? RomPreviewAsset { get; set; }

    public bool CloseSessionResult { get; set; }

    public byte[]? PreviewBytes { get; set; }

    public bool ClaimControlResult { get; set; }

    public bool SetInputStateResult { get; set; }

    public BackendStreamSubscription? StreamSubscription { get; set; }

    public Func<Guid, int, CancellationToken, Task<BackendStreamSubscription?>>? StreamSubscriptionFactory { get; set; }

    public List<StartSessionRequest> StartSessionRequests { get; } = [];

    public List<string> RomPreviewRequests { get; } = [];

    public List<Guid> CloseSessionCalls { get; } = [];

    public List<Guid> PreviewCalls { get; } = [];

    public List<(Guid SessionId, ClaimControlRequest Request)> ClaimCalls { get; } = [];

    public List<(Guid SessionId, ReleaseControlRequest Request)> ReleaseCalls { get; } = [];

    public List<(Guid SessionId, RefreshHeartbeatRequest Request)> HeartbeatCalls { get; } = [];

    public List<(Guid SessionId, SetInputStateRequest Request)> InputStateCalls { get; } = [];

    public List<(Guid SessionId, int AudioChunkSize)> StreamSubscriptionCalls { get; } = [];

    public Task<StartSessionResponse?> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default)
    {
        StartSessionRequests.Add(request);
        return Task.FromResult(StartSessionResult);
    }

    public Task<BackendMediaAsset?> GetRomPreviewAssetAsync(string romPath, CancellationToken cancellationToken = default)
    {
        RomPreviewRequests.Add(romPath);
        return Task.FromResult(RomPreviewAsset);
    }

    public Task<bool> CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        CloseSessionCalls.Add(sessionId);
        return Task.FromResult(CloseSessionResult);
    }

    public Task<byte[]?> GetSessionPreviewAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        PreviewCalls.Add(sessionId);
        return Task.FromResult(PreviewBytes);
    }

    public Task<bool> ClaimControlAsync(Guid sessionId, ClaimControlRequest request, CancellationToken cancellationToken = default)
    {
        ClaimCalls.Add((sessionId, request));
        return Task.FromResult(ClaimControlResult);
    }

    public Task ReleaseControlAsync(Guid sessionId, ReleaseControlRequest request, CancellationToken cancellationToken = default)
    {
        ReleaseCalls.Add((sessionId, request));
        return Task.CompletedTask;
    }

    public Task RefreshHeartbeatAsync(Guid sessionId, RefreshHeartbeatRequest request, CancellationToken cancellationToken = default)
    {
        HeartbeatCalls.Add((sessionId, request));
        return Task.CompletedTask;
    }

    public Task<bool> SetInputStateAsync(Guid sessionId, SetInputStateRequest request, CancellationToken cancellationToken = default)
    {
        InputStateCalls.Add((sessionId, request));
        return Task.FromResult(SetInputStateResult);
    }

    public Task<BackendStreamSubscription?> SubscribeStreamAsync(Guid sessionId, int audioChunkSize = 882, CancellationToken cancellationToken = default)
    {
        StreamSubscriptionCalls.Add((sessionId, audioChunkSize));
        if (StreamSubscriptionFactory != null)
            return StreamSubscriptionFactory(sessionId, audioChunkSize, cancellationToken);

        return Task.FromResult(StreamSubscription);
    }
}

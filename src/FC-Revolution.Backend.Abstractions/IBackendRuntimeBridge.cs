using System.Threading.Channels;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.Backend.Abstractions;

public interface IBackendSessionControlBridge
{
    Task<StartSessionResponse?> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default);
    Task<bool> CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

public interface IBackendPreviewQueryBridge
{
    Task<BackendMediaAsset?> GetRomPreviewAssetAsync(string romPath, CancellationToken cancellationToken = default);
    Task<byte[]?> GetSessionPreviewAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

public interface IBackendRemoteControlBridge
{
    Task<bool> ClaimControlAsync(Guid sessionId, ClaimControlRequest request, CancellationToken cancellationToken = default);
    Task ReleaseControlAsync(Guid sessionId, ReleaseControlRequest request, CancellationToken cancellationToken = default);
    Task RefreshHeartbeatAsync(Guid sessionId, RefreshHeartbeatRequest request, CancellationToken cancellationToken = default);
    Task<bool> SetInputStateAsync(Guid sessionId, SetInputStateRequest request, CancellationToken cancellationToken = default);
}

public interface IBackendStreamSubscriptionBridge
{
    /// <summary>订阅视频/音频流，返回带独立释放生命周期的订阅句柄。</summary>
    Task<BackendStreamSubscription?> SubscribeStreamAsync(Guid sessionId, int audioChunkSize = 882, CancellationToken cancellationToken = default);
}

public interface IBackendRuntimeBridge :
    IBackendSessionControlBridge,
    IBackendPreviewQueryBridge,
    IBackendRemoteControlBridge,
    IBackendStreamSubscriptionBridge;

public sealed record BackendMediaAsset(string FilePath, string ContentType);

public sealed class BackendStreamSubscription : IAsyncDisposable
{
    private readonly Func<ValueTask>? _disposeAsync;

    public BackendStreamSubscription(
        ChannelReader<VideoFramePacket> videoReader,
        ChannelReader<AudioPacket> audioReader,
        Func<ValueTask>? disposeAsync = null)
    {
        VideoReader = videoReader;
        AudioReader = audioReader;
        _disposeAsync = disposeAsync;
    }

    public ChannelReader<VideoFramePacket> VideoReader { get; }

    public ChannelReader<AudioPacket> AudioReader { get; }

    public ValueTask DisposeAsync() => _disposeAsync?.Invoke() ?? ValueTask.CompletedTask;
}

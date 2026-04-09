using FCRevolution.Backend.Abstractions;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Services;
using FCRevolution.Contracts.Sessions;
using FC_Revolution.UI.AppServices;

namespace FC_Revolution.UI.Tests;

public sealed class EmbeddedBackendRuntimeBridgeTests
{
    [Fact]
    public async Task SetInputStateAsync_DelegatesToAdapter()
    {
        var adapter = new RecordingArcadeRuntimeContractAdapter
        {
            SetInputStateResult = true
        };
        var bridge = new EmbeddedBackendRuntimeBridge(adapter);
        var sessionId = Guid.NewGuid();

        var changed = await bridge.SetInputStateAsync(
            sessionId,
            new SetInputStateRequest(
            [
                new InputActionValueDto("p2", "gamepad", "x", 1f)
            ]));

        Assert.True(changed);
        var inputCall = Assert.Single(adapter.InputCalls);
        Assert.Equal(sessionId, inputCall.SessionId);
        var action = Assert.Single(inputCall.Request.Actions);
        Assert.Equal("p2", action.PortId);
        Assert.Equal("x", action.ActionId);
        Assert.Equal(1f, action.Value);
    }

    private sealed class RecordingArcadeRuntimeContractAdapter : IArcadeRuntimeContractAdapter
    {
        public bool SetInputStateResult { get; set; }

        public List<(Guid SessionId, SetInputStateRequest Request)> InputCalls { get; } = [];

        public IReadOnlyList<RomSummaryDto> GetRomSummaries() => [];

        public IReadOnlyList<GameSessionSummaryDto> GetSessionSummaries() => [];

        public Task<IReadOnlyList<RomSummaryDto>> GetRomsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RomSummaryDto>>([]);

        public Task<IReadOnlyList<GameSessionSummaryDto>> GetSessionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GameSessionSummaryDto>>([]);

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

        public Task<bool> SetInputStateAsync(Guid sessionId, SetInputStateRequest request, CancellationToken cancellationToken = default)
        {
            InputCalls.Add((sessionId, request));
            return Task.FromResult(SetInputStateResult);
        }

        public Task<BackendStreamSubscription?> SubscribeStreamAsync(Guid sessionId, int audioChunkSize = 882, CancellationToken cancellationToken = default) =>
            Task.FromResult<BackendStreamSubscription?>(null);
    }
}

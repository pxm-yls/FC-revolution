using Avalonia.Input;
using FCRevolution.Backend.Abstractions;
using FCRevolution.Backend.Hosting;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Sessions;
using FCRevolution.Core.Input;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.Tests;

public sealed class LanArcadeServiceTests
{
    [Fact]
    public async Task StopAsync_WhenNotStarted_ReturnsTrue_NoOp()
    {
        var harness = CreateHarness();
        var service = new LanArcadeService(new NoopRuntimeAdapter(), harness.Seams);

        var stopped = await service.StopAsync();

        Assert.True(stopped);
        Assert.Empty(harness.CreatedHosts);
    }

    [Fact]
    public void UpdateStreamParameters_WhenNotStarted_DoesNotThrow()
    {
        var service = new LanArcadeService(new NoopRuntimeAdapter(), CreateHarness().Seams);

        service.UpdateStreamParameters(2, 85, LanStreamSharpenMode.Standard);
    }

    [Fact]
    public async Task UpdateStreamParameters_WhenStarted_ForwardsToHost()
    {
        var harness = CreateHarness();
        var service = new LanArcadeService(new NoopRuntimeAdapter(), harness.Seams);
        var options = new BackendHostOptions(22334);

        await service.StartAsync(options);
        service.UpdateStreamParameters(3, 92, LanStreamSharpenMode.Strong);

        var host = Assert.Single(harness.CreatedHosts);
        var update = Assert.Single(host.UpdateCalls);
        Assert.Equal(3, update.scaleMultiplier);
        Assert.Equal(92, update.jpegQuality);
        Assert.Equal(PixelEnhancementMode.VividColor, update.enhancementMode);
    }

    [Fact]
    public async Task GetLocalHealthAsync_WhenNotStarted_ReturnsNotStartedMessage()
    {
        var service = new LanArcadeService(new NoopRuntimeAdapter(), CreateHarness().Seams);

        var health = await service.GetLocalHealthAsync();

        Assert.Equal("局域网服务未启动", health);
    }

    [Fact]
    public async Task StartAsync_SamePort_DoesNotRestartHost()
    {
        var harness = CreateHarness();
        var service = new LanArcadeService(new NoopRuntimeAdapter(), harness.Seams);
        var options = new BackendHostOptions(22335);

        await service.StartAsync(options);
        await service.StartAsync(options);

        var host = Assert.Single(harness.CreatedHosts);
        Assert.Equal(1, host.StartCount);
        Assert.Equal(0, host.StopCount);
    }

    [Fact]
    public async Task StartAsync_PortSwitch_StopsOldHost_ThenStartsNewHost()
    {
        var harness = CreateHarness();
        var service = new LanArcadeService(new NoopRuntimeAdapter(), harness.Seams);

        await service.StartAsync(new BackendHostOptions(22336));
        await service.StartAsync(new BackendHostOptions(22337));

        Assert.Equal(2, harness.CreatedHosts.Count);
        Assert.Equal(1, harness.CreatedHosts[0].StartCount);
        Assert.Equal(1, harness.CreatedHosts[0].StopCount);
        Assert.True(harness.CreatedHosts[0].Disposed);
        Assert.Equal(1, harness.CreatedHosts[1].StartCount);
        Assert.Equal(0, harness.CreatedHosts[1].StopCount);
    }

    [Fact]
    public async Task Dispose_WhenRunning_StopsAndDisposesCurrentHost_AndClearsRunningState()
    {
        var harness = CreateHarness();
        var service = new LanArcadeService(new NoopRuntimeAdapter(), harness.Seams);
        await service.StartAsync(new BackendHostOptions(22338));

        service.Dispose();

        var host = Assert.Single(harness.CreatedHosts);
        Assert.Equal(1, host.StartCount);
        Assert.Equal(1, host.StopCount);
        Assert.True(host.Disposed);
        Assert.False(service.IsRunning);
    }

    private static LanArcadeServiceHarness CreateHarness()
    {
        var createdHosts = new List<FakeLanBackendHost>();
        var seams = new LanArcadeService.TestSeams
        {
            CreateHost = (options, _, _) =>
            {
                var host = new FakeLanBackendHost(options.Port);
                createdHosts.Add(host);
                return host;
            },
            VerifyReachabilityAsync = _ => Task.CompletedTask,
        };
        return new LanArcadeServiceHarness(seams, createdHosts);
    }

    private sealed record LanArcadeServiceHarness(
        LanArcadeService.TestSeams Seams,
        List<FakeLanBackendHost> CreatedHosts);

    private sealed class FakeLanBackendHost : LanArcadeService.ILanBackendHost
    {
        public FakeLanBackendHost(int port)
        {
            Port = port;
        }

        public int Port { get; }
        public bool IsRunning { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public bool Disposed { get; private set; }
        public List<(int scaleMultiplier, int jpegQuality, PixelEnhancementMode enhancementMode)> UpdateCalls { get; } = [];

        public Task StartAsync()
        {
            StartCount++;
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task<bool> StopAsync(TimeSpan? timeout = null)
        {
            StopCount++;
            IsRunning = false;
            return Task.FromResult(true);
        }

        public void UpdateStreamParameters(int scaleMultiplier, int jpegQuality, PixelEnhancementMode enhancementMode)
            => UpdateCalls.Add((scaleMultiplier, jpegQuality, enhancementMode));

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoopRuntimeAdapter : IArcadeRuntimeContractAdapter
    {
        public IReadOnlyList<RomSummaryDto> GetRomSummaries() => [];
        public IReadOnlyList<GameSessionSummaryDto> GetSessionSummaries() => [];
        public Task<BackendMediaAsset?> GetRomPreviewAssetAsync(string romPath, CancellationToken cancellationToken = default) =>
            Task.FromResult<BackendMediaAsset?>(null);
        public Task<byte[]?> GetSessionPreviewAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]?>(null);
        public Task<BackendStreamSubscription?> SubscribeStreamAsync(Guid sessionId, int audioChunkSize = 882, CancellationToken cancellationToken = default) =>
            Task.FromResult<BackendStreamSubscription?>(null);
        public Task<IReadOnlyList<RomSummaryDto>> GetRomsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RomSummaryDto>>([]);
        public Task<IReadOnlyList<GameSessionSummaryDto>> GetSessionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GameSessionSummaryDto>>([]);
        public Task<StartSessionResponse?> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<StartSessionResponse?>(null);
        public Task<bool> CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
        public Task<bool> ClaimControlAsync(Guid sessionId, ClaimControlRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
        public Task ReleaseControlAsync(Guid sessionId, ReleaseControlRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task RefreshHeartbeatAsync(Guid sessionId, RefreshHeartbeatRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<bool> SetInputStateAsync(Guid sessionId, SetInputStateRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}

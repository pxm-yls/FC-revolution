using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FCRevolution.Backend.Hosting;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.AppServices;

public sealed class LanArcadeService : ILanArcadeService
{
    internal sealed class TestSeams
    {
        public required Func<BackendHostOptions, IArcadeRuntimeContractAdapter, Action<string>?, ILanBackendHost> CreateHost { get; init; }
        public required Func<int, Task> VerifyReachabilityAsync { get; init; }
    }

    internal interface ILanBackendHost : IAsyncDisposable
    {
        bool IsRunning { get; }
        Task StartAsync();
        Task<bool> StopAsync(TimeSpan? timeout = null);
        void UpdateStreamParameters(int scaleMultiplier, int jpegQuality, PixelEnhancementMode enhancementMode);
    }

    private readonly IArcadeRuntimeContractAdapter _runtimeContractAdapter;
    private readonly Action<string>? _reportTraffic;
    private readonly TestSeams _seams;
    private readonly LanBackendHostLifecycleService _hostLifecycleService;

    public LanArcadeService(
        IArcadeRuntimeContractAdapter runtimeContractAdapter,
        Action<string>? reportTraffic = null)
        : this(runtimeContractAdapter, CreateDefaultSeams(), reportTraffic)
    {
    }

    internal LanArcadeService(
        IArcadeRuntimeContractAdapter runtimeContractAdapter,
        TestSeams seams,
        Action<string>? reportTraffic = null)
    {
        _runtimeContractAdapter = runtimeContractAdapter;
        _seams = seams;
        _reportTraffic = reportTraffic;
        _hostLifecycleService = new LanBackendHostLifecycleService(runtimeContractAdapter, seams, reportTraffic);
    }

    public bool IsRunning => _hostLifecycleService.IsRunning;
    public int Port => _hostLifecycleService.Port;
    public string LocalBaseUrl => $"http://127.0.0.1:{Port}/";

    public async Task StartAsync(BackendHostOptions options)
    {
        await _hostLifecycleService.StartAsync(options);
    }

    public async Task<bool> StopAsync(TimeSpan? timeout = null)
    {
        return await _hostLifecycleService.StopAsync(timeout);
    }

    public void UpdateStreamParameters(int scaleMultiplier, int jpegQuality, LanStreamSharpenMode sharpenMode)
    {
        _hostLifecycleService.UpdateStreamParameters(scaleMultiplier, jpegQuality, ToPixelEnhancementMode(sharpenMode));
    }

    private static PixelEnhancementMode ToPixelEnhancementMode(LanStreamSharpenMode mode) => mode switch
    {
        LanStreamSharpenMode.Subtle   => PixelEnhancementMode.SubtleSharpen,
        LanStreamSharpenMode.Standard => PixelEnhancementMode.CrtScanlines,
        LanStreamSharpenMode.Strong   => PixelEnhancementMode.VividColor,
        _                             => PixelEnhancementMode.None,
    };

    public async Task<string> GetLocalHealthAsync(TimeSpan? timeout = null)
    {
        var baseAddress = _hostLifecycleService.LocalBaseAddress;
        if (!IsRunning || baseAddress == null)
            return "局域网服务未启动";

        using var client = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = timeout ?? TimeSpan.FromSeconds(2)
        };

        return await client.GetStringAsync("api/health");
    }

    public void Dispose()
    {
        _hostLifecycleService.Dispose();
    }

    private static async Task VerifyExternalReachabilityAsync(int port)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            StartupDiagnostics.Write("lan-service", $"VerifyExternalReachabilityAsync attempt {attempt + 1}/8 for port {port}");
            try
            {
                using var socket = new TcpClient();
                using var connectCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
                await socket.ConnectAsync("127.0.0.1", port, connectCts.Token);

                using var client = new HttpClient
                {
                    BaseAddress = new Uri($"http://127.0.0.1:{port}/"),
                    Timeout = TimeSpan.FromMilliseconds(600)
                };
                var content = await client.GetStringAsync("api/health");
                if (!string.IsNullOrWhiteSpace(content))
                {
                    StartupDiagnostics.Write("lan-service", $"health endpoint returned payload on attempt {attempt + 1}");
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
                StartupDiagnostics.Write("lan-service", $"reachability attempt {attempt + 1} failed: {ex.Message}");
                await Task.Delay(150);
            }
        }

        StartupDiagnostics.Write("lan-service", $"reachability verification failed for port {port}: {lastError?.Message}");
        throw new InvalidOperationException($"局域网服务启动后未通过 127.0.0.1:{port} 连通校验。{lastError?.Message}");
    }

    private static TestSeams CreateDefaultSeams() => new()
    {
        CreateHost = static (options, runtimeContractAdapter, reportTraffic) =>
            new BackendHostAdapter(
                options,
                new EmbeddedBackendRuntimeBridge(runtimeContractAdapter),
                reportTraffic),
        VerifyReachabilityAsync = VerifyExternalReachabilityAsync,
    };

    private sealed class BackendHostAdapter : ILanBackendHost
    {
        private readonly BackendHostService _host;

        public BackendHostAdapter(
            BackendHostOptions options,
            EmbeddedBackendRuntimeBridge runtimeBridge,
            Action<string>? reportTraffic)
        {
            _host = new BackendHostService(options, runtimeBridge, reportTraffic);
        }

        public bool IsRunning => _host.IsRunning;

        public Task StartAsync() => _host.StartAsync();

        public Task<bool> StopAsync(TimeSpan? timeout = null) => _host.StopAsync(timeout);

        public void UpdateStreamParameters(int scaleMultiplier, int jpegQuality, PixelEnhancementMode enhancementMode)
            => _host.UpdateStreamParameters(scaleMultiplier, jpegQuality, enhancementMode);

        public ValueTask DisposeAsync() => _host.DisposeAsync();
    }
}

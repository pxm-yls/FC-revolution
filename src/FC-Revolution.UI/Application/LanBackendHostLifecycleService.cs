using System;
using System.Threading.Tasks;
using FCRevolution.Backend.Hosting;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.AppServices;

internal sealed class LanBackendHostLifecycleService : IDisposable
{
    private readonly IArcadeRuntimeContractAdapter _runtimeContractAdapter;
    private readonly LanArcadeService.TestSeams _seams;
    private readonly Action<string>? _reportTraffic;
    private LanArcadeService.ILanBackendHost? _backendHost;
    private BackendContractClient? _backendClient;
    private int _port = SystemConfigProfile.DefaultLanArcadePort;

    public LanBackendHostLifecycleService(
        IArcadeRuntimeContractAdapter runtimeContractAdapter,
        LanArcadeService.TestSeams seams,
        Action<string>? reportTraffic = null)
    {
        _runtimeContractAdapter = runtimeContractAdapter;
        _seams = seams;
        _reportTraffic = reportTraffic;
    }

    public bool IsRunning => _backendHost?.IsRunning == true;

    public int Port => _port;

    public Uri? LocalBaseAddress => _backendClient?.BaseAddress;

    public async Task StartAsync(BackendHostOptions options)
    {
        StartupDiagnostics.Write("lan-service", $"StartAsync begin; requestedPort={options.Port}");
        if (_backendHost?.IsRunning == true)
        {
            if (_port == options.Port)
            {
                StartupDiagnostics.Write("lan-service", "backend host already running on requested port");
                return;
            }

            StartupDiagnostics.Write("lan-service", $"backend host already running on {_port}, stopping first");
            await StopAsync();
        }

        _port = options.Port;
        _backendHost = _seams.CreateHost(options, _runtimeContractAdapter, _reportTraffic);
        _backendClient?.Dispose();
        _backendClient = new BackendContractClient($"http://127.0.0.1:{options.Port}/");
        StartupDiagnostics.Write("lan-service", "starting BackendHostService");
        await _backendHost.StartAsync();
        StartupDiagnostics.Write("lan-service", "BackendHostService started; verifying loopback reachability");
        await _seams.VerifyReachabilityAsync(options.Port);
        StartupDiagnostics.Write("lan-service", "loopback reachability verification passed");
    }

    public async Task<bool> StopAsync(TimeSpan? timeout = null)
    {
        StartupDiagnostics.Write("lan-service", $"StopAsync begin; timeoutMs={(timeout ?? TimeSpan.FromSeconds(2)).TotalMilliseconds:F0}");
        _backendClient?.Dispose();
        _backendClient = null;

        if (_backendHost == null)
        {
            StartupDiagnostics.Write("lan-service", "StopAsync no-op because backend host is null");
            return true;
        }

        var host = _backendHost;
        _backendHost = null;
        var stopped = await host.StopAsync(timeout);
        await host.DisposeAsync();
        StartupDiagnostics.Write("lan-service", $"StopAsync complete; stopped={stopped}");
        return stopped;
    }

    public void UpdateStreamParameters(int scaleMultiplier, int jpegQuality, PixelEnhancementMode enhancementMode)
    {
        _backendHost?.UpdateStreamParameters(scaleMultiplier, jpegQuality, enhancementMode);
    }

    public void Dispose()
    {
        _backendClient?.Dispose();
        _backendClient = null;

        if (_backendHost != null)
        {
            _backendHost.StopAsync().GetAwaiter().GetResult();
            _backendHost.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _backendHost = null;
        }
    }
}

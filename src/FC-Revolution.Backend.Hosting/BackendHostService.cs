using FCRevolution.Backend.Abstractions;
using FCRevolution.Backend.Hosting.Endpoints;
using FCRevolution.Backend.Hosting.Streaming;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Services;
using FCRevolution.Contracts.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FCRevolution.Backend.Hosting;

public sealed class BackendHostService : IAsyncDisposable
{
    private readonly BackendHostOptions _options;
    private readonly IBackendSessionControlBridge _sessionControlBridge;
    private readonly IBackendPreviewQueryBridge _previewQueryBridge;
    private readonly IBackendRemoteControlBridge _remoteControlBridge;
    private readonly IBackendStreamSubscriptionBridge _streamSubscriptionBridge;
    private readonly Action<string>? _reportTraffic;
    private readonly BackendStreamSettingsStore _streamSettings;
    private WebApplication? _app;

    public BackendHostService(
        BackendHostOptions options,
        IBackendSessionControlBridge sessionControlBridge,
        IBackendPreviewQueryBridge previewQueryBridge,
        IBackendRemoteControlBridge remoteControlBridge,
        IBackendStreamSubscriptionBridge streamSubscriptionBridge,
        Action<string>? reportTraffic = null)
    {
        _options = options;
        _sessionControlBridge = sessionControlBridge;
        _previewQueryBridge = previewQueryBridge;
        _remoteControlBridge = remoteControlBridge;
        _streamSubscriptionBridge = streamSubscriptionBridge;
        _reportTraffic = reportTraffic;
        _streamSettings = new BackendStreamSettingsStore(options);
    }

    public BackendHostService(BackendHostOptions options, IBackendRuntimeBridge runtimeBridge, Action<string>? reportTraffic = null)
        : this(options, runtimeBridge, runtimeBridge, runtimeBridge, runtimeBridge, reportTraffic)
    {
    }

    public bool IsRunning => _app != null;

    public int Port => _options.Port;

    /// <summary>热更新串流质量参数，立即对下一帧生效，无需重启服务器。</summary>
    public void UpdateStreamParameters(int scaleMultiplier, int jpegQuality, PixelEnhancementMode enhancementMode)
        => _streamSettings.Update(scaleMultiplier, jpegQuality, enhancementMode);

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app != null)
            return;

        var builder = WebApplication.CreateSlimBuilder();
        ConfigureServices(builder);

        var app = builder.Build();
        MapModules(app);
        await app.StartAsync(cancellationToken);
        _app = app;
    }

    public async Task<bool> StopAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (_app == null)
            return true;

        var app = _app;
        _app = null;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(2));
            await app.StopAsync(cts.Token);
            await app.DisposeAsync();
            return true;
        }
        catch
        {
            try
            {
                await app.DisposeAsync();
            }
            catch
            {
            }

            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(_options.Port, listen => listen.Protocols = HttpProtocols.Http1);
        });
        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = BackendJsonDefaults.SerializerOptions.PropertyNamingPolicy;
        });

        builder.Services.AddSingleton<BackendRuntimeState>();
        builder.Services.AddSingleton(_sessionControlBridge);
        builder.Services.AddSingleton(_previewQueryBridge);
        builder.Services.AddSingleton(_remoteControlBridge);
        builder.Services.AddSingleton(_streamSubscriptionBridge);
        builder.Services.AddSingleton(_streamSettings);
        builder.Services.AddSingleton<BackendContractFacade>();
        builder.Services.AddSingleton<WebSockets.BackendControlWebSocketHandler>();
        builder.Services.AddSingleton<BackendStreamWebSocketHandler>();
        builder.Services.AddSingleton<IRomCatalogContract>(sp =>
            _previewQueryBridge as IRomCatalogContract ??
            _sessionControlBridge as IRomCatalogContract ??
            sp.GetRequiredService<BackendContractFacade>());
        builder.Services.AddSingleton<IGameSessionContract>(sp =>
            _sessionControlBridge as IGameSessionContract ??
            _remoteControlBridge as IGameSessionContract ??
            sp.GetRequiredService<BackendContractFacade>());
        builder.Services.AddSingleton<IRemoteControlContract>(sp =>
            _remoteControlBridge as IRemoteControlContract ?? sp.GetRequiredService<BackendContractFacade>());
        builder.Services.AddSingleton<IBackendStateSyncContract>(sp => sp.GetRequiredService<BackendContractFacade>());
    }

    private void MapModules(WebApplication app)
    {
        BackendEndpointMapper.Map(app, _options, _reportTraffic);
    }
}

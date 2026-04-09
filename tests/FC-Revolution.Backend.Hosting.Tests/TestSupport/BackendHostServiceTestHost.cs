using System.Net.Sockets;
using FCRevolution.Backend.Hosting;

namespace FC_Revolution.Backend.Hosting.Tests;

internal sealed class BackendHostServiceTestHost : IAsyncDisposable
{
    private BackendHostServiceTestHost(int port, BackendHostService service, RecordingRuntimeBridge bridge)
    {
        Port = port;
        Service = service;
        Bridge = bridge;
        HttpBaseAddress = new Uri($"http://127.0.0.1:{port}/");
        Client = new HttpClient { BaseAddress = HttpBaseAddress };
    }

    internal int Port { get; }

    internal Uri HttpBaseAddress { get; }

    internal HttpClient Client { get; }

    internal BackendHostService Service { get; }

    internal RecordingRuntimeBridge Bridge { get; }

    internal static async Task<BackendHostServiceTestHost> StartAsync(
        RecordingRuntimeBridge? bridge = null,
        Func<int, BackendHostOptions>? optionsFactory = null)
    {
        bridge ??= new RecordingRuntimeBridge();
        var port = GetFreeTcpPort();
        var service = new BackendHostService(
            (optionsFactory ?? (value => new BackendHostOptions(value))).Invoke(port),
            bridge,
            bridge,
            bridge,
            bridge);
        await service.StartAsync();
        return new BackendHostServiceTestHost(port, service, bridge);
    }

    internal Uri CreateWebSocketUri(string path)
    {
        var builder = new UriBuilder(HttpBaseAddress)
        {
            Scheme = HttpBaseAddress.Scheme == Uri.UriSchemeHttps ? Uri.UriSchemeWss : Uri.UriSchemeWs,
            Path = path.TrimStart('/')
        };

        return builder.Uri;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Service.DisposeAsync();
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }
}

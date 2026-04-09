using Microsoft.AspNetCore.Builder;

namespace FCRevolution.Backend.Hosting.Endpoints;

internal static class BackendEndpointMapper
{
    internal static void Map(WebApplication app, BackendHostOptions options, Action<string>? reportTraffic)
    {
        app.Use(async (context, next) =>
        {
            var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            reportTraffic?.Invoke($"{DateTime.Now:HH:mm:ss} {remoteIp} {context.Request.Method} {context.Request.Path}");
            await next();
        });

        app.UseWebSockets();

        if (options.EnableWebPad)
            BackendWebPadEndpointModule.Map(app);

        if (options.EnableDebugPages)
            BackendDebugEndpointModule.Map(app, options);

        BackendApiEndpointModule.Map(app, options);
        BackendStreamWebSocketEndpointModule.Map(app);
    }
}

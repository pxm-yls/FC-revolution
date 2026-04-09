using FCRevolution.Backend.Hosting.WebPad;
using Microsoft.AspNetCore.Builder;

namespace FCRevolution.Backend.Hosting.Endpoints;

internal static class BackendDebugEndpointModule
{
    internal static void Map(WebApplication app, BackendHostOptions options)
    {
        app.MapGet("/debug/plain", context =>
            BackendStaticResponseWriter.WriteAsync(
                context,
                $"LAN PAD OK\nPORT={options.Port}\nUTC={DateTime.UtcNow:O}\nHOST={Environment.MachineName}\n",
                "text/plain; charset=utf-8"));

        app.MapGet("/debug/html", context =>
            BackendStaticResponseWriter.WriteAsync(
                context,
                "<!doctype html><html><head><meta charset=\"utf-8\"><title>LAN PAD HTML DEBUG</title></head><body><h1>LAN PAD HTML DEBUG OK</h1><p>No CSS. No JS. Plain HTML response.</p></body></html>",
                "text/html; charset=utf-8"));

        app.MapGet("/debug/minimal", context =>
            BackendStaticResponseWriter.WriteAsync(
                context,
                WebPadAssetCatalog.DebugMinimalHtml.Content,
                WebPadAssetCatalog.DebugMinimalHtml.ContentType));
    }
}

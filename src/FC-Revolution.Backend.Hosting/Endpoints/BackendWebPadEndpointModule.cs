using FCRevolution.Backend.Hosting.WebPad;
using FCRevolution.Backend.Hosting.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace FCRevolution.Backend.Hosting.Endpoints;

internal static class BackendWebPadEndpointModule
{
    internal static void Map(WebApplication app)
    {
        app.MapGet("/assets/webpad/app.js", context =>
            BackendStaticResponseWriter.WriteAsync(context, WebPadAssetCatalog.AppJs.Content, WebPadAssetCatalog.AppJs.ContentType));
        app.MapGet("/assets/webpad/app.css", context =>
            BackendStaticResponseWriter.WriteAsync(context, WebPadAssetCatalog.AppCss.Content, WebPadAssetCatalog.AppCss.ContentType));
        app.MapGet("/", context =>
            BackendStaticResponseWriter.WriteAsync(context, WebPadAssetCatalog.IndexHtml.Content, WebPadAssetCatalog.IndexHtml.ContentType));
        app.Map("/ws", wsApp => wsApp.Run(context =>
            context.RequestServices.GetRequiredService<BackendControlWebSocketHandler>()
                .HandleAsync(context, context.RequestAborted)));
    }
}

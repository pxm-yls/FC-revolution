using FCRevolution.Backend.Abstractions;
using FCRevolution.Backend.Hosting.Streaming;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FCRevolution.Backend.Hosting.Endpoints;

internal static class BackendStreamWebSocketEndpointModule
{
    internal static void Map(WebApplication app)
    {
        app.MapGet("/api/sessions/{sessionId:guid}/stream/ws", async context =>
        {
            if (!TryGetSessionId(context, out var sessionId))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var streamBridge = context.RequestServices.GetRequiredService<IBackendStreamSubscriptionBridge>();
            var streamHandler = context.RequestServices.GetRequiredService<BackendStreamWebSocketHandler>();
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            await streamHandler.HandleAsync(webSocket, streamBridge, sessionId, context.RequestAborted);
        });
    }

    private static bool TryGetSessionId(HttpContext context, out Guid sessionId)
    {
        if (context.Request.RouteValues.TryGetValue("sessionId", out var rawSessionId) &&
            Guid.TryParse(rawSessionId?.ToString(), out sessionId))
        {
            return true;
        }

        sessionId = Guid.Empty;
        return false;
    }
}

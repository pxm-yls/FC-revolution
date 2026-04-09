using System.Text;
using Microsoft.AspNetCore.Http;

namespace FCRevolution.Backend.Hosting.Endpoints;

internal static class BackendStaticResponseWriter
{
    internal static async Task WriteAsync(HttpContext context, string content, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = contentType;
        context.Response.ContentLength = bytes.Length;
        context.Response.Headers.Connection = "close";
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.Body.WriteAsync(bytes, context.RequestAborted);
    }
}

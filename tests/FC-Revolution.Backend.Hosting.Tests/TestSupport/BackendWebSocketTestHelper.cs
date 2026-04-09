using System.Net.WebSockets;
using System.Text.Json;

namespace FC_Revolution.Backend.Hosting.Tests;

internal static class BackendWebSocketTestHelper
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal static async Task<ClientWebSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var socket = new ClientWebSocket();
        var token = GetToken(cancellationToken, out var timeoutSource);
        try
        {
            await socket.ConnectAsync(uri, token);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
        finally
        {
            timeoutSource?.Dispose();
        }
    }

    internal static async Task SendJsonAsync(ClientWebSocket socket, object payload, CancellationToken cancellationToken = default)
    {
        var token = GetToken(cancellationToken, out var timeoutSource);
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, token);
        }
        finally
        {
            timeoutSource?.Dispose();
        }
    }

    internal static async Task<JsonDocument> ReceiveJsonAsync(ClientWebSocket socket, CancellationToken cancellationToken = default)
    {
        var message = await ReceiveMessageAsync(socket, cancellationToken);
        Assert.Equal(WebSocketMessageType.Text, message.MessageType);
        return JsonDocument.Parse(message.Payload);
    }

    internal static async Task<WebSocketTestMessage> ReceiveBinaryAsync(ClientWebSocket socket, CancellationToken cancellationToken = default)
    {
        var message = await ReceiveMessageAsync(socket, cancellationToken);
        Assert.Equal(WebSocketMessageType.Binary, message.MessageType);
        return message;
    }

    internal static async Task<WebSocketTestMessage> ReceiveMessageAsync(ClientWebSocket socket, CancellationToken cancellationToken = default)
    {
        var token = GetToken(cancellationToken, out var timeoutSource);
        try
        {
            using var stream = new MemoryStream();
            var buffer = new byte[4096];
            try
            {
                while (true)
                {
                    var result = await socket.ReceiveAsync(buffer.AsMemory(), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return new WebSocketTestMessage(result.MessageType, []);

                    stream.Write(buffer, 0, result.Count);
                    if (result.EndOfMessage)
                        return new WebSocketTestMessage(result.MessageType, stream.ToArray());
                }
            }
            catch (WebSocketException)
            {
                return new WebSocketTestMessage(WebSocketMessageType.Close, []);
            }
        }
        finally
        {
            timeoutSource?.Dispose();
        }
    }

    private static CancellationToken GetToken(CancellationToken cancellationToken, out CancellationTokenSource? timeoutSource)
    {
        if (cancellationToken.CanBeCanceled)
        {
            timeoutSource = null;
            return cancellationToken;
        }

        timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return timeoutSource.Token;
    }
}

internal readonly record struct WebSocketTestMessage(WebSocketMessageType MessageType, byte[] Payload);

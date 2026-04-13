using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FCRevolution.Contracts.RemoteControl;

namespace FCRevolution.Backend.Hosting.WebSockets;

internal sealed class ControlMessageCodec
{
    private readonly WebSocket _socket;

    internal ControlMessageCodec(WebSocket socket)
    {
        _socket = socket;
    }

    internal bool IsSocketOpen => _socket.State == WebSocketState.Open;

    internal static Guid? ParseSessionId(string? raw)
        => Guid.TryParse(raw, out var parsed) ? parsed : null;

    internal static bool TryResolveControlPort(string? portId, out string resolvedPortId)
    {
        resolvedPortId = string.IsNullOrWhiteSpace(portId) ? string.Empty : portId.Trim();
        return !string.IsNullOrWhiteSpace(resolvedPortId);
    }

    internal static SocketMessage CreateSocketMessage(
        string type,
        string? message = null,
        string? sessionId = null,
        string? portId = null)
        => new(type, message, sessionId, portId);

    internal async Task<SocketClientMessage?> ReceiveClientMessageAsync(CancellationToken cancellationToken)
    {
        var buffer = new ArraySegment<byte>(new byte[4096]);
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await _socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            stream.Write(buffer.Array!, buffer.Offset, result.Count);
            if (result.EndOfMessage)
                break;
        }

        var json = Encoding.UTF8.GetString(stream.ToArray());
        return JsonSerializer.Deserialize<SocketClientMessage>(json, BackendJsonDefaults.SerializerOptions);
    }

    internal Task SendSocketMessageAsync(SocketMessage message)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, BackendJsonDefaults.SerializerOptions);
        return _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
    }
}

internal sealed record SocketClientMessage(
    string Action,
    string? SessionId,
    string? Button,
    bool? Pressed,
    string? ClientName,
    string? PortId = null,
    IReadOnlyList<InputActionValueDto>? Inputs = null);

internal sealed record SocketMessage(
    string Type,
    string? Message = null,
    string? SessionId = null,
    string? PortId = null);

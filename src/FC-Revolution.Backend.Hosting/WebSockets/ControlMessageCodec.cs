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

    internal static bool IsSupportedPlayer(int? player)
        => player is { } value && RemoteControlPorts.IsSupportedPlayer(value);

    internal static bool TryResolveControlPort(string? portId, int? player, out int resolvedPlayer, out string resolvedPortId)
    {
        var normalizedPortId = RemoteControlPorts.NormalizePortId(portId);
        if (normalizedPortId != null && RemoteControlPorts.TryGetPlayer(normalizedPortId, out resolvedPlayer))
        {
            resolvedPortId = normalizedPortId;
            return true;
        }

        if (player is { } value &&
            RemoteControlPorts.IsSupportedPlayer(value) &&
            RemoteControlPorts.TryGetPortId(value, out resolvedPortId))
        {
            resolvedPlayer = value;
            return true;
        }

        resolvedPlayer = default;
        resolvedPortId = string.Empty;
        return false;
    }

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
    int? Player,
    string? Button,
    bool? Pressed,
    string? ClientName,
    string? PortId = null,
    IReadOnlyList<InputActionValueDto>? Inputs = null);

internal sealed record SocketMessage(
    string Type,
    string? Message = null,
    string? SessionId = null,
    int? Player = null,
    string? PortId = null);

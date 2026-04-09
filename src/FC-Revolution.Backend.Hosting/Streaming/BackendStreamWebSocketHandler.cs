using System.Net.WebSockets;
using System.Threading.Channels;
using FCRevolution.Backend.Abstractions;
using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.Backend.Hosting.Streaming;

internal sealed class BackendStreamWebSocketHandler
{
    private readonly BackendStreamSettingsStore _streamSettings;

    public BackendStreamWebSocketHandler(BackendStreamSettingsStore streamSettings)
    {
        _streamSettings = streamSettings;
    }

    internal async Task HandleAsync(
        WebSocket webSocket,
        IBackendStreamSubscriptionBridge bridge,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var subscription = await bridge.SubscribeStreamAsync(sessionId, 882, cancellationToken);
        if (subscription == null)
            return;

        await using var activeSubscription = subscription;
        var sendLock = new SemaphoreSlim(1, 1);
        var videoTask = PushVideoFramesAsync(webSocket, sendLock, subscription.VideoReader, cancellationToken);
        var audioTask = PushAudioChunksAsync(webSocket, sendLock, subscription.AudioReader, cancellationToken);

        try
        {
            await Task.WhenAll(videoTask, audioTask);
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
        finally
        {
            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "stream ended", CancellationToken.None);
                }
                catch
                {
                }
            }
        }
    }

    private async Task PushVideoFramesAsync(
        WebSocket webSocket,
        SemaphoreSlim sendLock,
        ChannelReader<VideoFramePacket> reader,
        CancellationToken cancellationToken)
    {
        using var videoEncoder = new BackendVideoFrameEncoder();
        var sequence = 0;

        await foreach (var packet in reader.ReadAllAsync(cancellationToken))
        {
            if (webSocket.State != WebSocketState.Open)
                break;

            var scale = _streamSettings.ScaleMultiplier;
            var quality = _streamSettings.JpegQuality;
            var enhancement = _streamSettings.EnhancementMode;

            if (packet.Width != 256 || packet.Height != 240)
                continue;

            var frame = videoEncoder.Encode(packet.Pixels, scale, quality, enhancement);
            if (frame == null)
                continue;

            var (message, messageLength) = BackendStreamPacketWriter.RentMessage(
                frame.Value.Jpeg.Length,
                BackendStreamProtocol.MessageTypeVideo,
                BackendStreamProtocol.CodecJpeg,
                sequence++,
                frame.Value.Metadata);
            BackendStreamPacketWriter.CopyPayload(message, frame.Value.Jpeg);

            try
            {
                await SendBinaryAsync(webSocket, sendLock, message, messageLength, cancellationToken);
            }
            finally
            {
                BackendStreamPacketWriter.Return(message);
            }
        }
    }

    private static async Task PushAudioChunksAsync(
        WebSocket webSocket,
        SemaphoreSlim sendLock,
        ChannelReader<AudioPacket> reader,
        CancellationToken cancellationToken)
    {
        var sequence = 0;

        await foreach (var packet in reader.ReadAllAsync(cancellationToken))
        {
            if (webSocket.State != WebSocketState.Open)
                break;

            var payloadLength = BackendAudioChunkEncoder.GetPayloadLength(packet.Samples);
            var (message, messageLength) = BackendStreamPacketWriter.RentMessage(
                payloadLength,
                BackendStreamProtocol.MessageTypeAudio,
                BackendStreamProtocol.CodecPcm16Mono,
                sequence++,
                BackendAudioChunkEncoder.OutputSampleRate);
            BackendAudioChunkEncoder.FillPcm16Le(packet.Samples, BackendStreamPacketWriter.GetPayloadSpan(message, payloadLength));

            try
            {
                await SendBinaryAsync(webSocket, sendLock, message, messageLength, cancellationToken);
            }
            finally
            {
                BackendStreamPacketWriter.Return(message);
            }
        }
    }

    private static async Task SendBinaryAsync(
        WebSocket webSocket,
        SemaphoreSlim sendLock,
        byte[] message,
        int messageLength,
        CancellationToken cancellationToken)
    {
        await sendLock.WaitAsync(cancellationToken);
        try
        {
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.SendAsync(
                    message.AsMemory(0, messageLength),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    cancellationToken);
            }
        }
        finally
        {
            sendLock.Release();
        }
    }
}

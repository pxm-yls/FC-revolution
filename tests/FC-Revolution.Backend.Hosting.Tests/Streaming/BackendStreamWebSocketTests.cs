using System.Buffers.Binary;
using System.Net;
using System.Net.WebSockets;
using System.Threading.Channels;
using FCRevolution.Backend.Abstractions;
using FCRevolution.Backend.Hosting;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.Backend.Hosting.Tests;

public sealed class BackendStreamWebSocketTests
{
    [Fact]
    public async Task Stream_WebSocket_Route_Returns_BadRequest_For_Http_Request()
    {
        await using var host = await BackendHostServiceTestHost.StartAsync();

        using var response = await host.Client.GetAsync($"api/sessions/{Guid.NewGuid()}/stream/ws");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Stream_WebSocket_Pushes_Video_And_Audio_Messages_With_Current_Stream_Settings()
    {
        var videoChannel = Channel.CreateUnbounded<VideoFramePacket>();
        var audioChannel = Channel.CreateUnbounded<AudioPacket>();
        videoChannel.Writer.TryWrite(CreateVideoPacket(0xFF00FF00u));
        videoChannel.Writer.TryComplete();
        audioChannel.Writer.TryWrite(CreateAudioPacket([0f, 0.25f, -0.25f, 0.5f]));
        audioChannel.Writer.TryComplete();

        var bridge = new RecordingRuntimeBridge
        {
            StreamSubscription = new BackendStreamSubscription(videoChannel.Reader, audioChannel.Reader)
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        host.Service.UpdateStreamParameters(3, 90, PixelEnhancementMode.None);

        var sessionId = Guid.NewGuid();
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(
            host.CreateWebSocketUri($"/api/sessions/{sessionId}/stream/ws"));

        var messages = new List<WebSocketTestMessage>();
        while (messages.Count < 2)
        {
            var message = await BackendWebSocketTestHelper.ReceiveMessageAsync(socket);
            if (message.MessageType == WebSocketMessageType.Close)
                break;

            messages.Add(message);
        }

        Assert.Equal((sessionId, 882), bridge.StreamSubscriptionCalls.Single());
        Assert.Equal(2, messages.Count);

        var videoMessage = Assert.Single(messages, message => message.Payload[0] == 0x01);
        var audioMessage = Assert.Single(messages, message => message.Payload[0] == 0x02);

        Assert.True(videoMessage.Payload.Length > 12);
        Assert.Equal(0x01, videoMessage.Payload[1]);
        Assert.Equal(0x01, videoMessage.Payload[2]);

        var videoMetadata = BinaryPrimitives.ReadInt32LittleEndian(videoMessage.Payload.AsSpan(8, 4));
        Assert.Equal(768, (videoMetadata >> 16) & 0xFFFF);
        Assert.Equal(720, videoMetadata & 0xFFFF);

        Assert.True(audioMessage.Payload.Length > 12);
        Assert.Equal(0x01, audioMessage.Payload[1]);
        Assert.Equal(0x01, audioMessage.Payload[2]);
        Assert.Equal(48000, BinaryPrimitives.ReadInt32LittleEndian(audioMessage.Payload.AsSpan(8, 4)));
    }

    [Fact]
    public async Task Stream_WebSocket_Null_Subscription_Ends_Cleanly_Without_Payload()
    {
        var bridge = new RecordingRuntimeBridge
        {
            StreamSubscription = null
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        var sessionId = Guid.NewGuid();
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(
            host.CreateWebSocketUri($"/api/sessions/{sessionId}/stream/ws"));

        var message = await BackendWebSocketTestHelper.ReceiveMessageAsync(socket);
        Assert.Equal(WebSocketMessageType.Close, message.MessageType);
        Assert.Equal((sessionId, 882), bridge.StreamSubscriptionCalls.Single());
    }

    [Fact]
    public async Task Stream_WebSocket_Empty_Readers_Close_Without_Data_Frames()
    {
        var videoChannel = Channel.CreateUnbounded<VideoFramePacket>();
        var audioChannel = Channel.CreateUnbounded<AudioPacket>();
        videoChannel.Writer.TryComplete();
        audioChannel.Writer.TryComplete();

        var bridge = new RecordingRuntimeBridge
        {
            StreamSubscription = new BackendStreamSubscription(videoChannel.Reader, audioChannel.Reader)
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        var sessionId = Guid.NewGuid();
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(
            host.CreateWebSocketUri($"/api/sessions/{sessionId}/stream/ws"));

        var message = await BackendWebSocketTestHelper.ReceiveMessageAsync(socket);
        Assert.Equal(WebSocketMessageType.Close, message.MessageType);
        Assert.Equal((sessionId, 882), bridge.StreamSubscriptionCalls.Single());
    }

    [Fact]
    public async Task Stream_WebSocket_NonDefault_EnhancementMode_Still_Produces_Video_With_Metadata()
    {
        var videoChannel = Channel.CreateUnbounded<VideoFramePacket>();
        var audioChannel = Channel.CreateUnbounded<AudioPacket>();
        videoChannel.Writer.TryWrite(CreateVideoPacket(0xFF336699u));
        videoChannel.Writer.TryComplete();
        audioChannel.Writer.TryComplete();

        var bridge = new RecordingRuntimeBridge
        {
            StreamSubscription = new BackendStreamSubscription(videoChannel.Reader, audioChannel.Reader)
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        host.Service.UpdateStreamParameters(2, 85, PixelEnhancementMode.CrtScanlines);

        var sessionId = Guid.NewGuid();
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(
            host.CreateWebSocketUri($"/api/sessions/{sessionId}/stream/ws"));

        var message = await BackendWebSocketTestHelper.ReceiveBinaryAsync(socket);
        Assert.Equal(0x01, message.Payload[0]); // video
        Assert.Equal(0x01, message.Payload[1]); // protocol version
        Assert.Equal(0x01, message.Payload[2]); // jpeg codec
        Assert.True(message.Payload.Length > 12);

        var metadata = BinaryPrimitives.ReadInt32LittleEndian(message.Payload.AsSpan(8, 4));
        Assert.Equal(512, (metadata >> 16) & 0xFFFF);
        Assert.Equal(480, metadata & 0xFFFF);
        Assert.Equal((sessionId, 882), bridge.StreamSubscriptionCalls.Single());
    }

    [Fact]
    public async Task Stream_WebSocket_Completes_Subscription_And_Disposes_It_When_Readers_End()
    {
        var videoChannel = Channel.CreateUnbounded<VideoFramePacket>();
        var audioChannel = Channel.CreateUnbounded<AudioPacket>();
        videoChannel.Writer.TryComplete();
        audioChannel.Writer.TryComplete();

        var disposeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bridge = new RecordingRuntimeBridge
        {
            StreamSubscription = new BackendStreamSubscription(
                videoChannel.Reader,
                audioChannel.Reader,
                disposeAsync: () =>
                {
                    disposeSignal.TrySetResult();
                    return ValueTask.CompletedTask;
                })
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        var sessionId = Guid.NewGuid();
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(
            host.CreateWebSocketUri($"/api/sessions/{sessionId}/stream/ws"));

        var message = await BackendWebSocketTestHelper.ReceiveMessageAsync(socket);
        Assert.Equal(WebSocketMessageType.Close, message.MessageType);

        await host.Service.StopAsync(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => disposeSignal.Task.IsCompleted);
        Assert.Equal((sessionId, 882), bridge.StreamSubscriptionCalls.Single());
    }

    [Fact]
    public async Task Stream_WebSocket_Client_Abort_Disposes_Subscription_Even_When_Video_Reader_Remains_Open()
    {
        var videoChannel = Channel.CreateUnbounded<VideoFramePacket>();
        var audioChannel = Channel.CreateUnbounded<AudioPacket>();
        videoChannel.Writer.TryWrite(CreateVideoPacket(0xFFAA3300u));
        audioChannel.Writer.TryComplete();

        var disposeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bridge = new RecordingRuntimeBridge
        {
            StreamSubscription = new BackendStreamSubscription(
                videoChannel.Reader,
                audioChannel.Reader,
                disposeAsync: () =>
                {
                    disposeSignal.TrySetResult();
                    return ValueTask.CompletedTask;
                })
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        var sessionId = Guid.NewGuid();
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(
            host.CreateWebSocketUri($"/api/sessions/{sessionId}/stream/ws"));

        var firstMessage = await BackendWebSocketTestHelper.ReceiveMessageAsync(socket);
        Assert.Equal(WebSocketMessageType.Binary, firstMessage.MessageType);

        socket.Abort();
        videoChannel.Writer.TryWrite(CreateVideoPacket(0xFF00AA11u));

        await WaitUntilAsync(() => disposeSignal.Task.IsCompleted, timeoutMs: 3000);
        videoChannel.Writer.TryComplete();
        Assert.Equal((sessionId, 882), bridge.StreamSubscriptionCalls.Single());
    }

    private static uint[] CreateSolidFrame(uint pixel)
    {
        var frame = new uint[256 * 240];
        Array.Fill(frame, pixel);
        return frame;
    }

    private static VideoFramePacket CreateVideoPacket(uint pixel) => new()
    {
        Pixels = CreateSolidFrame(pixel),
        Width = 256,
        Height = 240,
        PixelFormat = "argb32",
        PresentationIndex = 1,
        TimestampSeconds = 0
    };

    private static AudioPacket CreateAudioPacket(float[] samples) => new()
    {
        Samples = samples,
        SampleRate = 44744,
        Channels = 1,
        SampleFormat = "f32",
        SampleCount = samples.Length,
        TimestampSeconds = 0
    };

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if ((DateTime.UtcNow - start).TotalMilliseconds > timeoutMs)
                throw new TimeoutException("Condition was not met within the timeout.");

            await Task.Delay(20);
        }
    }
}

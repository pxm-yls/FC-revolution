using System.Net.WebSockets;
using System.Text;
using FCRevolution.Contracts.RemoteControl;

namespace FC_Revolution.Backend.Hosting.Tests;

public sealed class BackendControlWebSocketTests
{
    [Fact]
    public async Task Control_WebSocket_Claim_Heartbeat_Button_And_Release_Delegate_To_Runtime_Bridge()
    {
        var bridge = new RecordingRuntimeBridge
        {
            ClaimControlResult = true,
            SetInputStateResult = true
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(host.CreateWebSocketUri("/ws"));
        using var ready = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        var sessionId = Guid.NewGuid();

        Assert.Equal("ready", ready.RootElement.GetProperty("type").GetString());

        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "claim",
            sessionId = sessionId.ToString(),
            portId = "p1",
            clientName = "ipad"
        });

        using var claimed = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("claimed", claimed.RootElement.GetProperty("type").GetString());
        Assert.Equal("控制权已分配", claimed.RootElement.GetProperty("message").GetString());

        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "heartbeat",
            sessionId = sessionId.ToString(),
            portId = "p1"
        });

        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "button",
            sessionId = sessionId.ToString(),
            portId = "p1",
            button = "A",
            pressed = true
        });

        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "release",
            sessionId = sessionId.ToString(),
            portId = "p1"
        });

        using var released = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("released", released.RootElement.GetProperty("type").GetString());
        Assert.Equal("控制权已释放", released.RootElement.GetProperty("message").GetString());

        Assert.Equal(sessionId, bridge.ClaimCalls.Single().SessionId);
        Assert.EndsWith("127.0.0.1", bridge.ClaimCalls.Single().Request.ClientIp, StringComparison.Ordinal);
        Assert.Equal("ipad", bridge.ClaimCalls.Single().Request.ClientName);
        Assert.Equal("p1", bridge.ClaimCalls.Single().Request.PortId);
        Assert.Null(bridge.ClaimCalls.Single().Request.Player);
        Assert.Equal(sessionId, bridge.HeartbeatCalls.Single().SessionId);
        Assert.Equal("p1", bridge.HeartbeatCalls.Single().Request.PortId);
        Assert.Null(bridge.HeartbeatCalls.Single().Request.Player);
        var buttonInput = Assert.Single(bridge.InputStateCalls);
        var action = Assert.Single(buttonInput.Request.Actions);
        Assert.Equal("p1", action.PortId);
        Assert.Equal("A", action.ActionId);
        Assert.Equal(1f, action.Value);
        Assert.Equal(sessionId, bridge.ReleaseCalls.Single().SessionId);
        Assert.Equal("p1", bridge.ReleaseCalls.Single().Request.PortId);
        Assert.Null(bridge.ReleaseCalls.Single().Request.Player);
        Assert.Equal("网页控制已释放", bridge.ReleaseCalls.Single().Request.Reason);
    }

    [Fact]
    public async Task Control_WebSocket_Button_Uses_Generic_Action_When_Legacy_Enum_Does_Not_Match()
    {
        var bridge = new RecordingRuntimeBridge
        {
            ClaimControlResult = true,
            SetInputStateResult = true
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(host.CreateWebSocketUri("/ws"));
        using var ready = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("ready", ready.RootElement.GetProperty("type").GetString());

        var sessionId = Guid.NewGuid();
        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "claim",
            sessionId = sessionId.ToString(),
            portId = "p1"
        });
        using var claimed = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("claimed", claimed.RootElement.GetProperty("type").GetString());

        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "button",
            sessionId = sessionId.ToString(),
            portId = "p1",
            button = "turboA",
            pressed = true
        });

        await WaitUntilAsync(() => bridge.InputStateCalls.Count == 1);
        var buttonCall = Assert.Single(bridge.InputStateCalls);
        var action = Assert.Single(buttonCall.Request.Actions);
        Assert.Equal("p1", action.PortId);
        Assert.Equal("turboA", action.ActionId);
        Assert.Equal(1f, action.Value);
    }

    [Fact]
    public async Task Control_WebSocket_Input_Delegates_Generic_Input_Actions_To_Runtime_Bridge()
    {
        var bridge = new RecordingRuntimeBridge
        {
            ClaimControlResult = true,
            SetInputStateResult = true
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(host.CreateWebSocketUri("/ws"));
        using var ready = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("ready", ready.RootElement.GetProperty("type").GetString());

        var sessionId = Guid.NewGuid();
        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "claim",
            sessionId = sessionId.ToString(),
            portId = "p1"
        });
        using var claimed = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("claimed", claimed.RootElement.GetProperty("type").GetString());

        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "input",
            sessionId = sessionId.ToString(),
            portId = "p1",
            inputs = new[]
            {
                new { portId = "p1", deviceType = "gamepad", actionId = "x", value = 1.0f },
                new { portId = "p1", deviceType = "gamepad", actionId = "l1", value = 1.0f }
            }
        });

        await WaitUntilAsync(() => bridge.InputStateCalls.Count == 1);
        var inputCall = Assert.Single(bridge.InputStateCalls);
        Assert.Equal(sessionId, inputCall.SessionId);
        Assert.Equal(2, inputCall.Request.Actions.Count);
        Assert.Equal("p1", inputCall.Request.Actions[0].PortId);
        Assert.Equal("x", inputCall.Request.Actions[0].ActionId);
        Assert.Equal(1f, inputCall.Request.Actions[0].Value);
        Assert.Equal("l1", inputCall.Request.Actions[1].ActionId);
    }

    [Fact]
    public async Task Control_WebSocket_Button_Without_Claim_Returns_Error()
    {
        await using var host = await BackendHostServiceTestHost.StartAsync(new RecordingRuntimeBridge
        {
            ClaimControlResult = true,
            SetInputStateResult = true
        });
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(host.CreateWebSocketUri("/ws"));
        using var ready = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);

        Assert.Equal("ready", ready.RootElement.GetProperty("type").GetString());

        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "button",
            button = "A",
            pressed = true
        });

        using var error = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("error", error.RootElement.GetProperty("type").GetString());
        Assert.Equal("请先 claim 玩家槽位", error.RootElement.GetProperty("message").GetString());

        Assert.Empty(host.Bridge.ClaimCalls);
    }

    [Fact]
    public async Task Control_WebSocket_Claim_Switch_Releases_Previous_Target()
    {
        var bridge = new RecordingRuntimeBridge
        {
            ClaimControlResult = true,
            SetInputStateResult = true
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(host.CreateWebSocketUri("/ws"));
        using var ready = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("ready", ready.RootElement.GetProperty("type").GetString());

        var firstSessionId = Guid.NewGuid();
        var secondSessionId = Guid.NewGuid();

        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "claim",
            sessionId = firstSessionId.ToString(),
            portId = "p1"
        });
        using var firstClaimed = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("claimed", firstClaimed.RootElement.GetProperty("type").GetString());

        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "claim",
            sessionId = secondSessionId.ToString(),
            portId = "p2"
        });
        using var secondClaimed = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("claimed", secondClaimed.RootElement.GetProperty("type").GetString());

        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "release",
            sessionId = secondSessionId.ToString(),
            portId = "p2"
        });
        using var released = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("released", released.RootElement.GetProperty("type").GetString());

        Assert.Equal(2, bridge.ClaimCalls.Count);
        var switchedRelease = bridge.ReleaseCalls.Single(call => call.SessionId == firstSessionId && call.Request.PortId == "p1");
        Assert.Equal("p1", switchedRelease.Request.PortId);
        Assert.Null(switchedRelease.Request.Player);
        Assert.Equal("网页控制目标已切换", switchedRelease.Request.Reason);
    }

    [Fact]
    public async Task Control_WebSocket_Unknown_Action_Is_Ignored_And_Followup_Claim_Still_Works()
    {
        var bridge = new RecordingRuntimeBridge
        {
            ClaimControlResult = true,
            SetInputStateResult = true
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(host.CreateWebSocketUri("/ws"));
        using var ready = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("ready", ready.RootElement.GetProperty("type").GetString());

        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "noop",
            sessionId = Guid.NewGuid().ToString(),
            player = 0
        });

        var sessionId = Guid.NewGuid();
        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "claim",
            sessionId = sessionId.ToString(),
            portId = "p1"
        });

        using var claimed = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("claimed", claimed.RootElement.GetProperty("type").GetString());
        Assert.Equal(sessionId.ToString(), claimed.RootElement.GetProperty("sessionId").GetString());
        Assert.Single(bridge.ClaimCalls);
    }

    [Fact]
    public async Task Control_WebSocket_Invalid_Json_After_Claim_Releases_On_Disconnect()
    {
        var bridge = new RecordingRuntimeBridge
        {
            ClaimControlResult = true,
            SetInputStateResult = true
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(host.CreateWebSocketUri("/ws"));
        using var ready = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("ready", ready.RootElement.GetProperty("type").GetString());

        var sessionId = Guid.NewGuid();
        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "claim",
            sessionId = sessionId.ToString(),
            player = 0
        });
        using var claimed = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("claimed", claimed.RootElement.GetProperty("type").GetString());

        var invalidPayload = Encoding.UTF8.GetBytes("{\"action\":\"claim\"");
        await socket.SendAsync(invalidPayload, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

        await WaitUntilAsync(() =>
            bridge.ReleaseCalls.Any(call =>
                call.SessionId == sessionId &&
                call.Request.PortId == "p1" &&
                call.Request.Reason == "网页连接已断开，已恢复本地控制"));

        Assert.All(
            bridge.ReleaseCalls.Where(call => call.SessionId == sessionId && call.Request.PortId == "p1"),
            call => Assert.Null(call.Request.Player));
    }

    [Fact]
    public async Task Control_WebSocket_Repeated_Release_Is_Handled_Without_Extra_Disconnect_Release()
    {
        var bridge = new RecordingRuntimeBridge
        {
            ClaimControlResult = true,
            SetInputStateResult = true
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(host.CreateWebSocketUri("/ws"));
        using var ready = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("ready", ready.RootElement.GetProperty("type").GetString());

        var sessionId = Guid.NewGuid();
        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "claim",
            sessionId = sessionId.ToString(),
            portId = "p1"
        });
        using var claimed = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("claimed", claimed.RootElement.GetProperty("type").GetString());

        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "release",
            sessionId = sessionId.ToString(),
            portId = "p1"
        });
        using var firstReleased = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("released", firstReleased.RootElement.GetProperty("type").GetString());

        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "release",
            sessionId = sessionId.ToString(),
            portId = "p1"
        });
        using var secondReleased = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("released", secondReleased.RootElement.GetProperty("type").GetString());

        Assert.Equal(2, bridge.ReleaseCalls.Count(call =>
            call.SessionId == sessionId &&
            call.Request.PortId == "p1" &&
            call.Request.Reason == "网页控制已释放"));
        Assert.All(
            bridge.ReleaseCalls.Where(call => call.SessionId == sessionId && call.Request.PortId == "p1"),
            call => Assert.Null(call.Request.Player));
        Assert.DoesNotContain(bridge.ReleaseCalls, call => call.Request.Reason == "网页连接已断开，已恢复本地控制");
    }

    [Fact]
    public async Task Control_WebSocket_Normal_Client_Close_After_Claim_Releases_Lease()
    {
        var bridge = new RecordingRuntimeBridge
        {
            ClaimControlResult = true,
            SetInputStateResult = true
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(host.CreateWebSocketUri("/ws"));
        using var ready = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("ready", ready.RootElement.GetProperty("type").GetString());

        var sessionId = Guid.NewGuid();
        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "claim",
            sessionId = sessionId.ToString(),
            portId = "p2"
        });
        using var claimed = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("claimed", claimed.RootElement.GetProperty("type").GetString());

        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "client done", CancellationToken.None);

        await WaitUntilAsync(() =>
            bridge.ReleaseCalls.Any(call =>
                call.SessionId == sessionId &&
                call.Request.PortId == "p2" &&
                call.Request.Reason == "网页连接已断开，已恢复本地控制"));

        Assert.All(
            bridge.ReleaseCalls.Where(call => call.SessionId == sessionId && call.Request.PortId == "p2"),
            call => Assert.Null(call.Request.Player));
    }

    [Fact]
    public async Task Control_WebSocket_Accepts_Custom_PortIds()
    {
        var bridge = new RecordingRuntimeBridge
        {
            ClaimControlResult = true,
            SetInputStateResult = true
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        using var socket = await BackendWebSocketTestHelper.ConnectAsync(host.CreateWebSocketUri("/ws"));
        using var ready = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("ready", ready.RootElement.GetProperty("type").GetString());

        var sessionId = Guid.NewGuid();
        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "claim",
            sessionId = sessionId.ToString(),
            portId = "pad-west"
        });

        using var claimed = await BackendWebSocketTestHelper.ReceiveJsonAsync(socket);
        Assert.Equal("claimed", claimed.RootElement.GetProperty("type").GetString());
        Assert.Equal("pad-west", claimed.RootElement.GetProperty("portId").GetString());

        await BackendWebSocketTestHelper.SendJsonAsync(socket, new
        {
            action = "button",
            sessionId = sessionId.ToString(),
            portId = "pad-west",
            button = "jump",
            pressed = true
        });

        await WaitUntilAsync(() => bridge.InputStateCalls.Count == 1);

        var claimCall = Assert.Single(bridge.ClaimCalls);
        Assert.Equal("pad-west", claimCall.Request.PortId);
        Assert.Null(claimCall.Request.Player);

        var inputCall = Assert.Single(bridge.InputStateCalls);
        var action = Assert.Single(inputCall.Request.Actions);
        Assert.Equal("pad-west", action.PortId);
        Assert.Equal("jump", action.ActionId);
        Assert.Equal(1f, action.Value);
    }

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

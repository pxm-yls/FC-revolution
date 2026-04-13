using System.Net;
using System.Net.Http.Json;
using FCRevolution.Backend.Abstractions;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Sessions;

namespace FC_Revolution.Backend.Hosting.Tests;

public sealed class BackendApiEndpointTests
{
    [Fact]
    public async Task Sync_Endpoints_Populate_Rom_And_Session_Read_Models()
    {
        await using var host = await BackendHostServiceTestHost.StartAsync();
        var roms = new[]
        {
            new RomSummaryDto("Contra", "/roms/contra.nes", true, true)
        };
        var sessions = new[]
        {
            new GameSessionSummaryDto(Guid.NewGuid(), "Contra", "/roms/contra.nes", "当前本地控制", PlayerControlSourceDto.Local, PlayerControlSourceDto.Remote)
        };

        using var romResponse = await host.Client.PostAsJsonAsync("internal/sync/roms", roms);
        using var sessionResponse = await host.Client.PostAsJsonAsync("internal/sync/sessions", sessions);

        romResponse.EnsureSuccessStatusCode();
        sessionResponse.EnsureSuccessStatusCode();

        var syncedRoms = await host.Client.GetFromJsonAsync<List<RomSummaryDto>>("api/roms");
        var syncedSessions = await host.Client.GetFromJsonAsync<List<GameSessionSummaryDto>>("api/sessions");

        Assert.NotNull(syncedRoms);
        Assert.Single(syncedRoms!);
        Assert.Equal("Contra", syncedRoms[0].DisplayName);

        Assert.NotNull(syncedSessions);
        Assert.Single(syncedSessions!);
        Assert.Equal("当前本地控制", syncedSessions[0].ControlSummary);
        Assert.Equal(PlayerControlSourceDto.Remote, syncedSessions[0].Player2ControlSource);
    }

    [Fact]
    public async Task Rom_Preview_Route_Returns_File_When_Runtime_Provides_Asset()
    {
        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, "video-bytes"u8.ToArray());
        var bridge = new RecordingRuntimeBridge
        {
            RomPreviewAsset = new BackendMediaAsset(tempPath, "video/mp4")
        };

        try
        {
            await using var host = await BackendHostServiceTestHost.StartAsync(bridge);

            using var response = await host.Client.GetAsync($"api/roms/preview?romPath={Uri.EscapeDataString("/roms/mario.nes")}");

            response.EnsureSuccessStatusCode();
            Assert.Equal("video/mp4", response.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task Control_Routes_Delegate_To_Runtime_Bridge()
    {
        var bridge = new RecordingRuntimeBridge
        {
            StartSessionResult = new StartSessionResponse(Guid.NewGuid()),
            CloseSessionResult = true,
            PreviewBytes = "png-bytes"u8.ToArray(),
            ClaimControlResult = true,
            SetInputStateResult = true
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        var sessionId = Guid.NewGuid();

        using var start = await host.Client.PostAsJsonAsync("api/sessions", new StartSessionRequest("/roms/mario.nes"));
        using var close = await host.Client.PostAsync($"api/sessions/{sessionId}/close", content: null);
        using var preview = await host.Client.GetAsync($"api/sessions/{sessionId}/preview");
        using var claim = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/claim",
            new ClaimControlRequest(ClientIp: "127.0.0.1", ClientName: "ipad", PortId: "p1"));
        using var legacyButton = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/buttons",
            new
            {
                player = 0,
                button = "A",
                pressed = true
            });
        using var genericButton = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/buttons",
            new
            {
                player = 1,
                pressed = true,
                portId = "p2",
                actionId = "right"
            });
        using var genericInput = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/input",
            new SetInputStateRequest(
            [
                new InputActionValueDto("p1", "gamepad", "x", 1f),
                new InputActionValueDto("p1", "gamepad", "l1", 1f)
            ]));
        using var heartbeat = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/heartbeat",
            new RefreshHeartbeatRequest(PortId: "p1"));
        using var release = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/release",
            new ReleaseControlRequest(Reason: "done", PortId: "p1"));

        start.EnsureSuccessStatusCode();
        close.EnsureSuccessStatusCode();
        preview.EnsureSuccessStatusCode();
        claim.EnsureSuccessStatusCode();
        legacyButton.EnsureSuccessStatusCode();
        genericButton.EnsureSuccessStatusCode();
        genericInput.EnsureSuccessStatusCode();
        heartbeat.EnsureSuccessStatusCode();
        release.EnsureSuccessStatusCode();

        Assert.Equal("/roms/mario.nes", bridge.StartSessionRequests.Single().RomPath);
        Assert.Equal(sessionId, Assert.Single(bridge.CloseSessionCalls));
        Assert.Equal(sessionId, Assert.Single(bridge.PreviewCalls));
        Assert.Equal(sessionId, bridge.ClaimCalls.Single().SessionId);
        Assert.Equal("127.0.0.1", bridge.ClaimCalls.Single().Request.ClientIp);
        Assert.Equal("p1", bridge.ClaimCalls.Single().Request.PortId);
        Assert.Equal(3, bridge.InputStateCalls.Count);
        var legacyButtonCall = bridge.InputStateCalls[0];
        Assert.Equal(sessionId, legacyButtonCall.SessionId);
        var legacyButtonAction = Assert.Single(legacyButtonCall.Request.Actions);
        Assert.Equal("p1", legacyButtonAction.PortId);
        Assert.Equal("A", legacyButtonAction.ActionId);
        Assert.Equal(1f, legacyButtonAction.Value);
        var genericButtonCall = bridge.InputStateCalls[1];
        Assert.Equal(sessionId, genericButtonCall.SessionId);
        var genericButtonAction = Assert.Single(genericButtonCall.Request.Actions);
        Assert.Equal("p2", genericButtonAction.PortId);
        Assert.Equal("right", genericButtonAction.ActionId);
        Assert.Equal(1f, genericButtonAction.Value);
        var inputCall = bridge.InputStateCalls[2];
        Assert.Equal(sessionId, inputCall.SessionId);
        Assert.Equal(2, inputCall.Request.Actions.Count);
        Assert.Equal("p1", inputCall.Request.Actions[0].PortId);
        Assert.Equal("x", inputCall.Request.Actions[0].ActionId);
        Assert.Equal(1f, inputCall.Request.Actions[0].Value);
        Assert.Equal("l1", inputCall.Request.Actions[1].ActionId);
        Assert.Equal("p1", bridge.HeartbeatCalls.Single().Request.PortId);
        Assert.Single(bridge.ReleaseCalls);
        Assert.Equal("p1", bridge.ReleaseCalls.Single().Request.PortId);
    }

    [Fact]
    public async Task Session_Preview_Returns_NotFound_When_Runtime_Has_No_Image()
    {
        await using var host = await BackendHostServiceTestHost.StartAsync();

        using var response = await host.Client.GetAsync($"api/sessions/{Guid.NewGuid()}/preview");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Control_Routes_Return_Conflict_When_Runtime_Rejects()
    {
        var bridge = new RecordingRuntimeBridge
        {
            CloseSessionResult = false,
            ClaimControlResult = false,
            SetInputStateResult = false
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        var sessionId = Guid.NewGuid();

        using var close = await host.Client.PostAsync($"api/sessions/{sessionId}/close", content: null);
        using var claim = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/claim",
            new ClaimControlRequest(ClientIp: "127.0.0.1", PortId: "p1"));
        using var button = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/buttons",
            new
            {
                player = 0,
                pressed = true,
                actionId = "b"
            });
        using var input = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/input",
            new SetInputStateRequest([new InputActionValueDto("p1", "gamepad", "a", 1f)]));

        Assert.Equal(HttpStatusCode.NotFound, close.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, claim.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, button.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, input.StatusCode);
    }

    [Fact]
    public async Task Control_Routes_Normalize_Legacy_Player_Requests_To_PortId_First()
    {
        var bridge = new RecordingRuntimeBridge
        {
            ClaimControlResult = true
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        var sessionId = Guid.NewGuid();

        using var claim = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/claim",
            new
            {
                player = 1,
                clientIp = "127.0.0.1",
                clientName = "ipad"
            });
        using var heartbeat = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/heartbeat",
            new
            {
                player = 1
            });
        using var release = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/release",
            new
            {
                player = 1,
                reason = "done"
            });

        claim.EnsureSuccessStatusCode();
        heartbeat.EnsureSuccessStatusCode();
        release.EnsureSuccessStatusCode();

        var claimRequest = Assert.Single(bridge.ClaimCalls).Request;
        Assert.Equal("p2", claimRequest.PortId);
        Assert.Null(claimRequest.Player);

        var heartbeatRequest = Assert.Single(bridge.HeartbeatCalls).Request;
        Assert.Equal("p2", heartbeatRequest.PortId);
        Assert.Null(heartbeatRequest.Player);

        var releaseRequest = Assert.Single(bridge.ReleaseCalls).Request;
        Assert.Equal("p2", releaseRequest.PortId);
        Assert.Null(releaseRequest.Player);
    }

    [Fact]
    public async Task Control_Routes_Accept_Custom_PortIds_Without_Forcing_Legacy_P1_P2()
    {
        var bridge = new RecordingRuntimeBridge
        {
            ClaimControlResult = true,
            SetInputStateResult = true
        };

        await using var host = await BackendHostServiceTestHost.StartAsync(bridge);
        var sessionId = Guid.NewGuid();

        using var claim = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/claim",
            new ClaimControlRequest(ClientIp: "127.0.0.1", PortId: " pad-west "));
        using var heartbeat = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/heartbeat",
            new RefreshHeartbeatRequest(PortId: " pad-west "));
        using var button = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/buttons",
            new
            {
                portId = " pad-west ",
                actionId = "jump",
                pressed = true
            });
        using var release = await host.Client.PostAsJsonAsync(
            $"api/sessions/{sessionId}/release",
            new ReleaseControlRequest(Reason: "done", PortId: " pad-west "));

        claim.EnsureSuccessStatusCode();
        heartbeat.EnsureSuccessStatusCode();
        button.EnsureSuccessStatusCode();
        release.EnsureSuccessStatusCode();

        var claimRequest = Assert.Single(bridge.ClaimCalls).Request;
        Assert.Equal("pad-west", claimRequest.PortId);
        Assert.Null(claimRequest.Player);

        var heartbeatRequest = Assert.Single(bridge.HeartbeatCalls).Request;
        Assert.Equal("pad-west", heartbeatRequest.PortId);
        Assert.Null(heartbeatRequest.Player);

        var inputRequest = Assert.Single(bridge.InputStateCalls).Request;
        var action = Assert.Single(inputRequest.Actions);
        Assert.Equal("pad-west", action.PortId);
        Assert.Equal("jump", action.ActionId);

        var releaseRequest = Assert.Single(bridge.ReleaseCalls).Request;
        Assert.Equal("pad-west", releaseRequest.PortId);
        Assert.Null(releaseRequest.Player);
    }
}

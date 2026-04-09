using System.Collections.ObjectModel;
using Avalonia.Input;
using FCRevolution.Backend.Hosting;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Core.Input;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.Tests;

public sealed class SessionRemoteControlServiceTests
{
    [Fact]
    public void ClaimControl_UsesPortId_WhenProvided()
    {
        var gameSession = new FakeGameSessionService
        {
            ClaimResult = true
        };
        var service = new SessionRemoteControlService(gameSession, _ => { });
        var sessionId = Guid.NewGuid();

        var claimed = service.ClaimControl(
            sessionId,
            new ClaimControlRequest(ClientIp: "127.0.0.1", ClientName: "ipad", PortId: "p2"));

        Assert.True(claimed);
        Assert.Equal(sessionId, gameSession.LastClaimSessionId);
        Assert.Equal(1, gameSession.LastClaimPlayer);
        Assert.Equal("127.0.0.1", gameSession.LastClaimClientIp);
        Assert.Equal("ipad", gameSession.LastClaimClientName);
    }

    [Fact]
    public void ReleaseControl_UsesPortId_WhenProvided()
    {
        var gameSession = new FakeGameSessionService();
        var service = new SessionRemoteControlService(gameSession, _ => { });
        var sessionId = Guid.NewGuid();

        service.ReleaseControl(
            sessionId,
            new ReleaseControlRequest(Reason: "done", PortId: "p2"));

        Assert.Equal(sessionId, gameSession.LastReleaseSessionId);
        Assert.Equal(1, gameSession.LastReleasePlayer);
        Assert.Equal("done", gameSession.LastReleaseReason);
    }

    [Fact]
    public void RefreshHeartbeat_UsesPortId_WhenProvided()
    {
        var gameSession = new FakeGameSessionService();
        var service = new SessionRemoteControlService(gameSession, _ => { });
        var sessionId = Guid.NewGuid();

        service.RefreshHeartbeat(
            sessionId,
            new RefreshHeartbeatRequest(PortId: "p2"));

        Assert.Equal(sessionId, gameSession.LastHeartbeatSessionId);
        Assert.Equal(1, gameSession.LastHeartbeatPlayer);
    }

    [Fact]
    public void SetInputState_ForwardsGenericActions_AndPreservesValues()
    {
        var gameSession = new FakeGameSessionService();
        var service = new SessionRemoteControlService(gameSession, _ => { });
        var sessionId = Guid.NewGuid();
        var request = new SetInputStateRequest(
        [
            new InputActionValueDto("p1", "gamepad", "A", 0.49f),
            new InputActionValueDto("p2", "gamepad", "right", 0.5f)
        ]);

        var applied = service.SetInputState(sessionId, request);

        Assert.True(applied);
        Assert.Equal(2, gameSession.SetInputCalls.Count);

        var first = gameSession.SetInputCalls[0];
        Assert.Equal(sessionId, first.SessionId);
        Assert.Equal("p1", first.PortId);
        Assert.Equal("A", first.ActionId);
        Assert.Equal(0.49f, first.Value);

        var second = gameSession.SetInputCalls[1];
        Assert.Equal(sessionId, second.SessionId);
        Assert.Equal("p2", second.PortId);
        Assert.Equal("right", second.ActionId);
        Assert.Equal(0.5f, second.Value);
    }

    [Fact]
    public void SetInputState_ReturnsFalse_ForUnknownPort()
    {
        var gameSession = new FakeGameSessionService();
        var service = new SessionRemoteControlService(gameSession, _ => { });
        var request = new SetInputStateRequest(
        [
            new InputActionValueDto("p3", "gamepad", "a", 1f)
        ]);

        var applied = service.SetInputState(Guid.NewGuid(), request);

        Assert.False(applied);
        Assert.Empty(gameSession.SetInputCalls);
    }

    [Fact]
    public void SetInputState_ForwardsUnknownAction_ToSessionLayer()
    {
        var gameSession = new FakeGameSessionService();
        var service = new SessionRemoteControlService(gameSession, _ => { });
        var sessionId = Guid.NewGuid();
        var request = new SetInputStateRequest(
        [
            new InputActionValueDto("p1", "gamepad", "fire", 1f)
        ]);

        var applied = service.SetInputState(sessionId, request);

        Assert.True(applied);
        var call = Assert.Single(gameSession.SetInputCalls);
        Assert.Equal(sessionId, call.SessionId);
        Assert.Equal("p1", call.PortId);
        Assert.Equal("fire", call.ActionId);
        Assert.Equal(1f, call.Value);
    }

    [Fact]
    public void SetInputState_ForwardsAliasAndReservedActions_ToSessionLayer()
    {
        var gameSession = new FakeGameSessionService();
        var service = new SessionRemoteControlService(gameSession, _ => { });
        var request = new SetInputStateRequest(
        [
            new InputActionValueDto("p1", "gamepad", "x", 1f),
            new InputActionValueDto("p1", "gamepad", "l1", 1f)
        ]);

        var applied = service.SetInputState(Guid.NewGuid(), request);

        Assert.True(applied);
        Assert.Equal(2, gameSession.SetInputCalls.Count);
        Assert.Equal("x", gameSession.SetInputCalls[0].ActionId);
        Assert.Equal("l1", gameSession.SetInputCalls[1].ActionId);
    }

    [Fact]
    public void SetInputState_ReturnsFalse_WhenAnyGenericInputApplyFails()
    {
        var gameSession = new FakeGameSessionService();
        gameSession.TrySetResults.Enqueue(true);
        gameSession.TrySetResults.Enqueue(false);
        var service = new SessionRemoteControlService(gameSession, _ => { });
        var request = new SetInputStateRequest(
        [
            new InputActionValueDto("p1", "gamepad", "a", 1f),
            new InputActionValueDto("p2", "gamepad", "b", 1f)
        ]);

        var applied = service.SetInputState(Guid.NewGuid(), request);

        Assert.False(applied);
        Assert.Equal(2, gameSession.SetInputCalls.Count);
    }

    private sealed class FakeGameSessionService : IGameSessionService
    {
        public ObservableCollection<ActiveGameSessionItem> Sessions { get; } = [];
        public int Count => Sessions.Count;
        public bool HasAny => Sessions.Count > 0;
        public List<SetInputCall> SetInputCalls { get; } = [];
        public Queue<bool> TrySetResults { get; } = [];
        public bool ClaimResult { get; set; }
        public Guid? LastClaimSessionId { get; private set; }
        public int LastClaimPlayer { get; private set; } = -1;
        public string? LastClaimClientIp { get; private set; }
        public string? LastClaimClientName { get; private set; }
        public Guid? LastReleaseSessionId { get; private set; }
        public int LastReleasePlayer { get; private set; } = -1;
        public string? LastReleaseReason { get; private set; }
        public Guid? LastHeartbeatSessionId { get; private set; }
        public int LastHeartbeatPlayer { get; private set; } = -1;

        public ActiveGameSessionItem StartSessionWithInputBindings(
            string displayName,
            string romPath,
            GameAspectRatioMode aspectRatioMode,
            IReadOnlyDictionary<string, Dictionary<string, Key>> inputBindingsByPort,
            IReadOnlyList<ExtraInputBindingProfile>? extraInputBindings = null,
            Action? onSessionsChanged = null,
            MacUpscaleMode upscaleMode = MacUpscaleMode.None,
            MacUpscaleOutputResolution upscaleOutputResolution = MacUpscaleOutputResolution.Hd1080,
            PixelEnhancementMode enhancementMode = PixelEnhancementMode.None,
            double volume = 15.0,
            IReadOnlyDictionary<string, ShortcutGesture>? shortcutBindings = null,
            string? coreId = null) => throw new NotSupportedException();

        public void CloseSession(ActiveGameSessionItem session) => throw new NotSupportedException();
        public void CloseAllSessions() => throw new NotSupportedException();
        public ActiveGameSessionItem? FindSession(Guid sessionId) => throw new NotSupportedException();
        public bool TryAcquireRemoteControl(Guid sessionId, int player, string clientIp, string? clientName = null)
        {
            LastClaimSessionId = sessionId;
            LastClaimPlayer = player;
            LastClaimClientIp = clientIp;
            LastClaimClientName = clientName;
            return ClaimResult;
        }

        public void ReleaseRemoteControl(Guid sessionId, int player, string? reason = null)
        {
            LastReleaseSessionId = sessionId;
            LastReleasePlayer = player;
            LastReleaseReason = reason;
        }

        public void RefreshRemoteHeartbeat(Guid sessionId, int player)
        {
            LastHeartbeatSessionId = sessionId;
            LastHeartbeatPlayer = player;
        }

        public bool TrySetRemoteInputState(Guid sessionId, string portId, string actionId, float value, string? clientIp = null, string? clientName = null)
        {
            SetInputCalls.Add(new SetInputCall(sessionId, portId, actionId, value));
            if (TrySetResults.Count > 0)
                return TrySetResults.Dequeue();
            return true;
        }

        public bool IsRemoteOwner(Guid sessionId, int player, string clientIp, string? clientName = null) => throw new NotSupportedException();
        public bool AnyForRomPath(string romPath) => throw new NotSupportedException();
    }

    private readonly record struct SetInputCall(Guid SessionId, string PortId, string ActionId, float Value);
}

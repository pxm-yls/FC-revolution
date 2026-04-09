using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Avalonia.Input;
using FCRevolution.Backend.Hosting;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.AppServices;

public sealed class GameSessionService : IGameSessionService
{
    private readonly GameSessionRegistry _registry = new();

    public ObservableCollection<ActiveGameSessionItem> Sessions => _registry.Sessions;
    public int Count => _registry.Count;
    public bool HasAny => _registry.HasAny;

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
        string? coreId = null) =>
        _registry.StartSessionWithInputBindings(
            displayName,
            romPath,
            aspectRatioMode,
            inputBindingsByPort,
            extraInputBindings,
            onSessionsChanged,
            upscaleMode,
            upscaleOutputResolution,
            enhancementMode,
            volume,
            shortcutBindings,
            coreId);

    public void CloseSession(ActiveGameSessionItem session) => _registry.CloseSession(session);
    public void CloseAllSessions() => _registry.CloseAllSessions();
    public ActiveGameSessionItem? FindSession(Guid sessionId) => _registry.FindSession(sessionId);
    public bool TryAcquireRemoteControl(Guid sessionId, int player, string clientIp, string? clientName = null) =>
        _registry.TryAcquireRemoteControl(sessionId, player, clientIp, clientName);
    public void ReleaseRemoteControl(Guid sessionId, int player, string? reason = null) =>
        _registry.ReleaseRemoteControl(sessionId, player, reason);
    public void RefreshRemoteHeartbeat(Guid sessionId, int player) => _registry.RefreshRemoteHeartbeat(sessionId, player);
    public bool TrySetRemoteInputState(Guid sessionId, string portId, string actionId, float value, string? clientIp = null, string? clientName = null) =>
        _registry.TrySetRemoteInputState(sessionId, portId, actionId, value, clientIp, clientName);
    public bool IsRemoteOwner(Guid sessionId, int player, string clientIp, string? clientName = null) =>
        _registry.IsRemoteOwner(sessionId, player, clientIp, clientName);
    public bool AnyForRomPath(string romPath) => _registry.AnyForRomPath(romPath);
}

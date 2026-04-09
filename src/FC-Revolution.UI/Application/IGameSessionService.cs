using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Avalonia.Input;
using FCRevolution.Backend.Hosting;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.AppServices;

public interface IGameSessionService
{
    ObservableCollection<ActiveGameSessionItem> Sessions { get; }
    int Count { get; }
    bool HasAny { get; }
    ActiveGameSessionItem StartSessionWithInputBindings(
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
        string? coreId = null);
    void CloseSession(ActiveGameSessionItem session);
    void CloseAllSessions();
    ActiveGameSessionItem? FindSession(Guid sessionId);
    bool TryAcquireRemoteControl(Guid sessionId, int player, string clientIp, string? clientName = null);
    void ReleaseRemoteControl(Guid sessionId, int player, string? reason = null);
    void RefreshRemoteHeartbeat(Guid sessionId, int player);
    bool TrySetRemoteInputState(Guid sessionId, string portId, string actionId, float value, string? clientIp = null, string? clientName = null);
    bool IsRemoteOwner(Guid sessionId, int player, string clientIp, string? clientName = null);
    bool AnyForRomPath(string romPath);
}

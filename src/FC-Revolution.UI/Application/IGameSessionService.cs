using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Avalonia.Input;
using FCRevolution.Backend.Hosting;
using FCRevolution.Core.Input;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.AppServices;

public interface IGameSessionService
{
    ObservableCollection<ActiveGameSessionItem> Sessions { get; }
    int Count { get; }
    bool HasAny { get; }
    ActiveGameSessionItem StartSession(
        string displayName,
        string romPath,
        GameAspectRatioMode aspectRatioMode,
        IReadOnlyDictionary<int, Dictionary<NesButton, Key>> inputMaps,
        IReadOnlyList<ExtraInputBindingProfile>? extraInputBindings = null,
        Action? onSessionsChanged = null,
        MacUpscaleMode upscaleMode = MacUpscaleMode.None,
        MacUpscaleOutputResolution upscaleOutputResolution = MacUpscaleOutputResolution.Hd1080,
        PixelEnhancementMode enhancementMode = PixelEnhancementMode.None,
        double volume = 15.0,
        IReadOnlyDictionary<string, ShortcutGesture>? shortcutBindings = null);
    ActiveGameSessionItem StartSessionWithCore(
        string displayName,
        string romPath,
        GameAspectRatioMode aspectRatioMode,
        IReadOnlyDictionary<int, Dictionary<NesButton, Key>> inputMaps,
        IReadOnlyList<ExtraInputBindingProfile>? extraInputBindings = null,
        Action? onSessionsChanged = null,
        MacUpscaleMode upscaleMode = MacUpscaleMode.None,
        MacUpscaleOutputResolution upscaleOutputResolution = MacUpscaleOutputResolution.Hd1080,
        PixelEnhancementMode enhancementMode = PixelEnhancementMode.None,
        double volume = 15.0,
        IReadOnlyDictionary<string, ShortcutGesture>? shortcutBindings = null,
        string? coreId = null) =>
        StartSession(
            displayName,
            romPath,
            aspectRatioMode,
            inputMaps,
            extraInputBindings,
            onSessionsChanged,
            upscaleMode,
            upscaleOutputResolution,
            enhancementMode,
            volume,
            shortcutBindings);
    void CloseSession(ActiveGameSessionItem session);
    void CloseAllSessions();
    ActiveGameSessionItem? FindSession(Guid sessionId);
    bool TryAcquireRemoteControl(Guid sessionId, int player, string clientIp, string? clientName = null);
    void ReleaseRemoteControl(Guid sessionId, int player, string? reason = null);
    void RefreshRemoteHeartbeat(Guid sessionId, int player);
    bool TrySetRemoteInputState(Guid sessionId, string portId, string actionId, float value, string? clientIp = null, string? clientName = null);
    bool TrySetRemoteButtonState(Guid sessionId, int player, NesButton button, bool pressed, string? clientIp = null, string? clientName = null);
    void ClearRemoteButtons(Guid sessionId, int player);
    bool IsRemoteOwner(Guid sessionId, int player, string clientIp, string? clientName = null);
    bool AnyForRomPath(string romPath);
}

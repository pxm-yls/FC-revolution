using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.AppServices;

internal sealed class SessionLifecycleService
{
    private readonly IReadOnlyList<RomLibraryItem> _romLibrary;
    private readonly IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, Key>>> _romInputOverrides;
    private readonly ObservableCollection<InputBindingEntry> _globalInputBindings;
    private readonly IGameSessionService _gameSessionService;
    private readonly Func<GameAspectRatioMode> _getAspectRatioMode;
    private readonly Func<MacUpscaleMode> _getUpscaleMode;
    private readonly Func<MacUpscaleOutputResolution> _getUpscaleOutputResolution;
    private readonly Func<IReadOnlyDictionary<string, ShortcutGesture>> _getGameWindowShortcutBindings;
    private readonly Action _syncLoadedFlags;
    private readonly Func<string, string, bool> _pathsEqual;
    private readonly Action<string> _reportStatus;

    public SessionLifecycleService(
        IReadOnlyList<RomLibraryItem> romLibrary,
        IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverrides,
        ObservableCollection<InputBindingEntry> globalInputBindings,
        IGameSessionService gameSessionService,
        Func<GameAspectRatioMode> getAspectRatioMode,
        Func<MacUpscaleMode> getUpscaleMode,
        Func<MacUpscaleOutputResolution> getUpscaleOutputResolution,
        Func<IReadOnlyDictionary<string, ShortcutGesture>> getGameWindowShortcutBindings,
        Action syncLoadedFlags,
        Func<string, string, bool> pathsEqual,
        Action<string> reportStatus)
    {
        _romLibrary = romLibrary;
        _romInputOverrides = romInputOverrides;
        _globalInputBindings = globalInputBindings;
        _gameSessionService = gameSessionService;
        _getAspectRatioMode = getAspectRatioMode;
        _getUpscaleMode = getUpscaleMode;
        _getUpscaleOutputResolution = getUpscaleOutputResolution;
        _getGameWindowShortcutBindings = getGameWindowShortcutBindings;
        _syncLoadedFlags = syncLoadedFlags;
        _pathsEqual = pathsEqual;
        _reportStatus = reportStatus;
    }

    public StartSessionResponse? StartSession(StartSessionRequest request)
    {
        var rom = _romLibrary.FirstOrDefault(item => _pathsEqual(item.Path, request.RomPath));
        if (rom == null)
            return null;

        var inputMaps = _romInputOverrides.TryGetValue(rom.Path, out var overrideMap)
            ? CloneActionBindings(overrideMap)
            : InputBindingContractAdapter.BuildActionBindingsFromEntries(_globalInputBindings);
        var session = _gameSessionService.StartSessionWithInputBindings(
            rom.DisplayName,
            rom.Path,
            _getAspectRatioMode(),
            inputMaps,
            null,
            _syncLoadedFlags,
            _getUpscaleMode(),
            _getUpscaleOutputResolution(),
            shortcutBindings: _getGameWindowShortcutBindings(),
            coreId: ResolveOptionalCoreId(request));
        _reportStatus($"局域网点播已启动: {rom.DisplayName}");
        return new StartSessionResponse(session.SessionId);
    }

    public bool CloseSession(Guid sessionId)
    {
        var session = _gameSessionService.FindSession(sessionId);
        if (session == null)
            return false;

        _gameSessionService.CloseSession(session);
        _reportStatus($"局域网页端已关闭游戏窗口: {session.DisplayName}");
        return true;
    }

    private static Dictionary<string, Dictionary<string, Key>> CloneActionBindings(
        IReadOnlyDictionary<string, Dictionary<string, Key>> source)
    {
        return source.ToDictionary(
            pair => pair.Key,
            pair => new Dictionary<string, Key>(pair.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveOptionalCoreId(StartSessionRequest request)
    {
        var property = request.GetType().GetProperty("CoreId");
        if (property?.PropertyType != typeof(string))
            return null;

        return property.GetValue(request) as string;
    }
}

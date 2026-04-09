using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Threading;
using FCRevolution.Backend.Abstractions;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Contracts.Roms;
using FCRevolution.Contracts.Sessions;
using FCRevolution.Core;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.AppServices;

public sealed class ArcadeRuntimeContractAdapter : IArcadeRuntimeContractAdapter
{
    private readonly IReadOnlyList<RomLibraryItem> _romLibrary;
    private readonly IGameSessionService _gameSessionService;
    private readonly Func<string, string, bool> _pathsEqual;
    private readonly SessionQueryService _sessionQueryService;
    private readonly SessionRemoteControlService _sessionRemoteControlService;
    private readonly SessionLifecycleService _sessionLifecycleService;
    private readonly PreviewAssetResolver _previewAssetResolver = new();

    private readonly object _streamLock = new();
    private readonly Dictionary<Guid, SessionStreamBroadcaster> _streamBroadcasters = new();

    public ArcadeRuntimeContractAdapter(
        IReadOnlyList<RomLibraryItem> romLibrary,
        IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, Key>>> romInputOverridesByPortAction,
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
        _gameSessionService = gameSessionService;
        _pathsEqual = pathsEqual;
        _sessionQueryService = new SessionQueryService(gameSessionService);
        _sessionRemoteControlService = new SessionRemoteControlService(gameSessionService, reportStatus);
        _sessionLifecycleService = new SessionLifecycleService(
            romLibrary,
            romInputOverridesByPortAction,
            globalInputBindings,
            gameSessionService,
            getAspectRatioMode,
            getUpscaleMode,
            getUpscaleOutputResolution,
            getGameWindowShortcutBindings,
            syncLoadedFlags,
            pathsEqual,
            reportStatus);
    }

    public IReadOnlyList<RomSummaryDto> GetRomSummaries() =>
        _romLibrary.Select(rom =>
        {
            var resolvedPreviewPath = _previewAssetResolver.ResolveRomPreviewAssetPath(rom.Path, rom.PreviewFilePath);
            var hasResolvedPreview = !string.IsNullOrWhiteSpace(resolvedPreviewPath) && File.Exists(resolvedPreviewPath);
            return new RomSummaryDto(
                rom.DisplayName,
                rom.Path,
                rom.IsLoaded,
                rom.HasPreview || hasResolvedPreview,
                $"/api/roms/preview?romPath={Uri.EscapeDataString(rom.Path)}",
                hasResolvedPreview ? resolvedPreviewPath : null);
        }).ToList();

    public IReadOnlyList<GameSessionSummaryDto> GetSessionSummaries() =>
        _sessionQueryService.GetSessionSummaries();

    public async Task<IReadOnlyList<RomSummaryDto>> GetRomsAsync(CancellationToken cancellationToken = default)
    {
        return await Dispatcher.UIThread.InvokeAsync(
            GetRomSummaries,
            DispatcherPriority.Background,
            cancellationToken);
    }

    public async Task<IReadOnlyList<GameSessionSummaryDto>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        return await Dispatcher.UIThread.InvokeAsync(
            GetSessionSummaries,
            DispatcherPriority.Background,
            cancellationToken);
    }

    public async Task<BackendMediaAsset?> GetRomPreviewAssetAsync(string romPath, CancellationToken cancellationToken = default)
    {
        return await Dispatcher.UIThread.InvokeAsync(
            () => _previewAssetResolver.ResolveRomPreviewAsset(_romLibrary, romPath, _pathsEqual));
    }

    public async Task<StartSessionResponse?> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default)
    {
        return await Dispatcher.UIThread.InvokeAsync(() => _sessionLifecycleService.StartSession(request));
    }

    public async Task<bool> CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await Dispatcher.UIThread.InvokeAsync(() => _sessionLifecycleService.CloseSession(sessionId));
    }

    public async Task<byte[]?> GetSessionPreviewAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await Dispatcher.UIThread.InvokeAsync(() => _sessionQueryService.GetSessionPreview(sessionId));
    }

    public async Task<bool> ClaimControlAsync(Guid sessionId, ClaimControlRequest request, CancellationToken cancellationToken = default)
    {
        return await Dispatcher.UIThread.InvokeAsync(() => _sessionRemoteControlService.ClaimControl(sessionId, request));
    }

    public async Task ReleaseControlAsync(Guid sessionId, ReleaseControlRequest request, CancellationToken cancellationToken = default)
    {
        await Dispatcher.UIThread.InvokeAsync(() => _sessionRemoteControlService.ReleaseControl(sessionId, request));
    }

    public Task RefreshHeartbeatAsync(Guid sessionId, RefreshHeartbeatRequest request, CancellationToken cancellationToken = default)
    {
        Dispatcher.UIThread.Post(() => _sessionRemoteControlService.RefreshHeartbeat(sessionId, request));
        return Task.CompletedTask;
    }

    public async Task<bool> SetInputStateAsync(Guid sessionId, SetInputStateRequest request, CancellationToken cancellationToken = default)
    {
        return await Dispatcher.UIThread.InvokeAsync(() => _sessionRemoteControlService.SetInputState(sessionId, request));
    }

    public async Task<BackendStreamSubscription?> SubscribeStreamAsync(Guid sessionId, int audioChunkSize = 882, CancellationToken cancellationToken = default)
    {
        var session = await Dispatcher.UIThread.InvokeAsync(
            () => _gameSessionService.FindSession(sessionId),
            DispatcherPriority.Background,
            cancellationToken);
        if (session == null)
            return null;

        SessionStreamBroadcaster broadcaster;
        lock (_streamLock)
        {
            if (!_streamBroadcasters.TryGetValue(sessionId, out broadcaster!))
            {
                broadcaster = new SessionStreamBroadcaster(sessionId, session.ViewModel.CoreSession, RemoveBroadcaster);
                _streamBroadcasters[sessionId] = broadcaster;
            }
        }

        return broadcaster.Subscribe(audioChunkSize);
    }

    private void RemoveBroadcaster(Guid sessionId, SessionStreamBroadcaster broadcaster)
    {
        lock (_streamLock)
        {
            if (_streamBroadcasters.TryGetValue(sessionId, out var current) && ReferenceEquals(current, broadcaster))
                _streamBroadcasters.Remove(sessionId);
        }
    }

}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using FCRevolution.Backend.Hosting;
using FCRevolution.Core.Timeline.Persistence;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Emulation.Host;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.Audio;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.Views;

namespace FC_Revolution.UI.ViewModels;

public sealed partial class GameWindowViewModel : ViewModelBase, IDisposable
{
    private static readonly int ScreenWidth = FrameRenderDefaults.Width;
    private static readonly int ScreenHeight = FrameRenderDefaults.Height;
    private static readonly GameWindowStatusToastController StatusToastController = new();

    private readonly IEmulatorCoreSession _coreSession;
    private readonly GameWindowFramePresenterController _framePresenter;
    private readonly GameWindowInputStateController _inputState = new();
    private readonly GameWindowRemoteControlWorkflowController _remoteControlWorkflow =
        new(RemoteControlStateController);
    private readonly GameWindowRemoteControlRuntimeController _remoteControlRuntime =
        new(RemoteControlStateController);
    private readonly GameWindowRenderDiagnosticsStateController _renderDiagnostics = new();
    private readonly GameWindowSessionRuntimeController _sessionRuntime;
    private readonly GameWindowSaveStateWorkflowController _saveStateWorkflow;
    private readonly GameWindowDebugWindowHost _debugWindowHost;
    private readonly GameWindowDebugWindowOpenController _debugWindowOpenController;
    private readonly GameWindowSessionFailureHandler _sessionFailureHandler;
    private readonly GameWindowSessionCommandController _sessionCommandController;
    private readonly GameWindowModifiedMemoryRuntimeController _modifiedMemoryRuntimeController;
    private readonly GameWindowRomLoadHandler _romLoadHandler;
    private readonly GameWindowProfileTrustHandler _profileTrustHandler;
    private readonly GameWindowDisposeHandler _disposeHandler;
    private readonly ITimeTravelService _timeTravelService;
    private readonly GameWindowLayeredFrameBuilderController _layeredFrameBuilder;
    private readonly CoreAudioPlayer _audio = new();

    internal IEmulatorCoreSession CoreSession => _coreSession;
    internal string CoreId => _coreSession.RuntimeInfo.CoreId;
    private readonly DispatcherTimer _uiTimer;
    private readonly DispatcherTimer _toastTimer;
    private readonly Stopwatch _fpsWatch = Stopwatch.StartNew();
    private readonly TimelineRepository _timelineRepository = new();
    private readonly string _romPath;
    private readonly string _romId;
    private readonly Dictionary<Key, (int Player, string ActionId)> _keyMap;
    private readonly IReadOnlyList<GameWindowResolvedExtraInputBinding> _extraInputBindings;
    private readonly HashSet<Key> _pressedKeys = [];
    private readonly CoreBranchTree _branchTree = new();
    private TimelineManifest _timelineManifest;
    private Guid _currentBranchId;
    private readonly GameWindowSessionLoopHost _sessionLoopHost;
    private volatile int _emuFpsRaw;
    private volatile int _frameTimeMicros;
    private int _fpsCounter;
    private int _branchGalleryRefreshTick;
    private WriteableBitmap? _screenBitmap;
    private string _statusText;
    private string _fpsText = "";
    private double _viewportAspectWidth = 256;
    private double _viewportAspectHeight = 240;
    private string _aspectRatioLabel = "原始 256:240";
    private readonly GameWindowShortcutRouter _shortcutRouter = new();
    private string _shortcutHintText = "";
    private MacUpscaleMode _configuredUpscaleMode = MacUpscaleMode.None;
    private MacUpscaleOutputResolution _configuredUpscaleOutputResolution = MacUpscaleOutputResolution.Hd1080;
    private string _viewportRendererLabel = "软件 / 无超分";
    private string _viewportRenderDiagnostics = "内部 256x240 -> 渲染 256x240 -> 显示 256x240";
    private bool _hasInitializedUpscaleMode;
    private bool _isOverlayVisible;
    private bool _isBranchGalleryVisible;
    private PixelEnhancementMode _enhancementMode;
    private bool _isDisposed;
    private bool _isRewinding;
    private bool _hasSessionFailure;
    private bool _profileTrustInitialized;
    private bool _turboPulseActive;
    private readonly Dictionary<Key, int> _turboTickCounters = new();
    private GamePlayerControlSource _player1ControlSource;
    private GamePlayerControlSource _player2ControlSource;
    private string _transientMessage = "";
    private string _remoteControlStatusText = "";
    private string _timelinePositionText = "时间线位置: 帧 0";
    private string _timelineHintText = "右侧可直接切换轴点和时间尺度；右键节点可载入、创建分支或生成画面节点";
    private DateTime _timelineManifestWriteTimeUtc;

    public GameWindowViewModel(
        string displayName,
        string romPath,
        GameAspectRatioMode aspectRatioMode,
        IReadOnlyDictionary<string, Dictionary<string, Key>> inputBindingsByPort,
        IReadOnlyList<ExtraInputBindingProfile>? extraInputBindings = null,
        MacUpscaleMode upscaleMode = MacUpscaleMode.None,
        MacUpscaleOutputResolution upscaleOutputResolution = MacUpscaleOutputResolution.Hd1080,
        PixelEnhancementMode enhancementMode = PixelEnhancementMode.None,
        double initialVolume = 15.0)
        : this(
            displayName,
            romPath,
            aspectRatioMode,
            inputBindingsByPort,
            CreateDefaultCoreSession(),
            extraInputBindings,
            upscaleMode,
            upscaleOutputResolution,
            enhancementMode,
            initialVolume)
    {
    }

    internal GameWindowViewModel(
        string displayName,
        string romPath,
        GameAspectRatioMode aspectRatioMode,
        IReadOnlyDictionary<string, Dictionary<string, Key>> inputBindingsByPort,
        IEmulatorCoreSession coreSession,
        IReadOnlyList<ExtraInputBindingProfile>? extraInputBindings = null,
        MacUpscaleMode upscaleMode = MacUpscaleMode.None,
        MacUpscaleOutputResolution upscaleOutputResolution = MacUpscaleOutputResolution.Hd1080,
        PixelEnhancementMode enhancementMode = PixelEnhancementMode.None,
        double initialVolume = 15.0)
    {
        _coreSession = coreSession;
        _timeTravelService = CoreSessionCapabilityResolver.ResolveTimeTravelService(coreSession);
        var inputStateWriter = CoreSessionCapabilityResolver.ResolveInputStateWriter(coreSession);
        _sessionRuntime = new GameWindowSessionRuntimeController(
            new object(),
            coreSession,
            CoreSessionCapabilityResolver.ResolveDebugSurface(coreSession),
            _timeTravelService,
            inputStateWriter);
        _sessionLoopHost = new GameWindowSessionLoopHost(
            $"GameSession-{displayName}",
            () => _isRewinding || _sessionRuntime.IsPaused,
            () => _sessionRuntime.RunFrame(),
            frameTimeMicros => _frameTimeMicros = frameTimeMicros,
            fps => _emuFpsRaw = fps,
            ex => Dispatcher.UIThread.Post(
                () => HandleSessionFailure("游戏运行异常，当前会话已停止，请重新启动游戏。", ex),
                DispatcherPriority.Send));
        _layeredFrameBuilder = new GameWindowLayeredFrameBuilderController(
            CoreSessionCapabilityResolver.ResolveLayeredFrameProvider(coreSession));
        _enhancementMode = enhancementMode;
        DisplayName = displayName;
        _romPath = romPath;
        _romId = TimelineStoragePaths.ComputeRomId(romPath);
        var timelineState = GameWindowTimelinePersistenceController.LoadTimelineState(
            _timelineRepository,
            _branchTree,
            _romId,
            displayName,
            _romPath,
            ScreenWidth,
            ScreenHeight);
        var sessionDebugWindowDisplaySettings = DebugWindowDisplaySettingsProfile.Sanitize(
            SystemConfigProfile.Load().DebugWindowDisplaySettings);
        _saveStateWorkflow = new GameWindowSaveStateWorkflowController(
            Path.ChangeExtension(romPath, ".fcs"),
            () => _sessionRuntime.CaptureState(),
            state => _sessionRuntime.RestoreState(state),
            () =>
            {
                RequestTemporalHistoryReset(MacMetalTemporalResetReason.SaveStateLoaded);
                RefreshBranchGallery(force: true);
            });
        _modifiedMemoryRuntimeController = new GameWindowModifiedMemoryRuntimeController(
            _sessionRuntime.ApplySavedMemoryDecision,
            _sessionRuntime.UpsertModifiedMemoryEntry,
            _sessionRuntime.RemoveModifiedMemoryEntry,
            _sessionRuntime.ReplaceModifiedMemoryEntries,
            _romPath,
            status => StatusText = status);
        var debugViewModelFactory = new GameWindowDebugViewModelFactory(
            displayName,
            romPath,
            _sessionRuntime,
            _modifiedMemoryRuntimeController.UpsertRuntimeEntry,
            _modifiedMemoryRuntimeController.RemoveRuntimeEntry,
            _modifiedMemoryRuntimeController.ReplaceRuntimeEntries,
            message => HandleSessionFailure(message));
        _debugWindowHost = new GameWindowDebugWindowHost(
            $"实时调试台 - {displayName}",
            () => debugViewModelFactory.Create(sessionDebugWindowDisplaySettings));
        _debugWindowOpenController = new GameWindowDebugWindowOpenController(
            new GameWindowDebugWindowWorkflowController(),
            () => _hasSessionFailure,
            () => Dispatcher.UIThread.CheckAccess(),
            action => Dispatcher.UIThread.Post(action, DispatcherPriority.Send),
            () => _debugWindowHost.OpenOrActivate(),
            UpdateStatus,
            ShowToast);
        _framePresenter = new GameWindowFramePresenterController(ScreenWidth, ScreenHeight);
        _sessionFailureHandler = new GameWindowSessionFailureHandler(
            new GameWindowSessionFailureController(),
            () => _isDisposed,
            () => _hasSessionFailure,
            () => _hasSessionFailure = true,
            () => _sessionLoopHost.Stop(),
            () => _framePresenter.ClearPendingFrame(),
            () => _sessionRuntime.PauseForFailure(),
            diagnostic => RuntimeDiagnostics.Write("session", diagnostic),
            UpdateStatus);
        _sessionCommandController = new GameWindowSessionCommandController(
            () => _saveStateWorkflow.QuickSave(),
            () => _saveStateWorkflow.QuickLoad(),
            () => _sessionRuntime.TogglePause(),
            UpdateStatus);
        _romLoadHandler = new GameWindowRomLoadHandler(
            () => _layeredFrameBuilder.ResetTemporalHistory(),
            romPath => _sessionRuntime.LoadRom(romPath),
            DescribeMapper,
            status => StatusText = status,
            message => RuntimeDiagnostics.Write("mapper", message),
            RequestTemporalHistoryReset);
        _profileTrustHandler = new GameWindowProfileTrustHandler(
            () => _profileTrustInitialized,
            () => _profileTrustInitialized = true,
            _romPath,
            RomConfigProfile.LoadValidated,
            ProfileTrustDialog.ShowAsync,
            RomConfigProfile.TrustCurrentMachine,
            _modifiedMemoryRuntimeController.ApplySavedProfile,
            status => StatusText = status);
        _disposeHandler = new GameWindowDisposeHandler(
            () => _isDisposed,
            () => _isDisposed = true,
            () => _framePresenter.ClearPendingFrame(),
            () =>
            {
                _coreSession.VideoFrameReady -= OnFrameReady;
                _coreSession.AudioReady -= OnAudioReady;
            },
            () => _sessionLoopHost.Stop(),
            () => _uiTimer?.Stop(),
            () => _toastTimer?.Stop(),
            () => _debugWindowHost.Close(),
            () => _audio.Dispose(),
            () => _coreSession.Dispose(),
            () => _framePresenter.Dispose());
        _timelineManifest = timelineState.Manifest;
        _currentBranchId = timelineState.CurrentBranchId;
        _keyMap = GameWindowInputBindingResolver.BuildKeyMap(inputBindingsByPort);
        _extraInputBindings = GameWindowInputBindingResolver.ResolveExtraInputBindings(extraInputBindings);
        _statusText = $"正在启动 {displayName}";
        ScreenBitmap = _framePresenter.CurrentBitmap;
        ApplyShortcutBindings(ShortcutCatalog.BuildDefaultGestureMap());
        ApplyUpscaleOutputResolution(upscaleOutputResolution);
        ApplyUpscaleMode(upscaleMode);

        _uiTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0)
        };
        _uiTimer.Tick += OnUiTick;
        _uiTimer.Start();
        _toastTimer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            ApplyToastClear();
        };

        _coreSession.VideoFrameReady += OnFrameReady;
        _coreSession.AudioReady += OnAudioReady;
        _audio.Volume = (float)initialVolume;
        ApplyAspectRatio(aspectRatioMode);
        if (!_audio.IsAvailable)
            ShowToast($"音频不可用: {_audio.InitError ?? "无可用输出设备"}");

        LoadRom(_romPath);
        BranchGallery = new BranchGalleryViewModel(
            _timeTravelService,
            _branchTree,
            _romPath,
            null,
            PersistBranchPoint,
            DeleteBranchPoint,
            RenameBranchPoint,
            ActivateBranch,
            exportRange: null,
            persistPreviewNode: PersistPreviewNode,
            deletePersistedPreviewNode: DeletePersistedPreviewNode,
            renamePersistedPreviewNode: RenamePersistedPreviewNode,
            notifyTimelineJump: () => RequestTemporalHistoryReset(MacMetalTemporalResetReason.TimelineJump),
            useCenteredTimelineRail: true);
        BranchGallery.ReplacePreviewNodes(timelineState.PreviewNodes);
        _timelineManifestWriteTimeUtc = timelineState.ManifestWriteTimeUtc;
        RefreshBranchGallery(force: true);
        _sessionLoopHost.Start();
    }

    private static IEmulatorCoreSession CreateDefaultCoreSession() =>
        DefaultEmulatorCoreHost.Create().CreateSession(new CoreSessionLaunchRequest());

    public string DisplayName { get; }

    public WriteableBitmap? ScreenBitmap
    {
        get => _screenBitmap;
        private set
        {
            if (SetProperty(ref _screenBitmap, value))
                ScreenBitmapUpdated?.Invoke(value);
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string FpsText
    {
        get => _fpsText;
        private set => SetProperty(ref _fpsText, value);
    }

    public string ShortcutHintText
    {
        get => _shortcutHintText;
        private set => SetProperty(ref _shortcutHintText, value);
    }

    public BranchGalleryViewModel BranchGallery { get; }

    public string TransientMessage
    {
        get => _transientMessage;
        private set
        {
            if (SetProperty(ref _transientMessage, value))
                OnPropertyChanged(nameof(HasTransientMessage));
        }
    }

    public bool HasTransientMessage => !string.IsNullOrWhiteSpace(TransientMessage);

    public string RemoteControlStatusText
    {
        get => _remoteControlStatusText;
        private set
        {
            if (SetProperty(ref _remoteControlStatusText, value))
                OnPropertyChanged(nameof(HasRemoteControlStatus));
        }
    }

    public bool HasRemoteControlStatus => !string.IsNullOrWhiteSpace(RemoteControlStatusText);

    public bool IsBranchGalleryVisible
    {
        get => _isBranchGalleryVisible;
        private set => SetProperty(ref _isBranchGalleryVisible, value);
    }

    public string TimelinePositionText
    {
        get => _timelinePositionText;
        private set => SetProperty(ref _timelinePositionText, value);
    }

    public string TimelineHintText
    {
        get => _timelineHintText;
        private set => SetProperty(ref _timelineHintText, value);
    }

    public event Action<WriteableBitmap?>? ScreenBitmapUpdated;
    public event Action<ReadOnlyMemory<uint>>? RawFramePresented;
    public event Action<LayeredFrameData>? LayeredFramePresented;

    public double ViewportAspectWidth
    {
        get => _viewportAspectWidth;
        private set => SetProperty(ref _viewportAspectWidth, value);
    }

    public double ViewportAspectHeight
    {
        get => _viewportAspectHeight;
        private set => SetProperty(ref _viewportAspectHeight, value);
    }

    public string AspectRatioLabel
    {
        get => _aspectRatioLabel;
        private set => SetProperty(ref _aspectRatioLabel, value);
    }

    public MacUpscaleMode UpscaleMode => _configuredUpscaleMode;

    public string ConfiguredUpscaleModeLabel => GetUpscaleModeLabel(_configuredUpscaleMode);

    public MacUpscaleOutputResolution UpscaleOutputResolution => _configuredUpscaleOutputResolution;

    public string ConfiguredUpscaleOutputResolutionLabel => GetUpscaleOutputResolutionLabel(_configuredUpscaleOutputResolution);

    public string ViewportRendererLabel
    {
        get => _viewportRendererLabel;
        private set => SetProperty(ref _viewportRendererLabel, value);
    }

    public string ViewportRenderDiagnostics
    {
        get => _viewportRenderDiagnostics;
        private set => SetProperty(ref _viewportRenderDiagnostics, value);
    }

    public MacMetalTemporalResetReason TemporalHistoryResetReason => _renderDiagnostics.TemporalResetReason;

    public int TemporalHistoryResetVersion => _renderDiagnostics.TemporalResetVersion;

    public bool IsOverlayVisible
    {
        get => _isOverlayVisible;
        private set => SetProperty(ref _isOverlayVisible, value);
    }

    public GamePlayerControlSource Player1ControlSource
    {
        get => _player1ControlSource;
        private set => SetProperty(ref _player1ControlSource, value);
    }

    public GamePlayerControlSource Player2ControlSource
    {
        get => _player2ControlSource;
        private set => SetProperty(ref _player2ControlSource, value);
    }

    public void SetVolume(double volume) => _audio.Volume = (float)volume;

    public void ApplyAspectRatio(GameAspectRatioMode mode)
    {
        var projection = GameWindowAspectRatioProjectionController.Build(mode);
        ViewportAspectWidth = projection.Width;
        ViewportAspectHeight = projection.Height;
        AspectRatioLabel = projection.Label;
    }

    private void UpdateStatus(string status, string? toast = null)
    {
        var update = StatusToastController.BuildStatusUpdate(status, toast);
        StatusText = update.StatusText;
        if (update.ShouldShowToast)
            ShowToast(update.ToastMessage!);
    }

    private void ShowToast(string message)
    {
        var state = StatusToastController.BuildToastState(message);
        TransientMessage = state.TransientMessage;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void ApplyToastClear()
    {
        var state = StatusToastController.BuildClearedToastState();
        TransientMessage = state.TransientMessage;
    }
}

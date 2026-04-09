using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FCRevolution.Backend.Hosting;
using FCRevolution.Core;
using FCRevolution.Core.Input;
using FCRevolution.Core.Replay;
using FCRevolution.Core.State;
using FCRevolution.Core.Timeline;
using FCRevolution.Core.Timeline.Persistence;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Emulation.Host;
using FCRevolution.Rendering.Metal;
using FCRevolution.Storage;
using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Audio;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.Models.Previews;
using FC_Revolution.UI.Views;

namespace FC_Revolution.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const int EmulatorFramesPerSecond = 60;
    private const double MinShortRewindSeconds = 1d;
    private const double MaxShortRewindSeconds = 30d;
    private static readonly int PreviewSourceWidth = NesConstants.ScreenWidth;
    private static readonly int PreviewSourceHeight = NesConstants.ScreenHeight;
    private const int PreviewDurationSeconds = 60;
    private const int PreviewPlaybackFps = 30;
    private const int PreviewSourceFps = 60;
    private const int PreviewAnimationFrameCount = PreviewDurationSeconds * PreviewPlaybackFps;
    private const int PreviewCaptureStride = PreviewSourceFps / PreviewPlaybackFps;
    private const int PreviewFrameIntervalMs = 1000 / PreviewPlaybackFps;
    private const string PreviewMagicV1 = "FCPV1";
    private const string PreviewMagicV2 = "FCPV2";
    private const string LegacyPreviewExtension = ".fcpv";
    private const string LegacyQuickSaveStateFormat = "legacy/nes-state";
    private static readonly string[] SupportedVideoPreviewExtensions = [".mp4", ".mov", ".m4v", ".webm"];
    private static readonly IReadOnlyList<string> SupportedLocalDisplayEnhancementOptions =
    [
        "无",
        "CRT 扫描线",
        "鲜艳色彩"
    ];
    private static readonly IReadOnlyList<string> SupportedMacUpscaleOutputResolutionOptions =
    [
        "1080p",
        "1440p",
        "4K"
    ];
    private static readonly FilePickerFileType[] NotificationExportFileTypes =
    [
        new("文本文件")
        {
            Patterns = ["*.txt"],
            MimeTypes = ["text/plain"]
        }
    ];
    private const int MaxWarmedPreviews = 3;
    private const int MaxLoadedPreviewCache = 12;
    private const int MaxMemoryAnimatedPreviews = 4;
    private const int MaxShelfSmoothPlayback = 12;
    private const int ShelfColumns = 4;
    private const double ShelfRowHeight = 188d;
    private const double ShelfLayoutWidthValue = 1132d;
    private const double ShelfLayoutVerticalPadding = 54d;
    private const int KaleidoscopePageSize = 8;
    private const int ShelfWarmExtraRows = 1;
    private static readonly TimeSpan PreviewBuildTimeout = TimeSpan.FromSeconds(120);
    private static readonly Point[] KaleidoscopeBackdropBoundary =
    [
        new(470, 16),
        new(714, 90),
        new(816, 300),
        new(714, 510),
        new(470, 584),
        new(226, 510),
        new(124, 300),
        new(226, 90)
    ];
    private static readonly IReadOnlyList<Key> ConfigurableKeys =
    [
        Key.Z, Key.X, Key.A, Key.S,
        Key.Q, Key.W, Key.E, Key.R, Key.U, Key.O,
        Key.Up, Key.Down, Key.Left, Key.Right,
        Key.I, Key.J, Key.K, Key.L,
        Key.Enter, Key.Space, Key.LeftShift, Key.RightShift,
        Key.LeftCtrl, Key.RightCtrl
    ];
    private static readonly IReadOnlyDictionary<int, IReadOnlyDictionary<NesButton, Key>> DefaultKeyMaps =
        new Dictionary<int, IReadOnlyDictionary<NesButton, Key>>
        {
            [0] = new Dictionary<NesButton, Key>
            {
                [NesButton.A] = Key.Z,
                [NesButton.B] = Key.X,
                [NesButton.Select] = Key.A,
                [NesButton.Start] = Key.S,
                [NesButton.Up] = Key.Up,
                [NesButton.Down] = Key.Down,
                [NesButton.Left] = Key.Left,
                [NesButton.Right] = Key.Right,
            },
            [1] = new Dictionary<NesButton, Key>
            {
                [NesButton.A] = Key.U,
                [NesButton.B] = Key.O,
                [NesButton.Select] = Key.RightCtrl,
                [NesButton.Start] = Key.Enter,
                [NesButton.Up] = Key.I,
                [NesButton.Down] = Key.K,
                [NesButton.Left] = Key.J,
                [NesButton.Right] = Key.L,
            }
        };
    private readonly IEmulatorCoreSession _coreSession;
    private readonly ICoreInputStateWriter _inputStateWriter;
    private readonly ITimeTravelService _timeTravelService;
    private readonly BranchTree _branchTree = new();
    private readonly NesAudioPlayer _audio = new();
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _previewTimer;
    private readonly Stopwatch _fpsWatch = Stopwatch.StartNew();
    private readonly Stopwatch _previewPlaybackWatch = Stopwatch.StartNew();
    private readonly Random _kaleidoscopeBackdropRandom = new();
    private readonly ObservableCollection<RomLibraryItem> _romLibrary = new();
    private readonly List<RomLibraryItem> _allRomLibrary = [];
    private readonly ObservableCollection<ShelfSlotItem> _shelfSlots = new();
    private readonly ObservableCollection<ShelfRowItem> _shelfRows = new();
    private readonly ObservableCollection<KaleidoscopeSlotItem> _kaleidoscopeSlots = new();
    private readonly ObservableCollection<KaleidoscopePageItem> _kaleidoscopePages = new();
    private readonly ObservableCollection<PreviewGenerationTaskItem> _taskQueue = new();
    private readonly MainWindowTaskMessageController _taskMessageController;
    private readonly IGameSessionService _gameSessionService = new GameSessionService();
    private readonly ObservableCollection<InputBindingEntry> _globalInputBindings = new();
    private readonly ObservableCollection<InputBindingEntry> _globalInputBindingsPlayer1 = new();
    private readonly ObservableCollection<InputBindingEntry> _globalInputBindingsPlayer2 = new();
    private readonly ObservableCollection<ExtraInputBindingEntry> _globalExtraInputBindings = new();
    private readonly ObservableCollection<ExtraInputBindingEntry> _globalExtraInputBindingsPlayer1 = new();
    private readonly ObservableCollection<ExtraInputBindingEntry> _globalExtraInputBindingsPlayer2 = new();
    private readonly ObservableCollection<InputBindingEntry> _romInputBindings = new();
    private readonly ObservableCollection<InputBindingEntry> _romInputBindingsPlayer1 = new();
    private readonly ObservableCollection<InputBindingEntry> _romInputBindingsPlayer2 = new();
    private readonly ObservableCollection<ExtraInputBindingEntry> _romExtraInputBindings = new();
    private readonly ObservableCollection<ExtraInputBindingEntry> _romExtraInputBindingsPlayer1 = new();
    private readonly ObservableCollection<ExtraInputBindingEntry> _romExtraInputBindingsPlayer2 = new();
    private readonly ObservableCollection<ShortcutBindingEntry> _mainWindowShortcutBindings = new();
    private readonly ObservableCollection<ShortcutBindingEntry> _sharedGameShortcutBindings = new();
    private readonly ObservableCollection<ShortcutBindingEntry> _gameWindowShortcutBindings = new();
    private readonly object _romLock = new();
    private readonly Dictionary<string, Dictionary<int, Dictionary<NesButton, Key>>> _romInputOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ExtraInputBindingProfile>> _romExtraInputOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ShortcutBindingEntry> _shortcutBindings = new(StringComparer.Ordinal);
    private readonly HashSet<Key> _pressedKeys = [];
    private readonly TimelineRepository _timelineRepository = new();
    private readonly ReplayLogWriter _replayLogWriter = new();
    private readonly ILanArcadeService _lanArcadeService;
    private readonly ILanArcadeDiagnosticsService _lanArcadeDiagnosticsService = new LanArcadeDiagnosticsService();
    private readonly IRomResourceImportService _romResourceImportService = new RomResourceImportService();
    private readonly MainWindowPreviewCleanupController _previewCleanupController;
    private readonly MainWindowResourceManagementController _resourceManagementController;
    private readonly MainWindowResourceRootWorkflowController _resourceRootWorkflowController;
    private readonly MainWindowManagedCoreCatalogController _managedCoreCatalogController;
    private readonly MainWindowManagedCoreInstallController _managedCoreInstallController;
    private readonly MainWindowManagedCoreExportController _managedCoreExportController;
    private readonly MainWindowResourceCleanupWorkflowController _resourceCleanupWorkflowController;
    private readonly MainWindowRomResourceImportWorkflowController _romResourceImportWorkflowController;
    private readonly MainWindowRomDeleteWorkflowController _romDeleteWorkflowController;
    private readonly MainWindowBranchExportExecutionController _branchExportExecutionController;
    private readonly MainWindowCatalogLayoutController _catalogLayoutController;
    private readonly MainWindowLibraryCatalogController _libraryCatalogController;
    private readonly MainWindowLibraryNavigationController _libraryNavigationController;
    private readonly MainWindowLanArcadeController _lanArcadeController;
    private readonly MainWindowInputBindingsController _inputBindingsController;
    private readonly MainWindowInputDispatchController _inputDispatchController;
    private readonly MainWindowInputActionController _inputActionController;
    private readonly MainWindowInputStateController _inputStateController;
    private readonly MainWindowInputOverrideController _inputOverrideController;
    private readonly MainWindowInputLayoutController _inputLayoutController;
    private readonly MainWindowSessionLifecycleController _sessionLifecycleController;
    private readonly MainWindowSessionLaunchController _sessionLaunchController;
    private readonly MainWindowLanServerStateController _lanServerStateController;
    private readonly MainWindowPreviewAssetController _previewAssetController;
    private readonly MainWindowPreviewAssetReadyController _previewAssetReadyController;
    private readonly MainWindowPreviewQueueController _previewQueueController;
    private readonly MainWindowPreviewPlaybackPolicyController _previewPlaybackPolicyController;
    private readonly MainWindowPreviewWarmupController _previewWarmupController;
    private readonly MainWindowPreviewWarmupRequestController _previewWarmupRequestController;
    private readonly MainWindowPreviewGenerationController _previewGenerationController;
    private readonly MainWindowPreviewStartupController _previewStartupController;
    private readonly MainWindowPreviewStreamController _previewStreamController;
    private readonly MainWindowPreviewWarmupItemController _previewWarmupItemController;
    private readonly MainWindowPreviewLifecycleController _previewLifecycleController;
    private readonly MainWindowPreviewLoadController _previewLoadController;
    private readonly MainWindowPreviewSelectionController _previewSelectionController;
    private readonly MainWindowPreviewTickController _previewTickController;
    private readonly SemaphoreSlim _lanArcadeStateGate = new(1, 1);
    private readonly IArcadeRuntimeContractAdapter _arcadeRuntimeAdapter;
    private readonly IBackendStateMirror _backendStateMirror;
    private InputBindingLayoutProfile _inputBindingLayout = InputBindingLayoutProfile.CreateDefault();

    private BranchGalleryWindow? _galleryWindow;
    private CancellationTokenSource? _branchGalleryPreviewCts;
    private CancellationTokenSource? _previewWarmupCts;
    private Thread? _emuThread;
    private string? _romPath;
    private string? _currentRomId;
    private Guid _currentBranchId;
    private Guid? _currentSnapshotId;
    private TimelineManifest? _timelineManifest;
    private WriteableBitmap? _screenBitmap;
    private WriteableBitmap? _currentPreviewBitmap;
    private WriteableBitmap? _discDisplayBitmap;
    private WriteableBitmap? _lanArcadeQrCode;
    private WebProbeWindow? _webProbeWindow;
    private bool _isRomLoaded;
    private bool _isPaused;
    private bool _isGeneratingPreview;
    private bool _isSettingsOpen;
    private bool _isQuickRomInputEditorOpen;
    private bool _isReplacingGameSession;
    private bool _isBranchGalleryPreviewOpen;
    private bool _isBranchGalleryPreviewLoading;
    private bool _showDebugStatus;
    private bool _isStartupProgressVisible;
    private bool _isRomInputOverrideEnabled;
    private bool _isPreviewQueueProcessorRunning;
    private bool _isShuttingDown;
    private bool _isApplyingSystemConfig;
    private bool _turboPulseActive;
    private bool _isLanArcadeEnabled;
    private bool _isLanArcadeWebPadEnabled = true;
    private bool _isLanArcadeDebugPagesEnabled;
    private bool _isLanArcadeServerReady;
    private bool _isWindowOpened;
    private bool _hasLoadedStartupContent;
    private bool _canWarmPreviews = true;
    private bool _cleanupPreviewAnimationsSelected = true;
    private bool _cleanupThumbnailsSelected = true;
    private bool _cleanupTimelineSavesSelected;
    private bool _cleanupExportVideosSelected;
    private int _lanArcadePort = SystemConfigProfile.DefaultLanArcadePort;
    private string _lanArcadePortInput = SystemConfigProfile.DefaultLanArcadePort.ToString();
    private int _lanStreamScaleMultiplier = 2;
    private int _lanStreamJpegQuality = 85;
    private LanStreamSharpenMode _lanStreamSharpenMode = LanStreamSharpenMode.None;
    private PixelEnhancementMode _localDisplayEnhancementMode = PixelEnhancementMode.None;
    private List<string> _managedCoreProbePaths = [];
    private IReadOnlyList<MainWindowManagedCoreCatalogEntry> _managedCoreCatalogEntries = [];
    private string _managedCoreProbePathsInput = string.Empty;
    private IReadOnlyList<CoreManifest> _installedCoreManifests = [];
    private string _resourceRootPath = AppObjectStorage.GetDefaultResourceRoot();
    private string _resourceRootPathInput = AppObjectStorage.GetDefaultResourceRoot();
    private string _defaultCoreId = DefaultEmulatorCoreHost.DefaultCoreId;
    private string _lanFirewallStatusTitle = "尚未检测";
    private string _lanFirewallStatusDetail = "请点击刷新检测，确认系统防火墙不会拦截局域网访问。";
    private string _lanArcadeDiagnosticsText = "尚未执行局域网监听自检。";
    private string _lanArcadeLastTrafficText = "尚未收到任何局域网请求。";
    private int _lanArcadeTrafficCount;
    private string _statusText = "正在扫描 roms 目录";
    private string _fpsText = "";
    private string _frameText = "";
    private string _librarySummary = "";
    private string _startupDiagnosticsText = "启动日志尚未就绪。";
    private string _startupCurrentStepText = "正在准备主界面。";
    private string _startupGameListStatusText = "等待开始";
    private string _startupPreviewStatusText = "等待开始";
    private string _startupLanStatusText = "等待开始";
    private string _previewStatusText = "尚未生成预览";
    private string _previewDebugText = "";
    private string _resourceCleanupSummary = "";
    private string _resourceCleanupResultText = "选择要清理的资源后执行。";
    private string _currentRomName = "尚未找到 ROM";
    private string _currentRomPathText = "请在当前工作目录下准备 roms 文件夹";
    private string _currentLayoutName = "经典书柜";
    private string _librarySearchText = string.Empty;
    private double _volume = 15.0;
    private int _previewPreloadWindowSeconds = StreamingPreviewSettings.VideoPreloadWindowSeconds;
    private int _previewGenerationSpeedMultiplier = 3;
    private PreviewEncodingMode _previewEncodingMode = PreviewEncodingMode.Auto;
    private double _previewResolutionScale = 1.0;
    private double _previewProgress;
    private double _carouselTranslateX;
    private double _carouselScaleX = 1.0;
    private double _carouselScaleY = 1.0;
    private double _carouselRotation;
    private double _leftCarouselTranslateX;
    private double _rightCarouselTranslateX;
    private double _leftCarouselScale = 1.0;
    private double _rightCarouselScale = 1.0;
    private int _fpsCounter;
    private int _previewTickCounter;
    private int _maxConcurrentGameWindows = 1;
    private int _previewGenerationParallelism = 1;
    private volatile int _emuFrameCount;
    private volatile int _emuFpsRaw;
    private volatile int _frameTimeMicros;
    private volatile bool _emuThreadAlive;
    private volatile uint[]? _pendingFrame;
    private volatile byte _player1InputMask;
    private volatile byte _player2InputMask;
    private uint[]? _lastFrame;
    private RomLibraryItem? _currentRom;
    private RomLibraryItem? _pendingLaunchRom;
    private LibraryLayoutMode _layoutMode = LibraryLayoutMode.BookShelf;
    private RomSortField _sortField = RomSortField.Name;
    private bool _sortDescending;
    private bool _isTaskMessagePanelVisible;
    private GameAspectRatioMode _gameAspectRatioMode = GameAspectRatioMode.Native;
    private MacUpscaleMode _macUpscaleMode = MacUpscaleMode.None;
    private MacUpscaleOutputResolution _macUpscaleOutputResolution = MacUpscaleOutputResolution.Hd1080;
    private TimelineMode _timelineMode = TimelineMode.FullTimeline;
    private int _shortRewindFrames = 5 * EmulatorFramesPerSecond;
    private string _shortRewindSecondsInput = "5.00";
    private string _shortRewindFramesInput = (5 * EmulatorFramesPerSecond).ToString(CultureInfo.InvariantCulture);
    private bool _isSyncingShortRewindInputs;
    private Thickness _branchGalleryPreviewMargin = new(24, 112, 0, 0);
    private int _shelfVisibleStartRow;
    private int _shelfVisibleRowCount = 2;
    private bool _isShelfScrolling;
    private int _branchGallerySyncTick;
    private DateTime _timelineManifestWriteTimeUtc;
    private int _kaleidoscopeCurrentPageIndex;
    private readonly KaleidoscopeBackdropActor _kaleidoscopePrimarySweep = new(220d, 2d, 88d, 18d, 286d, 156d);
    private readonly KaleidoscopeBackdropActor _kaleidoscopeSecondarySweep = new(170d, 2d, 74d, 18d, 652d, 438d);
    private readonly KaleidoscopeBackdropActor _kaleidoscopePrimaryDot = new(14d, 14d, 56d, 10d, 638d, 174d);
    private readonly KaleidoscopeBackdropActor _kaleidoscopeSecondaryDot = new(8d, 8d, 68d, 8d, 284d, 430d);
    private bool _hasInitializedKaleidoscopeBackdrop;
    private double _lastKaleidoscopeBackdropSeconds = -1d;
    private double _kaleidoscopeSweepOffset;
    private double _kaleidoscopeSweepTop;
    private double _kaleidoscopeSweepAngle;
    private double _kaleidoscopeSweepOffsetSecondary;
    private double _kaleidoscopeSweepTopSecondary;
    private double _kaleidoscopeSweepAngleSecondary;
    private double _kaleidoscopePulseOpacity = 0.28;
    private double _kaleidoscopeOrbitDotX = 120;
    private double _kaleidoscopeOrbitDotY = 80;
    private double _kaleidoscopeOrbitDotSecondaryX = 80;
    private double _kaleidoscopeOrbitDotSecondaryY = 120;

    public MainWindowViewModel(bool deferStartupWork = false)
    {
        var bootstrapProfile = SystemConfigProfile.Load();
        AppObjectStorage.ConfigureResourceRoot(bootstrapProfile.ResourceRootPath);
        _resourceRootPath = AppObjectStorage.GetResourceRoot();
        _resourceRootPathInput = _resourceRootPath;
        _managedCoreProbePaths = [.. bootstrapProfile.ManagedCoreProbePaths];
        _managedCoreProbePathsInput = FormatManagedCoreProbePathsInput(_managedCoreProbePaths);
        _managedCoreCatalogController = new MainWindowManagedCoreCatalogController();
        _managedCoreInstallController = new MainWindowManagedCoreInstallController();
        _managedCoreExportController = new MainWindowManagedCoreExportController();
        var bootstrapManagedCoreCatalog = _managedCoreCatalogController.LoadCatalog(_resourceRootPath, _managedCoreProbePaths);
        _managedCoreCatalogEntries = bootstrapManagedCoreCatalog.Entries;
        _installedCoreManifests = bootstrapManagedCoreCatalog.Manifests;
        _defaultCoreId = NormalizeConfiguredCoreId(bootstrapProfile.DefaultCoreId);
        _coreSession = CreateMainCoreSession();
        _timeTravelService = CoreSessionCapabilityResolver.ResolveTimeTravelService(_coreSession);
        _inputStateWriter = CoreSessionCapabilityResolver.ResolveInputStateWriter(_coreSession);
        _taskMessageController = new MainWindowTaskMessageController(TaskMessageHub.Instance);
        _taskMessageController.StateChanged += RefreshTaskMessageSummary;
        _previewCleanupController = new MainWindowPreviewCleanupController();
        _resourceManagementController = new MainWindowResourceManagementController(_romResourceImportService, _previewCleanupController);
        _resourceRootWorkflowController = new MainWindowResourceRootWorkflowController(_resourceManagementController);
        _resourceCleanupWorkflowController = new MainWindowResourceCleanupWorkflowController(_resourceManagementController);
        _romDeleteWorkflowController = new MainWindowRomDeleteWorkflowController(_resourceManagementController);
        _branchExportExecutionController = new MainWindowBranchExportExecutionController();
        _catalogLayoutController = new MainWindowCatalogLayoutController();
        _libraryCatalogController = new MainWindowLibraryCatalogController(_catalogLayoutController);
        _libraryNavigationController = new MainWindowLibraryNavigationController();
        _inputBindingsController = new MainWindowInputBindingsController();
        _inputDispatchController = new MainWindowInputDispatchController();
        _inputActionController = new MainWindowInputActionController();
        _inputStateController = new MainWindowInputStateController();
        _inputOverrideController = new MainWindowInputOverrideController(_inputBindingsController, ConfigurableKeys);
        _inputLayoutController = new MainWindowInputLayoutController();
        _sessionLifecycleController = new MainWindowSessionLifecycleController(
            (tracked, handler) => tracked.PropertyChanged += handler,
            (tracked, handler) => tracked.PropertyChanged -= handler,
            tracked =>
            {
                if (tracked is not GameWindowViewModel changedVm)
                    return false;

                var session = _gameSessionService.Sessions.FirstOrDefault(item => ReferenceEquals(item.ViewModel, changedVm));
                session?.RefreshRemoteControlSummary();
                return session != null;
            },
            RefreshActiveSessionState);
        _sessionLaunchController = new MainWindowSessionLaunchController();
        _lanServerStateController = new MainWindowLanServerStateController();
        _previewAssetController = new MainWindowPreviewAssetController(SupportedVideoPreviewExtensions, LegacyPreviewExtension);
        _previewAssetReadyController = new MainWindowPreviewAssetReadyController();
        _romResourceImportWorkflowController = new MainWindowRomResourceImportWorkflowController(_resourceManagementController, _previewAssetReadyController);
        _previewQueueController = new MainWindowPreviewQueueController();
        _previewPlaybackPolicyController = new MainWindowPreviewPlaybackPolicyController();
        _previewWarmupController = new MainWindowPreviewWarmupController(_previewPlaybackPolicyController);
        _previewWarmupRequestController = new MainWindowPreviewWarmupRequestController();
        _previewGenerationController = new MainWindowPreviewGenerationController(
            CreatePreviewCoreSession,
            PreviewSourceWidth,
            PreviewSourceHeight,
            PreviewDurationSeconds,
            PreviewPlaybackFps,
            PreviewSourceFps,
            PreviewAnimationFrameCount,
            PreviewCaptureStride,
            PreviewFrameIntervalMs,
            PreviewBuildTimeout,
            PreviewMagicV1,
            PreviewMagicV2,
            LegacyPreviewExtension);
        _previewStartupController = new MainWindowPreviewStartupController();
        _previewStreamController = new MainWindowPreviewStreamController(PreviewMagicV1, PreviewMagicV2);
        _previewWarmupItemController = new MainWindowPreviewWarmupItemController(_previewStreamController);
        _previewLifecycleController = new MainWindowPreviewLifecycleController();
        _previewLoadController = new MainWindowPreviewLoadController(_previewLifecycleController);
        _previewSelectionController = new MainWindowPreviewSelectionController();
        _previewTickController = new MainWindowPreviewTickController();

        var ctorWatch = Stopwatch.StartNew();
        LogStartup($"constructor begin; deferStartupWork={deferStartupWork}");
        _canWarmPreviews = !deferStartupWork;
        AppObjectStorage.EnsureDefaults();
        LogStartup($"AppObjectStorage.EnsureDefaults complete in {ctorWatch.ElapsedMilliseconds} ms");
        _screenBitmap = CreateBitmap(PreviewSourceWidth, PreviewSourceHeight);
        LogStartup($"CreateBitmap complete in {ctorWatch.ElapsedMilliseconds} ms");

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();
        LogStartup($"render timer started in {ctorWatch.ElapsedMilliseconds} ms");

        _previewTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(PreviewFrameIntervalMs)
        };
        _previewTimer.Tick += OnPreviewTick;
        _gameSessionService.Sessions.CollectionChanged += _sessionLifecycleController.OnActiveGameSessionsChanged;
        LogStartup($"preview timer configured in {ctorWatch.ElapsedMilliseconds} ms");

        _coreSession.VideoFrameReady += OnCoreVideoFrameReady;
        _coreSession.AudioReady += OnCoreAudioReady;
        _audio.Volume = (float)_volume;
        InitializeInputBindings();
        LogStartup($"InitializeInputBindings complete in {ctorWatch.ElapsedMilliseconds} ms");
        InitializeShortcutBindings();
        LogStartup($"InitializeShortcutBindings complete in {ctorWatch.ElapsedMilliseconds} ms");
        LoadSystemConfig();
        LogStartup($"LoadSystemConfig complete in {ctorWatch.ElapsedMilliseconds} ms; resourceRoot={ResourceRootPath}");
        AppObjectStorage.EnsureDefaults();
        LogStartup($"AppObjectStorage.EnsureDefaults(recheck) complete in {ctorWatch.ElapsedMilliseconds} ms");
        var runtimeDependencies = MainWindowRuntimeDependencyBundle.Create(
            _romLibrary,
            _romInputOverrides,
            _globalInputBindings,
            _gameSessionService,
            () => GameAspectRatioMode,
            () => MacUpscaleMode,
            () => MacUpscaleOutputResolution,
            BuildGameWindowShortcutMap,
            SyncLoadedFlags,
            PathsEqual,
            text => StatusText = text,
            CreateBackendStateSyncClient);
        _arcadeRuntimeAdapter = runtimeDependencies.ArcadeRuntimeAdapter;
        _lanArcadeService = runtimeDependencies.LanArcadeService;
        _backendStateMirror = runtimeDependencies.BackendStateMirror;
        _lanArcadeController = new MainWindowLanArcadeController(_lanArcadeService, _lanArcadeDiagnosticsService);
        _isLanArcadeServerReady = runtimeDependencies.IsLanArcadeServerReady;
        LogStartup($"service graph ready in {ctorWatch.ElapsedMilliseconds} ms");

        _emuThreadAlive = true;
        _emuThread = new Thread(EmuThreadLoop) { IsBackground = true, Name = "NesEmu" };
        _emuThread.Start();
        LogStartup($"emulation thread started in {ctorWatch.ElapsedMilliseconds} ms");

        StartupDiagnostics.Updated += OnStartupDiagnosticsUpdated;
        RefreshStartupDiagnosticsSnapshot();
        RefreshTaskMessageSummary();

        if (!deferStartupWork)
        {
            LogStartup("constructor running startup content immediately");
            EnsureStartupContentLoaded();
        }
        else
        {
            IsStartupProgressVisible = true;
            UpdateStartupStep(
                "主界面已就绪，等待启动阶段开始。",
                gameListStatus: "等待开始",
                previewStatus: "等待开始",
                lanStatus: IsLanArcadeEnabled ? "等待开始" : "等待开始（功能可关闭）");
        }

        LogStartup($"constructor complete in {ctorWatch.ElapsedMilliseconds} ms");
    }

    public ObservableCollection<RomLibraryItem> RomLibrary => _romLibrary;

    public ObservableCollection<ShelfSlotItem> ShelfSlots => _shelfSlots;

    public ObservableCollection<ShelfRowItem> ShelfRows => _shelfRows;

    public double ShelfLayoutWidth => ShelfLayoutWidthValue;

    public double ShelfLayoutHeight => (Math.Max(1, _shelfRows.Count) * ShelfRowHeight) + ShelfLayoutVerticalPadding;

    public ObservableCollection<KaleidoscopeSlotItem> KaleidoscopeSlots => _kaleidoscopeSlots;

    public ObservableCollection<KaleidoscopePageItem> KaleidoscopePages => _kaleidoscopePages;

    public KaleidoscopeSlotItem? KaleidoscopeSlot0 => GetKaleidoscopeSlot(0);

    public KaleidoscopeSlotItem? KaleidoscopeSlot1 => GetKaleidoscopeSlot(1);

    public KaleidoscopeSlotItem? KaleidoscopeSlot2 => GetKaleidoscopeSlot(2);

    public KaleidoscopeSlotItem? KaleidoscopeSlot3 => GetKaleidoscopeSlot(3);

    public KaleidoscopeSlotItem? KaleidoscopeSlot4 => GetKaleidoscopeSlot(4);

    public KaleidoscopeSlotItem? KaleidoscopeSlot5 => GetKaleidoscopeSlot(5);

    public KaleidoscopeSlotItem? KaleidoscopeSlot6 => GetKaleidoscopeSlot(6);

    public KaleidoscopeSlotItem? KaleidoscopeSlot7 => GetKaleidoscopeSlot(7);

    public ObservableCollection<PreviewGenerationTaskItem> TaskQueue => _taskQueue;

    public ObservableCollection<TaskMessage> FilteredTaskMessages => _taskMessageController.FilteredMessages;

    public ObservableCollection<ActiveGameSessionItem> ActiveGameSessions => _gameSessionService.Sessions;
    public ObservableCollection<InputBindingEntry> GlobalInputBindings => _globalInputBindings;
    public ObservableCollection<InputBindingEntry> GlobalInputBindingsPlayer1 => _globalInputBindingsPlayer1;
    public ObservableCollection<InputBindingEntry> GlobalInputBindingsPlayer2 => _globalInputBindingsPlayer2;
    public ObservableCollection<ExtraInputBindingEntry> GlobalExtraInputBindings => _globalExtraInputBindings;
    public ObservableCollection<ExtraInputBindingEntry> GlobalExtraInputBindingsPlayer1 => _globalExtraInputBindingsPlayer1;
    public ObservableCollection<ExtraInputBindingEntry> GlobalExtraInputBindingsPlayer2 => _globalExtraInputBindingsPlayer2;
    public ObservableCollection<InputBindingEntry> RomInputBindings => _romInputBindings;
    public ObservableCollection<InputBindingEntry> RomInputBindingsPlayer1 => _romInputBindingsPlayer1;
    public ObservableCollection<InputBindingEntry> RomInputBindingsPlayer2 => _romInputBindingsPlayer2;
    public ObservableCollection<ExtraInputBindingEntry> RomExtraInputBindings => _romExtraInputBindings;
    public ObservableCollection<ExtraInputBindingEntry> RomExtraInputBindingsPlayer1 => _romExtraInputBindingsPlayer1;
    public ObservableCollection<ExtraInputBindingEntry> RomExtraInputBindingsPlayer2 => _romExtraInputBindingsPlayer2;
    public ObservableCollection<InputBindingEntry> InputLayoutDebugBindings => _globalInputBindingsPlayer1;

    public WriteableBitmap? ScreenBitmap
    {
        get => _screenBitmap;
        private set
        {
            if (SetProperty(ref _screenBitmap, value))
                UpdateDiscDisplayBitmap();
        }
    }

    public WriteableBitmap? CurrentPreviewBitmap
    {
        get => _currentPreviewBitmap;
        private set
        {
            if (SetProperty(ref _currentPreviewBitmap, value))
            {
                OnPropertyChanged(nameof(HasPreviewBitmap));
                UpdateDiscDisplayBitmap();
            }
        }
    }

    public WriteableBitmap? DiscDisplayBitmap
    {
        get => _discDisplayBitmap;
        private set
        {
            if (SetProperty(ref _discDisplayBitmap, value))
            {
                OnPropertyChanged(nameof(HasDiscDisplayBitmap));
                OnPropertyChanged(nameof(NoDiscDisplayBitmap));
            }
        }
    }

    public bool HasPreviewBitmap => CurrentPreviewBitmap != null;

    public bool HasDiscDisplayBitmap => DiscDisplayBitmap != null;

    public bool NoDiscDisplayBitmap => DiscDisplayBitmap == null;

    public bool IsRomLoaded
    {
        get => _isRomLoaded;
        private set
        {
            if (SetProperty(ref _isRomLoaded, value))
                UpdateDiscDisplayBitmap();
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set => SetProperty(ref _isPaused, value);
    }

    public bool IsGeneratingPreview
    {
        get => _isGeneratingPreview;
        private set
        {
            if (SetProperty(ref _isGeneratingPreview, value))
            {
                OnPropertyChanged(nameof(CanGeneratePreview));
                OnPropertyChanged(nameof(PreviewActionText));
                OnPropertyChanged(nameof(PreviewQuickActionText));
                OnPropertyChanged(nameof(CurrentRomActionSummary));
            }
        }
    }

    public bool HasTaskQueue => _taskQueue.Count > 0;

    public double NotificationPanelWidth => 520;

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        private set
        {
            if (SetProperty(ref _isSettingsOpen, value))
                OnPropertyChanged(nameof(SettingsOverlayContent));
        }
    }

    public bool IsQuickRomInputEditorOpen
    {
        get => _isQuickRomInputEditorOpen;
        private set
        {
            if (SetProperty(ref _isQuickRomInputEditorOpen, value))
                OnPropertyChanged(nameof(QuickRomInputEditorContent));
        }
    }

    public MainWindowViewModel? SettingsOverlayContent => IsSettingsOpen ? this : null;

    public MainWindowViewModel? QuickRomInputEditorContent => IsQuickRomInputEditorOpen ? this : null;

    public ObservableCollection<ShortcutBindingEntry> MainWindowShortcutBindings => _mainWindowShortcutBindings;

    public ObservableCollection<ShortcutBindingEntry> SharedGameShortcutBindings => _sharedGameShortcutBindings;

    public ObservableCollection<ShortcutBindingEntry> GameWindowShortcutBindings => _gameWindowShortcutBindings;

    public bool IsShuttingDown => _isShuttingDown;

    public bool ShowDebugStatus
    {
        get => _showDebugStatus;
        set
        {
            if (SetProperty(ref _showDebugStatus, value))
                SaveSystemConfig();
        }
    }

    public string FpsText
    {
        get => _fpsText;
        private set
        {
            SetProperty(ref _fpsText, value);
        }
    }

    public string FrameText
    {
        get => _frameText;
        private set
        {
            SetProperty(ref _frameText, value);
        }
    }

    public string LibrarySummary
    {
        get => _librarySummary;
        private set => SetProperty(ref _librarySummary, value);
    }

    public int PreviewPreloadWindowSeconds
    {
        get => _previewPreloadWindowSeconds;
        private set
        {
            if (SetProperty(ref _previewPreloadWindowSeconds, value))
            {
                StreamingPreviewSettings.VideoPreloadWindowSeconds = value;
                OnPropertyChanged(nameof(PreviewPreloadWindowSummary));
                SaveSystemConfig();
            }
        }
    }

    public string PreviewPreloadWindowSummary =>
        $"视频预加载窗口: {PreviewPreloadWindowSeconds} 秒。窗口越短越省内存，但会更频繁触发后台解码。";

    public string CurrentRomName
    {
        get => _currentRomName;
        private set => SetProperty(ref _currentRomName, value);
    }

    public string CurrentRomPathText
    {
        get => _currentRomPathText;
        private set => SetProperty(ref _currentRomPathText, value);
    }

    public string CurrentLayoutName
    {
        get => _currentLayoutName;
        private set => SetProperty(ref _currentLayoutName, value);
    }

    public string LibrarySearchText
    {
        get => _librarySearchText;
        set
        {
            if (!SetProperty(ref _librarySearchText, value))
                return;

            OnPropertyChanged(nameof(HasLibrarySearchText));
            OnPropertyChanged(nameof(CurrentRomActionSummary));
            RebuildVisibleRomLibrary();
            UpdateLibrarySummary();
        }
    }

    public double Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, value))
            {
                _audio.Volume = (float)value;
                foreach (var session in _gameSessionService.Sessions)
                    session.ViewModel.SetVolume(value);
                SaveSystemConfig();
            }
        }
    }

    public double PreviewProgress
    {
        get => _previewProgress;
        private set => SetProperty(ref _previewProgress, value);
    }

    public double CarouselTranslateX
    {
        get => _carouselTranslateX;
        private set => SetProperty(ref _carouselTranslateX, value);
    }

    public double CarouselScaleX
    {
        get => _carouselScaleX;
        private set => SetProperty(ref _carouselScaleX, value);
    }

    public double CarouselScaleY
    {
        get => _carouselScaleY;
        private set => SetProperty(ref _carouselScaleY, value);
    }

    public double CarouselRotation
    {
        get => _carouselRotation;
        private set => SetProperty(ref _carouselRotation, value);
    }

    public double LeftCarouselTranslateX
    {
        get => _leftCarouselTranslateX;
        private set => SetProperty(ref _leftCarouselTranslateX, value);
    }

    public double RightCarouselTranslateX
    {
        get => _rightCarouselTranslateX;
        private set => SetProperty(ref _rightCarouselTranslateX, value);
    }

    public double LeftCarouselScale
    {
        get => _leftCarouselScale;
        private set => SetProperty(ref _leftCarouselScale, value);
    }

    public double RightCarouselScale
    {
        get => _rightCarouselScale;
        private set => SetProperty(ref _rightCarouselScale, value);
    }

    public RomLibraryItem? CurrentRom
    {
        get => _currentRom;
        private set
        {
            if (_currentRom == value)
                return;

            if (_currentRom != null)
                _currentRom.IsCurrent = false;

            if (SetProperty(ref _currentRom, value))
            {
                if (_currentRom != null)
                    _currentRom.IsCurrent = true;

                OnPropertyChanged(nameof(HasCurrentRom));
                OnPropertyChanged(nameof(PreviousRom));
                OnPropertyChanged(nameof(NextRom));
                OnPropertyChanged(nameof(CanSelectPrevious));
                OnPropertyChanged(nameof(CanSelectNext));
                OnPropertyChanged(nameof(CanPlaySelectedRom));
                OnPropertyChanged(nameof(CanGeneratePreview));
                OnPropertyChanged(nameof(PreviewActionText));
                OnPropertyChanged(nameof(PreviewQuickActionText));
                OnPropertyChanged(nameof(ShelfScrollSummary));
                OnPropertyChanged(nameof(RomInputOverrideSummary));
                OnPropertyChanged(nameof(CurrentRomActionSummary));
                SyncKaleidoscopePageWithCurrentRom();
                UpdateCurrentRomPresentation();
                var hasAnyAnimatedPreview = _romLibrary.Any(item => item.IsPreviewAnimated);
                _previewSelectionController.ApplyCurrentRomSelection(
                    _currentRom,
                    hasLoadedCurrentRomPreview: _currentRom?.HasLoadedPreview == true,
                    hasAnyAnimatedPreview,
                    syncCurrentPreviewBitmap: rom => CurrentPreviewBitmap = rom?.CurrentPreviewBitmap,
                    startPreviewPlayback: StartPreviewPlayback,
                    stopPreviewPlayback: StopPreviewPlayback,
                    updateDiscDisplayBitmap: UpdateDiscDisplayBitmap,
                    requestPreviewWarmup: RequestPreviewWarmup);
                OnPropertyChanged(nameof(BranchGalleryPreviewTitle));
                OnPropertyChanged(nameof(BranchGalleryPreviewBitmap));
                RefreshRomInputBindings();
            }
        }
    }

    public bool HasCurrentRom => CurrentRom != null;

    public bool IsLibraryEmpty => _romLibrary.Count == 0;

    public bool HasRomLibrary => _romLibrary.Count > 0;

    public RomLibraryItem? PreviousRom => _libraryNavigationController.GetNeighbor(_romLibrary, CurrentRom, -1);

    public RomLibraryItem? NextRom => _libraryNavigationController.GetNeighbor(_romLibrary, CurrentRom, 1);

    public bool CanSelectPrevious => PreviousRom != null;

    public bool CanSelectNext => NextRom != null;

    public bool CanPlaySelectedRom => CurrentRom != null;

    public bool CanGeneratePreview => CurrentRom != null;

    public bool HasLibrarySearchText => !string.IsNullOrWhiteSpace(LibrarySearchText);

    public string CurrentRomActionSummary
    {
        get
        {
            if (_allRomLibrary.Count == 0)
                return "把 .nes 放进当前工作目录的 roms 文件夹，或在设置里导入 ROM。";

            if (CurrentRom == null)
            {
                if (!HasLibrarySearchText)
                    return "选择一个 ROM 后可直接开始、生成预览、打开分支或编辑独立按键。";

                return $"没有匹配“{LibrarySearchText.Trim()}”的 ROM，可调整关键词或清空搜索。";
            }

            var segments = new List<string>();
            if (CurrentRom.IsLoaded)
                segments.Add("运行中");

            segments.Add(PreviewStatusText);
            segments.Add(CurrentRom.SizeLabel);
            return string.Join(" · ", segments.Where(static segment => !string.IsNullOrWhiteSpace(segment)));
        }
    }

    public int KaleidoscopeCurrentPageIndex
    {
        get => _kaleidoscopeCurrentPageIndex;
        private set
        {
            if (SetProperty(ref _kaleidoscopeCurrentPageIndex, value))
            {
                OnPropertyChanged(nameof(KaleidoscopeCurrentPageDisplay));
                UpdateKaleidoscopePages();
                RebuildKaleidoscopeSlots();
                RequestPreviewWarmup(CurrentRom);
            }
        }
    }

    public string KaleidoscopeCurrentPageDisplay => _kaleidoscopePages.Count == 0
        ? "0 / 0"
        : $"{KaleidoscopeCurrentPageIndex + 1} / {_kaleidoscopePages.Count}";

    public bool HasKaleidoscopePagination => _kaleidoscopePages.Count > 1;

    public double KaleidoscopeSweepOffset
    {
        get => _kaleidoscopeSweepOffset;
        private set => SetProperty(ref _kaleidoscopeSweepOffset, value);
    }

    public double KaleidoscopeSweepTop
    {
        get => _kaleidoscopeSweepTop;
        private set => SetProperty(ref _kaleidoscopeSweepTop, value);
    }

    public double KaleidoscopeSweepAngle
    {
        get => _kaleidoscopeSweepAngle;
        private set => SetProperty(ref _kaleidoscopeSweepAngle, value);
    }

    public double KaleidoscopeSweepOffsetSecondary
    {
        get => _kaleidoscopeSweepOffsetSecondary;
        private set => SetProperty(ref _kaleidoscopeSweepOffsetSecondary, value);
    }

    public double KaleidoscopeSweepTopSecondary
    {
        get => _kaleidoscopeSweepTopSecondary;
        private set => SetProperty(ref _kaleidoscopeSweepTopSecondary, value);
    }

    public double KaleidoscopeSweepAngleSecondary
    {
        get => _kaleidoscopeSweepAngleSecondary;
        private set => SetProperty(ref _kaleidoscopeSweepAngleSecondary, value);
    }

    public double KaleidoscopePulseOpacity
    {
        get => _kaleidoscopePulseOpacity;
        private set => SetProperty(ref _kaleidoscopePulseOpacity, value);
    }

    public double KaleidoscopeOrbitDotX
    {
        get => _kaleidoscopeOrbitDotX;
        private set => SetProperty(ref _kaleidoscopeOrbitDotX, value);
    }

    public double KaleidoscopeOrbitDotY
    {
        get => _kaleidoscopeOrbitDotY;
        private set => SetProperty(ref _kaleidoscopeOrbitDotY, value);
    }

    public double KaleidoscopeOrbitDotSecondaryX
    {
        get => _kaleidoscopeOrbitDotSecondaryX;
        private set => SetProperty(ref _kaleidoscopeOrbitDotSecondaryX, value);
    }

    public double KaleidoscopeOrbitDotSecondaryY
    {
        get => _kaleidoscopeOrbitDotSecondaryY;
        private set => SetProperty(ref _kaleidoscopeOrbitDotSecondaryY, value);
    }

    public bool CanGenerateAllPreviews => _allRomLibrary.Any(item => !item.HasPreview && !HasPendingPreviewJob(item.Path));

    public bool HasActiveGameSessions => _gameSessionService.HasAny;

    public bool IsReplacingGameSession
    {
        get => _isReplacingGameSession;
        private set => SetProperty(ref _isReplacingGameSession, value);
    }

    public bool IsBranchGalleryPreviewOpen
    {
        get => _isBranchGalleryPreviewOpen;
        private set => SetProperty(ref _isBranchGalleryPreviewOpen, value);
    }

    public bool IsBranchGalleryPreviewLoading
    {
        get => _isBranchGalleryPreviewLoading;
        private set => SetProperty(ref _isBranchGalleryPreviewLoading, value);
    }

    public bool IsRomInputOverrideEnabled
    {
        get => _isRomInputOverrideEnabled;
        private set => SetProperty(ref _isRomInputOverrideEnabled, value);
    }

    public int MaxConcurrentGameWindows
    {
        get => _maxConcurrentGameWindows;
        private set
        {
            if (SetProperty(ref _maxConcurrentGameWindows, Math.Clamp(value, 1, 8)))
                SaveSystemConfig();
        }
    }

    public string ActiveSessionSummary => HasActiveGameSessions
        ? $"当前已运行 {_gameSessionService.Count} 个游戏窗口 / 上限 {MaxConcurrentGameWindows}"
        : $"当前没有运行中的游戏窗口 / 上限 {MaxConcurrentGameWindows}";

    public bool IsLanArcadeEnabled
    {
        get => _isLanArcadeEnabled;
        set
        {
            if (SetProperty(ref _isLanArcadeEnabled, value))
            {
                SaveSystemConfig();
                if (!_isApplyingSystemConfig && _isLanArcadeServerReady)
                    _ = ApplyLanArcadeServerStateAsync();
            }
        }
    }

    public int LanArcadePort
    {
        get => _lanArcadePort;
        private set
        {
            var clamped = Math.Clamp(value, 1024, 65535);
            if (SetProperty(ref _lanArcadePort, clamped))
            {
                LanArcadePortInput = clamped.ToString();
                OnPropertyChanged(nameof(LanArcadeEntryUrl));
                OnPropertyChanged(nameof(LanArcadeAccessSummary));
                OnPropertyChanged(nameof(LanArcadePortSummary));
            }
        }
    }

    public bool IsLanArcadeWebPadEnabled
    {
        get => _isLanArcadeWebPadEnabled;
        set
        {
            if (SetProperty(ref _isLanArcadeWebPadEnabled, value))
            {
                SaveSystemConfig();
                if (!_isApplyingSystemConfig && _isLanArcadeServerReady && IsLanArcadeEnabled)
                    _ = ApplyLanArcadeServerStateAsync();
            }
        }
    }

    public bool IsLanArcadeDebugPagesEnabled
    {
        get => _isLanArcadeDebugPagesEnabled;
        set
        {
            if (SetProperty(ref _isLanArcadeDebugPagesEnabled, value))
            {
                SaveSystemConfig();
                if (!_isApplyingSystemConfig && _isLanArcadeServerReady && IsLanArcadeEnabled)
                    _ = ApplyLanArcadeServerStateAsync();
            }
        }
    }

    public string LanArcadePortInput
    {
        get => _lanArcadePortInput;
        set => SetProperty(ref _lanArcadePortInput, value);
    }

    public string LanArcadeEntryUrl => $"http://{LanNetworkHelper.GetPreferredLanAddress() ?? "127.0.0.1"}:{LanArcadePort}/";

    public WriteableBitmap? LanArcadeQrCode
    {
        get => _lanArcadeQrCode;
        private set => SetProperty(ref _lanArcadeQrCode, value);
    }

    public string LanArcadeAccessSummary => _lanArcadeController.BuildAccessSummary(IsLanArcadeEnabled, LanArcadeEntryUrl);

    public string LanArcadeControlSummary
    {
        get
        {
            var remoteSessions = _gameSessionService.Sessions
                .Where(session => !string.IsNullOrWhiteSpace(session.ViewModel.RemoteControlStatusText))
                .Select(session => $"{session.DisplayName}: {session.ViewModel.RemoteControlStatusText}")
                .ToList();

            return remoteSessions.Count > 0
                ? string.Join(Environment.NewLine, remoteSessions)
                : "当前没有网页手柄接管中的 1P / 2P 槽位";
        }
    }

    public string LanArcadePortSummary =>
        $"当前端口 {LanArcadePort}。宿主模式为 Backend.Hosting (embedded)，网页手柄 {(IsLanArcadeWebPadEnabled ? "已启用" : "已关闭")}，调试端点当前停用。";

    public string LanStreamSummary =>
        $"串流分辨率 {LanStreamScaleMultiplier * 256}×{LanStreamScaleMultiplier * 240}，JPEG 质量 {LanStreamJpegQuality}，画面增强: {LanStreamSharpenModeLabel}。参数即时生效，无需重启服务。";

    public string LanStreamSharpenModeLabel => _lanStreamSharpenMode switch
    {
        LanStreamSharpenMode.Subtle   => "轻微锐化",
        LanStreamSharpenMode.Standard => "CRT 扫描线",
        LanStreamSharpenMode.Strong   => "鲜艳色彩",
        _                             => "无"
    };

    // 供 ComboBox SelectedIndex 绑定（0-based）
    public int LanStreamScaleMultiplierIndex
    {
        get => LanStreamScaleMultiplier - 1;
        set => LanStreamScaleMultiplier = value + 1;
    }

    public int LanStreamSharpenModeIndex
    {
        get => (int)_lanStreamSharpenMode;
        set => LanStreamSharpenMode = (LanStreamSharpenMode)value;
    }

    public int LanStreamScaleMultiplier
    {
        get => _lanStreamScaleMultiplier;
        set
        {
            var clamped = Math.Clamp(value, 1, 3);
            if (SetProperty(ref _lanStreamScaleMultiplier, clamped))
            {
                OnPropertyChanged(nameof(LanStreamScaleMultiplierIndex));
                OnPropertyChanged(nameof(LanStreamSummary));
                ApplyStreamParameters();
                SaveSystemConfig();
            }
        }
    }

    public int LanStreamJpegQuality
    {
        get => _lanStreamJpegQuality;
        set
        {
            var clamped = Math.Clamp(value, 60, 95);
            if (SetProperty(ref _lanStreamJpegQuality, clamped))
            {
                OnPropertyChanged(nameof(LanStreamSummary));
                ApplyStreamParameters();
                SaveSystemConfig();
            }
        }
    }

    public LanStreamSharpenMode LanStreamSharpenMode
    {
        get => _lanStreamSharpenMode;
        set
        {
            if (SetProperty(ref _lanStreamSharpenMode, value))
            {
                OnPropertyChanged(nameof(LanStreamSharpenModeIndex));
                OnPropertyChanged(nameof(LanStreamSharpenModeLabel));
                OnPropertyChanged(nameof(LanStreamSummary));
                ApplyStreamParameters();
                SaveSystemConfig();
            }
        }
    }

    public string LanFirewallStatusTitle
    {
        get => _lanFirewallStatusTitle;
        private set => SetProperty(ref _lanFirewallStatusTitle, value);
    }

    public string LanFirewallStatusDetail
    {
        get => _lanFirewallStatusDetail;
        private set => SetProperty(ref _lanFirewallStatusDetail, value);
    }

    public string LanArcadeDiagnosticsText
    {
        get => _lanArcadeDiagnosticsText;
        private set => SetProperty(ref _lanArcadeDiagnosticsText, value);
    }

    public string LanArcadeLastTrafficText
    {
        get => _lanArcadeLastTrafficText;
        private set => SetProperty(ref _lanArcadeLastTrafficText, value);
    }

    public string LanArcadeTrafficSummary => _lanArcadeTrafficCount == 0
        ? "命中计数: 0"
        : $"命中计数: {_lanArcadeTrafficCount}";

    public int PreviewGenerationParallelism
    {
        get => _previewGenerationParallelism;
        private set
        {
            if (SetProperty(ref _previewGenerationParallelism, Math.Clamp(value, 1, 8)))
            {
                OnPropertyChanged(nameof(PreviewGenerationParallelismSummary));
                SaveSystemConfig();
            }
        }
    }

    public string PreviewGenerationParallelismSummary =>
        $"当前批量预览并行任务数 {PreviewGenerationParallelism}，并行越高越快，但 CPU 与内存占用也会增加。";

    public int PreviewGenerationSpeedMultiplier
    {
        get => _previewGenerationSpeedMultiplier;
        set
        {
            if (SetProperty(ref _previewGenerationSpeedMultiplier, Math.Clamp(value, 1, 10)))
            {
                OnPropertyChanged(nameof(PreviewGenerationSpeedSummary));
                SaveSystemConfig();
            }
        }
    }

    public string PreviewGenerationSpeedSummary =>
        $"离线预览按 FC 原始 60fps 的 {PreviewGenerationSpeedMultiplier}x 节奏推进，目标生成速度约 {PreviewGenerationSpeedMultiplier * PreviewSourceFps} fps。";

    public PreviewEncodingMode SelectedPreviewEncodingMode
    {
        get => _previewEncodingMode;
        set
        {
            if (SetProperty(ref _previewEncodingMode, value))
            {
                OnPropertyChanged(nameof(PreviewEncodingModeLabel));
                OnPropertyChanged(nameof(PreviewEncodingModeSummary));
                OnPropertyChanged(nameof(PreviewGenerationCompatibilitySummary));
                SaveSystemConfig();
            }
        }
    }

    public string PreviewEncodingModeLabel => SelectedPreviewEncodingMode switch
    {
        PreviewEncodingMode.Software => "软件编码",
        _ => "自动硬件优先",
    };

    public string PreviewEncodingModeSummary => SelectedPreviewEncodingMode switch
    {
        PreviewEncodingMode.Software =>
            "固定使用软件编码，兼容性最高，但生成预览时会更吃 CPU。",
        _ =>
            OperatingSystem.IsMacOS()
                ? "默认优先使用 VideoToolbox 硬件编码，失败会自动回退到软件 MPEG-4。"
                : OperatingSystem.IsWindows()
                    ? "默认优先尝试 NVENC、QSV、AMF 硬件编码，失败会自动回退到软件 MPEG-4。"
                    : "当前平台默认使用兼容性优先的自动编码策略。"
    };

    public double PreviewResolutionScale
    {
        get => _previewResolutionScale;
        set
        {
            var clamped = Math.Round(Math.Clamp(value, 0.5, 1.0), 2);
            if (SetProperty(ref _previewResolutionScale, clamped))
            {
                OnPropertyChanged(nameof(PreviewResolutionScaleSummary));
                SaveSystemConfig();
            }
        }
    }

    public string PreviewResolutionScaleSummary
    {
        get
        {
            var size = _previewGenerationController.GetPreviewOutputSize(PreviewResolutionScale);
            return $"当前输出 {size.Width}x{size.Height}，缩放 {PreviewResolutionScale:P0}。100% 为 FC 原始分辨率 256x240。";
        }
    }

    public string RomInputOverrideSummary => CurrentRom == null
        ? "未选择游戏"
        : IsRomInputOverrideEnabled ? $"{CurrentRom.DisplayName} 正在使用独立按键配置" : $"{CurrentRom.DisplayName} 当前继承全局按键配置";

    public string ShelfScrollSummary
    {
        get
        {
            return _libraryNavigationController.BuildShelfScrollSummary(
                _shelfSlots.Count,
                _romLibrary,
                CurrentRom,
                ShelfColumns,
                shelfRowsPerPage: 2);
        }
    }

    public string SortDescription =>
        $"{GetSortFieldLabel(SortField)} · {(SortDescending ? "降序" : "升序")}";

    public string SortFieldText => GetSortFieldLabel(SortField);

    public RomSortField SortField
    {
        get => _sortField;
        private set
        {
            if (SetProperty(ref _sortField, value))
            {
                OnPropertyChanged(nameof(IsSortByName));
                OnPropertyChanged(nameof(IsSortBySize));
                OnPropertyChanged(nameof(IsSortByImportedAt));
                OnPropertyChanged(nameof(SortFieldText));
                OnPropertyChanged(nameof(SortDescription));
                SaveSystemConfig();
            }
        }
    }

    public bool SortDescending
    {
        get => _sortDescending;
        private set
        {
            if (SetProperty(ref _sortDescending, value))
            {
                OnPropertyChanged(nameof(SortDirectionText));
                OnPropertyChanged(nameof(SortDirectionCompactText));
                OnPropertyChanged(nameof(SortDescription));
                SaveSystemConfig();
            }
        }
    }

    public bool IsSortByName => SortField == RomSortField.Name;

    public bool IsSortBySize => SortField == RomSortField.Size;

    public bool IsSortByImportedAt => SortField == RomSortField.ImportedAt;

    public string SortDirectionText => SortDescending ? "降序" : "升序";

    public string SortDirectionCompactText => SortDescending ? "降" : "升";

    public GameAspectRatioMode GameAspectRatioMode
    {
        get => _gameAspectRatioMode;
        private set
        {
            if (SetProperty(ref _gameAspectRatioMode, value))
            {
                OnPropertyChanged(nameof(IsAspectNative));
                OnPropertyChanged(nameof(IsAspect8By7));
                OnPropertyChanged(nameof(IsAspect4By3));
                OnPropertyChanged(nameof(IsAspect16By9));
                OnPropertyChanged(nameof(GameAspectRatioLabel));
                SaveSystemConfig();
            }
        }
    }

    public string DefaultCoreId
    {
        get => _defaultCoreId;
        set
        {
            var normalizedCoreId = NormalizeConfiguredCoreId(value);
            if (SetProperty(ref _defaultCoreId, normalizedCoreId))
            {
                OnPropertyChanged(nameof(DefaultCoreDisplayName));
                OnPropertyChanged(nameof(SelectedDefaultCoreManifest));
                OnPropertyChanged(nameof(DefaultCoreSummary));
                OnPropertyChanged(nameof(InstalledCoreCatalogSummary));
                OnPropertyChanged(nameof(SelectedDefaultCoreSystemId));
                OnPropertyChanged(nameof(SelectedDefaultCoreVersion));
                OnPropertyChanged(nameof(SelectedDefaultCoreBinaryKind));
                OnPropertyChanged(nameof(SelectedDefaultCoreSourceLabel));
                OnPropertyChanged(nameof(SelectedDefaultCoreRemovabilityLabel));
                OnPropertyChanged(nameof(SelectedDefaultCoreAssemblyPathDisplay));
                OnPropertyChanged(nameof(SelectedDefaultCoreSourceSummary));
                OnPropertyChanged(nameof(CanUninstallSelectedManagedCore));
                UninstallSelectedManagedCoreCommand.NotifyCanExecuteChanged();
                SaveSystemConfig();
            }
        }
    }

    public IReadOnlyList<CoreManifest> InstalledCoreManifests => _installedCoreManifests;

    public CoreManifest? SelectedDefaultCoreManifest
    {
        get => InstalledCoreManifests.FirstOrDefault(
            manifest => string.Equals(manifest.CoreId, DefaultCoreId, StringComparison.OrdinalIgnoreCase));
        set => DefaultCoreId = value?.CoreId ?? ResolveInstalledFallbackCoreId();
    }

    public string DefaultCoreDisplayName =>
        InstalledCoreManifests.FirstOrDefault(manifest => string.Equals(manifest.CoreId, DefaultCoreId, StringComparison.OrdinalIgnoreCase))?.DisplayName
        ?? DefaultCoreId;

    public string DefaultCoreSummary => SelectedDefaultCoreManifest is { } manifest
        ? $"当前默认核心：{manifest.DisplayName}  ·  system={manifest.SystemId}  ·  version={manifest.Version}  ·  {manifest.BinaryKind}"
        : $"当前默认核心：{DefaultCoreId}";

    public string InstalledCoreCatalogSummary =>
        $"已发现 {InstalledCoreManifests.Count} 个核心，当前默认 {DefaultCoreDisplayName}；已安装核心包目录 {ManagedCoreInstallDirectory}；附加开发探测目录 {EffectiveManagedCoreProbeDirectories.Count} 个。";

    public string SelectedDefaultCoreSystemId =>
        SelectedDefaultCoreManifest?.SystemId ?? "unknown";

    public string SelectedDefaultCoreVersion =>
        SelectedDefaultCoreManifest?.Version ?? "unknown";

    public string SelectedDefaultCoreBinaryKind =>
        SelectedDefaultCoreManifest?.BinaryKind ?? "unknown";

    public string SelectedDefaultCoreSourceLabel =>
        GetSelectedManagedCoreCatalogEntry()?.SourceLabel ?? "未知";

    public string SelectedDefaultCoreRemovabilityLabel =>
        CanUninstallSelectedManagedCore ? "可卸载" : "不可卸载";

    public string SelectedDefaultCoreAssemblyPathDisplay =>
        GetSelectedManagedCoreCatalogEntry() is { } entry
            ? string.IsNullOrWhiteSpace(entry.InstallDirectory)
                ? entry.AssemblyPath ?? "入口程序集路径不可用。"
                : $"{entry.InstallDirectory}{Environment.NewLine}入口程序集：{entry.AssemblyPath ?? "不可用"}"
            : "当前核心尚未暴露安装位置或入口程序集。";

    public string SelectedDefaultCoreSourceSummary =>
        _managedCoreCatalogController.BuildSelectedCoreSourceSummary(GetSelectedManagedCoreCatalogEntry());

    public bool CanUninstallSelectedManagedCore =>
        GetSelectedManagedCoreCatalogEntry() is { CanUninstall: true };

    public string ManagedCoreInstallDirectory => AppObjectStorage.GetInstalledCoreRootDirectory(ResourceRootPath);

    public string ManagedCoreInstallHint =>
        $"安装核心包会解压到 {ManagedCoreInstallDirectory}，并同步写入 core-registry.fcr；安装后会立即刷新默认核心列表，并用于后续新建会话。";

    public bool IsAspectNative => GameAspectRatioMode == GameAspectRatioMode.Native;

    public bool IsAspect8By7 => GameAspectRatioMode == GameAspectRatioMode.Aspect8By7;

    public bool IsAspect4By3 => GameAspectRatioMode == GameAspectRatioMode.Aspect4By3;

    public bool IsAspect16By9 => GameAspectRatioMode == GameAspectRatioMode.Aspect16By9;

    public string GameAspectRatioLabel => GameAspectRatioMode switch
    {
        GameAspectRatioMode.Aspect8By7 => "8:7",
        GameAspectRatioMode.Aspect4By3 => "4:3",
        GameAspectRatioMode.Aspect16By9 => "16:9",
        _ => "原始 256:240",
    };

    public MacUpscaleMode MacUpscaleMode
    {
        get => _macUpscaleMode;
        private set
        {
            if (SetProperty(ref _macUpscaleMode, value))
            {
                OnPropertyChanged(nameof(IsMacUpscaleNone));
                OnPropertyChanged(nameof(IsMacUpscaleSpatial));
                OnPropertyChanged(nameof(MacUpscaleModeLabel));
                foreach (var session in _gameSessionService.Sessions)
                    session.ViewModel.ApplyUpscaleMode(value);
                SaveSystemConfig();
            }
        }
    }

    public bool IsMacUpscaleNone => MacUpscaleMode == global::FCRevolution.Rendering.Metal.MacUpscaleMode.None;

    public bool IsMacUpscaleSpatial => MacUpscaleMode == global::FCRevolution.Rendering.Metal.MacUpscaleMode.Spatial;

    public string MacUpscaleModeLabel => MacUpscaleMode switch
    {
        global::FCRevolution.Rendering.Metal.MacUpscaleMode.Spatial => "Spatial",
        global::FCRevolution.Rendering.Metal.MacUpscaleMode.Temporal => "Temporal",
        _ => "无超分",
    };

    public MacUpscaleOutputResolution MacUpscaleOutputResolution
    {
        get => _macUpscaleOutputResolution;
        private set
        {
            if (SetProperty(ref _macUpscaleOutputResolution, value))
            {
                OnPropertyChanged(nameof(SelectedMacUpscaleOutputResolutionOption));
                OnPropertyChanged(nameof(MacUpscaleOutputResolutionLabel));
                foreach (var session in _gameSessionService.Sessions)
                    session.ViewModel.ApplyUpscaleOutputResolution(value);
                SaveSystemConfig();
            }
        }
    }

    public IReadOnlyList<string> MacUpscaleOutputResolutionOptions => SupportedMacUpscaleOutputResolutionOptions;

    public string SelectedMacUpscaleOutputResolutionOption
    {
        get => GetMacUpscaleOutputResolutionLabel(_macUpscaleOutputResolution);
        set => SetMacUpscaleOutputResolution(ParseMacUpscaleOutputResolutionOption(value));
    }

    public string MacUpscaleOutputResolutionLabel => GetMacUpscaleOutputResolutionLabel(_macUpscaleOutputResolution);

    public PixelEnhancementMode LocalDisplayEnhancementMode
    {
        get => _localDisplayEnhancementMode;
        set
        {
            var sanitized = SanitizeLocalDisplayEnhancementMode(value);
            if (SetProperty(ref _localDisplayEnhancementMode, sanitized))
            {
                OnPropertyChanged(nameof(SelectedLocalDisplayEnhancementOption));
                OnPropertyChanged(nameof(LocalDisplayEnhancementLabel));
                foreach (var session in _gameSessionService.Sessions)
                    session.ViewModel.ApplyEnhancementMode(sanitized);
                SaveSystemConfig();
            }
        }
    }

    public IReadOnlyList<string> LocalDisplayEnhancementOptions => SupportedLocalDisplayEnhancementOptions;

    public string SelectedLocalDisplayEnhancementOption
    {
        get => GetLocalDisplayEnhancementOptionLabel(_localDisplayEnhancementMode);
        set => LocalDisplayEnhancementMode = ParseLocalDisplayEnhancementOption(value);
    }

    public string LocalDisplayEnhancementLabel => GetLocalDisplayEnhancementOptionLabel(_localDisplayEnhancementMode);

    public Thickness BranchGalleryPreviewMargin
    {
        get => _branchGalleryPreviewMargin;
        private set => SetProperty(ref _branchGalleryPreviewMargin, value);
    }

    public string BranchGalleryPreviewTitle => CurrentRom == null
        ? "分支画廊"
        : $"{CurrentRom.DisplayName} 的分支画廊";

    public string BranchGalleryPreviewHint => IsCarouselMode
        ? "当前游戏的时间线分支入口，点击动态列表项进入；点击空白区域关闭。"
        : IsKaleidoscopeMode
            ? "当前游戏的时间线分支入口，点击环形 ROM 位进入；点击空白区域关闭。"
        : "为当前单击选中的游戏快速进入分支画廊，点击柜位进入；点击空白区域关闭。";

    public string BranchGalleryPreviewLoadingText => IsCarouselMode
        ? "正在载入当前动态列表项的分支画廊..."
        : IsKaleidoscopeMode
            ? "正在载入当前环形 ROM 位的分支画廊..."
            : "正在载入当前游戏的分支画廊...";

    public string LayoutModeSummary => LayoutMode switch
    {
        LibraryLayoutMode.BookShelf => "经典木书柜：强调层板、立柱和整排陈列，保留当前 4 列浏览效率。",
        LibraryLayoutMode.Kaleidoscope => "万花筒：8 个 ROM 按八边形环状分布，中间聚焦当前预览，并支持分页切换。",
        _ => "动态列表：突出当前游戏与前后动态列表项的切换关系，适合专注挑选。"
    };

    public string FooterLibraryHint => LayoutMode switch
    {
        LibraryLayoutMode.BookShelf => "ROM 自动扫描目录: 当前工作目录 / roms    •    支持搜索、单击柜位预览，双击书柜中的游戏可直接开始",
        LibraryLayoutMode.Kaleidoscope => "ROM 自动扫描目录: 当前工作目录 / roms    •    支持搜索，单击环形 ROM 位更新中心预览，双击可直接开始",
        _ => "ROM 自动扫描目录: 当前工作目录 / roms    •    支持搜索，在左侧动态列表选择当前游戏，回车或按钮直接开始"
    };

    public WriteableBitmap? BranchGalleryPreviewBitmap =>
        CurrentRom?.CurrentPreviewBitmap ?? CurrentPreviewBitmap ?? DiscDisplayBitmap;

    public string PreviewActionText => IsGeneratingPreview
        ? $"正在生成 {PreviewDurationSeconds} 秒预览"
        : CurrentRom?.HasPreview == true ? $"重新生成 {PreviewDurationSeconds} 秒预览" : $"生成 {PreviewDurationSeconds} 秒预览";

    public string PreviewQuickActionText => IsGeneratingPreview
        ? "生成中"
        : CurrentRom?.HasPreview == true ? "重生成预览" : "生成预览";

    public string PreviewGenerationCompatibilitySummary =>
        SelectedPreviewEncodingMode == PreviewEncodingMode.Software
            ? "当前已强制软件编码。若机器支持硬件编码，可切回自动模式降低预览生成时的 CPU 压力。"
            : "自动模式只加速视频编码阶段；FC 模拟跑帧和缩放仍主要依赖 CPU。";

    public string InputLayoutDebugSummary =>
        "拖动样例手柄上的框即可修正对应按键位置。现在使用统一的中心点坐标，改动会同步作用到全局按键、ROM 独立按键和快速悬浮设置。";

    public Thickness InputLayoutBridgeMargin => new(_inputBindingLayout.BridgeX, _inputBindingLayout.BridgeY, 0, 0);
    public Thickness InputLayoutLeftCircleMargin => new(_inputBindingLayout.LeftCircleX, _inputBindingLayout.LeftCircleY, 0, 0);
    public Thickness InputLayoutDPadHorizontalMargin => new(_inputBindingLayout.DPadHorizontalX, _inputBindingLayout.DPadHorizontalY, 0, 0);
    public Thickness InputLayoutDPadVerticalMargin => new(_inputBindingLayout.DPadVerticalX, _inputBindingLayout.DPadVerticalY, 0, 0);
    public Thickness InputLayoutRightCircleMargin => new(_inputBindingLayout.RightCircleX, _inputBindingLayout.RightCircleY, 0, 0);
    public Thickness InputLayoutBDecorationMargin => new(_inputBindingLayout.BDecorationX, _inputBindingLayout.BDecorationY, 0, 0);
    public Thickness InputLayoutADecorationMargin => new(_inputBindingLayout.ADecorationX, _inputBindingLayout.ADecorationY, 0, 0);
    public Thickness InputLayoutSelectDecorationMargin => new(_inputBindingLayout.SelectDecorationX, _inputBindingLayout.SelectDecorationY, 0, 0);
    public Thickness InputLayoutStartDecorationMargin => new(_inputBindingLayout.StartDecorationX, _inputBindingLayout.StartDecorationY, 0, 0);

    public string QuickRomInputEditorTitle => CurrentRom == null
        ? "快速独立按键"
        : $"{CurrentRom.DisplayName} 的快速独立按键";

    public LibraryLayoutMode LayoutMode
    {
        get => _layoutMode;
        private set
        {
            if (SetProperty(ref _layoutMode, value))
            {
                CurrentLayoutName = GetLayoutDisplayName(value);
                OnPropertyChanged(nameof(IsBookShelfMode));
                OnPropertyChanged(nameof(IsClassicBookShelfMode));
                OnPropertyChanged(nameof(IsShelfLayoutMode));
                OnPropertyChanged(nameof(IsCarouselMode));
                OnPropertyChanged(nameof(IsDynamicListMode));
                OnPropertyChanged(nameof(IsKaleidoscopeMode));
                OnPropertyChanged(nameof(BranchGalleryPreviewHint));
                OnPropertyChanged(nameof(BranchGalleryPreviewLoadingText));
                OnPropertyChanged(nameof(LayoutModeSummary));
                OnPropertyChanged(nameof(FooterLibraryHint));
                RequestPreviewWarmup(CurrentRom);
                SaveSystemConfig();
            }
        }
    }

    public bool IsBookShelfMode => LayoutMode == LibraryLayoutMode.BookShelf;

    public bool IsClassicBookShelfMode => LayoutMode == LibraryLayoutMode.BookShelf;

    public bool IsShelfLayoutMode => LayoutMode == LibraryLayoutMode.BookShelf;

    public bool IsCarouselMode => LayoutMode == LibraryLayoutMode.Carousel;

    public bool IsDynamicListMode => LayoutMode == LibraryLayoutMode.Carousel;

    public bool IsKaleidoscopeMode => LayoutMode == LibraryLayoutMode.Kaleidoscope;

    public TimelineMode TimelineMode
    {
        get => _timelineMode;
        set
        {
            if (SetProperty(ref _timelineMode, value))
            {
                ApplyTimelineMode();
                ReopenReplayLog(resetFile: false);
                SaveSystemConfig();
            }
        }
    }

    public double ShortRewindSeconds => ConvertFramesToSeconds(_shortRewindFrames);

    public int ShortRewindFrames => _shortRewindFrames;

    public string ShortRewindSecondsInput
    {
        get => _shortRewindSecondsInput;
        set
        {
            if (!SetProperty(ref _shortRewindSecondsInput, value) || _isSyncingShortRewindInputs)
                return;

            if (TryParseFiniteDouble(value, out var seconds))
                UpdateShortRewindFrames(ConvertSecondsToFrames(seconds));
        }
    }

    public string ShortRewindFramesInput
    {
        get => _shortRewindFramesInput;
        set
        {
            if (!SetProperty(ref _shortRewindFramesInput, value) || _isSyncingShortRewindInputs)
                return;

            if (TryParsePositiveInt(value, out var frames))
                UpdateShortRewindFrames(frames);
        }
    }

    public string ShortRewindSummary =>
        $"当前按 {FormatShortRewindSeconds(ShortRewindSeconds)} 秒 / {ShortRewindFrames} 帧回退。F7 会按这个值回退；短回溯模式下不会记录分支，也不会开放分支画廊和视频导出。";

    public string TimelineModeSummary => TimelineMode switch
    {
        TimelineMode.Disabled => "关闭时间线：仅保留快速存档/读档，不记录回溯历史。",
        TimelineMode.ShortRewindOnly => $"短回溯模式：仅保留最近 {FormatShortRewindSeconds(ShortRewindSeconds)} 秒（{ShortRewindFrames} 帧）的回溯，不启用分支画廊和视频导出。",
        _ => "完整时间线：启用分支树、分支存档、输入日志和视频导出。",
    };

    private void LoadSystemConfig()
    {
        var watch = Stopwatch.StartNew();
        LogStartup("LoadSystemConfig begin");
        _isApplyingSystemConfig = true;
        try
        {
            var profile = SystemConfigProfile.Load();
            Volume = profile.Volume;
            ShowDebugStatus = false;
            MaxConcurrentGameWindows = profile.MaxConcurrentGameWindows;
            PreviewGenerationParallelism = profile.PreviewGenerationParallelism;
            PreviewGenerationSpeedMultiplier = profile.PreviewGenerationSpeedMultiplier;
            PreviewResolutionScale = profile.PreviewResolutionScale;
            PreviewPreloadWindowSeconds = profile.PreviewPreloadWindowSeconds;
            if (Enum.TryParse<PreviewEncodingMode>(profile.PreviewEncodingMode, out var previewEncodingMode))
                SelectedPreviewEncodingMode = previewEncodingMode;

            if (Enum.TryParse<LibraryLayoutMode>(profile.LayoutMode, out var layoutMode))
                LayoutMode = layoutMode;

            if (Enum.TryParse<RomSortField>(profile.SortField, out var sortField))
                SortField = sortField;

            SortDescending = profile.SortDescending;

            if (Enum.TryParse<GameAspectRatioMode>(profile.GameAspectRatioMode, out var aspectRatioMode))
                GameAspectRatioMode = aspectRatioMode;
            if (Enum.TryParse<MacUpscaleMode>(profile.MacRenderUpscaleMode, out var parsedUpscaleMode))
                _macUpscaleMode = parsedUpscaleMode;
            if (Enum.TryParse<MacUpscaleOutputResolution>(profile.MacRenderUpscaleOutputResolution, out var parsedUpscaleOutputResolution))
                _macUpscaleOutputResolution = parsedUpscaleOutputResolution;

            if (Enum.TryParse<TimelineMode>(profile.TimelineMode, out var timelineMode))
                TimelineMode = timelineMode;

            UpdateShortRewindFrames(profile.ShortRewindFrames, saveConfig: false);
            AppObjectStorage.ConfigureResourceRoot(profile.ResourceRootPath);
            ResourceRootPath = AppObjectStorage.GetResourceRoot();
            LoadManagedCoreSourceSettings(profile);
            DefaultCoreId = profile.DefaultCoreId;
            LanArcadePort = profile.LanArcadePort;
            IsLanArcadeEnabled = profile.IsLanArcadeEnabled;
            IsLanArcadeWebPadEnabled = profile.IsLanArcadeWebPadEnabled;
            IsLanArcadeDebugPagesEnabled = false;
            _lanStreamScaleMultiplier = Math.Clamp(profile.LanStreamScaleMultiplier, 1, 3);
            _lanStreamJpegQuality = Math.Clamp(profile.LanStreamJpegQuality, 60, 95);
            if (Enum.TryParse<LanStreamSharpenMode>(profile.LanStreamSharpenMode, out var parsedSharpen))
                _lanStreamSharpenMode = parsedSharpen;
            if (Enum.TryParse<PixelEnhancementMode>(profile.LocalDisplayEnhancementMode, out var parsedLocalEnhancement))
                _localDisplayEnhancementMode = SanitizeLocalDisplayEnhancementMode(parsedLocalEnhancement);
            LoadGlobalInputConfig(profile);
            LoadShortcutBindings(profile);
            _inputBindingLayout = (profile.InputBindingLayout ?? InputBindingLayoutProfile.CreateDefault()).Clone();
            _inputBindingLayout.Sanitize();
            _inputLayoutController.ApplyInputBindingLayoutToAllEntries(
                _inputBindingLayout,
                _globalInputBindings,
                _romInputBindings,
                propertyName => OnPropertyChanged(propertyName));
        }
        finally
        {
            _isApplyingSystemConfig = false;
        }

        LogStartup($"LoadSystemConfig complete in {watch.ElapsedMilliseconds} ms; resourceRoot={ResourceRootPath}, lanEnabled={IsLanArcadeEnabled}, lanPort={LanArcadePort}");
    }

    private void SaveSystemConfig()
    {
        if (_isApplyingSystemConfig)
            return;

        try
        {
            var profile = SystemConfigProfile.Load();
            profile.Volume = Volume;
            profile.ShowDebugStatus = ShowDebugStatus;
            profile.MaxConcurrentGameWindows = MaxConcurrentGameWindows;
            profile.PreviewGenerationParallelism = PreviewGenerationParallelism;
            profile.PreviewGenerationSpeedMultiplier = PreviewGenerationSpeedMultiplier;
            profile.PreviewResolutionScale = PreviewResolutionScale;
            profile.PreviewPreloadWindowSeconds = PreviewPreloadWindowSeconds;
            profile.PreviewEncodingMode = SelectedPreviewEncodingMode.ToString();
            profile.LayoutMode = LayoutMode.ToString();
            profile.SortField = SortField.ToString();
            profile.SortDescending = SortDescending;
            profile.GameAspectRatioMode = GameAspectRatioMode.ToString();
            profile.DefaultCoreId = DefaultCoreId;
            profile.ManagedCoreProbePaths = [.. _managedCoreProbePaths];
            profile.MacRenderUpscaleMode = _macUpscaleMode.ToString();
            profile.MacRenderUpscaleOutputResolution = _macUpscaleOutputResolution.ToString();
            profile.TimelineMode = TimelineMode.ToString();
            profile.ShortRewindSeconds = ShortRewindSeconds;
            profile.ShortRewindFrames = ShortRewindFrames;
            profile.ResourceRootPath = ResourceRootPath;
            profile.LanArcadePort = LanArcadePort;
            profile.IsLanArcadeEnabled = IsLanArcadeEnabled;
            profile.IsLanArcadeWebPadEnabled = IsLanArcadeWebPadEnabled;
            profile.IsLanArcadeDebugPagesEnabled = false;
            profile.LanStreamScaleMultiplier = _lanStreamScaleMultiplier;
            profile.LanStreamJpegQuality = _lanStreamJpegQuality;
            profile.LanStreamSharpenMode = _lanStreamSharpenMode.ToString();
            profile.LocalDisplayEnhancementMode = _localDisplayEnhancementMode.ToString();
            var inputConfigSaveState = _inputBindingsController.BuildGlobalInputConfigSaveState(
                _globalInputBindings,
                _globalExtraInputBindings,
                _shortcutBindings,
                _inputBindingLayout);
            profile.PlayerInputOverrides = inputConfigSaveState.PlayerInputOverrides;
            profile.ExtraInputBindings = inputConfigSaveState.ExtraInputBindings;
            profile.ShortcutBindings = inputConfigSaveState.ShortcutBindings;
            profile.InputBindingLayout = inputConfigSaveState.InputBindingLayout;
            SystemConfigProfile.Save(profile);
        }
        catch
        {
        }
    }

    private void EmuThreadLoop()
    {
        var sw = Stopwatch.StartNew();
        var frameTimer = Stopwatch.StartNew();
        const double frameMs = 1000.0 / 60.0;
        var frameCount = 0;
        var fpsSw = Stopwatch.StartNew();
        var fpsBucket = 0;

        while (_emuThreadAlive)
        {
            var targetMs = ++frameCount * frameMs;

            if (IsRomLoaded && !IsPaused)
            {
                try
                {
                    frameTimer.Restart();
                    RunCoreFrame();
                    AppendReplayFrame();
                    _frameTimeMicros = (int)(frameTimer.Elapsed.TotalMilliseconds * 1000);

                    fpsBucket++;
                    if (fpsSw.Elapsed.TotalSeconds >= 0.5)
                    {
                        _emuFpsRaw = (int)(fpsBucket / fpsSw.Elapsed.TotalSeconds);
                        _emuFrameCount += fpsBucket;
                        fpsBucket = 0;
                        fpsSw.Restart();
                    }
                }
                catch (Exception ex)
                {
                    var message = ex.Message;
                    _emuThreadAlive = false;
                    Dispatcher.UIThread.Post(() =>
                    {
                        PauseCoreSession();

                        StatusText = $"错误: {message}";
                    });
                    return;
                }
            }
            else
            {
                Thread.Sleep(1);
                frameCount = (int)(sw.Elapsed.TotalMilliseconds / frameMs);
                continue;
            }

            var nowMs = sw.Elapsed.TotalMilliseconds;
            var sleepMs = targetMs - nowMs;
            if (sleepMs > 1.5)
                Thread.Sleep((int)sleepMs);
            else if (sleepMs < -200)
                frameCount = (int)(sw.Elapsed.TotalMilliseconds / frameMs);
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        UpdateTurboPulse();
        UpdateKaleidoscopeBackdrop();
        var fb = _pendingFrame;
        if (fb == null)
        {
            MaybeSyncBranchGalleryState();
            return;
        }

        _pendingFrame = null;
        UpdateDisplay(fb);
        UpdateFps();
        MaybeSyncBranchGalleryState();
    }

    private void UpdateKaleidoscopeBackdrop()
    {
        if (!_hasInitializedKaleidoscopeBackdrop)
            InitializeKaleidoscopeBackdrop();

        var t = _fpsWatch.Elapsed.TotalSeconds;
        var deltaSeconds = _lastKaleidoscopeBackdropSeconds < 0d
            ? 1d / 60d
            : Math.Clamp(t - _lastKaleidoscopeBackdropSeconds, 1d / 240d, 1d / 24d);
        _lastKaleidoscopeBackdropSeconds = t;

        AdvanceKaleidoscopeBackdropActor(_kaleidoscopePrimarySweep, deltaSeconds);
        AdvanceKaleidoscopeBackdropActor(_kaleidoscopeSecondarySweep, deltaSeconds);
        AdvanceKaleidoscopeBackdropActor(_kaleidoscopePrimaryDot, deltaSeconds);
        AdvanceKaleidoscopeBackdropActor(_kaleidoscopeSecondaryDot, deltaSeconds);

        KaleidoscopePulseOpacity = 0.16 + (Math.Sin(t * 0.9d) + 1d) * 0.05d;
        SyncKaleidoscopeBackdropProperties();
    }

    private void InitializeKaleidoscopeBackdrop()
    {
        InitializeKaleidoscopeBackdropActor(_kaleidoscopePrimarySweep);
        InitializeKaleidoscopeBackdropActor(_kaleidoscopeSecondarySweep);
        InitializeKaleidoscopeBackdropActor(_kaleidoscopePrimaryDot);
        InitializeKaleidoscopeBackdropActor(_kaleidoscopeSecondaryDot);
        _hasInitializedKaleidoscopeBackdrop = true;
        SyncKaleidoscopeBackdropProperties();
    }

    private void InitializeKaleidoscopeBackdropActor(KaleidoscopeBackdropActor actor)
    {
        var angle = _kaleidoscopeBackdropRandom.NextDouble() * Math.PI * 2d;
        actor.VelocityX = Math.Cos(angle) * actor.Speed;
        actor.VelocityY = Math.Sin(angle) * actor.Speed;
    }

    private void AdvanceKaleidoscopeBackdropActor(KaleidoscopeBackdropActor actor, double deltaSeconds)
    {
        var nextX = actor.CenterX + actor.VelocityX * deltaSeconds;
        var nextY = actor.CenterY + actor.VelocityY * deltaSeconds;

        for (var pass = 0; pass < 2; pass++)
        {
            var collided = false;
            for (var i = 0; i < KaleidoscopeBackdropBoundary.Length; i++)
            {
                var start = KaleidoscopeBackdropBoundary[i];
                var end = KaleidoscopeBackdropBoundary[(i + 1) % KaleidoscopeBackdropBoundary.Length];
                var edge = end - start;
                var inward = NormalizeVector(new Vector(-edge.Y, edge.X));
                var distance = Dot(nextX - start.X, nextY - start.Y, inward);
                if (distance >= actor.Padding)
                    continue;

                var correction = actor.Padding - distance + 0.5d;
                nextX += inward.X * correction;
                nextY += inward.Y * correction;

                var velocityDot = Dot(actor.VelocityX, actor.VelocityY, inward);
                if (velocityDot < 0d)
                {
                    actor.VelocityX -= 2d * velocityDot * inward.X;
                    actor.VelocityY -= 2d * velocityDot * inward.Y;
                }

                collided = true;
            }

            if (!collided)
                break;
        }

        actor.CenterX = nextX;
        actor.CenterY = nextY;
    }

    private void SyncKaleidoscopeBackdropProperties()
    {
        KaleidoscopeSweepOffset = _kaleidoscopePrimarySweep.CenterX - _kaleidoscopePrimarySweep.Width / 2d;
        KaleidoscopeSweepTop = _kaleidoscopePrimarySweep.CenterY - _kaleidoscopePrimarySweep.Height / 2d;
        KaleidoscopeSweepAngle = Math.Atan2(_kaleidoscopePrimarySweep.VelocityY, _kaleidoscopePrimarySweep.VelocityX) * 180d / Math.PI;

        KaleidoscopeSweepOffsetSecondary = _kaleidoscopeSecondarySweep.CenterX - _kaleidoscopeSecondarySweep.Width / 2d;
        KaleidoscopeSweepTopSecondary = _kaleidoscopeSecondarySweep.CenterY - _kaleidoscopeSecondarySweep.Height / 2d;
        KaleidoscopeSweepAngleSecondary = Math.Atan2(_kaleidoscopeSecondarySweep.VelocityY, _kaleidoscopeSecondarySweep.VelocityX) * 180d / Math.PI;

        KaleidoscopeOrbitDotX = _kaleidoscopePrimaryDot.CenterX - _kaleidoscopePrimaryDot.Width / 2d;
        KaleidoscopeOrbitDotY = _kaleidoscopePrimaryDot.CenterY - _kaleidoscopePrimaryDot.Height / 2d;
        KaleidoscopeOrbitDotSecondaryX = _kaleidoscopeSecondaryDot.CenterX - _kaleidoscopeSecondaryDot.Width / 2d;
        KaleidoscopeOrbitDotSecondaryY = _kaleidoscopeSecondaryDot.CenterY - _kaleidoscopeSecondaryDot.Height / 2d;
    }

    private static Vector NormalizeVector(Vector vector)
    {
        var length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
        return length <= double.Epsilon
            ? new Vector(0d, 1d)
            : new Vector(vector.X / length, vector.Y / length);
    }

    private static double Dot(double x, double y, Vector vector) => x * vector.X + y * vector.Y;

    private sealed class KaleidoscopeBackdropActor
    {
        public KaleidoscopeBackdropActor(double width, double height, double speed, double padding, double centerX, double centerY)
        {
            Width = width;
            Height = height;
            Speed = speed;
            Padding = padding;
            CenterX = centerX;
            CenterY = centerY;
        }

        public double Width { get; }

        public double Height { get; }

        public double Speed { get; }

        public double Padding { get; }

        public double CenterX { get; set; }

        public double CenterY { get; set; }

        public double VelocityX { get; set; }

        public double VelocityY { get; set; }
    }

    private void OnPreviewTick(object? sender, EventArgs e)
    {
        var elapsedMs = _previewPlaybackWatch.ElapsedMilliseconds;
        var result = _previewTickController.OnPreviewTick(
            elapsedMs,
            _previewTickCounter,
            ShowDebugStatus,
            _previewTimer.IsEnabled,
            CurrentRom,
            GetPreviewAnimationTargets(),
            ShouldShowLiveGameOnDisc());
        _previewTickCounter = result.NextTickCounter;

        if (result.ShouldSyncCurrentPreviewBitmap && CurrentRom != null)
        {
            CurrentPreviewBitmap = CurrentRom.CurrentPreviewBitmap;
            OnPropertyChanged(nameof(CurrentPreviewBitmap));
            OnPropertyChanged(nameof(HasPreviewBitmap));
        }

        if (result.DebugText != null)
            PreviewDebugText = result.DebugText;

        if (result.ShouldRefreshDiscBitmap)
        {
            UpdateDiscDisplayBitmap();
            OnPropertyChanged(nameof(DiscDisplayBitmap));
            OnPropertyChanged(nameof(HasDiscDisplayBitmap));
            OnPropertyChanged(nameof(NoDiscDisplayBitmap));
        }
    }

    public void UpdateShelfViewport(double verticalOffset, double viewportHeight, bool isScrolling)
    {
        var startRow = Math.Max(0, (int)Math.Floor(verticalOffset / ShelfRowHeight));
        var effectiveViewportHeight = Math.Max(1d, viewportHeight);
        var endRow = Math.Max(startRow, (int)Math.Floor(Math.Max(0d, verticalOffset + effectiveViewportHeight - 1d) / ShelfRowHeight));
        var visibleRows = Math.Max(1, endRow - startRow + 1);
        var changed = startRow != _shelfVisibleStartRow || visibleRows != _shelfVisibleRowCount || _isShelfScrolling != isScrolling;

        _shelfVisibleStartRow = startRow;
        _shelfVisibleRowCount = visibleRows;
        _isShelfScrolling = isScrolling;

        if (!changed || !IsShelfLayoutMode)
            return;

        if (!isScrolling)
            RequestPreviewWarmup(CurrentRom);
    }

    private unsafe void UpdateDisplay(uint[] frameBuffer)
    {
        var bitmap = ScreenBitmap;
        if (bitmap == null)
        {
            bitmap = CreateBitmap(PreviewSourceWidth, PreviewSourceHeight);
            ScreenBitmap = bitmap;
        }

        using var locked = bitmap.Lock();
        fixed (uint* src = frameBuffer)
        {
            Buffer.MemoryCopy(
                src,
                (void*)locked.Address,
                (long)locked.RowBytes * PreviewSourceHeight,
                (long)frameBuffer.Length * sizeof(uint));
        }

        OnPropertyChanged(nameof(ScreenBitmap));
        if (ShouldShowLiveGameOnDisc())
        {
            OnPropertyChanged(nameof(DiscDisplayBitmap));
            OnPropertyChanged(nameof(HasDiscDisplayBitmap));
            OnPropertyChanged(nameof(NoDiscDisplayBitmap));
        }
    }

    private void UpdateFps()
    {
        _fpsCounter++;
        var elapsed = _fpsWatch.Elapsed.TotalSeconds;
        if (elapsed < 0.5)
            return;

        var fps = _fpsCounter / elapsed;
        _fpsCounter = 0;
        _fpsWatch.Restart();

        var frameMs = _frameTimeMicros / 1000.0;
        var audioTag = _audio.IsAvailable ? "音频:正常" : $"音频:失败({_audio.InitError ?? "无设备"})";
        FpsText = $"显示:{fps:F0} 模拟:{_emuFpsRaw} 帧耗:{frameMs:F1}ms | {audioTag}";
        FrameText = $"帧 {_timeTravelService.CurrentFrame}";
    }

    [RelayCommand]
    private async Task OpenRomAsync()
    {
        var taskItem = EnqueueImportTask("导入单个 ROM");
        try
        {
            var path = await PickSingleFileAsync(
                "导入 NES ROM",
                new FilePickerFileType("NES ROM") { Patterns = ["*.nes"] },
                new FilePickerFileType("All Files") { Patterns = ["*.*"] });
            if (string.IsNullOrWhiteSpace(path))
            {
                taskItem.Complete("已取消");
                return;
            }

            taskItem.Status = $"正在导入: {Path.GetFileName(path)}";
            var imported = _romResourceImportService.ImportRom(path);
            RefreshRomLibrary();
            var importedRom = _romLibrary.FirstOrDefault(item => PathsEqual(item.Path, imported.AbsolutePath));
            if (importedRom != null)
            {
                CurrentRom = importedRom;
                LoadRom(importedRom.Path);
            }

            taskItem.Complete($"导入成功: {Path.GetFileName(imported.AbsolutePath)}");
            StatusText = $"已导入 ROM: {Path.GetFileName(imported.AbsolutePath)}";
        }
        catch (Exception ex)
        {
            taskItem.Complete($"导入失败: {ex.Message}");
            StatusText = $"导入 ROM 失败: {ex.Message}";
        }
        finally
        {
            PruneCompletedTaskQueueItems();
        }
    }

    [RelayCommand]
    private async Task ImportRomFolderAsync()
    {
        var taskItem = EnqueueImportTask("导入 ROM 文件夹");
        try
        {
            var topLevel = GetDesktopMainWindow();
            if (topLevel == null)
            {
                taskItem.Complete("导入失败: 无法访问主窗口");
                return;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "导入 ROM 文件夹",
                AllowMultiple = false
            });

            if (folders.Count == 0)
            {
                taskItem.Complete("已取消");
                return;
            }

            var path = folders[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                taskItem.Complete("已取消");
                return;
            }

            taskItem.Status = $"正在扫描: {Path.GetFileName(path)}";
            var imported = _romResourceImportService.ImportRomDirectory(path);
            RefreshRomLibrary();
            taskItem.Complete(imported.Count == 0
                ? "未找到可导入的 .nes 文件"
                : $"导入完成: {imported.Count} 个 ROM");
            StatusText = imported.Count == 0
                ? "所选文件夹中未找到 .nes 文件"
                : $"已导入 ROM {imported.Count} 个";
        }
        catch (Exception ex)
        {
            taskItem.Complete($"导入失败: {ex.Message}");
            StatusText = $"导入 ROM 文件夹失败: {ex.Message}";
        }
        finally
        {
            PruneCompletedTaskQueueItems();
        }
    }

    private Window? GetDesktopMainWindow()
    {
        return Avalonia.Application.Current?.ApplicationLifetime is
            IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
    }

    private async Task<string?> PickSingleFileAsync(string title, params FilePickerFileType[] fileTypes)
    {
        var topLevel = GetDesktopMainWindow();
        if (topLevel == null)
            return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });

        if (files.Count == 0)
            return null;

        return files[0].TryGetLocalPath();
    }

    [RelayCommand]
    private void RefreshRomLibrary()
    {
        var refreshWatch = Stopwatch.StartNew();
        var romDirectory = GetRomDirectory();
        var isInitialStartupRefresh = !_hasLoadedStartupContent;
        LogStartup($"RefreshRomLibrary begin; romDirectory={romDirectory}");
        Directory.CreateDirectory(romDirectory);
        Directory.CreateDirectory(GetPreviewDirectory());

        var preferredPath = CurrentRom?.Path ?? _romPath;

        _romInputOverrides.Clear();
        _romExtraInputOverrides.Clear();
        _previewCleanupController.ClearPreviewFrames(_allRomLibrary, clearPreviewAvailability: false);

        var snapshot = _libraryCatalogController.CaptureCatalogSnapshot(
            romDirectory,
            ResolvePreviewPlaybackPath,
            isInitialStartupRefresh,
            onRomFilesScanned: count => LogStartup($"RefreshRomLibrary found {count} ROM file(s) in {refreshWatch.ElapsedMilliseconds} ms"),
            onRomProcessing: (processed, total, romPath) => LogStartup($"RefreshRomLibrary processing {processed}/{total}: {romPath}"),
            onRomDiscovered: romPath => _inputOverrideController.LoadRomProfileInputOverride(
                romPath,
                _romInputOverrides,
                _romExtraInputOverrides));

        _allRomLibrary.Clear();
        _allRomLibrary.AddRange(snapshot.AllItems);
        RebuildVisibleRomLibrary(preferredPath, stopPreviewTimer: true);
        UpdateLibrarySummary();

        RefreshResourceCleanupSummary();
        _ = SyncBackendStateMirrorAsync();
        LogStartup($"RefreshRomLibrary complete in {refreshWatch.ElapsedMilliseconds} ms; visible={_romLibrary.Count}, all={_allRomLibrary.Count}");
    }

    private void RebuildVisibleRomLibrary(string? preferredPath = null, bool stopPreviewTimer = false)
    {
        var selection = _libraryCatalogController.BuildVisibleSelection(
            _allRomLibrary,
            LibrarySearchText,
            SortField,
            SortDescending,
            preferredPath,
            CurrentRom?.Path,
            _romPath,
            PathsEqual);

        _romLibrary.Clear();
        foreach (var item in selection.VisibleItems)
            _romLibrary.Add(item);

        OnPropertyChanged(nameof(IsLibraryEmpty));
        OnPropertyChanged(nameof(HasRomLibrary));
        OnPropertyChanged(nameof(CanGenerateAllPreviews));
        OnPropertyChanged(nameof(ShelfScrollSummary));
        RebuildShelfSlots();
        RebuildKaleidoscopePages();

        if (stopPreviewTimer)
            _previewTimer.Stop();

        SyncLoadedFlags();

        var selected = selection.PreferredRom;
        CurrentRom = selected;
        RequestPreviewWarmup(selected);

        if (_romLibrary.Count == 0)
        {
            _previewSelectionController.HandleEmptyLibrary(
                stopPreviewPlayback: StopPreviewPlayback,
                clearCurrentPreviewBitmap: () => CurrentPreviewBitmap = null);
            UpdateEmptyLibraryState();
        }
        else
        {
            StatusText = _libraryCatalogController.BuildNonEmptyStatusText(_romLibrary.Count, HasLibrarySearchText);
        }

        OnPropertyChanged(nameof(CurrentRomActionSummary));
    }

    private void UpdateLibrarySummary()
    {
        var romDirectory = GetRomDirectory();
        LibrarySummary = _libraryCatalogController.BuildLibrarySummary(
            romDirectory,
            _allRomLibrary.Count,
            _romLibrary.Count,
            HasLibrarySearchText,
            SortDescription);
    }

    private void UpdateEmptyLibraryState()
    {
        var emptyState = _libraryCatalogController.BuildEmptyLibraryState(
            GetRomDirectory(),
            _allRomLibrary.Count,
            LibrarySearchText);
        CurrentRomName = emptyState.CurrentRomName;
        CurrentRomPathText = emptyState.CurrentRomPathText;
        PreviewStatusText = emptyState.PreviewStatusText;
        StatusText = emptyState.StatusText;
    }

    private bool MatchesLibrarySearch(RomLibraryItem item)
    {
        return _catalogLayoutController.MatchesLibrarySearch(item, LibrarySearchText);
    }

    [RelayCommand]
    private void SelectRom(RomLibraryItem? item)
    {
        if (item != null)
            CurrentRom = item;
    }

    [RelayCommand]
    private async Task SelectPreviousAsync()
    {
        if (PreviousRom != null)
            await ApplyCarouselSelectionAsync(PreviousRom, -1);
    }

    [RelayCommand]
    private async Task SelectNextAsync()
    {
        if (NextRom != null)
            await ApplyCarouselSelectionAsync(NextRom, 1);
    }

    [RelayCommand]
    private async Task SelectCarouselRomAsync(RomLibraryItem? item)
    {
        if (item == null)
            return;

        if (CurrentRom == null || ReferenceEquals(CurrentRom, item))
        {
            CurrentRom = item;
            ShowBranchGalleryPreview(item);
            return;
        }

        var currentIndex = _romLibrary.IndexOf(CurrentRom);
        var targetIndex = _romLibrary.IndexOf(item);
        var direction = targetIndex < currentIndex ? -1 : 1;
        await ApplyCarouselSelectionAsync(item, direction);
    }

    [RelayCommand]
    private void SelectKaleidoscopeRom(KaleidoscopeSlotItem? slot)
    {
        if (slot?.Rom == null)
            return;

        ShowBranchGalleryPreview(slot.Rom);
        StatusText = $"已选中: {slot.Rom.DisplayName}";
    }

    [RelayCommand]
    private void SelectKaleidoscopePage(KaleidoscopePageItem? page)
    {
        if (page == null)
            return;

        SetKaleidoscopePage(page.Index, preserveSelection: false);
    }

    [RelayCommand]
    private void SelectPreviousKaleidoscopePage()
    {
        if (_kaleidoscopePages.Count == 0)
            return;

        SetKaleidoscopePage((KaleidoscopeCurrentPageIndex - 1 + _kaleidoscopePages.Count) % _kaleidoscopePages.Count, preserveSelection: false);
    }

    [RelayCommand]
    private void SelectNextKaleidoscopePage()
    {
        if (_kaleidoscopePages.Count == 0)
            return;

        SetKaleidoscopePage((KaleidoscopeCurrentPageIndex + 1) % _kaleidoscopePages.Count, preserveSelection: false);
    }

    [RelayCommand]
    private void UseBookShelfLayout() => LayoutMode = LibraryLayoutMode.BookShelf;

    [RelayCommand]
    private void UseCarouselLayout() => LayoutMode = LibraryLayoutMode.Carousel;

    [RelayCommand]
    private void UseKaleidoscopeLayout() => LayoutMode = LibraryLayoutMode.Kaleidoscope;

    [RelayCommand]
    private void ToggleLayoutMode() =>
        LayoutMode = LayoutMode switch
        {
            LibraryLayoutMode.BookShelf => LibraryLayoutMode.Carousel,
            LibraryLayoutMode.Carousel => LibraryLayoutMode.Kaleidoscope,
            _ => LibraryLayoutMode.BookShelf
        };

    [RelayCommand]
    private void ClearLibrarySearch() => LibrarySearchText = string.Empty;

    [RelayCommand]
    private void SortByName() => SetSort(RomSortField.Name);

    [RelayCommand]
    private void SortBySize() => SetSort(RomSortField.Size);

    [RelayCommand]
    private void SortByImportedAt() => SetSort(RomSortField.ImportedAt);

    [RelayCommand]
    private void CycleSortField()
    {
        var next = SortField switch
        {
            RomSortField.Name => RomSortField.Size,
            RomSortField.Size => RomSortField.ImportedAt,
            _ => RomSortField.Name,
        };

        SetSort(next);
    }

    [RelayCommand]
    private void ToggleSortDirection()
    {
        SortDescending = !SortDescending;
        ApplySortToLibrary();
    }

    [RelayCommand]
    private void IncreaseMaxConcurrentGameWindows()
    {
        if (MaxConcurrentGameWindows >= 8)
            return;

        MaxConcurrentGameWindows++;
        OnPropertyChanged(nameof(ActiveSessionSummary));
    }

    [RelayCommand]
    private void UseNativeAspectRatio() => SetGameAspectRatio(GameAspectRatioMode.Native);

    [RelayCommand]
    private void UseAspectRatio8By7() => SetGameAspectRatio(GameAspectRatioMode.Aspect8By7);

    [RelayCommand]
    private void UseAspectRatio4By3() => SetGameAspectRatio(GameAspectRatioMode.Aspect4By3);

    [RelayCommand]
    private void UseAspectRatio16By9() => SetGameAspectRatio(GameAspectRatioMode.Aspect16By9);

    [RelayCommand]
    private void UseMacUpscaleNone() => SetMacUpscaleMode(global::FCRevolution.Rendering.Metal.MacUpscaleMode.None);

    [RelayCommand]
    private void UseMacUpscaleSpatial() => SetMacUpscaleMode(global::FCRevolution.Rendering.Metal.MacUpscaleMode.Spatial);

    [RelayCommand]
    private void SetTimelineModeDisabled() => TimelineMode = TimelineMode.Disabled;

    [RelayCommand]
    private void SetTimelineModeShortRewind() => TimelineMode = TimelineMode.ShortRewindOnly;

    [RelayCommand]
    private void SetTimelineModeFull() => TimelineMode = TimelineMode.FullTimeline;

    [RelayCommand]
    private void SetShortRewindSeconds3() => UpdateShortRewindFrames(ConvertSecondsToFrames(3));

    [RelayCommand]
    private void SetShortRewindSeconds5() => UpdateShortRewindFrames(ConvertSecondsToFrames(5));

    [RelayCommand]
    private void SetShortRewindSeconds10() => UpdateShortRewindFrames(ConvertSecondsToFrames(10));

    [RelayCommand]
    private void DecreaseMaxConcurrentGameWindows()
    {
        if (MaxConcurrentGameWindows <= 1)
            return;

        MaxConcurrentGameWindows--;
        OnPropertyChanged(nameof(ActiveSessionSummary));
    }

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;

    [RelayCommand]
    private void HideBranchGalleryPreview() => ResetBranchGalleryPreviewState();

    [RelayCommand]
    private void OpenBranchGalleryFromPreview()
    {
        if (CurrentRom == null)
            return;

        ResetBranchGalleryPreviewState();
        OpenBranchGallery();
    }

    [RelayCommand]
    private void PlayRomFromMenu(RomLibraryItem? rom)
    {
        if (rom == null)
            return;

        CurrentRom = rom;
        PlaySelectedRom();
    }

    [RelayCommand]
    private async Task DeleteRomAsync(RomLibraryItem? rom)
    {
        if (rom == null)
            return;

        var owner = GetDesktopMainWindow();
        if (owner == null)
            return;

        var choice = await DeleteRomResourcesDialog.ShowAsync(owner, rom.DisplayName, _romDeleteWorkflowController.BuildResourceSummary(rom.Path));
        if (choice == DeleteRomResourcesChoice.Cancel)
            return;

        try
        {
            var workflowResult = _romDeleteWorkflowController.ExecuteConfirmedDelete(
                rom.Path,
                rom.DisplayName,
                deleteAssociatedResources: choice == DeleteRomResourcesChoice.DeleteRomWithResources);

            _romInputOverrides.Remove(rom.Path);
            if (_romPath != null && PathsEqual(_romPath, rom.Path))
            {
                _romPath = null;
                IsRomLoaded = false;
            }

            RefreshRomLibrary();
            StatusText = workflowResult.StatusText;
        }
        catch (Exception ex)
        {
            StatusText = $"删除失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void PlaySelectedRom()
    {
        if (CurrentRom == null)
            return;

        if (MaxConcurrentGameWindows <= 1 && _gameSessionService.Count > 0)
        {
            _gameSessionService.CloseAllSessions();

            StartGameSession(CurrentRom);
            return;
        }

        if (_gameSessionService.Count < MaxConcurrentGameWindows)
        {
            StartGameSession(CurrentRom);
            return;
        }

        _pendingLaunchRom = CurrentRom;
        IsReplacingGameSession = true;
        StatusText = $"已达到 {MaxConcurrentGameWindows} 个游戏窗口上限，请选择需要替换的窗口";
    }

    private void LoadRom(string path)
    {
        var mapperDescription = DescribeRomMapper(path);

        try
        {
            LoadCoreMedia(path);

            _romPath = path;
            _currentRomId = TimelineStoragePaths.ComputeRomId(path);
            _currentBranchId = TimelineStoragePaths.GetStableMainBranchId(_currentRomId);
            _timelineManifest = null;
            _currentSnapshotId = null;
            _branchTree.Clear();

            if (TimelineMode == TimelineMode.FullTimeline)
            {
                _timelineManifest = _timelineRepository.LoadOrCreate(_currentRomId, Path.GetFileNameWithoutExtension(path));
                _currentBranchId = _timelineManifest.CurrentBranchId;
                _currentSnapshotId = _timelineManifest.Branches.FirstOrDefault(branch => branch.BranchId == _currentBranchId)?.HeadSnapshotId;
                _timelineRepository.PopulateBranchTree(_branchTree, _timelineManifest, _currentRomId, path);
                _timelineManifestWriteTimeUtc = GetTimelineManifestWriteTimeUtc();
            }

            _player1InputMask = 0;
            _player2InputMask = 0;
            _activeInputRuntime.RefreshContext(isRomLoaded: false, activeRomPath: null);
            ApplyLegacyActiveInputRuntimeMirror(_activeInputWorkflowController.BuildLegacyMirror(_activeInputRuntime));
            ApplyTimelineMode();
            ReopenReplayLog(resetFile: false);
            IsRomLoaded = true;
            IsPaused = false;

            var existing = _romLibrary.FirstOrDefault(item => PathsEqual(item.Path, path));
            if (existing != null)
                CurrentRom = existing;

            SyncLoadedFlags();
            UpdateCurrentRomPresentation();
            UpdateDiscDisplayBitmap();
            RefreshActiveInputState();
            CloseSettings();
            StatusText = $"当前正在运行 {Path.GetFileName(path)}，其核心为 {mapperDescription}";
            RuntimeDiagnostics.Write("mapper", $"主窗口载入 {Path.GetFileName(path)}，其核心为 {mapperDescription}");
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {Path.GetFileName(path)}，其核心为 {mapperDescription}，{ex.Message}";
            RuntimeDiagnostics.Write("mapper", $"主窗口载入失败 {Path.GetFileName(path)}，其核心为 {mapperDescription}，{ex.Message}");
        }
    }

    private static string DescribeRomMapper(string romPath)
    {
        try
        {
            return $"mapper {RomMapperInspector.Inspect(romPath).DisplayLabel}";
        }
        catch (Exception ex)
        {
            return $"mapper 未知（{ex.Message}）";
        }
    }

    [RelayCommand]
    private void Reset()
    {
        if (!IsRomLoaded)
            return;

        ResetCoreSession();
        ApplyTimelineMode();

        IsPaused = false;
        StatusText = "已重置";
    }

    [RelayCommand]
    private void TogglePause()
    {
        if (!IsRomLoaded)
            return;

        IsPaused = !IsPaused;
        if (IsPaused)
            PauseCoreSession();
        else
            ResumeCoreSession();

        StatusText = IsPaused ? "已暂停" : "运行中";
    }

    [RelayCommand]
    private void QuickSave()
    {
        if (!IsRomLoaded)
            return;

        try
        {
            CoreStateBlob state;
            long frame;
            double timestampSeconds;
            lock (_romLock)
            {
                state = _coreSession.CaptureState();
                frame = _timeTravelService.CurrentFrame;
                timestampSeconds = _timeTravelService.CurrentTimestampSeconds;
            }

            var savePath = GetQuickSavePath();
            File.WriteAllBytes(savePath, CoreStateBlobFileCodec.Serialize(state));
            PersistQuickSaveSnapshot(frame, timestampSeconds);
            StatusText = $"存档已保存: {Path.GetFileName(savePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"存档失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void QuickLoad()
    {
        if (!IsRomLoaded || _romPath == null)
            return;

        try
        {
            var savePath = GetQuickSavePath();
            if (!File.Exists(savePath))
            {
                StatusText = "未找到存档文件";
                return;
            }

            lock (_romLock)
            {
                var state = CoreStateBlobFileCodec.Deserialize(
                    File.ReadAllBytes(savePath),
                    LegacyQuickSaveStateFormat);
                _coreSession.RestoreState(state);
            }

            SyncCurrentSnapshotFromManifest();
            StatusText = "存档已读取";
        }
        catch (Exception ex)
        {
            StatusText = $"读档失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void About()
    {
        StatusText = "FC-Revolution v0.1 | 多模式 FC ROM 库 | .NET 10 + Avalonia";
    }

    [RelayCommand]
    private async Task ExitAsync()
    {
        if (_isShuttingDown)
            return;

        LogStartup("ExitAsync begin");
        _isShuttingDown = true;
        OnPropertyChanged(nameof(IsShuttingDown));
        StartupDiagnostics.Updated -= OnStartupDiagnosticsUpdated;
        _taskMessageController.StateChanged -= RefreshTaskMessageSummary;
        _taskMessageController.Dispose();
        ResetBranchGalleryPreviewState();
        var warmupCts = Interlocked.Exchange(ref _previewWarmupCts, null);
        _previewCleanupController.ReleasePreviewRuntime(
            warmupCts,
            _romLibrary,
            ReleaseAllSmoothPlayback,
            () => _previewTimer.Stop(),
            () => CurrentPreviewBitmap = null);
        _galleryWindow?.Close();
        _emuThreadAlive = false;
        _emuThread?.Join(300);
        await _lanArcadeService.StopAsync(TimeSpan.FromMilliseconds(800));
        _replayLogWriter.Dispose();
        _backendStateMirror.Dispose();
        _lanArcadeService.Dispose();
        _coreSession.VideoFrameReady -= OnCoreVideoFrameReady;
        _coreSession.AudioReady -= OnCoreAudioReady;
        _audio.Dispose();
        _coreSession.Dispose();
        _taskMessageController.Hub.Flush();
        _gameSessionService.CloseAllSessions();
        if (Avalonia.Application.Current?.ApplicationLifetime is
            IClassicDesktopStyleApplicationLifetime dl)
        {
            dl.Shutdown();
        }

        StartupDiagnostics.Write("main-vm", "ExitAsync complete");
    }

    [RelayCommand]
    private void OpenBranchGallery()
    {
        if (TimelineMode != TimelineMode.FullTimeline)
        {
            StatusText = "当前时间线模式未启用完整分支画廊";
            return;
        }

        if (!HasBranchGalleryForCurrentRom())
        {
            StatusText = "当前游戏还没有分支存档，暂不打开分支画廊";
            return;
        }

        if (_galleryWindow == null || !_galleryWindow.IsVisible)
        {
            var vm = new BranchGalleryViewModel(
                _timeTravelService,
                _branchTree,
                CurrentRom?.Path ?? _romPath,
                _lastFrame,
                PersistBranchPoint,
                DeletePersistedBranch,
                RenamePersistedBranch,
                ActivatePersistedBranch,
                ExportBranchRangeAsync,
                PersistPreviewNode,
                DeletePersistedPreviewNode,
                RenamePersistedPreviewNode);
            LoadPersistedPreviewNodes(vm);
            _galleryWindow = new BranchGalleryWindow { DataContext = vm };
            _galleryWindow.Show();
        }
        else
        {
            if (_galleryWindow.DataContext is BranchGalleryViewModel gvm)
            {
                gvm.SetRomPath(CurrentRom?.Path ?? _romPath);
                gvm.SetLastFrame(_lastFrame ?? Array.Empty<uint>());
                LoadPersistedPreviewNodes(gvm);
                gvm.RefreshAll();
            }

            _galleryWindow.Activate();
        }
    }

    private string GetQuickSavePath()
    {
        if (_currentRomId == null)
            return "quicksave.fcs";

        TimelineStoragePaths.EnsureBranchDirectory(_currentRomId, _currentBranchId);
        return TimelineStoragePaths.GetQuickSavePath(_currentRomId, _currentBranchId);
    }

    private void PersistQuickSaveSnapshot(long frame, double timestampSeconds)
    {
        if (_timelineManifest == null || _currentRomId == null)
            return;

        var record = _timelineRepository.UpsertQuickSaveSnapshot(
            _timelineManifest,
            _currentBranchId,
            frame,
            timestampSeconds);

        _currentSnapshotId = record.SnapshotId;
        _timelineRepository.Save(_timelineManifest);
    }

    private void PersistBranchPoint(CoreBranchPoint branchPoint, Guid? parentBranchId)
    {
        if (_timelineManifest == null || _currentRomId == null)
            return;

        _timelineRepository.SaveBranchPoint(
            _timelineManifest,
            _currentRomId,
            CoreTimelineModelBridge.ToLegacyBranchPoint(branchPoint, _romPath),
            parentBranchId);
    }

    private void DeletePersistedBranch(Guid branchId)
    {
        if (_timelineManifest == null || _currentRomId == null)
            return;

        _timelineRepository.DeleteBranch(_timelineManifest, _currentRomId, branchId);
        _currentBranchId = _timelineManifest.CurrentBranchId;
        _currentSnapshotId = _timelineManifest.Branches.FirstOrDefault(branch => branch.BranchId == _currentBranchId)?.HeadSnapshotId;
        ReopenReplayLog(resetFile: false);
    }

    private void RenamePersistedBranch(CoreBranchPoint branchPoint)
    {
        if (_timelineManifest == null)
            return;

        _timelineRepository.RenameBranch(_timelineManifest, branchPoint.Id, branchPoint.Name);
    }

    private void ActivatePersistedBranch(Guid branchId)
    {
        if (_timelineManifest == null)
            return;

        _currentBranchId = branchId;
        _timelineManifest.CurrentBranchId = branchId;
        _timelineRepository.Save(_timelineManifest);
        ReopenReplayLog(resetFile: false);
    }

    private void SyncCurrentSnapshotFromManifest()
    {
        if (_timelineManifest == null)
            return;

        _timelineManifest.CurrentBranchId = _currentBranchId;
        _currentSnapshotId = _timelineRepository.GetQuickSaveSnapshot(_timelineManifest, _currentBranchId)?.SnapshotId;
        _timelineRepository.Save(_timelineManifest);
    }

    private void LoadPersistedPreviewNodes(BranchGalleryViewModel galleryViewModel)
    {
        if (_timelineManifest == null || _romPath == null)
        {
            galleryViewModel.ReplacePreviewNodes(Array.Empty<BranchPreviewNode>());
            return;
        }

        var nodes = _timelineRepository.LoadPreviewNodes(_timelineManifest)
            .Select(entry => CreatePreviewNode(entry.Record, CoreTimelineModelBridge.ToCoreTimelineSnapshot(entry.Snapshot)))
            .ToList();
        galleryViewModel.ReplacePreviewNodes(nodes);
        _timelineManifestWriteTimeUtc = GetTimelineManifestWriteTimeUtc();
    }

    private BranchPreviewNode? PersistPreviewNode(BranchCanvasNode node)
    {
        if (_timelineManifest == null || _currentRomId == null)
            return null;

        CoreTimelineSnapshot? snapshot;
        lock (_romLock)
        {
            snapshot = node.BranchPoint?.Snapshot ?? _timeTravelService.GetNearestSnapshot(node.Frame);
        }

        if (snapshot == null)
            return null;

        var previewNodeId = Guid.NewGuid();
        var record = _timelineRepository.SavePreviewNode(
            _timelineManifest,
            _currentRomId,
            node.BranchPoint?.Id ?? _currentBranchId,
            previewNodeId,
            node.Title,
            CoreTimelineModelBridge.ToLegacyFrameSnapshot(snapshot));
        _timelineManifestWriteTimeUtc = GetTimelineManifestWriteTimeUtc();

        return CreatePreviewNode(record, snapshot);
    }

    private void DeletePersistedPreviewNode(Guid previewNodeId)
    {
        if (_timelineManifest == null || _currentRomId == null)
            return;

        _timelineRepository.DeletePreviewNode(_timelineManifest, _currentRomId, previewNodeId);
        _timelineManifestWriteTimeUtc = GetTimelineManifestWriteTimeUtc();
    }

    private void RenamePersistedPreviewNode(Guid previewNodeId, string title)
    {
        if (_timelineManifest == null)
            return;

        _timelineRepository.RenamePreviewNode(_timelineManifest, previewNodeId, title);
        _timelineManifestWriteTimeUtc = GetTimelineManifestWriteTimeUtc();
    }

    private void AppendReplayFrame()
    {
        if (TimelineMode != TimelineMode.FullTimeline || !_replayLogWriter.IsOpen)
            return;

        _replayLogWriter.Append(new FrameInputRecord(_timeTravelService.CurrentFrame, _player1InputMask, _player2InputMask));
    }

    private void ReopenReplayLog(bool resetFile)
    {
        if (_currentRomId == null || TimelineMode != TimelineMode.FullTimeline)
        {
            _replayLogWriter.Close();
            return;
        }

        TimelineStoragePaths.EnsureBranchDirectory(_currentRomId, _currentBranchId);
        _replayLogWriter.Open(TimelineStoragePaths.GetInputLogPath(_currentRomId, _currentBranchId), resetFile);
    }

    private void ApplyTimelineMode()
    {
        lock (_romLock)
        {
            switch (TimelineMode)
            {
                case TimelineMode.Disabled:
                    _timeTravelService.PauseRecording();
                    break;
                case TimelineMode.ShortRewindOnly:
                    _timeTravelService.ResumeRecording();
                    _timeTravelService.SnapshotInterval = 1;
                    break;
                default:
                    _timeTravelService.ResumeRecording();
                    _timeTravelService.SnapshotInterval = 5;
                    break;
            }
        }

        if (TimelineMode != TimelineMode.FullTimeline)
        {
            _branchTree.Clear();
            _galleryWindow?.Close();
            _galleryWindow = null;
        }

        OnPropertyChanged(nameof(TimelineModeSummary));
    }

    private void UpdateShortRewindFrames(int frames, bool saveConfig = true)
    {
        var minFrames = ConvertSecondsToFrames(MinShortRewindSeconds);
        var maxFrames = ConvertSecondsToFrames(MaxShortRewindSeconds);
        var normalizedFrames = Math.Clamp(frames, minFrames, maxFrames);
        var changed = SetProperty(ref _shortRewindFrames, normalizedFrames, nameof(ShortRewindFrames));
        SyncShortRewindInputs();

        if (changed)
        {
            OnPropertyChanged(nameof(ShortRewindSeconds));
            OnPropertyChanged(nameof(ShortRewindSummary));
            OnPropertyChanged(nameof(TimelineModeSummary));

            if (saveConfig)
                SaveSystemConfig();
        }
    }

    private void SyncShortRewindInputs()
    {
        _isSyncingShortRewindInputs = true;
        try
        {
            SetProperty(ref _shortRewindSecondsInput, ShortRewindSeconds.ToString("0.00", CultureInfo.InvariantCulture), nameof(ShortRewindSecondsInput));
            SetProperty(ref _shortRewindFramesInput, _shortRewindFrames.ToString(CultureInfo.InvariantCulture), nameof(ShortRewindFramesInput));
        }
        finally
        {
            _isSyncingShortRewindInputs = false;
        }
    }

    private static int ConvertSecondsToFrames(double seconds)
    {
        var normalizedSeconds = double.IsFinite(seconds)
            ? Math.Clamp(seconds, MinShortRewindSeconds, MaxShortRewindSeconds)
            : MinShortRewindSeconds;
        return Math.Max(1, (int)Math.Round(normalizedSeconds * EmulatorFramesPerSecond, MidpointRounding.AwayFromZero));
    }

    private static double ConvertFramesToSeconds(int frames)
    {
        return Math.Round(Math.Max(1, frames) / (double)EmulatorFramesPerSecond, 2, MidpointRounding.AwayFromZero);
    }

    private static string FormatShortRewindSeconds(double seconds) =>
        seconds.ToString("0.##", CultureInfo.CurrentCulture);

    private static bool TryParseFiniteDouble(string? text, out double value)
    {
        var styles = NumberStyles.Float | NumberStyles.AllowThousands;
        if (double.TryParse(text, styles, CultureInfo.InvariantCulture, out var invariantValue) && double.IsFinite(invariantValue))
        {
            value = invariantValue;
            return true;
        }

        if (double.TryParse(text, styles, CultureInfo.CurrentCulture, out var currentValue) && double.IsFinite(currentValue))
        {
            value = currentValue;
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryParsePositiveInt(string? text, out int value)
    {
        var styles = NumberStyles.Integer;
        if (int.TryParse(text, styles, CultureInfo.InvariantCulture, out var invariantValue) && invariantValue > 0)
        {
            value = invariantValue;
            return true;
        }

        if (int.TryParse(text, styles, CultureInfo.CurrentCulture, out var currentValue) && currentValue > 0)
        {
            value = currentValue;
            return true;
        }

        value = 0;
        return false;
    }

    private static PixelEnhancementMode SanitizeLocalDisplayEnhancementMode(PixelEnhancementMode mode)
    {
        return mode is PixelEnhancementMode.SubtleSharpen or PixelEnhancementMode.SoftBlur
            ? PixelEnhancementMode.None
            : mode;
    }

    private static string GetLocalDisplayEnhancementOptionLabel(PixelEnhancementMode mode)
    {
        return SanitizeLocalDisplayEnhancementMode(mode) switch
        {
            PixelEnhancementMode.CrtScanlines => "CRT 扫描线",
            PixelEnhancementMode.VividColor => "鲜艳色彩",
            _ => "无"
        };
    }

    private static PixelEnhancementMode ParseLocalDisplayEnhancementOption(string? option)
    {
        return option switch
        {
            "CRT 扫描线" => PixelEnhancementMode.CrtScanlines,
            "鲜艳色彩" => PixelEnhancementMode.VividColor,
            _ => PixelEnhancementMode.None
        };
    }

    private static string GetMacUpscaleOutputResolutionLabel(MacUpscaleOutputResolution outputResolution)
    {
        return outputResolution switch
        {
            MacUpscaleOutputResolution.Qhd1440 => "1440p",
            MacUpscaleOutputResolution.Uhd2160 => "4K",
            _ => "1080p",
        };
    }

    private static MacUpscaleOutputResolution ParseMacUpscaleOutputResolutionOption(string? option)
    {
        return option switch
        {
            "1440p" => MacUpscaleOutputResolution.Qhd1440,
            "4K" => MacUpscaleOutputResolution.Uhd2160,
            _ => MacUpscaleOutputResolution.Hd1080,
        };
    }

    private void RewindRecentFrames(int frames)
    {
        if (!IsRomLoaded)
            return;

        if (TimelineMode == TimelineMode.Disabled)
        {
            StatusText = "当前模式已关闭时间回溯";
            return;
        }

        var allowedFrames = TimelineMode == TimelineMode.ShortRewindOnly
            ? Math.Min(frames, ShortRewindFrames)
            : frames;
        long landed;
        lock (_romLock)
        {
            landed = _timeTravelService.RewindFrames(Math.Max(1, allowedFrames));
        }

        var allowedSeconds = ConvertFramesToSeconds(allowedFrames);
        StatusText = landed < 0 ? "无可用回溯快照" : $"已回退 {allowedSeconds:0.00} 秒（{allowedFrames} 帧）至帧 {landed}";
    }

    private void UpdateInputMask(int player, NesButton button, bool pressed)
    {
        var bit = (byte)button;
        if (player == 0)
            _player1InputMask = pressed ? (byte)(_player1InputMask | bit) : (byte)(_player1InputMask & ~bit);
        else
            _player2InputMask = pressed ? (byte)(_player2InputMask | bit) : (byte)(_player2InputMask & ~bit);
    }

    private Task<string> ExportBranchRangeAsync(BranchCanvasNode startNode, long startFrame, long endFrame)
    {
        var exportPlan = MainWindowBranchExportWorkflowController.BuildPlan(
            _currentRomId,
            _romPath,
            _currentBranchId,
            startNode,
            startFrame,
            endFrame,
            frame =>
            {
                lock (_romLock)
                {
                    return _timeTravelService.GetNearestState(frame, includeThumbnail: true);
                }
            });

        return _branchExportExecutionController.ExecuteAsync(exportPlan, startFrame, endFrame);
    }

    private void SyncLoadedFlags()
    {
        foreach (var item in _allRomLibrary)
            item.IsLoaded = _gameSessionService.AnyForRomPath(item.Path) ||
                (_romPath != null && PathsEqual(item.Path, _romPath));

        OnPropertyChanged(nameof(CurrentRomActionSummary));
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private void MaybeSyncBranchGalleryState()
    {
        if (_galleryWindow?.DataContext is not BranchGalleryViewModel galleryViewModel ||
            !_galleryWindow.IsVisible ||
            _timelineManifest == null ||
            _currentRomId == null ||
            ++_branchGallerySyncTick % 15 != 0)
        {
            return;
        }

        var manifestWriteTimeUtc = GetTimelineManifestWriteTimeUtc();
        if (manifestWriteTimeUtc <= _timelineManifestWriteTimeUtc)
            return;

        _timelineManifest = _timelineRepository.LoadOrCreate(_currentRomId, Path.GetFileNameWithoutExtension(_romPath ?? CurrentRom?.Path ?? "ROM"));
        _currentBranchId = _timelineManifest.CurrentBranchId;
        _currentSnapshotId = _timelineManifest.Branches.FirstOrDefault(branch => branch.BranchId == _currentBranchId)?.HeadSnapshotId;
        _timelineRepository.PopulateBranchTree(_branchTree, _timelineManifest, _currentRomId, _romPath);
        LoadPersistedPreviewNodes(galleryViewModel);
        galleryViewModel.RefreshAll();
    }

    private DateTime GetTimelineManifestWriteTimeUtc()
    {
        if (_currentRomId == null)
            return DateTime.MinValue;

        var manifestPath = TimelineStoragePaths.GetManifestPath(_currentRomId);
        return File.Exists(manifestPath) ? File.GetLastWriteTimeUtc(manifestPath) : DateTime.MinValue;
    }

    private BranchPreviewNode CreatePreviewNode(TimelineSnapshotRecord record, CoreTimelineSnapshot snapshot)
    {
        if (_romPath == null)
            throw new InvalidOperationException("当前没有可用 ROM，无法恢复画面节点。");

        return new BranchPreviewNode
        {
            Id = record.SnapshotId,
            Frame = record.Frame,
            TimestampSeconds = record.TimestampSeconds,
            Title = record.Name ?? $"画面节点 {record.Frame}",
            Bitmap = ThumbnailItem.CreateBitmap(snapshot.Thumbnail, PreviewSourceWidth, PreviewSourceHeight),
        };
    }

    private static WriteableBitmap CreateBitmap(uint[] frameBuffer, int width, int height)
    {
        var bitmap = CreateBitmap(width, height);
        using var locked = bitmap.Lock();
        unsafe
        {
            fixed (uint* source = frameBuffer)
            {
                Buffer.MemoryCopy(
                    source,
                    (void*)locked.Address,
                    (long)locked.RowBytes * height,
                    (long)frameBuffer.Length * sizeof(uint));
            }
        }

        return bitmap;
    }

    private static WriteableBitmap CreateBitmap(int width, int height) =>
        new(
            new Avalonia.PixelSize(width, height),
            new Avalonia.Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

    private string GetRomDirectory() => AppObjectStorage.GetRomsDirectory();

    private void RebuildShelfSlots()
    {
        _shelfSlots.Clear();
        _shelfRows.Clear();
        var layout = _catalogLayoutController.BuildShelfLayout(_romLibrary, ShelfColumns);
        foreach (var slot in layout.Slots)
            _shelfSlots.Add(slot);
        foreach (var row in layout.Rows)
            _shelfRows.Add(row);

        OnPropertyChanged(nameof(ShelfSlots));
        OnPropertyChanged(nameof(ShelfRows));
        OnPropertyChanged(nameof(ShelfLayoutHeight));
    }

    private void RebuildKaleidoscopeSlots()
    {
        _kaleidoscopeSlots.Clear();
        var slots = _catalogLayoutController.BuildKaleidoscopeSlots(
            _romLibrary,
            KaleidoscopePageSize,
            KaleidoscopeCurrentPageIndex);
        foreach (var slot in slots)
            _kaleidoscopeSlots.Add(slot);
        NotifyKaleidoscopeSlotProperties();
    }

    private void RebuildKaleidoscopePages()
    {
        var normalizedIndex = _libraryNavigationController.NormalizeKaleidoscopePageIndex(
            _romLibrary.Count,
            KaleidoscopePageSize,
            KaleidoscopeCurrentPageIndex);
        if (_kaleidoscopeCurrentPageIndex != normalizedIndex)
            _kaleidoscopeCurrentPageIndex = normalizedIndex;

        _kaleidoscopePages.Clear();
        var pages = _catalogLayoutController.BuildKaleidoscopePages(
            _romLibrary.Count,
            KaleidoscopePageSize,
            KaleidoscopeCurrentPageIndex);
        foreach (var page in pages)
            _kaleidoscopePages.Add(page);

        RebuildKaleidoscopeSlots();
        OnPropertyChanged(nameof(KaleidoscopeCurrentPageDisplay));
        OnPropertyChanged(nameof(HasKaleidoscopePagination));
    }

    private void UpdateKaleidoscopePages()
    {
        var pages = _catalogLayoutController.BuildKaleidoscopePages(
            _romLibrary.Count,
            KaleidoscopePageSize,
            KaleidoscopeCurrentPageIndex);
        _kaleidoscopePages.Clear();
        foreach (var page in pages)
            _kaleidoscopePages.Add(page);
    }

    private void SetKaleidoscopePage(int pageIndex, bool preserveSelection)
    {
        if (_kaleidoscopePages.Count == 0)
            return;

        var decision = _libraryNavigationController.DecideKaleidoscopePageSelection(
            _romLibrary,
            KaleidoscopePageSize,
            pageIndex,
            CurrentRom,
            preserveSelection);
        KaleidoscopeCurrentPageIndex = decision.PageIndex;
        if (!decision.KeepCurrentSelection && decision.FallbackSelection != null)
            CurrentRom = decision.FallbackSelection;
    }

    private void SyncKaleidoscopePageWithCurrentRom()
    {
        var targetPage = _libraryNavigationController.ResolveKaleidoscopePageForCurrentRom(
            _romLibrary,
            CurrentRom,
            KaleidoscopePageSize);
        if (!targetPage.HasValue)
            return;

        if (targetPage.Value == KaleidoscopeCurrentPageIndex)
            return;

        _kaleidoscopeCurrentPageIndex = targetPage.Value;
        UpdateKaleidoscopePages();
        RebuildKaleidoscopeSlots();
        OnPropertyChanged(nameof(KaleidoscopeCurrentPageDisplay));
        OnPropertyChanged(nameof(HasKaleidoscopePagination));
    }

    private KaleidoscopeSlotItem? GetKaleidoscopeSlot(int index)
    {
        return index >= 0 && index < _kaleidoscopeSlots.Count
            ? _kaleidoscopeSlots[index]
            : null;
    }

    private void NotifyKaleidoscopeSlotProperties()
    {
        OnPropertyChanged(nameof(KaleidoscopeSlot0));
        OnPropertyChanged(nameof(KaleidoscopeSlot1));
        OnPropertyChanged(nameof(KaleidoscopeSlot2));
        OnPropertyChanged(nameof(KaleidoscopeSlot3));
        OnPropertyChanged(nameof(KaleidoscopeSlot4));
        OnPropertyChanged(nameof(KaleidoscopeSlot5));
        OnPropertyChanged(nameof(KaleidoscopeSlot6));
        OnPropertyChanged(nameof(KaleidoscopeSlot7));
    }

    private void SetSort(RomSortField field)
    {
        if (SortField == field)
            return;

        SortField = field;
        ApplySortToLibrary();
    }

    private void ApplySortToLibrary()
    {
        RebuildVisibleRomLibrary();
        UpdateLibrarySummary();
    }

    private void ApplySort(List<RomLibraryItem> items)
    {
        _catalogLayoutController.ApplySort(items, SortField, SortDescending);
    }


    private void SetGameAspectRatio(GameAspectRatioMode mode)
    {
        if (GameAspectRatioMode == mode)
            return;

        GameAspectRatioMode = mode;
        foreach (var session in _gameSessionService.Sessions)
            session.ViewModel.ApplyAspectRatio(mode);

        StatusText = $"游戏画面比例已切换为 {GameAspectRatioLabel}";
    }

    private void SetMacUpscaleMode(MacUpscaleMode mode)
    {
        if (MacUpscaleMode == mode)
            return;

        MacUpscaleMode = mode;
        StatusText = $"macOS 超分路径已切换为 {MacUpscaleModeLabel}";
    }

    private void SetMacUpscaleOutputResolution(MacUpscaleOutputResolution outputResolution)
    {
        if (MacUpscaleOutputResolution == outputResolution)
            return;

        MacUpscaleOutputResolution = outputResolution;
        StatusText = $"macOS 超分输出档位已切换为 {MacUpscaleOutputResolutionLabel}";
    }

    private static string GetLayoutDisplayName(LibraryLayoutMode mode) => mode switch
    {
        LibraryLayoutMode.BookShelf => "经典书柜",
        LibraryLayoutMode.Kaleidoscope => "万花筒",
        _ => "动态列表",
    };

    private static string GetSortFieldLabel(RomSortField field) => field switch
    {
        RomSortField.Size => "按大小",
        RomSortField.ImportedAt => "按导入时间",
        _ => "按名称",
    };

    private async Task ApplyCarouselSelectionAsync(RomLibraryItem target, int direction)
    {
        if (direction < 0)
        {
            LeftCarouselTranslateX = 154;
            LeftCarouselScale = 0.96;
        }
        else
        {
            RightCarouselTranslateX = -154;
            RightCarouselScale = 0.96;
        }

        CarouselTranslateX = 132 * direction;
        CarouselScaleX = 0.74;
        CarouselScaleY = 1.2;
        CarouselRotation = -7 * direction;
        await Task.Delay(105);
        CurrentRom = target;
        CarouselTranslateX = -52 * direction;
        CarouselScaleX = 1.12;
        CarouselScaleY = 0.9;
        CarouselRotation = 3 * direction;
        await Task.Delay(145);
        CarouselTranslateX = 0;
        CarouselScaleX = 1.0;
        CarouselScaleY = 1.0;
        CarouselRotation = 0;
        LeftCarouselTranslateX = 0;
        RightCarouselTranslateX = 0;
        LeftCarouselScale = 1.0;
        RightCarouselScale = 1.0;
        ShowBranchGalleryPreview(target);
    }

    public void PlayRomFromShelf(RomLibraryItem? item)
    {
        if (item == null)
            return;

        ResetBranchGalleryPreviewState();
        CurrentRom = item;
        PlaySelectedRom();
    }

    public void PreviewRomFromShelf(RomLibraryItem? item)
    {
        if (item == null)
            return;

        ShowBranchGalleryPreview(item);
        StatusText = $"已选中: {item.DisplayName}";
    }

    private void OpenUrlInBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    ArgumentList = { url },
                    UseShellExecute = false
                });
            }
            else if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    ArgumentList = { url },
                    UseShellExecute = false
                });
            }

            StatusText = $"已请求系统浏览器打开: {url}";
        }
        catch (Exception ex)
        {
            StatusText = $"打开浏览器失败: {ex.Message}";
        }
    }

    private void OnCoreVideoFrameReady(VideoFramePacket packet)
    {
        _lastFrame = packet.Pixels;
        _pendingFrame = packet.Pixels;
    }

    private void OnCoreAudioReady(AudioPacket packet) => _audio.PushChunk(packet.Samples);

    private void LoadCoreMedia(string path)
    {
        lock (_romLock)
        {
            var loadResult = _coreSession.LoadMedia(new CoreMediaLoadRequest(path));
            if (!loadResult.Success)
                throw new InvalidOperationException(loadResult.ErrorMessage ?? $"Failed to load ROM '{path}'.");
        }
    }

    private void ResetCoreSession()
    {
        lock (_romLock)
        {
            _coreSession.Reset();
        }
    }

    private void PauseCoreSession()
    {
        lock (_romLock)
        {
            _coreSession.Pause();
        }
    }

    private void ResumeCoreSession()
    {
        lock (_romLock)
        {
            _coreSession.Resume();
        }
    }

    private void RunCoreFrame()
    {
        lock (_romLock)
        {
            var stepResult = _coreSession.RunFrame();
            if (!stepResult.Success)
                throw new InvalidOperationException(stepResult.ErrorMessage ?? "Core frame execution failed.");
        }
    }

    private static IReadOnlyList<CoreManifest> LoadInstalledCoreManifests(
        string? resourceRootPath,
        IReadOnlyList<string>? managedCoreProbePaths)
    {
        var directorySource = new DirectoryManagedCoreModuleRegistrationSource(
            "main-window-managed-core-probe-paths",
            () => SystemConfigProfile.ResolveEffectiveManagedCoreProbeDirectories(resourceRootPath, managedCoreProbePaths));
        return new EmulatorCoreHost(
            DefaultManagedCoreModuleCatalog.CreateModules(directorySource.LoadModules()),
            DefaultEmulatorCoreHost.DefaultCoreId)
            .GetInstalledCoreManifests();
    }

    private string NormalizeConfiguredCoreId(string? coreId) =>
        NormalizeConfiguredCoreId(coreId, InstalledCoreManifests);

    private static string NormalizeConfiguredCoreId(string? coreId, IReadOnlyList<CoreManifest> installedCoreManifests)
    {
        var fallbackCoreId = ResolveInstalledFallbackCoreId(installedCoreManifests);
        if (string.IsNullOrWhiteSpace(coreId))
            return fallbackCoreId;

        var normalizedCoreId = coreId.Trim();
        return installedCoreManifests.Any(manifest => string.Equals(manifest.CoreId, normalizedCoreId, StringComparison.OrdinalIgnoreCase))
            ? normalizedCoreId
            : fallbackCoreId;
    }

    private string ResolveInstalledFallbackCoreId() =>
        ResolveInstalledFallbackCoreId(InstalledCoreManifests);

    private static string ResolveInstalledFallbackCoreId(IReadOnlyList<CoreManifest> installedCoreManifests)
    {
        if (installedCoreManifests.Any(manifest => string.Equals(
                manifest.CoreId,
                DefaultEmulatorCoreHost.DefaultCoreId,
                StringComparison.OrdinalIgnoreCase)))
        {
            return DefaultEmulatorCoreHost.DefaultCoreId;
        }

        return installedCoreManifests.FirstOrDefault()?.CoreId ?? DefaultEmulatorCoreHost.DefaultCoreId;
    }

    private IEmulatorCoreSession CreateMainCoreSession() =>
        DefaultEmulatorCoreHost.Create().CreateSession(new CoreSessionLaunchRequest(DefaultCoreId));

    private IEmulatorCoreSession CreatePreviewCoreSession() =>
        DefaultEmulatorCoreHost.Create().CreateSession(new CoreSessionLaunchRequest(DefaultCoreId));
}

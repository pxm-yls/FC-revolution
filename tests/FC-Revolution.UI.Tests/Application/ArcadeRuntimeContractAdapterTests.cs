using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FCRevolution.Backend.Hosting;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Core.Input;
using FCRevolution.Rendering.Metal;
using FCRevolution.Storage;
using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Models;
using System.Runtime.InteropServices;
using System.Threading;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class ArcadeRuntimeContractAdapterTests
{
    [Fact]
    public async Task StartAndCloseSessionAsync_DelegatesToSessionService_AndReportsStatus()
    {
        using var host = new GameWindowViewModelTestHost();
        var romName = "StartClose.nes";
        var romDisplayName = "StartClose";
        var startedSession = new ActiveGameSessionItem(
            Guid.NewGuid(),
            romDisplayName,
            host.RomPath,
            null!,
            host.ViewModel);
        var sessionService = new FakeGameSessionService
        {
            StartSessionResult = startedSession
        };
        var statuses = new List<string>();
        var adapter = CreateAdapter(
            romLibrary:
            [
                new RomLibraryItem(
                    romName,
                    host.RomPath,
                    string.Empty,
                    hasPreview: false,
                    fileSizeBytes: 1,
                    importedAtUtc: DateTime.UtcNow)
            ],
            sessionService: sessionService,
            onStatus: statuses.Add);

        var started = await AwaitWithUiDrain(adapter.StartSessionAsync(new StartSessionRequest(host.RomPath)));
        var closed = await AwaitWithUiDrain(adapter.CloseSessionAsync(startedSession.SessionId));

        Assert.NotNull(started);
        Assert.Equal(startedSession.SessionId, started!.SessionId);
        Assert.Equal(romDisplayName, sessionService.LastStartDisplayName);
        Assert.Equal(host.RomPath, sessionService.LastStartRomPath);
        Assert.True(closed);
        Assert.Equal(startedSession, sessionService.LastClosedSession);
        Assert.Contains(statuses, text => text.Contains("局域网点播已启动: StartClose", StringComparison.Ordinal));
        Assert.Contains(statuses, text => text.Contains("局域网页端已关闭游戏窗口: StartClose", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartSessionAsync_ReturnsNull_WhenRomMissing()
    {
        var sessionService = new FakeGameSessionService();
        var adapter = CreateAdapter([], sessionService, _ => { });

        var started = await AwaitWithUiDrain(adapter.StartSessionAsync(new StartSessionRequest("/tmp/missing.nes")));

        Assert.Null(started);
        Assert.False(sessionService.StartSessionCalled);
    }

    [Fact]
    public async Task CloseSessionAsync_ReturnsFalse_WhenSessionMissing()
    {
        var sessionService = new FakeGameSessionService();
        var adapter = CreateAdapter([], sessionService, _ => { });

        var closed = await AwaitWithUiDrain(adapter.CloseSessionAsync(Guid.NewGuid()));

        Assert.False(closed);
        Assert.Null(sessionService.LastClosedSession);
    }

    [Fact]
    public async Task GetRomPreviewAssetAsync_ReturnsResolvedAsset_AndNullForMissingOrUnsupported()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-arcade-adapter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            AppObjectStorage.EnsureDefaults();

            var romPath = Path.Combine(AppObjectStorage.GetRomsDirectory(), "Contra.nes");
            File.WriteAllBytes(romPath, [0x4E, 0x45, 0x53, 0x1A]);

            var mp4Path = Path.Combine(AppObjectStorage.GetPreviewVideosDirectory(), "contra-preview.mp4");
            File.WriteAllBytes(mp4Path, [0x00]);
            var txtPath = Path.Combine(AppObjectStorage.GetPreviewVideosDirectory(), "contra-preview.txt");
            File.WriteAllBytes(txtPath, [0x00]);

            var adapter = CreateAdapter(
                romLibrary:
                [
                    new RomLibraryItem("Contra.nes", romPath, mp4Path, hasPreview: true, fileSizeBytes: 1, importedAtUtc: DateTime.UtcNow),
                    new RomLibraryItem("Unsupported.nes", Path.Combine(AppObjectStorage.GetRomsDirectory(), "Unsupported.nes"), txtPath, hasPreview: true, fileSizeBytes: 1, importedAtUtc: DateTime.UtcNow),
                    new RomLibraryItem("Missing.nes", Path.Combine(AppObjectStorage.GetRomsDirectory(), "Missing.nes"), Path.Combine(AppObjectStorage.GetPreviewVideosDirectory(), "missing.mp4"), hasPreview: true, fileSizeBytes: 1, importedAtUtc: DateTime.UtcNow)
                ],
                sessionService: new FakeGameSessionService(),
                onStatus: _ => { });

            var supported = await AwaitWithUiDrain(adapter.GetRomPreviewAssetAsync(romPath));
            var unsupported = await AwaitWithUiDrain(adapter.GetRomPreviewAssetAsync(Path.Combine(AppObjectStorage.GetRomsDirectory(), "Unsupported.nes")));
            var missing = await AwaitWithUiDrain(adapter.GetRomPreviewAssetAsync(Path.Combine(AppObjectStorage.GetRomsDirectory(), "Missing.nes")));

            Assert.NotNull(supported);
            Assert.Equal(mp4Path, supported!.FilePath);
            Assert.Equal("video/mp4", supported.ContentType);
            Assert.Null(unsupported);
            Assert.Null(missing);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ClaimAndReleaseControl_DelegatesToSessionService_AndReportsStatus()
    {
        var sessionService = new FakeGameSessionService
        {
            ClaimResult = true
        };
        var statuses = new List<string>();
        var sessionId = Guid.NewGuid();
        var adapter = CreateAdapter([], sessionService, statuses.Add);

        var claimed = await AwaitWithUiDrain(adapter.ClaimControlAsync(
            sessionId,
            new ClaimControlRequest(ClientIp: "192.168.0.10", ClientName: "pad", PortId: "p2")));
        await AwaitWithUiDrain(adapter.RefreshHeartbeatAsync(
            sessionId,
            new RefreshHeartbeatRequest(PortId: "p2")));
        await AwaitWithUiDrain(adapter.ReleaseControlAsync(
            sessionId,
            new ReleaseControlRequest(Reason: "done", PortId: "p2")));

        Assert.True(claimed);
        Assert.Equal(sessionId, sessionService.LastClaimSessionId);
        Assert.Equal(1, sessionService.LastClaimPlayer);
        Assert.Equal("192.168.0.10", sessionService.LastClaimClientIp);
        Assert.Equal("pad", sessionService.LastClaimClientName);
        Assert.Equal(sessionId, sessionService.LastReleaseSessionId);
        Assert.Equal(1, sessionService.LastReleasePlayer);
        Assert.Equal("done", sessionService.LastReleaseReason);
        Assert.Equal(sessionId, sessionService.LastHeartbeatSessionId);
        Assert.Equal(1, sessionService.LastHeartbeatPlayer);
        Assert.Contains(statuses, text => text.Contains("已分配远程控制", StringComparison.Ordinal));
        Assert.Contains(statuses, text => text.Contains("已释放远程控制", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetInputStateAsync_DelegatesToGenericInputSessionService()
    {
        var sessionService = new FakeGameSessionService
        {
            SetInputResult = true
        };
        var sessionId = Guid.NewGuid();
        var adapter = CreateAdapter([], sessionService, _ => { });

        var changed = await AwaitWithUiDrain(adapter.SetInputStateAsync(
            sessionId,
            new SetInputStateRequest([new InputActionValueDto("p2", "gamepad", "fire", 1f)])));

        Assert.True(changed);
        Assert.Equal(sessionId, sessionService.LastSetInputSessionId);
        Assert.Equal("p2", sessionService.LastSetInputPortId);
        Assert.Equal("fire", sessionService.LastSetInputActionId);
        Assert.Equal(1f, sessionService.LastSetInputValue);
    }

    [Fact]
    public void GetSessionSummaries_UsesLocalControlFallback_WhenRemoteStatusEmpty()
    {
        using var host = new GameWindowViewModelTestHost();
        var session = new ActiveGameSessionItem(
            Guid.NewGuid(),
            "Test Session",
            host.RomPath,
            null!,
            host.ViewModel);
        var sessionService = new FakeGameSessionService();
        sessionService.Sessions.Add(session);
        var adapter = CreateAdapter([], sessionService, _ => { });

        var summaries = adapter.GetSessionSummaries();

        var summary = Assert.Single(summaries);
        Assert.Equal("当前本地控制", summary.ControlSummary);
    }

    [Fact]
    public void SessionQueryService_ReturnsEmptyAndNull_WhenSessionMissing()
    {
        var sessionService = new FakeGameSessionService();
        var queryService = new SessionQueryService(sessionService);

        var summaries = queryService.GetSessionSummaries();
        var preview = queryService.GetSessionPreview(Guid.NewGuid());

        Assert.Empty(summaries);
        Assert.Null(preview);
    }

    [Fact]
    public async Task GetSessionPreviewAsync_ReturnsEncodedBytes_WhenSnapshotBitmapExists()
    {
        using var host = new GameWindowViewModelTestHost();
        var session = new ActiveGameSessionItem(
            Guid.NewGuid(),
            "Preview Session",
            host.RomPath,
            null!,
            host.ViewModel)
        {
            SnapshotBitmap = CreateBitmapWithSolidPixel(4, 4, 0xFF3366CC)
        };
        var sessionService = new FakeGameSessionService();
        sessionService.Sessions.Add(session);
        var adapter = CreateAdapter([], sessionService, _ => { });

        var previewBytes = await AwaitWithUiDrain(adapter.GetSessionPreviewAsync(session.SessionId));

        Assert.NotNull(previewBytes);
    }

    [Fact]
    public async Task GetSessionPreviewAsync_ReturnsNull_WhenSessionMissingOrSnapshotMissing()
    {
        using var host = new GameWindowViewModelTestHost();
        var sessionWithoutSnapshot = new ActiveGameSessionItem(
            Guid.NewGuid(),
            "No Snapshot Session",
            host.RomPath,
            null!,
            host.ViewModel);
        var sessionService = new FakeGameSessionService();
        sessionService.Sessions.Add(sessionWithoutSnapshot);
        var adapter = CreateAdapter([], sessionService, _ => { });

        var missingSession = await AwaitWithUiDrain(adapter.GetSessionPreviewAsync(Guid.NewGuid()));
        var missingSnapshot = await AwaitWithUiDrain(adapter.GetSessionPreviewAsync(sessionWithoutSnapshot.SessionId));

        Assert.Null(missingSession);
        Assert.Null(missingSnapshot);
    }

    private static ArcadeRuntimeContractAdapter CreateAdapter(
        IReadOnlyList<RomLibraryItem> romLibrary,
        IGameSessionService sessionService,
        Action<string> onStatus)
    {
        return new ArcadeRuntimeContractAdapter(
            romLibrary,
            new Dictionary<string, Dictionary<string, Dictionary<string, Key>>>(StringComparer.OrdinalIgnoreCase),
            new System.Collections.ObjectModel.ObservableCollection<InputBindingEntry>(),
            sessionService,
            () => GameAspectRatioMode.Native,
            () => MacUpscaleMode.None,
            () => MacUpscaleOutputResolution.Hd1080,
            () => ShortcutCatalog.BuildDefaultGestureMap(),
            () => { },
            (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            onStatus);
    }

    private static async Task AwaitWithUiDrain(Task task, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(2);
        var deadline = DateTime.UtcNow + limit;

        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            AvaloniaThreadingTestHelper.DrainJobs();
            Thread.Sleep(10);
        }

        AvaloniaThreadingTestHelper.DrainJobs();
        await task.WaitAsync(limit);
    }

    private static async Task<T> AwaitWithUiDrain<T>(Task<T> task, TimeSpan? timeout = null)
    {
        await AwaitWithUiDrain((Task)task, timeout);
        return await task;
    }

    private static WriteableBitmap CreateBitmapWithSolidPixel(int width, int height, uint bgraColor)
    {
        var bitmap = new WriteableBitmap(
            new Avalonia.PixelSize(width, height),
            new Avalonia.Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using var locked = bitmap.Lock();
        var pixels = new byte[locked.RowBytes * locked.Size.Height];
        var b = (byte)(bgraColor & 0xFF);
        var g = (byte)((bgraColor >> 8) & 0xFF);
        var r = (byte)((bgraColor >> 16) & 0xFF);
        var a = (byte)((bgraColor >> 24) & 0xFF);

        for (var y = 0; y < locked.Size.Height; y++)
        {
            for (var x = 0; x < locked.Size.Width; x++)
            {
                var offset = y * locked.RowBytes + x * 4;
                pixels[offset] = b;
                pixels[offset + 1] = g;
                pixels[offset + 2] = r;
                pixels[offset + 3] = a;
            }
        }

        Marshal.Copy(pixels, 0, locked.Address, pixels.Length);
        return bitmap;
    }

    private sealed class FakeGameSessionService : IGameSessionService
    {
        public System.Collections.ObjectModel.ObservableCollection<ActiveGameSessionItem> Sessions { get; } = [];
        public int Count => Sessions.Count;
        public bool HasAny => Sessions.Count > 0;

        public bool StartSessionCalled { get; private set; }
        public bool ClaimResult { get; set; } = true;
        public Guid? LastClaimSessionId { get; private set; }
        public int LastClaimPlayer { get; private set; } = -1;
        public string? LastClaimClientIp { get; private set; }
        public string? LastClaimClientName { get; private set; }
        public Guid? LastReleaseSessionId { get; private set; }
        public int LastReleasePlayer { get; private set; } = -1;
        public string? LastReleaseReason { get; private set; }
        public Guid? LastHeartbeatSessionId { get; private set; }
        public int LastHeartbeatPlayer { get; private set; } = -1;
        public bool SetInputResult { get; set; }
        public Guid? LastSetInputSessionId { get; private set; }
        public string? LastSetInputPortId { get; private set; }
        public string? LastSetInputActionId { get; private set; }
        public float LastSetInputValue { get; private set; }
        public ActiveGameSessionItem? StartSessionResult { get; set; }
        public string? LastStartDisplayName { get; private set; }
        public string? LastStartRomPath { get; private set; }
        public ActiveGameSessionItem? LastClosedSession { get; private set; }

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
            string? coreId = null)
        {
            StartSessionCalled = true;
            LastStartDisplayName = displayName;
            LastStartRomPath = romPath;

            var session = StartSessionResult ?? throw new NotSupportedException();
            Sessions.Add(session);
            onSessionsChanged?.Invoke();
            return session;
        }

        public void CloseSession(ActiveGameSessionItem session)
        {
            LastClosedSession = session;
            Sessions.Remove(session);
        }
        public void CloseAllSessions() => Sessions.Clear();
        public ActiveGameSessionItem? FindSession(Guid sessionId) => Sessions.FirstOrDefault(s => s.SessionId == sessionId);

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
            LastSetInputSessionId = sessionId;
            LastSetInputPortId = portId;
            LastSetInputActionId = actionId;
            LastSetInputValue = value;
            return SetInputResult;
        }

        public bool IsRemoteOwner(Guid sessionId, int player, string clientIp, string? clientName = null) => false;
        public bool AnyForRomPath(string romPath) => Sessions.Any(s => string.Equals(s.RomPath, romPath, StringComparison.OrdinalIgnoreCase));
    }
}

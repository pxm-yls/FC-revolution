using Avalonia;
using Avalonia.Input;
using Avalonia.Platform;
using FCRevolution.Core.Input;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.Controls;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;
using FC_Revolution.UI.Views;
using System.Reflection;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public class MacMetalStabilityTests
{
    [Fact]
    public void MacMetalViewHost_SpatialFailure_FallsBackToNoneBeforeRaisingFailure()
    {
        FakeMacMetalPresenter? fakePresenter = null;
        using var overrideFactory = MacMetalViewHost.OverridePresenterFactoryForTests((_, upscaleMode) =>
        {
            fakePresenter = new FakeMacMetalPresenter(
                upscaleMode,
                mode => mode == MacUpscaleMode.None);
            return fakePresenter;
        });

        var host = new MacMetalViewHost();
        host.SetUpscaleMode(MacUpscaleMode.Spatial);
        bool presentationFailed = false;
        host.PresentationFailed += _ => presentationFailed = true;

        IPlatformHandle? nativeHandle = null;
        try
        {
            nativeHandle = CreateNativeControl(host);

            host.PresentFrame(CreateSolidFrame(0xFF0000FFu));

            Assert.NotNull(fakePresenter);
            Assert.False(presentationFailed);
            Assert.Equal(MacUpscaleMode.None, fakePresenter!.CurrentMode);
            Assert.Equal([MacUpscaleMode.None], fakePresenter.SetModeCalls);
        }
        finally
        {
            if (nativeHandle != null)
                DestroyNativeControl(host, nativeHandle);
        }

        Assert.NotNull(fakePresenter);
        Assert.True(fakePresenter!.IsDisposed);
    }

    [Fact]
    public void MacMetalViewHost_TemporalFailure_FallsBackToNoneBeforeRaisingFailure()
    {
        FakeMacMetalPresenter? fakePresenter = null;
        using var overrideFactory = MacMetalViewHost.OverridePresenterFactoryForTests((_, upscaleMode) =>
        {
            fakePresenter = new FakeMacMetalPresenter(
                upscaleMode,
                mode => mode == MacUpscaleMode.None);
            return fakePresenter;
        });

        var host = new MacMetalViewHost();
        host.SetUpscaleMode(MacUpscaleMode.Temporal);
        bool presentationFailed = false;
        host.PresentationFailed += _ => presentationFailed = true;

        IPlatformHandle? nativeHandle = null;
        try
        {
            nativeHandle = CreateNativeControl(host);

            host.PresentFrame(CreateSolidFrame(0xFF123456u));

            Assert.NotNull(fakePresenter);
            Assert.False(presentationFailed);
            Assert.Equal(MacUpscaleMode.None, fakePresenter!.CurrentMode);
            Assert.Equal([MacUpscaleMode.None], fakePresenter.SetModeCalls);
        }
        finally
        {
            if (nativeHandle != null)
                DestroyNativeControl(host, nativeHandle);
        }

        Assert.NotNull(fakePresenter);
        Assert.True(fakePresenter!.IsDisposed);
    }

    [Fact]
    public void MacMetalViewHost_TemporalMode_CreatesPresenterWithWindowReopenResetReason()
    {
        FakeMacMetalPresenter? fakePresenter = null;
        using var overrideFactory = MacMetalViewHost.OverridePresenterFactoryForTests((_, upscaleMode) =>
        {
            fakePresenter = new FakeMacMetalPresenter(upscaleMode, static _ => true);
            return fakePresenter;
        });

        var host = new MacMetalViewHost();
        host.SetUpscaleMode(MacUpscaleMode.Temporal);

        IPlatformHandle? nativeHandle = null;
        try
        {
            nativeHandle = CreateNativeControl(host);

            Assert.NotNull(fakePresenter);
            Assert.Contains(
                MacMetalTemporalResetReason.PresenterRecreated,
                fakePresenter!.TemporalResetRequests);
            Assert.True(fakePresenter.Diagnostics.TemporalResetApplied);
            Assert.Equal(1u, fakePresenter.Diagnostics.TemporalResetCount);
            Assert.Equal(
                MacMetalTemporalResetReason.PresenterRecreated,
                fakePresenter.Diagnostics.TemporalResetReason);
        }
        finally
        {
            if (nativeHandle != null)
                DestroyNativeControl(host, nativeHandle);
        }
    }

    [Fact]
    public void MacMetalViewHost_TotalFailure_RaisesPresentationFailed()
    {
        FakeMacMetalPresenter? fakePresenter = null;
        using var overrideFactory = MacMetalViewHost.OverridePresenterFactoryForTests((_, upscaleMode) =>
        {
            fakePresenter = new FakeMacMetalPresenter(
                upscaleMode,
                static _ => false);
            return fakePresenter;
        });

        var host = new MacMetalViewHost();
        host.SetUpscaleMode(MacUpscaleMode.Spatial);
        string? failureMessage = null;
        host.PresentationFailed += message => failureMessage = message;

        IPlatformHandle? nativeHandle = null;
        try
        {
            nativeHandle = CreateNativeControl(host);

            host.PresentFrame(CreateSolidFrame(0xFF00FF00u));

            Assert.NotNull(fakePresenter);
            Assert.Equal("macOS Metal presenter 失败，已回退到软件显示路径。", failureMessage);
            Assert.Equal([MacUpscaleMode.None], fakePresenter!.SetModeCalls);
        }
        finally
        {
            if (nativeHandle != null)
                DestroyNativeControl(host, nativeHandle);
        }
    }

    [Fact]
    public void MacMetalViewHost_BoundsChange_UpdatesPresenterDisplaySize()
    {
        FakeMacMetalPresenter? fakePresenter = null;
        using var overrideFactory = MacMetalViewHost.OverridePresenterFactoryForTests((_, upscaleMode) =>
        {
            fakePresenter = new FakeMacMetalPresenter(upscaleMode, static _ => true);
            return fakePresenter;
        });

        var host = new MacMetalViewHost();
        host.Arrange(new Rect(0, 0, 320, 240));

        IPlatformHandle? nativeHandle = null;
        try
        {
            nativeHandle = CreateNativeControl(host);

            Assert.NotNull(fakePresenter);
            Assert.Contains(fakePresenter!.DisplaySizeCalls, entry => entry == (320d, 240d));

            host.SetDisplaySize(480, 360);

            Assert.Equal((480d, 360d), fakePresenter.DisplaySizeCalls[^1]);
        }
        finally
        {
            if (nativeHandle != null)
                DestroyNativeControl(host, nativeHandle);
        }
    }

    [Fact]
    public void MacMetalViewHost_OutputResolutionChange_UpdatesPresenter()
    {
        FakeMacMetalPresenter? fakePresenter = null;
        using var overrideFactory = MacMetalViewHost.OverridePresenterFactoryForTests((_, upscaleMode) =>
        {
            fakePresenter = new FakeMacMetalPresenter(upscaleMode, static _ => true);
            return fakePresenter;
        });

        var host = new MacMetalViewHost();

        IPlatformHandle? nativeHandle = null;
        try
        {
            nativeHandle = CreateNativeControl(host);

            host.SetUpscaleOutputResolution(MacUpscaleOutputResolution.Uhd2160);

            Assert.NotNull(fakePresenter);
            Assert.Equal(MacUpscaleOutputResolution.Uhd2160, fakePresenter!.OutputResolutionCalls[^1]);
        }
        finally
        {
            if (nativeHandle != null)
                DestroyNativeControl(host, nativeHandle);
        }
    }

    [Fact]
    public void MacMetalViewHost_CornerRadiusChange_UpdatesPresenter()
    {
        FakeMacMetalPresenter? fakePresenter = null;
        using var overrideFactory = MacMetalViewHost.OverridePresenterFactoryForTests((_, upscaleMode) =>
        {
            fakePresenter = new FakeMacMetalPresenter(upscaleMode, static _ => true);
            return fakePresenter;
        });

        var host = new MacMetalViewHost();
        host.SetCornerRadius(8);

        IPlatformHandle? nativeHandle = null;
        try
        {
            nativeHandle = CreateNativeControl(host);

            Assert.NotNull(fakePresenter);
            Assert.Equal(8d, fakePresenter!.CornerRadiusCalls[^1]);

            host.SetCornerRadius(10);

            Assert.Equal(10d, fakePresenter.CornerRadiusCalls[^1]);
        }
        finally
        {
            if (nativeHandle != null)
                DestroyNativeControl(host, nativeHandle);
        }
    }

    [Fact]
    public void GameWindow_WorkingAreaConstraint_ClampsRequestedSizeAndMinimums()
    {
        var constrained = GameWindow.ConstrainWindowSizeToWorkingArea(
            requestedWidth: 960,
            requestedHeight: 700,
            minWidth: 520,
            minHeight: 380,
            workingArea: new PixelRect(0, 0, 800, 600));

        Assert.Equal(752, constrained.Width);
        Assert.Equal(552, constrained.Height);
        Assert.Equal(520, constrained.MinWidth);
        Assert.Equal(380, constrained.MinHeight);
    }

    [Fact]
    public void GameWindow_WorkingAreaConstraint_PreservesRequestedSizeWhenAlreadyWithinBounds()
    {
        var constrained = GameWindow.ConstrainWindowSizeToWorkingArea(
            requestedWidth: 640,
            requestedHeight: 480,
            minWidth: 520,
            minHeight: 380,
            workingArea: new PixelRect(0, 0, 1200, 900));

        Assert.Equal(640, constrained.Width);
        Assert.Equal(480, constrained.Height);
        Assert.Equal(520, constrained.MinWidth);
        Assert.Equal(380, constrained.MinHeight);
    }

    [Fact]
    public void GameWindow_WorkingAreaConstraint_ReducesMinimumsWhenWorkingAreaIsTiny()
    {
        var constrained = GameWindow.ConstrainWindowSizeToWorkingArea(
            requestedWidth: 960,
            requestedHeight: 700,
            minWidth: 520,
            minHeight: 380,
            workingArea: new PixelRect(0, 0, 400, 300));

        Assert.Equal(376, constrained.Width);
        Assert.Equal(276, constrained.Height);
        Assert.Equal(376, constrained.MinWidth);
        Assert.Equal(276, constrained.MinHeight);
    }

    [Fact]
    public void GameWindow_ViewportAvailableSize_ReservesOverlaySpaceInsteadOfClipping()
    {
        var available = GameWindow.CalculateViewportAvailableSize(
            layoutWidth: 780,
            layoutHeight: 520,
            isOverlayVisible: true,
            overlayTopHeight: 82,
            overlayTopMargin: new Thickness(12, 12, 12, 6),
            overlayBottomHeight: 34,
            overlayBottomMargin: new Thickness(12, 6, 12, 12));

        Assert.Equal(780, available.AvailableWidth);
        Assert.Equal(368, available.AvailableHeight);
    }

    [Fact]
    public void GameWindow_ViewportAvailableSize_IgnoresOverlayReserveWhenOverlayHidden()
    {
        var available = GameWindow.CalculateViewportAvailableSize(
            layoutWidth: 780,
            layoutHeight: 520,
            isOverlayVisible: false,
            overlayTopHeight: 82,
            overlayTopMargin: new Thickness(12, 12, 12, 6),
            overlayBottomHeight: 34,
            overlayBottomMargin: new Thickness(12, 6, 12, 12));

        Assert.Equal(780, available.AvailableWidth);
        Assert.Equal(520, available.AvailableHeight);
    }

    [Fact]
    public void GameWindow_ViewportFrameMargin_OffsetsFrameIntoOverlaySafeBand()
    {
        var margin = GameWindow.CalculateViewportFrameMargin(
            overlayTopReserve: 100,
            overlayBottomReserve: 40);

        Assert.Equal(new Thickness(0, 100, 0, 40), margin);
    }

    [Fact]
    public void GameWindow_ViewportFrameMargin_ClampsNegativeOverlayReserve()
    {
        var margin = GameWindow.CalculateViewportFrameMargin(
            overlayTopReserve: -12,
            overlayBottomReserve: 18);

        Assert.Equal(new Thickness(0, 0, 0, 18), margin);
    }

    [Fact]
    public void GameWindow_ViewportSurfaceSize_UsesVisibleInnerGap()
    {
        var surface = GameWindow.CalculateViewportSurfaceSize(
            frameWidth: 800,
            frameHeight: 600);

        Assert.Equal(784, surface.Width);
        Assert.Equal(584, surface.Height);
    }

    [Fact]
    public void GameWindow_CanSwitchUpscaleModesAcrossCloseAndReopen()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        using var availability = MacMetalViewHost.OverrideAvailabilityForTests(isSupported: true);
        using var overrideFactory = MacMetalViewHost.OverridePresenterFactoryForTests((_, upscaleMode) =>
            new FakeMacMetalPresenter(upscaleMode, static _ => true));

        var romPath = CreateTestRomFile();
        GameWindowViewModel? firstViewModel = null;
        GameWindowViewModel? secondViewModel = null;
        GameWindow? firstWindow = null;
        GameWindow? secondWindow = null;

        try
        {
            var ex = Record.Exception(() =>
            {
                firstViewModel = CreateViewModel(romPath);
                firstWindow = new GameWindow { DataContext = firstViewModel };
                firstWindow.Show();
                DrainUi();

                firstViewModel.ApplyUpscaleMode(MacUpscaleMode.Spatial);
                DrainUi();
                firstViewModel.ApplyUpscaleMode(MacUpscaleMode.None);
                DrainUi();

                firstWindow.Close();
                DrainUi();

                secondViewModel = CreateViewModel(romPath, MacUpscaleMode.Spatial);
                secondWindow = new GameWindow { DataContext = secondViewModel };
                secondWindow.Show();
                DrainUi();

                secondViewModel.ApplyUpscaleMode(MacUpscaleMode.None);
                DrainUi();

                secondWindow.Close();
                DrainUi();
            });

            Assert.Null(ex);
        }
        finally
        {
            secondWindow?.Close();
            firstWindow?.Close();
            DrainUi();
            secondViewModel?.Dispose();
            firstViewModel?.Dispose();
            DeleteIfExists(romPath);
        }
    }

    [Fact]
    public void GameWindow_ReloadAndQuickLoad_CanStillSwitchUpscaleModes()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        using var availability = MacMetalViewHost.OverrideAvailabilityForTests(isSupported: true);
        using var overrideFactory = MacMetalViewHost.OverridePresenterFactoryForTests((_, upscaleMode) =>
            new FakeMacMetalPresenter(upscaleMode, static _ => true));

        var romPath = CreateTestRomFile();
        var quickSavePath = Path.ChangeExtension(romPath, ".fcs");
        GameWindowViewModel? viewModel = null;
        GameWindow? window = null;

        try
        {
            var ex = Record.Exception(() =>
            {
                viewModel = CreateViewModel(romPath);
                window = new GameWindow { DataContext = viewModel };
                window.Show();
                WaitFor(() => GetMacMetalViewHost(window) != null, TimeSpan.FromSeconds(1));

                viewModel.OnKeyDown(Key.F2);
                WaitFor(() => File.Exists(quickSavePath), TimeSpan.FromSeconds(1));
                viewModel.OnKeyDown(Key.F3);
                WaitFor(
                    () => viewModel.TemporalHistoryResetReason == MacMetalTemporalResetReason.SaveStateLoaded &&
                        viewModel.ViewportRenderDiagnostics.Contains("快速读档", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(1));

                InvokeLoadRom(viewModel, romPath);
                WaitFor(
                    () => viewModel.TemporalHistoryResetReason == MacMetalTemporalResetReason.RomLoaded &&
                        viewModel.ViewportRenderDiagnostics.Contains("ROM 载入", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(1));

                viewModel.ApplyUpscaleMode(MacUpscaleMode.Spatial);
                WaitFor(
                    () => viewModel.TemporalHistoryResetReason == MacMetalTemporalResetReason.UpscaleModeChanged &&
                        viewModel.ViewportRenderDiagnostics.Contains("超分模式切换", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(1));

                viewModel.ApplyUpscaleMode(MacUpscaleMode.None);
                WaitFor(
                    () => viewModel.TemporalHistoryResetReason == MacMetalTemporalResetReason.UpscaleModeChanged &&
                        viewModel.ViewportRenderDiagnostics.Contains("超分模式切换", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(1));
            });

            Assert.Null(ex);
            Assert.True(File.Exists(quickSavePath));
            Assert.Contains("运行中:", viewModel!.StatusText);
            Assert.True(viewModel.TemporalHistoryResetVersion >= 3);
            Assert.Equal(MacMetalTemporalResetReason.UpscaleModeChanged, viewModel.TemporalHistoryResetReason);
        }
        finally
        {
            window?.Close();
            DrainUi();
            viewModel?.Dispose();
            DeleteIfExists(quickSavePath);
            DeleteIfExists(romPath);
        }
    }

    [Fact]
    public void GameWindow_MetalPresentationFailure_ReleasesHostAndReturnsToSoftwareRenderer()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        using var availability = MacMetalViewHost.OverrideAvailabilityForTests(isSupported: true);
        using var overrideFactory = MacMetalViewHost.OverridePresenterFactoryForTests((_, upscaleMode) =>
            new FakeMacMetalPresenter(upscaleMode, static _ => true));

        var romPath = CreateTestRomFile();
        var viewModel = CreateViewModel(romPath, MacUpscaleMode.Spatial);
        var window = new GameWindow { DataContext = viewModel };

        try
        {
            window.Show();
            WaitFor(() => GetMacMetalViewHost(window) != null, TimeSpan.FromSeconds(1));

            InvokePresentationFailed(window, "macOS Metal presenter 失败，已回退到软件显示路径。");
            DrainUi();

            Assert.Null(GetMacMetalViewHost(window));
            Assert.Equal("软件 / Spatial / 1080p", viewModel.ViewportRendererLabel);
            Assert.Contains("已回退到软件显示路径", viewModel.ViewportRenderDiagnostics);
        }
        finally
        {
            window.Close();
            DrainUi();
            viewModel.Dispose();
            DeleteIfExists(romPath);
        }
    }

    [Fact]
    public void GameWindow_MetalPresentationFailure_PreservesTemporalRequestInSoftwareDiagnostics()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        using var availability = MacMetalViewHost.OverrideAvailabilityForTests(isSupported: true);
        using var overrideFactory = MacMetalViewHost.OverridePresenterFactoryForTests((_, upscaleMode) =>
            new FakeMacMetalPresenter(upscaleMode, static _ => true));

        var romPath = CreateTestRomFile();
        var viewModel = CreateViewModel(romPath, MacUpscaleMode.Temporal);
        var window = new GameWindow { DataContext = viewModel };

        try
        {
            window.Show();
            WaitFor(() => GetMacMetalViewHost(window) != null, TimeSpan.FromSeconds(1));

            InvokePresentationFailed(window, "macOS Metal presenter 失败，已回退到软件显示路径。");
            DrainUi();

            Assert.Null(GetMacMetalViewHost(window));
            Assert.Equal("软件 / Temporal / 1080p", viewModel.ViewportRendererLabel);
            Assert.Contains("已回退到软件显示路径", viewModel.ViewportRenderDiagnostics);
        }
        finally
        {
            window.Close();
            DrainUi();
            viewModel.Dispose();
            DeleteIfExists(romPath);
        }
    }

    private static IPlatformHandle CreateNativeControl(MacMetalViewHost host)
    {
        var method = typeof(MacMetalViewHost).GetMethod("CreateNativeControlCore", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<IPlatformHandle>(method!.Invoke(host, [new PlatformHandle(new IntPtr(1), "NSView")]));
    }

    private static void DestroyNativeControl(MacMetalViewHost host, IPlatformHandle handle)
    {
        var method = typeof(MacMetalViewHost).GetMethod("DestroyNativeControlCore", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(host, [handle]);
    }

    private static void DrainUi() => AvaloniaThreadingTestHelper.DrainJobs();

    private static GameWindowViewModel CreateViewModel(string romPath, MacUpscaleMode upscaleMode = MacUpscaleMode.None)
        => new(
            "Test Game",
            romPath,
            GameAspectRatioMode.Native,
            NesInputTestAdapter.BuildBindingsByPort(
                new Dictionary<int, Dictionary<NesButton, Key>>
                {
                    [0] = new Dictionary<NesButton, Key>
                    {
                        [NesButton.A] = Key.Z,
                        [NesButton.B] = Key.X,
                        [NesButton.Start] = Key.Enter,
                        [NesButton.Select] = Key.RightShift,
                        [NesButton.Up] = Key.Up,
                        [NesButton.Down] = Key.Down,
                        [NesButton.Left] = Key.Left,
                        [NesButton.Right] = Key.Right,
                    },
                    [1] = new Dictionary<NesButton, Key>
                    {
                        [NesButton.A] = Key.U,
                        [NesButton.B] = Key.O,
                        [NesButton.Start] = Key.RightCtrl,
                        [NesButton.Select] = Key.Space,
                        [NesButton.Up] = Key.I,
                        [NesButton.Down] = Key.K,
                        [NesButton.Left] = Key.J,
                        [NesButton.Right] = Key.L,
                    }
                }),
            upscaleMode: upscaleMode);

    private static void InvokeLoadRom(GameWindowViewModel vm, string romPath)
    {
        var method = typeof(GameWindowViewModel).GetMethod("LoadRom", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(vm, [romPath]);
    }

    private static MacMetalViewHost? GetMacMetalViewHost(GameWindow window)
    {
        var field = typeof(GameWindow).GetField("_macMetalViewHost", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (MacMetalViewHost?)field!.GetValue(window);
    }

    private static void InvokePresentationFailed(GameWindow window, string message)
    {
        var method = typeof(GameWindow).GetMethod("OnMacMetalPresentationFailed", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(window, [message]);
    }

    private static uint[] CreateSolidFrame(uint pixel)
    {
        var frame = new uint[256 * 240];
        Array.Fill(frame, pixel);
        return frame;
    }

    private static string CreateTestRomFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mac-metal-stability-rom-{Guid.NewGuid():N}.nes");
        File.WriteAllBytes(path, CreateMinimalTestRom());
        return path;
    }

    private static byte[] CreateMinimalTestRom()
    {
        var rom = new byte[16 + 16384 + 8192];
        rom[0] = (byte)'N';
        rom[1] = (byte)'E';
        rom[2] = (byte)'S';
        rom[3] = 0x1A;
        rom[4] = 1;
        rom[5] = 1;

        var prgStart = 16;
        rom[prgStart + 0x0000] = 0xEA;
        rom[prgStart + 0x0001] = 0x4C;
        rom[prgStart + 0x0002] = 0x00;
        rom[prgStart + 0x0003] = 0x80;

        rom[prgStart + 0x3FFA] = 0x00;
        rom[prgStart + 0x3FFB] = 0x80;
        rom[prgStart + 0x3FFC] = 0x00;
        rom[prgStart + 0x3FFD] = 0x80;
        rom[prgStart + 0x3FFE] = 0x00;
        rom[prgStart + 0x3FFF] = 0x80;

        return rom;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void WaitFor(Func<bool> predicate, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            DrainUi();
            if (predicate())
                return;

            Thread.Sleep(10);
        }

        DrainUi();
        Assert.True(predicate());
    }

    private sealed class FakeMacMetalPresenter : IMacMetalPresenterHost
    {
        private readonly Func<MacUpscaleMode, bool> _presentResult;

        public FakeMacMetalPresenter(MacUpscaleMode initialMode, Func<MacUpscaleMode, bool> presentResult)
        {
            CurrentMode = initialMode;
            _presentResult = presentResult;
            Diagnostics = CreateDiagnostics(initialMode, MacMetalFallbackReason.None);
        }

        public IntPtr ViewHandle { get; } = new(0x1234);

        public MacMetalPresenterDiagnostics Diagnostics { get; private set; }

        public MacUpscaleMode CurrentMode { get; private set; }

        public List<MacUpscaleMode> SetModeCalls { get; } = [];

        public List<MacUpscaleOutputResolution> OutputResolutionCalls { get; } = [];

        public List<(double Width, double Height)> DisplaySizeCalls { get; } = [];

        public List<double> CornerRadiusCalls { get; } = [];

        public List<MacMetalTemporalResetReason> TemporalResetRequests { get; } = [];

        public bool IsDisposed { get; private set; }

        public bool PresentFrame(ReadOnlySpan<uint> frameBuffer) => Present();

        public bool PresentFrame(LayeredFrameData frameData) => Present();

        public void SetDisplaySize(double width, double height)
        {
            DisplaySizeCalls.Add((width, height));
            Diagnostics = Diagnostics with
            {
                TargetWidthPoints = width,
                TargetHeightPoints = height,
                OutputWidth = (uint)Math.Max(1, Math.Round(width)),
                OutputHeight = (uint)Math.Max(1, Math.Round(height))
            };
        }

        public void SetCornerRadius(double radius)
        {
            CornerRadiusCalls.Add(radius);
        }

        public void SetUpscaleOutputResolution(MacUpscaleOutputResolution outputResolution)
        {
            OutputResolutionCalls.Add(outputResolution);
        }

        public void SetUpscaleMode(MacUpscaleMode upscaleMode)
        {
            CurrentMode = upscaleMode;
            SetModeCalls.Add(upscaleMode);
            Diagnostics = CreateDiagnostics(upscaleMode, MacMetalFallbackReason.None);
        }

        public void RequestTemporalHistoryReset(MacMetalTemporalResetReason reason)
        {
            if (reason == MacMetalTemporalResetReason.None)
                return;

            TemporalResetRequests.Add(reason);
            Diagnostics = Diagnostics with
            {
                TemporalResetPending = false,
                TemporalResetApplied = true,
                TemporalResetCount = Diagnostics.TemporalResetCount + 1,
                TemporalResetReason = reason
            };
        }

        public void Dispose()
        {
            IsDisposed = true;
        }

        private bool Present()
        {
            var succeeded = _presentResult(CurrentMode);
            Diagnostics = CreateDiagnostics(
                CurrentMode,
                succeeded ? MacMetalFallbackReason.None : MacMetalFallbackReason.RuntimeCommandFailure);
            return succeeded;
        }

        private static MacMetalPresenterDiagnostics CreateDiagnostics(MacUpscaleMode requestedMode, MacMetalFallbackReason fallbackReason)
        {
            var effectiveMode = fallbackReason == MacMetalFallbackReason.None
                ? requestedMode
                : MacUpscaleMode.None;

            return new MacMetalPresenterDiagnostics(
                RequestedUpscaleMode: requestedMode,
                EffectiveUpscaleMode: effectiveMode,
                FallbackReason: fallbackReason,
                InternalWidth: 256,
                InternalHeight: 240,
                OutputWidth: 512,
                OutputHeight: 480,
                DrawableWidth: 512,
                DrawableHeight: 480,
                TargetWidthPoints: 512,
                TargetHeightPoints: 480,
                DisplayScale: 1,
                HostWidthPoints: 512,
                HostHeightPoints: 480,
                LayerWidthPoints: 512,
                LayerHeightPoints: 480,
                TemporalResetPending: false,
                TemporalResetApplied: false,
                TemporalResetCount: 0,
                TemporalResetReason: MacMetalTemporalResetReason.None);
        }
    }
}

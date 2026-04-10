using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using FCRevolution.Backend.Hosting;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Emulation.Host;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;
using FC_Revolution.UI.Views;

namespace FC_Revolution.UI.Infrastructure;

public sealed class GameSessionRegistry
{
    private readonly ObservableCollection<ActiveGameSessionItem> _sessions = new();

    public ObservableCollection<ActiveGameSessionItem> Sessions => _sessions;

    public int Count => _sessions.Count;

    public bool HasAny => _sessions.Count > 0;

    public ActiveGameSessionItem StartSessionWithInputBindings(
        string displayName,
        string romPath,
        GameAspectRatioMode aspectRatioMode,
        IReadOnlyDictionary<string, Dictionary<string, Avalonia.Input.Key>> inputBindingsByPort,
        IReadOnlyList<ExtraInputBindingProfile>? extraInputBindings = null,
        Action? onSessionsChanged = null,
        MacUpscaleMode upscaleMode = MacUpscaleMode.None,
        MacUpscaleOutputResolution upscaleOutputResolution = MacUpscaleOutputResolution.Hd1080,
        PixelEnhancementMode enhancementMode = PixelEnhancementMode.None,
        double volume = 15.0,
        IReadOnlyDictionary<string, ShortcutGesture>? shortcutBindings = null,
        string? coreId = null)
    {
        StartupDiagnostics.Write("game-session", $"StartSession begin: {displayName} | rom={romPath}");
        GeometryDiagnostics.Write(
            "game-session",
            $"StartSession begin: {displayName} | rom={romPath} | aspect={aspectRatioMode} | upscale={upscaleMode} | output={upscaleOutputResolution}");

        IEmulatorCoreSession? coreSession = null;
        GameWindowViewModel? vm = null;
        GameWindow? window = null;
        ActiveGameSessionItem? session = null;

        try
        {
            var profile = SystemConfigProfile.Load();
            var runtimeOptions = new ManagedCoreRuntimeOptions(
                ResourceRootPath: profile.ResourceRootPath,
                ProbeDirectories: profile.GetEffectiveManagedCoreProbeDirectories());
            if (!ManagedCoreRuntime.TryCreateSession(
                    new CoreSessionLaunchRequest(coreId),
                    out coreSession,
                    defaultCoreId: coreId,
                    options: runtimeOptions) ||
                coreSession is null)
            {
                throw new InvalidOperationException("当前没有可用核心，请先安装、导入或启用模拟器核心。");
            }

            vm = CreateViewModel(
                displayName,
                romPath,
                aspectRatioMode,
                inputBindingsByPort,
                extraInputBindings,
                upscaleMode,
                upscaleOutputResolution,
                enhancementMode,
                volume,
                coreSession);
            var viewModelOwnsInjectedCoreSession = ReferenceEquals(vm.CoreSession, coreSession);
            if (shortcutBindings != null)
                vm.ApplyShortcutBindings(shortcutBindings);

            // 根据画面比例调整窗口初始宽度，使游戏内容两侧留白与 4:3 基准（~75px）保持一致。
            // 高度固定 700，宽度 = 游戏内容宽 + 固定水平开销 44px + 两侧边距 150px。
            // 游戏内容宽 = 可用高度 574px × 宽高比，水平开销 = 外边距 24 + Viewbox边距 20。
            var (windowWidth, windowHeight) = aspectRatioMode switch
            {
                GameAspectRatioMode.Native      => (806,  700),   // 256:240 ≈ 1.067 → 574×1.067+194 ≈ 806
                GameAspectRatioMode.Aspect8By7  => (850,  700),   // 256:224 ≈ 1.143 → 574×1.143+194 ≈ 850
                GameAspectRatioMode.Aspect16By9 => (960,  700),   // 16:9 受宽度限制，保持 960（上下边距较小但可接受）
                _                               => (960,  700),   // 4:3 基准，保持不变
            };
            window = new GameWindow
            {
                DataContext = vm,
                Title = $"FC-Revolution - {displayName}",
                Width = windowWidth,
                Height = windowHeight,
            };

            session = new ActiveGameSessionItem(
                Guid.NewGuid(),
                displayName,
                romPath,
                coreSession.RuntimeInfo.CoreId,
                coreSession.RuntimeInfo.DisplayName,
                coreSession,
                window,
                vm);
            session.SnapshotBitmap = vm.ScreenBitmap;
            void OnScreenBitmapUpdated(WriteableBitmap? bitmap) => session.SnapshotBitmap = bitmap;
            vm.ScreenBitmapUpdated += OnScreenBitmapUpdated;

            window.Closed += (_, _) =>
            {
                vm.ScreenBitmapUpdated -= OnScreenBitmapUpdated;
                vm.Dispose();
                if (!viewModelOwnsInjectedCoreSession)
                    session.DisposeCoreSession();
                _sessions.Remove(session);
                onSessionsChanged?.Invoke();
            };

            _sessions.Add(session);
            onSessionsChanged?.Invoke();
            ShowWindow(window);
            RequestForegroundActivation(window, "initial");
            StartupDiagnostics.Write("game-session", $"StartSession success: {displayName}");
            GeometryDiagnostics.Write(
                "game-session",
                $"StartSession success: {displayName} | window={window.Width:0.##}x{window.Height:0.##} | upscale={upscaleMode} | output={upscaleOutputResolution}");
            RuntimeDiagnostics.Write("session", $"游戏窗口已显示: {displayName}");
            return session;
        }
        catch (Exception ex)
        {
            StartupDiagnostics.WriteException("game-session", $"StartSession failed: {displayName}", ex);
            GeometryDiagnostics.WriteException("game-session", $"StartSession failed: {displayName}", ex);
            RuntimeDiagnostics.Write("session", $"游戏窗口启动失败 {displayName}: {ex}");

            if (session != null)
            {
                _sessions.Remove(session);
                onSessionsChanged?.Invoke();
            }

            if (window != null)
                window.Close();

            vm?.Dispose();
            coreSession?.Dispose();
            throw;
        }
    }

    public void CloseSession(ActiveGameSessionItem session)
    {
        if (!_sessions.Contains(session))
            return;

        session.Window.Close();
    }

    public void CloseAllSessions()
    {
        foreach (var session in _sessions.ToList())
            CloseSession(session);
    }

    public ActiveGameSessionItem? FindSession(Guid sessionId) =>
        _sessions.FirstOrDefault(session => session.SessionId == sessionId);

    public bool TryAcquireRemoteControl(Guid sessionId, int player, string clientIp, string? clientName = null)
    {
        var session = FindSession(sessionId);
        return session != null && session.ViewModel.AcquireRemoteControl(player, clientIp, clientName);
    }

    public void ReleaseRemoteControl(Guid sessionId, int player, string? reason = null)
    {
        var session = FindSession(sessionId);
        session?.ViewModel.ReleaseRemoteControl(player, reason);
    }

    public void RefreshRemoteHeartbeat(Guid sessionId, int player)
    {
        var session = FindSession(sessionId);
        session?.ViewModel.RefreshRemoteHeartbeat(player);
    }

    public bool TrySetRemoteInputState(Guid sessionId, string portId, string actionId, float value, string? clientIp = null, string? clientName = null)
    {
        var session = FindSession(sessionId);
        return session != null && session.ViewModel.SetRemoteInputState(portId, actionId, value, clientIp, clientName);
    }

    public bool IsRemoteOwner(Guid sessionId, int player, string clientIp, string? clientName = null)
    {
        var session = FindSession(sessionId);
        return session != null && session.ViewModel.IsRemoteOwner(player, clientIp, clientName);
    }

    public bool AnyForRomPath(string romPath) =>
        _sessions.Any(session => string.Equals(session.RomPath, romPath, StringComparison.OrdinalIgnoreCase));

    internal static Window? ResolveDesktopMainWindow(IApplicationLifetime? lifetime, Window? childWindow = null)
    {
        if (lifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            return null;

        return GetUsableOwner(mainWindow, childWindow);
    }

    internal static Window? GetUsableOwner(Window? owner, Window? childWindow = null)
    {
        if (owner == null || ReferenceEquals(owner, childWindow))
            return null;

        return owner;
    }

    internal static void ShowWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        StartupDiagnostics.Write("game-session", "showing GameWindow as independent top-level window");
        window.Show();
    }

    internal static void RequestForegroundActivation(Window window, string stage)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Activate();
        window.Focus();
        StartupDiagnostics.Write(
            "game-session",
            $"foreground request {stage} | visible={window.IsVisible} | state={window.WindowState} | bounds={window.Bounds.Width:0.##}x{window.Bounds.Height:0.##}");

        if (!string.Equals(stage, "initial", StringComparison.Ordinal))
            return;

        Dispatcher.UIThread.Post(
            () => RequestForegroundActivation(window, "loaded"),
            DispatcherPriority.Loaded);
        Dispatcher.UIThread.Post(
            () => RequestForegroundActivation(window, "background"),
            DispatcherPriority.Background);
    }

    private static GameWindowViewModel CreateViewModel(
        string displayName,
        string romPath,
        GameAspectRatioMode aspectRatioMode,
        IReadOnlyDictionary<string, Dictionary<string, Avalonia.Input.Key>> inputBindingsByPort,
        IReadOnlyList<ExtraInputBindingProfile>? extraInputBindings,
        MacUpscaleMode upscaleMode,
        MacUpscaleOutputResolution upscaleOutputResolution,
        PixelEnhancementMode enhancementMode,
        double volume,
        IEmulatorCoreSession coreSession)
    {
        var viewModelType = typeof(GameWindowViewModel);
        var constructor = viewModelType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(candidate =>
                candidate.GetParameters().Any(parameter => typeof(IEmulatorCoreSession).IsAssignableFrom(parameter.ParameterType)));
        if (constructor == null)
            return new GameWindowViewModel(displayName, romPath, aspectRatioMode, inputBindingsByPort, extraInputBindings, upscaleMode, upscaleOutputResolution, enhancementMode, volume);

        var parameters = constructor.GetParameters();
        var arguments = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
            arguments[i] = ResolveConstructorArgument(parameters[i], displayName, romPath, aspectRatioMode, inputBindingsByPort, extraInputBindings, upscaleMode, upscaleOutputResolution, enhancementMode, volume, coreSession);

        return (GameWindowViewModel)constructor.Invoke(arguments);
    }

    private static object? ResolveConstructorArgument(
        ParameterInfo parameter,
        string displayName,
        string romPath,
        GameAspectRatioMode aspectRatioMode,
        IReadOnlyDictionary<string, Dictionary<string, Avalonia.Input.Key>> inputBindingsByPort,
        IReadOnlyList<ExtraInputBindingProfile>? extraInputBindings,
        MacUpscaleMode upscaleMode,
        MacUpscaleOutputResolution upscaleOutputResolution,
        PixelEnhancementMode enhancementMode,
        double volume,
        IEmulatorCoreSession coreSession)
    {
        if (typeof(IEmulatorCoreSession).IsAssignableFrom(parameter.ParameterType))
            return coreSession;

        var name = parameter.Name ?? string.Empty;
        return name switch
        {
            "displayName" => displayName,
            "romPath" => romPath,
            "aspectRatioMode" => aspectRatioMode,
            "inputBindingsByPort" => inputBindingsByPort,
            "extraInputBindings" => extraInputBindings,
            "upscaleMode" => upscaleMode,
            "upscaleOutputResolution" => upscaleOutputResolution,
            "enhancementMode" => enhancementMode,
            "initialVolume" => volume,
            "volume" => volume,
            _ when parameter.HasDefaultValue => parameter.DefaultValue,
            _ => throw new InvalidOperationException($"无法绑定 GameWindowViewModel 构造参数: {name} ({parameter.ParameterType.FullName})")
        };
    }
}

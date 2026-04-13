using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.Views;

namespace FC_Revolution.UI.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private void OpenLanArcadeRootInBrowser()
    {
        OpenUrlInBrowser("http://127.0.0.1:" + LanArcadePort + "/");
    }

    [RelayCommand]
    private void OpenLanArcadeDebugPageInBrowser()
    {
        OpenUrlInBrowser("http://127.0.0.1:" + LanArcadePort + "/debug/minimal");
    }

    [RelayCommand]
    private void OpenLanArcadeProbeWindow()
    {
        if (_webProbeWindow?.IsVisible == true)
        {
            _webProbeWindow.Activate();
            return;
        }

        var vm = new WebProbeViewModel(
            "http://127.0.0.1:" + LanArcadePort,
            LanArcadeEntryUrl.TrimEnd('/'));

        _webProbeWindow = new WebProbeWindow
        {
            DataContext = vm
        };
        _webProbeWindow.Closed += (_, _) => _webProbeWindow = null;

        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime &&
            lifetime.MainWindow != null)
        {
            _webProbeWindow.Show(lifetime.MainWindow);
        }
        else
        {
            _webProbeWindow.Show();
        }

        vm.RefreshCommand.Execute(null);
    }

    [RelayCommand]
    private void CancelReplaceGameSession()
    {
        _sessionLifecycleController.CancelReplaceGameSession(
            clearPendingLaunchRom: () => _pendingLaunchRom = null,
            setIsReplacingGameSession: value => IsReplacingGameSession = value);
    }

    [RelayCommand]
    private void ReplaceGameSession(ActiveGameSessionItem? session)
    {
        _sessionLifecycleController.TryReplaceGameSession(
            session,
            _pendingLaunchRom,
            closeSession: target => _gameSessionService.CloseSession(target),
            startSession: StartGameSession,
            clearPendingLaunchRom: () => _pendingLaunchRom = null,
            setIsReplacingGameSession: value => IsReplacingGameSession = value);
    }

    [RelayCommand]
    private void FocusGameSession(ActiveGameSessionItem? session)
    {
        _sessionLifecycleController.TryFocusGameSession(
            session,
            focusSessionWindow: target =>
            {
                if (target.Window.WindowState == WindowState.Minimized)
                    target.Window.WindowState = WindowState.Normal;

                target.Window.Show();
                target.Window.Activate();
            },
            getDisplayName: target => target.DisplayName,
            setStatusText: value => StatusText = value);
    }

    [RelayCommand]
    private void CloseGameSessionFromMenu(ActiveGameSessionItem? session)
    {
        _sessionLifecycleController.TryCloseGameSessionFromMenu(
            session,
            closeSession: target => _gameSessionService.CloseSession(target),
            getDisplayName: target => target.DisplayName,
            setStatusText: value => StatusText = value);
    }

    private void StartGameSession(RomLibraryItem rom)
    {
        var inputMaps = GetEffectivePlayerInputMaps(rom.Path);
        var extraInputBindings = GetEffectiveExtraInputBindingProfiles(rom.Path);
        var mapperDescription = DescribeRomMapper(rom.Path);
        var gameWindowShortcuts = BuildGameWindowShortcutMap();
        var launchResult = _sessionLaunchController.Launch(
            rom.DisplayName,
            mapperDescription,
            () => _gameSessionService.StartSessionWithInputBindings(
                rom.DisplayName,
                rom.Path,
                GameAspectRatioMode,
                InputBindingContractAdapter.BuildActionBindingsFromPlayerMaps(inputMaps, _inputBindingSchema),
                extraInputBindings,
                SyncLoadedFlags,
                _macUpscaleMode,
                _macUpscaleOutputResolution,
                _localDisplayEnhancementMode,
                _volume,
                gameWindowShortcuts,
                DefaultCoreId));

        StatusText = launchResult.StatusText;
        RuntimeDiagnostics.Write("mapper", launchResult.RuntimeDiagnosticsMessage);
        if (!string.IsNullOrWhiteSpace(launchResult.RuntimeDiagnosticsExceptionText))
            RuntimeDiagnostics.Write("mapper", launchResult.RuntimeDiagnosticsExceptionText);
        if (launchResult.FailureException != null &&
            !string.IsNullOrWhiteSpace(launchResult.StartupDiagnosticsContext))
        {
            StartupDiagnostics.WriteException("main-vm", launchResult.StartupDiagnosticsContext, launchResult.FailureException);
        }
    }

    private async Task ApplyLanArcadeServerStateAsync()
    {
        if (!_isLanArcadeServerReady || !_isWindowOpened)
            return;

        LogStartup($"ApplyLanArcadeServerStateAsync begin; enabled={IsLanArcadeEnabled}, ready={_isLanArcadeServerReady}, windowOpened={_isWindowOpened}");
        await _lanArcadeStateGate.WaitAsync();
        try
        {
            if (IsLanArcadeEnabled)
                await StartLanArcadeServerAsync();
            else
                await StopLanArcadeServerAsync();
        }
        finally
        {
            _lanArcadeStateGate.Release();
            LogStartup("ApplyLanArcadeServerStateAsync complete");
        }
    }

    private async Task StartLanArcadeServerAsync()
    {
        var watch = Stopwatch.StartNew();
        LogStartup($"StartLanArcadeServerAsync begin; port={LanArcadePort}, webPad={IsLanArcadeWebPadEnabled}, debugPages={IsLanArcadeDebugPagesEnabled}");
        try
        {
            var result = await _lanArcadeController.StartServerAsync(
                LanArcadePort,
                IsLanArcadeWebPadEnabled,
                IsLanArcadeDebugPagesEnabled,
                _lanStreamScaleMultiplier,
                _lanStreamJpegQuality,
                _lanStreamSharpenMode,
                LanArcadeEntryUrl);
            var viewState = _lanServerStateController.BuildStartViewState(result);
            if (viewState.QrCode != null)
                LanArcadeQrCode = viewState.QrCode;
            if (!string.IsNullOrWhiteSpace(viewState.LastTrafficText))
                LanArcadeLastTrafficText = viewState.LastTrafficText;
            StatusText = viewState.StatusText;
            if (viewState.NotifyLanArcadeAccessSummary)
                OnPropertyChanged(nameof(LanArcadeAccessSummary));
            if (viewState.ShouldRefreshDiagnostics)
                _ = RefreshLanArcadeDiagnosticsAsync();
            LogStartup($"StartLanArcadeServerAsync complete in {watch.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
            var viewState = _lanServerStateController.BuildStartFailureViewState(ex);
            StatusText = viewState.StatusText;
            if (viewState.NotifyLanArcadeAccessSummary)
                OnPropertyChanged(nameof(LanArcadeAccessSummary));
            LanArcadeDiagnosticsText = viewState.DiagnosticsText;
            LogStartup($"StartLanArcadeServerAsync failed after {watch.ElapsedMilliseconds} ms: {ex}");
        }
    }

    private void ApplyStreamParameters()
    {
        _lanArcadeController.ApplyStreamParameters(_lanStreamScaleMultiplier, _lanStreamJpegQuality, _lanStreamSharpenMode);
    }

    private async Task StopLanArcadeServerAsync()
    {
        var watch = Stopwatch.StartNew();
        LogStartup("StopLanArcadeServerAsync begin");
        var result = await _lanArcadeController.StopServerAsync();
        var viewState = _lanServerStateController.BuildStopViewState(result);
        LanArcadeQrCode = viewState.QrCode;
        StatusText = viewState.StatusText;
        if (viewState.NotifyLanArcadeAccessSummary)
            OnPropertyChanged(nameof(LanArcadeAccessSummary));
        LanArcadeDiagnosticsText = viewState.DiagnosticsText;
        LanArcadeLastTrafficText = viewState.LastTrafficText;
        LogStartup($"StopLanArcadeServerAsync complete in {watch.ElapsedMilliseconds} ms; stopped={result.Stopped}");
    }

    [RelayCommand]
    private void ApplyLanArcadePort()
    {
        var decision = _lanArcadeController.ValidatePortApply(LanArcadePortInput, LanArcadePort, IsLanArcadeEnabled);
        var viewState = _lanServerStateController.BuildPortApplyViewState(
            decision,
            LanArcadePort,
            IsLanArcadeEnabled,
            _lanArcadeService.IsRunning);
        if (viewState.ShouldResetPortInput && !string.IsNullOrWhiteSpace(viewState.PortInputText))
            LanArcadePortInput = viewState.PortInputText;

        if (viewState.ShouldStopBeforeApply)
            _ = StopLanArcadeServerAsync();

        if (viewState.ShouldApplyNewPort && viewState.NewPort.HasValue)
            LanArcadePort = viewState.NewPort.Value;
        if (viewState.ShouldSaveConfig)
            SaveSystemConfig();

        if (viewState.ShouldStartAfterApply)
            _ = StartLanArcadeServerAsync();
        if (!string.IsNullOrWhiteSpace(viewState.StatusText))
            StatusText = viewState.StatusText;
    }

    [RelayCommand]
    private void RefreshLanFirewallStatus()
    {
        var watch = Stopwatch.StartNew();
        LogStartup("starting firewall probe");
        var status = _lanArcadeController.ProbeFirewall();
        LanFirewallStatusTitle = status.Title;
        LanFirewallStatusDetail = status.Detail;
        LogStartup($"firewall probe complete in {watch.ElapsedMilliseconds} ms; title={status.Title}");
    }

    [RelayCommand]
    private Task RefreshLanArcadeDiagnosticsAsync() => RunLanArcadeDiagnosticsAsync();

    private async Task RunLanArcadeDiagnosticsAsync()
    {
        try
        {
            var diagnostics = await _lanArcadeController.BuildDiagnosticsAsync(LanArcadePort, LanArcadeEntryUrl);
            LanArcadeDiagnosticsText = _lanServerStateController.BuildDiagnosticsViewState(diagnostics).DiagnosticsText;
        }
        catch (Exception ex)
        {
            LanArcadeDiagnosticsText = _lanServerStateController.BuildDiagnosticsFailureViewState(ex).DiagnosticsText;
        }
    }

    private void ReportLanArcadeTraffic(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var update = _lanArcadeController.AppendTraffic(LanArcadeLastTrafficText, _lanArcadeTrafficCount, message);
            var viewState = _lanServerStateController.BuildTrafficViewState(update);
            _lanArcadeTrafficCount = viewState.NextCount;
            if (viewState.NotifyLanArcadeTrafficSummary)
                OnPropertyChanged(nameof(LanArcadeTrafficSummary));
            LanArcadeLastTrafficText = viewState.NextText;
        });
    }

    private void RefreshActiveSessionState()
    {
        OnPropertyChanged(nameof(HasActiveGameSessions));
        OnPropertyChanged(nameof(ActiveSessionSummary));
        OnPropertyChanged(nameof(LanArcadeControlSummary));
        SyncLoadedFlags();
        _ = SyncBackendStateMirrorAsync();
    }

    private static IBackendStateSyncClient? CreateBackendStateSyncClient()
    {
        var baseUrl = Environment.GetEnvironmentVariable("FC_REVOLUTION_BACKEND_URL");
        return string.IsNullOrWhiteSpace(baseUrl)
            ? null
            : new BackendStateSyncClient(baseUrl);
    }

    private Task SyncBackendStateMirrorAsync() => _backendStateMirror.SyncAsync();
}

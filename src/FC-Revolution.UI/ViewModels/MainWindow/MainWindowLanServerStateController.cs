using System;
using Avalonia.Media.Imaging;

namespace FC_Revolution.UI.ViewModels;

internal sealed record LanServerStartViewState(
    WriteableBitmap? QrCode,
    string? LastTrafficText,
    string StatusText,
    bool NotifyLanArcadeAccessSummary,
    bool ShouldRefreshDiagnostics);

internal sealed record LanServerStartFailureViewState(
    string StatusText,
    string DiagnosticsText,
    bool NotifyLanArcadeAccessSummary);

internal sealed record LanServerStopViewState(
    WriteableBitmap? QrCode,
    string StatusText,
    string DiagnosticsText,
    string LastTrafficText,
    bool NotifyLanArcadeAccessSummary);

internal sealed record LanPortApplyViewState(
    bool ShouldResetPortInput,
    string? PortInputText,
    string? StatusText,
    bool ShouldStopBeforeApply,
    bool ShouldApplyNewPort,
    int? NewPort,
    bool ShouldSaveConfig,
    bool ShouldStartAfterApply);

internal sealed record LanDiagnosticsViewState(string DiagnosticsText);

internal sealed record LanTrafficViewState(int NextCount, string NextText, bool NotifyLanArcadeTrafficSummary);

internal sealed class MainWindowLanServerStateController
{
    public LanServerStartViewState BuildStartViewState(LanServerStartResult result)
    {
        return new LanServerStartViewState(
            QrCode: result.QrCode,
            LastTrafficText: result.LastTrafficText,
            StatusText: result.StatusText,
            NotifyLanArcadeAccessSummary: true,
            ShouldRefreshDiagnostics: result.IsSuccess && !result.IsAlreadyRunning);
    }

    public LanServerStartFailureViewState BuildStartFailureViewState(Exception exception)
    {
        return new LanServerStartFailureViewState(
            StatusText: $"局域网点播启动失败: {exception.Message}",
            DiagnosticsText: $"启动失败: {exception.Message}",
            NotifyLanArcadeAccessSummary: true);
    }

    public LanServerStopViewState BuildStopViewState(LanServerStopResult result)
    {
        return new LanServerStopViewState(
            QrCode: result.QrCode,
            StatusText: result.StatusText,
            DiagnosticsText: result.DiagnosticsText,
            LastTrafficText: result.LastTrafficText,
            NotifyLanArcadeAccessSummary: true);
    }

    public LanPortApplyViewState BuildPortApplyViewState(
        LanPortApplyDecision decision,
        int currentPort,
        bool isEnabled,
        bool isServiceRunning)
    {
        if (!decision.IsValid)
        {
            return new LanPortApplyViewState(
                ShouldResetPortInput: true,
                PortInputText: currentPort.ToString(),
                StatusText: decision.StatusText,
                ShouldStopBeforeApply: false,
                ShouldApplyNewPort: false,
                NewPort: null,
                ShouldSaveConfig: false,
                ShouldStartAfterApply: false);
        }

        if (!decision.IsChanged)
        {
            return new LanPortApplyViewState(
                ShouldResetPortInput: false,
                PortInputText: null,
                StatusText: decision.StatusText,
                ShouldStopBeforeApply: false,
                ShouldApplyNewPort: false,
                NewPort: null,
                ShouldSaveConfig: false,
                ShouldStartAfterApply: false);
        }

        return new LanPortApplyViewState(
            ShouldResetPortInput: false,
            PortInputText: null,
            StatusText: isEnabled ? null : decision.StatusText,
            ShouldStopBeforeApply: isEnabled && isServiceRunning,
            ShouldApplyNewPort: true,
            NewPort: decision.ResolvedPort,
            ShouldSaveConfig: true,
            ShouldStartAfterApply: isEnabled);
    }

    public LanDiagnosticsViewState BuildDiagnosticsViewState(string diagnosticsText) =>
        new(diagnosticsText);

    public LanDiagnosticsViewState BuildDiagnosticsFailureViewState(Exception exception) =>
        new($"局域网自检失败: {exception.Message}");

    public LanTrafficViewState BuildTrafficViewState(LanTrafficUpdate update) =>
        new(update.NextCount, update.NextText, NotifyLanArcadeTrafficSummary: true);
}

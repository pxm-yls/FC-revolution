using System;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowLanServerStateControllerTests
{
    [Fact]
    public void BuildStartViewState_MapsSuccessResultAndRefreshDecision()
    {
        var controller = new MainWindowLanServerStateController();
        var result = new LanServerStartResult(
            IsSuccess: true,
            IsAlreadyRunning: false,
            QrCode: null,
            StatusText: "started",
            LastTrafficText: "traffic");

        var viewState = controller.BuildStartViewState(result);

        Assert.Equal("started", viewState.StatusText);
        Assert.Equal("traffic", viewState.LastTrafficText);
        Assert.True(viewState.NotifyLanArcadeAccessSummary);
        Assert.True(viewState.ShouldRefreshDiagnostics);
    }

    [Fact]
    public void BuildStartFailureViewState_MapsFailureText()
    {
        var controller = new MainWindowLanServerStateController();
        var viewState = controller.BuildStartFailureViewState(new InvalidOperationException("boom"));

        Assert.Equal("局域网点播启动失败: boom", viewState.StatusText);
        Assert.Equal("启动失败: boom", viewState.DiagnosticsText);
        Assert.True(viewState.NotifyLanArcadeAccessSummary);
    }

    [Fact]
    public void BuildStopViewState_MapsStopPayload()
    {
        var controller = new MainWindowLanServerStateController();
        var result = new LanServerStopResult(
            Stopped: true,
            QrCode: null,
            StatusText: "stopped",
            DiagnosticsText: "diag",
            LastTrafficText: "last");

        var viewState = controller.BuildStopViewState(result);

        Assert.Equal("stopped", viewState.StatusText);
        Assert.Equal("diag", viewState.DiagnosticsText);
        Assert.Equal("last", viewState.LastTrafficText);
        Assert.True(viewState.NotifyLanArcadeAccessSummary);
    }

    [Fact]
    public void BuildPortApplyViewState_CoversInvalidUnchangedAndChangedBranches()
    {
        var controller = new MainWindowLanServerStateController();

        var invalid = controller.BuildPortApplyViewState(
            new LanPortApplyDecision(
                IsValid: false,
                IsChanged: false,
                IsOccupied: false,
                ResolvedPort: 18888,
                StatusText: "invalid"),
            currentPort: 16666,
            isEnabled: true,
            isServiceRunning: true);
        Assert.True(invalid.ShouldResetPortInput);
        Assert.Equal("16666", invalid.PortInputText);
        Assert.Equal("invalid", invalid.StatusText);
        Assert.False(invalid.ShouldApplyNewPort);

        var unchanged = controller.BuildPortApplyViewState(
            new LanPortApplyDecision(
                IsValid: true,
                IsChanged: false,
                IsOccupied: false,
                ResolvedPort: 18888,
                StatusText: "same"),
            currentPort: 16666,
            isEnabled: true,
            isServiceRunning: true);
        Assert.False(unchanged.ShouldResetPortInput);
        Assert.Equal("same", unchanged.StatusText);
        Assert.False(unchanged.ShouldApplyNewPort);

        var changedEnabled = controller.BuildPortApplyViewState(
            new LanPortApplyDecision(
                IsValid: true,
                IsChanged: true,
                IsOccupied: false,
                ResolvedPort: 18888,
                StatusText: "changed"),
            currentPort: 16666,
            isEnabled: true,
            isServiceRunning: true);
        Assert.True(changedEnabled.ShouldStopBeforeApply);
        Assert.True(changedEnabled.ShouldApplyNewPort);
        Assert.Equal(18888, changedEnabled.NewPort);
        Assert.True(changedEnabled.ShouldSaveConfig);
        Assert.True(changedEnabled.ShouldStartAfterApply);
        Assert.Null(changedEnabled.StatusText);

        var changedDisabled = controller.BuildPortApplyViewState(
            new LanPortApplyDecision(
                IsValid: true,
                IsChanged: true,
                IsOccupied: false,
                ResolvedPort: 19999,
                StatusText: "changed-disabled"),
            currentPort: 16666,
            isEnabled: false,
            isServiceRunning: false);
        Assert.False(changedDisabled.ShouldStopBeforeApply);
        Assert.True(changedDisabled.ShouldApplyNewPort);
        Assert.Equal(19999, changedDisabled.NewPort);
        Assert.True(changedDisabled.ShouldSaveConfig);
        Assert.False(changedDisabled.ShouldStartAfterApply);
        Assert.Equal("changed-disabled", changedDisabled.StatusText);
    }

    [Fact]
    public void BuildDiagnosticsFailureAndTrafficViewState_MapExpectedTextAndCounters()
    {
        var controller = new MainWindowLanServerStateController();

        var diagnostics = controller.BuildDiagnosticsFailureViewState(new Exception("timeout"));
        Assert.Equal("局域网自检失败: timeout", diagnostics.DiagnosticsText);

        var traffic = controller.BuildTrafficViewState(new LanTrafficUpdate(12, "line-12"));
        Assert.Equal(12, traffic.NextCount);
        Assert.Equal("line-12", traffic.NextText);
        Assert.True(traffic.NotifyLanArcadeTrafficSummary);
    }
}

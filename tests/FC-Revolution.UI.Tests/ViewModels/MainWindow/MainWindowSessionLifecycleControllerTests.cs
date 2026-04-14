using System.Collections.Specialized;
using System.ComponentModel;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowSessionLifecycleControllerTests
{
    [Fact]
    public void IsRemoteControlTrackedProperty_RecognizesExpectedPropertyNames()
    {
        Assert.True(MainWindowSessionLifecycleController.IsRemoteControlTrackedProperty("RemoteControlStatusText"));
        Assert.True(MainWindowSessionLifecycleController.IsRemoteControlTrackedProperty("RemoteControlPortsVersion"));
        Assert.False(MainWindowSessionLifecycleController.IsRemoteControlTrackedProperty("StatusText"));
        Assert.False(MainWindowSessionLifecycleController.IsRemoteControlTrackedProperty(null));
    }

    [Fact]
    public void ApplyTrackedSessionChanges_WiresHandlers_AndTrackedChangeTriggersRefresh()
    {
        var subscribeCalls = 0;
        var unsubscribeCalls = 0;
        var summaryRefreshCalls = 0;
        var activeRefreshCalls = 0;

        var controller = new MainWindowSessionLifecycleController(
            (tracked, handler) =>
            {
                subscribeCalls++;
                tracked.PropertyChanged += handler;
            },
            (tracked, handler) =>
            {
                unsubscribeCalls++;
                tracked.PropertyChanged -= handler;
            },
            _ =>
            {
                summaryRefreshCalls++;
                return true;
            },
            () => activeRefreshCalls++);

        var tracked = new FakeTrackedPropertySource();
        controller.ApplyTrackedSessionChanges([], [tracked]);

        Assert.Equal(1, subscribeCalls);
        Assert.Equal(0, unsubscribeCalls);
        Assert.Equal(1, activeRefreshCalls);

        tracked.RaisePropertyChanged("RemoteControlPortsVersion");
        Assert.Equal(1, summaryRefreshCalls);
        Assert.Equal(2, activeRefreshCalls);

        tracked.RaisePropertyChanged("StatusText");
        Assert.Equal(1, summaryRefreshCalls);
        Assert.Equal(2, activeRefreshCalls);

        controller.ApplyTrackedSessionChanges([tracked], []);
        Assert.Equal(1, unsubscribeCalls);
        Assert.Equal(3, activeRefreshCalls);

        tracked.RaisePropertyChanged("RemoteControlStatusText");
        Assert.Equal(1, summaryRefreshCalls);
        Assert.Equal(3, activeRefreshCalls);
    }

    [Fact]
    public void OnActiveGameSessionsChanged_WithUnsupportedItems_StillRefreshesState()
    {
        var activeRefreshCalls = 0;
        var controller = new MainWindowSessionLifecycleController(
            (_, _) => { },
            (_, _) => { },
            _ => false,
            () => activeRefreshCalls++);

        var e = new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Add,
            new object[] { "not-a-session-item" });

        controller.OnActiveGameSessionsChanged(sender: null, e);
        Assert.Equal(1, activeRefreshCalls);
    }

    [Fact]
    public void ReplaceCancelFocusClose_UseProvidedCallbacks()
    {
        var controller = new MainWindowSessionLifecycleController(
            (_, _) => { },
            (_, _) => { },
            _ => false,
            () => { });

        object? pending = new object();
        var replacing = true;
        var canceled = false;
        controller.CancelReplaceGameSession(
            clearPendingLaunchRom: () =>
            {
                pending = null;
                canceled = true;
            },
            setIsReplacingGameSession: value => replacing = value);
        Assert.True(canceled);
        Assert.Null(pending);
        Assert.False(replacing);

        var session = new FakeSession("Contra");
        var launchRom = new FakeRom("Contra");
        var closeCalls = 0;
        var startCalls = 0;
        var clearedAfterReplace = false;
        replacing = true;
        var replaced = controller.TryReplaceGameSession(
            session,
            launchRom,
            closeSession: _ => closeCalls++,
            startSession: _ => startCalls++,
            clearPendingLaunchRom: () => clearedAfterReplace = true,
            setIsReplacingGameSession: value => replacing = value);
        Assert.True(replaced);
        Assert.Equal(1, closeCalls);
        Assert.Equal(1, startCalls);
        Assert.True(clearedAfterReplace);
        Assert.False(replacing);

        var focused = false;
        string? focusStatus = null;
        var focusResult = controller.TryFocusGameSession(
            session,
            focusSessionWindow: _ => focused = true,
            getDisplayName: item => item.DisplayName,
            setStatusText: text => focusStatus = text);
        Assert.True(focusResult);
        Assert.True(focused);
        Assert.Equal("已聚焦游戏窗口: Contra", focusStatus);

        var closeMenuCalls = 0;
        string? closeStatus = null;
        var closeResult = controller.TryCloseGameSessionFromMenu(
            session,
            closeSession: _ => closeMenuCalls++,
            getDisplayName: item => item.DisplayName,
            setStatusText: text => closeStatus = text);
        Assert.True(closeResult);
        Assert.Equal(1, closeMenuCalls);
        Assert.Equal("已关闭游戏窗口: Contra", closeStatus);
    }

    private sealed class FakeTrackedPropertySource : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        internal void RaisePropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed record FakeSession(string DisplayName);

    private sealed record FakeRom(string DisplayName);
}

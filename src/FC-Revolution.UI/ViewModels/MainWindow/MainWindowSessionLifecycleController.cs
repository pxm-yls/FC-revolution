using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowSessionLifecycleController
{
    private readonly Action<INotifyPropertyChanged, PropertyChangedEventHandler> _subscribeTracked;
    private readonly Action<INotifyPropertyChanged, PropertyChangedEventHandler> _unsubscribeTracked;
    private readonly Func<object?, bool> _tryRefreshRemoteControlSummaryForTracked;
    private readonly Action _refreshActiveSessionState;

    public MainWindowSessionLifecycleController(
        Action<INotifyPropertyChanged, PropertyChangedEventHandler> subscribeTracked,
        Action<INotifyPropertyChanged, PropertyChangedEventHandler> unsubscribeTracked,
        Func<object?, bool> tryRefreshRemoteControlSummaryForTracked,
        Action refreshActiveSessionState)
    {
        _subscribeTracked = subscribeTracked;
        _unsubscribeTracked = unsubscribeTracked;
        _tryRefreshRemoteControlSummaryForTracked = tryRefreshRemoteControlSummaryForTracked;
        _refreshActiveSessionState = refreshActiveSessionState;
    }

    public void OnActiveGameSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var oldTracked = EnumerateTrackedItems(e.OldItems);
        var newTracked = EnumerateTrackedItems(e.NewItems);
        ApplyTrackedSessionChanges(oldTracked, newTracked);
    }

    internal void ApplyTrackedSessionChanges(
        IReadOnlyList<INotifyPropertyChanged> oldTracked,
        IReadOnlyList<INotifyPropertyChanged> newTracked)
    {
        foreach (var tracked in oldTracked)
            _unsubscribeTracked(tracked, OnTrackedSessionPropertyChanged);

        foreach (var tracked in newTracked)
            _subscribeTracked(tracked, OnTrackedSessionPropertyChanged);

        _refreshActiveSessionState();
    }

    internal void HandleTrackedSessionPropertyChangedCore(object? sender, string? propertyName)
    {
        if (!IsRemoteControlTrackedProperty(propertyName))
            return;

        _tryRefreshRemoteControlSummaryForTracked(sender);
        _refreshActiveSessionState();
    }

    internal static bool IsRemoteControlTrackedProperty(string? propertyName) =>
        propertyName is "RemoteControlStatusText" or "Player1ControlSource" or "Player2ControlSource";

    public void CancelReplaceGameSession(Action clearPendingLaunchRom, Action<bool> setIsReplacingGameSession)
    {
        clearPendingLaunchRom();
        setIsReplacingGameSession(false);
    }

    public bool TryReplaceGameSession<TSession, TRom>(
        TSession? session,
        TRom? pendingLaunchRom,
        Action<TSession> closeSession,
        Action<TRom> startSession,
        Action clearPendingLaunchRom,
        Action<bool> setIsReplacingGameSession)
        where TSession : class
        where TRom : class
    {
        if (session == null || pendingLaunchRom == null)
            return false;

        closeSession(session);
        startSession(pendingLaunchRom);
        clearPendingLaunchRom();
        setIsReplacingGameSession(false);
        return true;
    }

    public bool TryFocusGameSession<TSession>(
        TSession? session,
        Action<TSession> focusSessionWindow,
        Func<TSession, string> getDisplayName,
        Action<string> setStatusText)
        where TSession : class
    {
        if (session == null)
            return false;

        focusSessionWindow(session);
        setStatusText($"已聚焦游戏窗口: {getDisplayName(session)}");
        return true;
    }

    public bool TryCloseGameSessionFromMenu<TSession>(
        TSession? session,
        Action<TSession> closeSession,
        Func<TSession, string> getDisplayName,
        Action<string> setStatusText)
        where TSession : class
    {
        if (session == null)
            return false;

        closeSession(session);
        setStatusText($"已关闭游戏窗口: {getDisplayName(session)}");
        return true;
    }

    private void OnTrackedSessionPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        HandleTrackedSessionPropertyChangedCore(sender, e.PropertyName);

    private static IReadOnlyList<INotifyPropertyChanged> EnumerateTrackedItems(System.Collections.IList? items)
    {
        if (items == null || items.Count == 0)
            return [];

        return items
            .OfType<ActiveGameSessionItem>()
            .Select(item => (INotifyPropertyChanged)item.ViewModel)
            .ToArray();
    }
}

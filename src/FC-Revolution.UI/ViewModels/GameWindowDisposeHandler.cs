using System;

namespace FC_Revolution.UI.ViewModels;

internal sealed class GameWindowDisposeHandler
{
    private readonly Func<bool> _isDisposed;
    private readonly Action _markDisposed;
    private readonly Action _clearPendingFrame;
    private readonly Action _unsubscribeSessionEvents;
    private readonly Action _stopSessionLoop;
    private readonly Action _stopUiTimer;
    private readonly Action _stopToastTimer;
    private readonly Action _closeDebugWindow;
    private readonly Action _disposeAudio;
    private readonly Action _disposeCoreSession;
    private readonly Action _disposeFramePresenter;

    public GameWindowDisposeHandler(
        Func<bool> isDisposed,
        Action markDisposed,
        Action clearPendingFrame,
        Action unsubscribeSessionEvents,
        Action stopSessionLoop,
        Action stopUiTimer,
        Action stopToastTimer,
        Action closeDebugWindow,
        Action disposeAudio,
        Action disposeCoreSession,
        Action disposeFramePresenter)
    {
        ArgumentNullException.ThrowIfNull(isDisposed);
        ArgumentNullException.ThrowIfNull(markDisposed);
        ArgumentNullException.ThrowIfNull(clearPendingFrame);
        ArgumentNullException.ThrowIfNull(unsubscribeSessionEvents);
        ArgumentNullException.ThrowIfNull(stopSessionLoop);
        ArgumentNullException.ThrowIfNull(stopUiTimer);
        ArgumentNullException.ThrowIfNull(stopToastTimer);
        ArgumentNullException.ThrowIfNull(closeDebugWindow);
        ArgumentNullException.ThrowIfNull(disposeAudio);
        ArgumentNullException.ThrowIfNull(disposeCoreSession);
        ArgumentNullException.ThrowIfNull(disposeFramePresenter);

        _isDisposed = isDisposed;
        _markDisposed = markDisposed;
        _clearPendingFrame = clearPendingFrame;
        _unsubscribeSessionEvents = unsubscribeSessionEvents;
        _stopSessionLoop = stopSessionLoop;
        _stopUiTimer = stopUiTimer;
        _stopToastTimer = stopToastTimer;
        _closeDebugWindow = closeDebugWindow;
        _disposeAudio = disposeAudio;
        _disposeCoreSession = disposeCoreSession;
        _disposeFramePresenter = disposeFramePresenter;
    }

    public void Dispose()
    {
        if (_isDisposed())
            return;

        _markDisposed();
        _clearPendingFrame();
        _unsubscribeSessionEvents();
        _stopSessionLoop();
        _stopUiTimer();
        _stopToastTimer();
        _closeDebugWindow();
        _disposeAudio();
        _disposeCoreSession();
        _disposeFramePresenter();
    }
}

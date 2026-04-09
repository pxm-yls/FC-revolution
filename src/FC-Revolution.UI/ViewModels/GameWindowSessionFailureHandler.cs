using System;

namespace FC_Revolution.UI.ViewModels;

internal sealed class GameWindowSessionFailureHandler
{
    private readonly GameWindowSessionFailureController _controller;
    private readonly Func<bool> _isDisposed;
    private readonly Func<bool> _hasSessionFailure;
    private readonly Action _markSessionFailure;
    private readonly Action _stopSessionLoop;
    private readonly Action _clearPendingFrame;
    private readonly Action _pauseRuntime;
    private readonly Action<string> _writeDiagnostic;
    private readonly Action<string, string?> _updateStatus;

    public GameWindowSessionFailureHandler(
        GameWindowSessionFailureController controller,
        Func<bool> isDisposed,
        Func<bool> hasSessionFailure,
        Action markSessionFailure,
        Action stopSessionLoop,
        Action clearPendingFrame,
        Action pauseRuntime,
        Action<string> writeDiagnostic,
        Action<string, string?> updateStatus)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(isDisposed);
        ArgumentNullException.ThrowIfNull(hasSessionFailure);
        ArgumentNullException.ThrowIfNull(markSessionFailure);
        ArgumentNullException.ThrowIfNull(stopSessionLoop);
        ArgumentNullException.ThrowIfNull(clearPendingFrame);
        ArgumentNullException.ThrowIfNull(pauseRuntime);
        ArgumentNullException.ThrowIfNull(writeDiagnostic);
        ArgumentNullException.ThrowIfNull(updateStatus);

        _controller = controller;
        _isDisposed = isDisposed;
        _hasSessionFailure = hasSessionFailure;
        _markSessionFailure = markSessionFailure;
        _stopSessionLoop = stopSessionLoop;
        _clearPendingFrame = clearPendingFrame;
        _pauseRuntime = pauseRuntime;
        _writeDiagnostic = writeDiagnostic;
        _updateStatus = updateStatus;
    }

    public void Handle(string message, Exception? exception = null)
    {
        var decision = _controller.BuildDecision(_isDisposed(), _hasSessionFailure(), message, exception);
        if (!decision.ShouldHandleFailure)
            return;

        _markSessionFailure();
        if (decision.ShouldStopSessionLoop)
            _stopSessionLoop();
        if (decision.ShouldClearPendingFrame)
            _clearPendingFrame();
        if (decision.ShouldPauseRuntime)
            _pauseRuntime();

        foreach (var diagnostic in decision.Diagnostics)
            _writeDiagnostic(diagnostic);

        _updateStatus(decision.StatusText, decision.ToastText);
    }
}

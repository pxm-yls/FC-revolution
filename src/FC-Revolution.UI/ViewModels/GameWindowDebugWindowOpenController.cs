using System;

namespace FC_Revolution.UI.ViewModels;

internal sealed class GameWindowDebugWindowOpenController
{
    private readonly GameWindowDebugWindowWorkflowController _workflow;
    private readonly Func<bool> _hasSessionFailure;
    private readonly Func<bool> _hasUiThreadAccess;
    private readonly Action<Action> _postToUiThread;
    private readonly Action _openWindow;
    private readonly Action<string, string?> _updateStatus;
    private readonly Action<string> _showToast;

    public GameWindowDebugWindowOpenController(
        GameWindowDebugWindowWorkflowController workflow,
        Func<bool> hasSessionFailure,
        Func<bool> hasUiThreadAccess,
        Action<Action> postToUiThread,
        Action openWindow,
        Action<string, string?> updateStatus,
        Action<string> showToast)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(hasSessionFailure);
        ArgumentNullException.ThrowIfNull(hasUiThreadAccess);
        ArgumentNullException.ThrowIfNull(postToUiThread);
        ArgumentNullException.ThrowIfNull(openWindow);
        ArgumentNullException.ThrowIfNull(updateStatus);
        ArgumentNullException.ThrowIfNull(showToast);

        _workflow = workflow;
        _hasSessionFailure = hasSessionFailure;
        _hasUiThreadAccess = hasUiThreadAccess;
        _postToUiThread = postToUiThread;
        _openWindow = openWindow;
        _updateStatus = updateStatus;
        _showToast = showToast;
    }

    public void TryOpen()
    {
        var decision = _workflow.BuildPreOpenDecision(_hasSessionFailure(), _hasUiThreadAccess());

        if (decision.ShouldDispatchToUiThread)
        {
            _postToUiThread(TryOpen);
            return;
        }

        if (!decision.ShouldOpenWindow)
        {
            if (!string.IsNullOrWhiteSpace(decision.StatusText))
                _updateStatus(decision.StatusText, decision.ToastText);
            else if (!string.IsNullOrWhiteSpace(decision.ToastText))
                _showToast(decision.ToastText);

            return;
        }

        try
        {
            _openWindow();
            var successDecision = _workflow.BuildOpenSuccessDecision();
            if (!string.IsNullOrWhiteSpace(successDecision.ToastText))
                _showToast(successDecision.ToastText);
        }
        catch (Exception ex)
        {
            var failureDecision = _workflow.BuildOpenFailureDecision(ex);
            _updateStatus(failureDecision.StatusText!, failureDecision.ToastText);
        }
    }
}

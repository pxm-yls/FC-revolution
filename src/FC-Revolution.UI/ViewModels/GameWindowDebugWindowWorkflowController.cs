using System;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowDebugWindowWorkflowDecision(
    bool ShouldDispatchToUiThread,
    bool ShouldOpenWindow,
    string? StatusText,
    string? ToastText);

internal sealed class GameWindowDebugWindowWorkflowController
{
    public GameWindowDebugWindowWorkflowDecision BuildPreOpenDecision(bool hasSessionFailure, bool hasUiThreadAccess)
    {
        if (hasSessionFailure)
        {
            return new(
                ShouldDispatchToUiThread: false,
                ShouldOpenWindow: false,
                StatusText: null,
                ToastText: "当前游戏会话已终止，请重新启动游戏后再打开调试窗口");
        }

        if (!hasUiThreadAccess)
        {
            return new(
                ShouldDispatchToUiThread: true,
                ShouldOpenWindow: false,
                StatusText: null,
                ToastText: null);
        }

        return new(
            ShouldDispatchToUiThread: false,
            ShouldOpenWindow: true,
            StatusText: null,
            ToastText: null);
    }

    public GameWindowDebugWindowWorkflowDecision BuildOpenSuccessDecision() =>
        new(
            ShouldDispatchToUiThread: false,
            ShouldOpenWindow: false,
            StatusText: null,
            ToastText: "调试窗口已打开");

    public GameWindowDebugWindowWorkflowDecision BuildOpenFailureDecision(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new(
            ShouldDispatchToUiThread: false,
            ShouldOpenWindow: false,
            StatusText: $"打开调试窗口失败: {exception.Message}",
            ToastText: $"调试窗口打开失败: {exception.Message}");
    }
}

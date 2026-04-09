using System;
using System.Collections.Generic;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowSessionFailureDecision(
    bool ShouldHandleFailure,
    bool ShouldStopSessionLoop,
    bool ShouldClearPendingFrame,
    bool ShouldPauseRuntime,
    string StatusText,
    string ToastText,
    IReadOnlyList<string> Diagnostics);

internal sealed class GameWindowSessionFailureController
{
    public GameWindowSessionFailureDecision BuildDecision(
        bool isDisposed,
        bool hasSessionFailure,
        string message,
        Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (isDisposed || hasSessionFailure)
            return new(false, false, false, false, string.Empty, string.Empty, []);

        List<string> diagnostics = [message];
        if (exception != null)
            diagnostics.Add(exception.ToString());

        return new(
            ShouldHandleFailure: true,
            ShouldStopSessionLoop: true,
            ShouldClearPendingFrame: true,
            ShouldPauseRuntime: true,
            StatusText: $"当前游戏会话已停止 | {message}",
            ToastText: message,
            Diagnostics: diagnostics);
    }
}

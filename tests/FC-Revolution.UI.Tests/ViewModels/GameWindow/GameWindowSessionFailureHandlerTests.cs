using System;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowSessionFailureHandlerTests
{
    [Fact]
    public void Handle_WhenAvailable_AppliesFailureSideEffects()
    {
        var marked = false;
        var stopCalls = 0;
        var clearCalls = 0;
        var pauseCalls = 0;
        var diagnostics = new List<string>();
        string? status = null;
        string? toast = null;
        var handler = new GameWindowSessionFailureHandler(
            new GameWindowSessionFailureController(),
            isDisposed: () => false,
            hasSessionFailure: () => false,
            markSessionFailure: () => marked = true,
            stopSessionLoop: () => stopCalls++,
            clearPendingFrame: () => clearCalls++,
            pauseRuntime: () => pauseCalls++,
            writeDiagnostic: diagnostics.Add,
            updateStatus: (text, message) =>
            {
                status = text;
                toast = message;
            });

        handler.Handle("session exploded", new InvalidOperationException("boom"));

        Assert.True(marked);
        Assert.Equal(1, stopCalls);
        Assert.Equal(1, clearCalls);
        Assert.Equal(1, pauseCalls);
        Assert.Equal(2, diagnostics.Count);
        Assert.Equal("session exploded", diagnostics[0]);
        Assert.Contains("boom", diagnostics[1]);
        Assert.Equal("当前游戏会话已停止 | session exploded", status);
        Assert.Equal("session exploded", toast);
    }

    [Fact]
    public void Handle_WhenAlreadyFailed_DoesNothing()
    {
        var markCalls = 0;
        var stopCalls = 0;
        var clearCalls = 0;
        var pauseCalls = 0;
        var diagnostics = new List<string>();
        var updateCalls = 0;
        var handler = new GameWindowSessionFailureHandler(
            new GameWindowSessionFailureController(),
            isDisposed: () => false,
            hasSessionFailure: () => true,
            markSessionFailure: () => markCalls++,
            stopSessionLoop: () => stopCalls++,
            clearPendingFrame: () => clearCalls++,
            pauseRuntime: () => pauseCalls++,
            writeDiagnostic: diagnostics.Add,
            updateStatus: (_, _) => updateCalls++);

        handler.Handle("ignored");

        Assert.Equal(0, markCalls);
        Assert.Equal(0, stopCalls);
        Assert.Equal(0, clearCalls);
        Assert.Equal(0, pauseCalls);
        Assert.Empty(diagnostics);
        Assert.Equal(0, updateCalls);
    }
}

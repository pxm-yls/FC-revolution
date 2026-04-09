using System;
using System.Threading;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowSessionLoopHostTests
{
    [Fact]
    public void Start_WhenPaused_DoesNotRunFramesUntilResumed()
    {
        var pauseRequested = 1;
        var runCount = 0;
        Exception? observedFailure = null;

        using var host = new GameWindowSessionLoopHost(
            "test-session-loop",
            () => Volatile.Read(ref pauseRequested) == 1,
            () =>
            {
                Interlocked.Increment(ref runCount);
                Volatile.Write(ref pauseRequested, 1);
                return CoreStepResult.Ok();
            },
            _ => { },
            _ => { },
            ex => observedFailure = ex);

        host.Start();

        Thread.Sleep(50);
        Assert.Equal(0, Volatile.Read(ref runCount));

        Volatile.Write(ref pauseRequested, 0);
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref runCount) > 0, 1000));
        Assert.Null(observedFailure);
    }

    [Fact]
    public void RunFrameFailure_InvokesFailureCallback_AndStopsLoop()
    {
        using var failed = new ManualResetEventSlim(false);
        Exception? observedFailure = null;

        using var host = new GameWindowSessionLoopHost(
            "test-session-loop",
            () => false,
            () => throw new InvalidOperationException("boom"),
            _ => { },
            _ => { },
            ex =>
            {
                observedFailure = ex;
                failed.Set();
            });

        host.Start();

        Assert.True(failed.Wait(1000));
        Assert.False(host.IsRunning);
        var failure = Assert.IsType<InvalidOperationException>(observedFailure);
        Assert.Equal("boom", failure.Message);
    }

    [Fact]
    public void SuccessfulFrame_ReportsFrameTiming()
    {
        using var frameReported = new ManualResetEventSlim(false);
        var pauseRequested = 0;
        var runCount = 0;
        var frameTimeMicros = -1;
        Exception? observedFailure = null;

        using var host = new GameWindowSessionLoopHost(
            "test-session-loop",
            () => Volatile.Read(ref pauseRequested) == 1,
            () =>
            {
                Interlocked.Increment(ref runCount);
                Volatile.Write(ref pauseRequested, 1);
                return CoreStepResult.Ok();
            },
            value =>
            {
                frameTimeMicros = value;
                frameReported.Set();
            },
            _ => { },
            ex => observedFailure = ex);

        host.Start();

        Assert.True(frameReported.Wait(1000));
        Assert.True(frameTimeMicros >= 0);
        Assert.True(Volatile.Read(ref runCount) > 0);
        Assert.Null(observedFailure);
    }
}

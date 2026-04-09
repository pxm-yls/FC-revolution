using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FC_Revolution.UI.Models.Previews;

namespace FC_Revolution.UI.Tests;

public sealed class VideoPreviewDecodeLockPolicyTests
{
    [Fact]
    public async Task AcquireBlocking_WaitsUntilLocksAreAvailable()
    {
        using var decodeLimiter = new SemaphoreSlim(1, 1);
        using var decodeGate = new SemaphoreSlim(1, 1);
        decodeLimiter.Wait();
        decodeGate.Wait();

        var stopwatch = Stopwatch.StartNew();
        var acquireTask = Task.Run(() =>
        {
            using var decodeLock = VideoPreviewDecodeLockPolicy.AcquireBlocking(
                decodeLimiter,
                decodeGate,
                CancellationToken.None);
            return stopwatch.ElapsedMilliseconds;
        });

        await Task.Delay(120);
        Assert.False(acquireTask.IsCompleted);

        decodeLimiter.Release();
        decodeGate.Release();

        var elapsed = await acquireTask;
        Assert.True(elapsed >= 100);
    }

    [Fact]
    public void TryAcquireNonBlocking_WhenLimiterBusy_ReturnsFalseQuickly()
    {
        using var decodeLimiter = new SemaphoreSlim(1, 1);
        using var decodeGate = new SemaphoreSlim(1, 1);
        decodeLimiter.Wait();

        var stopwatch = Stopwatch.StartNew();
        var acquired = VideoPreviewDecodeLockPolicy.TryAcquireNonBlocking(
            decodeLimiter,
            decodeGate,
            CancellationToken.None,
            out var decodeLock);
        stopwatch.Stop();

        Assert.False(acquired);
        Assert.True(stopwatch.ElapsedMilliseconds < 100);

        decodeLock.Dispose();
        decodeLimiter.Release();
    }

    [Fact]
    public void TryAcquireNonBlocking_AcquiresAndReleasesLimiterAndGate()
    {
        using var decodeLimiter = new SemaphoreSlim(1, 1);
        using var decodeGate = new SemaphoreSlim(1, 1);

        var acquired = VideoPreviewDecodeLockPolicy.TryAcquireNonBlocking(
            decodeLimiter,
            decodeGate,
            CancellationToken.None,
            out var decodeLock);
        Assert.True(acquired);

        Assert.False(decodeLimiter.Wait(0));
        Assert.False(decodeGate.Wait(0));

        decodeLock.Dispose();

        Assert.True(decodeLimiter.Wait(0));
        decodeLimiter.Release();
        Assert.True(decodeGate.Wait(0));
        decodeGate.Release();
    }

    [Fact]
    public void TryAcquireNonBlocking_WhenGateDisposed_ReturnsFalseWithoutLeakingLimiter()
    {
        using var decodeLimiter = new SemaphoreSlim(1, 1);
        var decodeGate = new SemaphoreSlim(1, 1);
        decodeGate.Dispose();

        var acquired = VideoPreviewDecodeLockPolicy.TryAcquireNonBlocking(
            decodeLimiter,
            decodeGate,
            CancellationToken.None,
            out var decodeLock);

        Assert.False(acquired);
        decodeLock.Dispose();
        Assert.True(decodeLimiter.Wait(0));
        decodeLimiter.Release();
    }
}

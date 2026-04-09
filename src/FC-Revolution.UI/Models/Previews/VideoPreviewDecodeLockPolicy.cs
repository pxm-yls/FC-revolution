using System;
using System.Threading;

namespace FC_Revolution.UI.Models.Previews;

internal static class VideoPreviewDecodeLockPolicy
{
    public static DecodeLockHandle AcquireBlocking(
        SemaphoreSlim decodeLimiter,
        SemaphoreSlim decodeGate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decodeLimiter);
        ArgumentNullException.ThrowIfNull(decodeGate);

        var limiterTaken = false;
        try
        {
            decodeLimiter.Wait(cancellationToken);
            limiterTaken = true;
            decodeGate.Wait(cancellationToken);
            return new DecodeLockHandle(decodeLimiter, decodeGate);
        }
        catch
        {
            if (limiterTaken)
                decodeLimiter.Release();
            throw;
        }
    }

    public static bool TryAcquireNonBlocking(
        SemaphoreSlim decodeLimiter,
        SemaphoreSlim decodeGate,
        CancellationToken cancellationToken,
        out DecodeLockHandle decodeLock)
    {
        ArgumentNullException.ThrowIfNull(decodeLimiter);
        ArgumentNullException.ThrowIfNull(decodeGate);

        decodeLock = default;
        var limiterTaken = false;
        try
        {
            limiterTaken = decodeLimiter.Wait(0, cancellationToken);
            if (!limiterTaken)
                return false;

            if (!decodeGate.Wait(0, cancellationToken))
                return false;

            decodeLock = new DecodeLockHandle(decodeLimiter, decodeGate);
            limiterTaken = false;
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        finally
        {
            if (limiterTaken)
                decodeLimiter.Release();
        }
    }

    internal readonly struct DecodeLockHandle : IDisposable
    {
        private readonly SemaphoreSlim? _decodeLimiter;
        private readonly SemaphoreSlim? _decodeGate;

        public DecodeLockHandle(SemaphoreSlim decodeLimiter, SemaphoreSlim decodeGate)
        {
            _decodeLimiter = decodeLimiter;
            _decodeGate = decodeGate;
        }

        public void Dispose()
        {
            if (_decodeLimiter is null || _decodeGate is null)
                return;

            _decodeGate.Release();
            _decodeLimiter.Release();
        }
    }
}

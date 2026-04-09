using System;
using System.Threading;
using System.Threading.Tasks;

namespace FC_Revolution.UI.AppServices;

public sealed class BackendStateMirror : IBackendStateMirror
{
    private readonly IArcadeRuntimeContractAdapter _adapter;
    private readonly IBackendStateSyncClient? _syncClient;
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private int _pendingSync;

    public BackendStateMirror(IArcadeRuntimeContractAdapter adapter, IBackendStateSyncClient? syncClient)
    {
        _adapter = adapter;
        _syncClient = syncClient;
    }

    public bool IsEnabled => _syncClient != null;

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        if (_syncClient == null)
            return;

        Interlocked.Exchange(ref _pendingSync, 1);

        if (!await _syncGate.WaitAsync(0, cancellationToken))
            return;

        try
        {
            while (Interlocked.Exchange(ref _pendingSync, 0) == 1)
            {
                var roms = _adapter.GetRomSummaries();
                var sessions = _adapter.GetSessionSummaries();
                await _syncClient.PushRomsAsync(roms, cancellationToken);
                await _syncClient.PushSessionsAsync(sessions, cancellationToken);
            }
        }
        catch
        {
            // Backend is optional during the transition period. Failed mirrors must not affect UI runtime.
        }
        finally
        {
            _syncGate.Release();
        }
    }

    public void Dispose()
    {
        _syncGate.Dispose();
        if (_syncClient is IDisposable disposable)
            disposable.Dispose();
    }
}

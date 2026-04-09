using System;
using System.Threading;
using System.Threading.Tasks;

namespace FC_Revolution.UI.AppServices;

public interface IBackendStateMirror : IDisposable
{
    bool IsEnabled { get; }
    Task SyncAsync(CancellationToken cancellationToken = default);
}

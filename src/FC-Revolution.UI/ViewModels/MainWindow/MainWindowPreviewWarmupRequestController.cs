using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record PreviewWarmupRequestDecision(
    bool ShouldStartProcessor,
    bool ShouldCancelActiveWarmup);

internal sealed class MainWindowPreviewWarmupRequestController
{
    private readonly object _sync = new();
    private bool _isProcessorRunning;
    private bool _hasPendingRequest;
    private RomLibraryItem? _pendingPriorityRom;

    public PreviewWarmupRequestDecision Enqueue(RomLibraryItem? priorityRom)
    {
        lock (_sync)
        {
            _pendingPriorityRom = priorityRom;
            _hasPendingRequest = true;

            if (!_isProcessorRunning)
            {
                _isProcessorRunning = true;
                return new PreviewWarmupRequestDecision(
                    ShouldStartProcessor: true,
                    ShouldCancelActiveWarmup: false);
            }

            return new PreviewWarmupRequestDecision(
                ShouldStartProcessor: false,
                ShouldCancelActiveWarmup: true);
        }
    }

    public bool TryDequeue(out RomLibraryItem? priorityRom)
    {
        lock (_sync)
        {
            if (!_hasPendingRequest)
            {
                _isProcessorRunning = false;
                priorityRom = null;
                return false;
            }

            priorityRom = _pendingPriorityRom;
            _pendingPriorityRom = null;
            _hasPendingRequest = false;
            return true;
        }
    }
}

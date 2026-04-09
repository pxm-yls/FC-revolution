using System;
using System.Collections.Generic;
using System.Threading;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowPreviewCleanupController
{
    public void ClearPreviewFrames(IEnumerable<RomLibraryItem> romLibrary, bool clearPreviewAvailability)
    {
        foreach (var item in romLibrary)
        {
            item.ClearPreviewFrames();
            if (clearPreviewAvailability)
                item.HasPreview = false;
        }
    }

    public void ReleasePreviewRuntime(
        CancellationTokenSource? warmupCts,
        IEnumerable<RomLibraryItem> romLibrary,
        Action releaseAllSmoothPlayback,
        Action stopPreviewTimer,
        Action clearCurrentPreviewBitmap)
    {
        warmupCts?.Cancel();
        warmupCts?.Dispose();
        releaseAllSmoothPlayback();
        stopPreviewTimer();
        clearCurrentPreviewBitmap();
        ClearPreviewFrames(romLibrary, clearPreviewAvailability: false);
    }
}

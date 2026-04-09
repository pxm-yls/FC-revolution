using System;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowPreviewSelectionController
{
    public void ApplyCurrentRomSelection(
        RomLibraryItem? currentRom,
        bool hasLoadedCurrentRomPreview,
        bool hasAnyAnimatedPreview,
        Action<RomLibraryItem?> syncCurrentPreviewBitmap,
        Action startPreviewPlayback,
        Action stopPreviewPlayback,
        Action updateDiscDisplayBitmap,
        Action<RomLibraryItem?> requestPreviewWarmup)
    {
        syncCurrentPreviewBitmap(currentRom);

        if (hasLoadedCurrentRomPreview && hasAnyAnimatedPreview)
            startPreviewPlayback();
        else if (!hasAnyAnimatedPreview)
            stopPreviewPlayback();

        updateDiscDisplayBitmap();
        requestPreviewWarmup(currentRom);
    }

    public void HandleEmptyLibrary(
        Action stopPreviewPlayback,
        Action clearCurrentPreviewBitmap)
    {
        stopPreviewPlayback();
        clearCurrentPreviewBitmap();
    }
}

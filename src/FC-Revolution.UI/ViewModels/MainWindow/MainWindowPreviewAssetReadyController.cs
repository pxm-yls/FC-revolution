using System;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowPreviewAssetReadyController
{
    public void ApplyPreviewAssetReady(
        RomLibraryItem rom,
        string previewPlaybackPath,
        bool isCurrentRom,
        bool syncCurrentPreviewFrame,
        Action<RomLibraryItem> tryLoadItemPreview,
        Action refreshCurrentRomState,
        Action syncCurrentPreviewBitmap)
    {
        rom.UpdatePreviewFilePath(previewPlaybackPath);
        rom.HasPreview = true;
        rom.ClearPreviewFrames();
        tryLoadItemPreview(rom);

        if (!isCurrentRom)
            return;

        refreshCurrentRomState();
        if (!syncCurrentPreviewFrame)
            return;

        rom.SyncPreviewFrame(0);
        syncCurrentPreviewBitmap();
    }
}

using System;
using System.Collections.Generic;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record PreviewTickResult(
    int NextTickCounter,
    bool ShouldSyncCurrentPreviewBitmap,
    string? DebugText,
    bool ShouldRefreshDiscBitmap);

internal sealed class MainWindowPreviewTickController
{
    public PreviewTickResult OnPreviewTick(
        long elapsedMilliseconds,
        int currentTickCounter,
        bool showDebugStatus,
        bool isPreviewTimerEnabled,
        RomLibraryItem? currentRom,
        IReadOnlyList<RomLibraryItem> previewAnimationTargets,
        bool shouldShowLiveGameOnDisc)
    {
        var nextTickCounter = currentTickCounter + 1;
        var shouldSyncCurrentPreviewBitmap = false;
        foreach (var rom in previewAnimationTargets)
        {
            if (!rom.IsPreviewAnimated)
                continue;

            rom.SyncPreviewFrame(elapsedMilliseconds);
            if (ReferenceEquals(rom, currentRom))
                shouldSyncCurrentPreviewBitmap = true;
        }

        var debugText = showDebugStatus
            ? BuildDebugText(nextTickCounter, elapsedMilliseconds, isPreviewTimerEnabled, currentRom)
            : null;
        var shouldRefreshDiscBitmap = currentRom?.CurrentPreviewBitmap != null && !shouldShowLiveGameOnDisc;

        return new PreviewTickResult(
            NextTickCounter: nextTickCounter,
            ShouldSyncCurrentPreviewBitmap: shouldSyncCurrentPreviewBitmap,
            DebugText: debugText,
            ShouldRefreshDiscBitmap: shouldRefreshDiscBitmap);
    }

    private static string BuildDebugText(
        int previewTickCounter,
        long elapsedMilliseconds,
        bool isPreviewTimerEnabled,
        RomLibraryItem? currentRom)
    {
        if (currentRom?.HasLoadedPreview == true)
        {
            var intervalMs = Math.Max(1, currentRom.PreviewIntervalMs);
            var frameCount = Math.Max(1, currentRom.PreviewFrameCount);
            var targetFrame = (int)((elapsedMilliseconds / intervalMs) % frameCount);
            return
                $"预览调试 tick={previewTickCounter} timer={isPreviewTimerEnabled} elapsed={elapsedMilliseconds}ms target={targetFrame}/{frameCount - 1} interval={intervalMs}ms bitmap={(currentRom.CurrentPreviewBitmap != null ? "yes" : "no")}\n" +
                currentRom.PreviewDebugInfo;
        }

        return $"预览调试 tick={previewTickCounter} timer={isPreviewTimerEnabled} elapsed={elapsedMilliseconds}ms currentRom={(currentRom?.DisplayName ?? "-")} loaded={(currentRom?.HasLoadedPreview == true ? "yes" : "no")}";
    }
}

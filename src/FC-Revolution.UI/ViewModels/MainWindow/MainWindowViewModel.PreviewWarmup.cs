using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

public partial class MainWindowViewModel
{
    private void RequestPreviewWarmup(RomLibraryItem? priorityRom)
    {
        if (!_canWarmPreviews || _romLibrary.Count == 0 || _isShuttingDown)
            return;

        var decision = _previewWarmupRequestController.Enqueue(priorityRom);
        if (decision.ShouldCancelActiveWarmup)
            CancelActivePreviewWarmup();
        if (decision.ShouldStartProcessor)
            _ = ProcessPreviewWarmupRequestsAsync();
    }

    private void CancelActivePreviewWarmup()
    {
        Interlocked.CompareExchange(ref _previewWarmupCts, null, null)?.Cancel();
    }

    private async Task ProcessPreviewWarmupRequestsAsync()
    {
        while (_previewWarmupRequestController.TryDequeue(out var nextPriorityRom))
        {
            if (!_canWarmPreviews || _romLibrary.Count == 0 || _isShuttingDown)
                continue;

            await WarmPreviewFramesAsync(_romLibrary.ToList(), nextPriorityRom);
        }
    }

    private async Task WarmPreviewFramesAsync(IReadOnlyList<RomLibraryItem> items, RomLibraryItem? priorityRom)
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var previous = Interlocked.Exchange(ref _previewWarmupCts, cts);
        previous?.Cancel();
        previous?.Dispose();

        await _previewWarmupController.WarmPreviewFramesAsync(
            token,
            items,
            priorityRom,
            IsShelfLayoutMode,
            IsKaleidoscopeMode,
            _shelfVisibleRowCount,
            ShelfWarmExtraRows,
            ShelfColumns,
            KaleidoscopePageSize,
            MaxWarmedPreviews,
            GetVisibleShelfPreviewTargets(includeWarmRows: true),
            GetVisibleKaleidoscopePreviewTargets(),
            PathsEqual,
            item => item.HasPreview,
            item => item.HasLoadedPreview,
            item => item.KnownPreviewIsAnimated.HasValue
                ? item.KnownPreviewIsAnimated.Value || item.KnownPreviewFrameCount > 0
                : File.Exists(item.PreviewFilePath),
            async (allItems, keepAlivePaths, selectedPriorityRom) =>
                await Dispatcher.UIThread.InvokeAsync(() => TrimLoadedPreviewCache(allItems, keepAlivePaths, selectedPriorityRom)),
            async (item, cancellationToken) =>
                await _previewWarmupItemController.WarmItemAsync(
                    item,
                    cancellationToken,
                    rom => _previewStreamController.LoadPreviewStream(
                        rom.Path,
                        ResolvePreviewPlaybackPath,
                        GetPreviewPath,
                        (legacyPath, migratedPath) => _previewGenerationController.UpgradeLegacyPreview(legacyPath, migratedPath)),
                    async action => await Dispatcher.UIThread.InvokeAsync(action),
                    rom => ReferenceEquals(CurrentRom, rom),
                    interval => _previewTimer.Interval = interval,
                    (loadedRom, restartPlayback) => ApplyLoadedPreviewPlaybackState(loadedRom, restartPlayback),
                    loadedRom => loadedRom.ClearPreviewFrames(),
                    (romPath, isAnimated, intervalMs, frameCount, persistToken) =>
                        _ = Task.Run(() => RomConfigProfile.SavePreviewMetadata(romPath, isAnimated, intervalMs, frameCount), persistToken)),
            async () =>
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_romLibrary.Any(rom => rom.IsPreviewAnimated))
                        StartPreviewPlayback();
                    else
                        _previewTimer.Stop();
                });
            },
            PrimeAnimatedPreviewsAsync);
    }

    private IReadOnlyList<RomLibraryItem> GetPreviewAnimationTargets()
        => _previewPlaybackPolicyController.BuildPreviewAnimationTargets(
            IsCarouselMode,
            IsKaleidoscopeMode,
            _isShelfScrolling,
            CurrentRom,
            PreviousRom,
            NextRom,
            GetVisibleShelfPreviewTargets(includeWarmRows: false),
            GetVisibleKaleidoscopePreviewTargets(),
            item => item.IsPreviewAnimated);

    private IReadOnlyList<RomLibraryItem> GetVisibleShelfPreviewTargets(bool includeWarmRows)
        => _previewPlaybackPolicyController.BuildVisibleShelfPreviewTargets(
            _romLibrary,
            _shelfVisibleStartRow,
            _shelfVisibleRowCount,
            ShelfColumns,
            ShelfWarmExtraRows,
            includeWarmRows);

    private IReadOnlyList<RomLibraryItem> GetVisibleKaleidoscopePreviewTargets()
        => _previewPlaybackPolicyController.BuildVisibleKaleidoscopePreviewTargets(_kaleidoscopeSlots.Select(slot => slot.Rom));

    private async Task PrimeAnimatedPreviewsAsync()
        => await _previewWarmupController.PrimeAnimatedPreviewsAsync(
            IsCarouselMode,
            IsShelfLayoutMode,
            IsKaleidoscopeMode,
            _isShelfScrolling,
            CurrentRom,
            PreviousRom,
            NextRom,
            GetVisibleShelfPreviewTargets(includeWarmRows: false),
            GetVisibleKaleidoscopePreviewTargets(),
            _romLibrary.ToList(),
            MaxShelfSmoothPlayback,
            KaleidoscopePageSize,
            MaxMemoryAnimatedPreviews,
            item => item.HasLoadedPreview,
            item => item.IsPreviewAnimated,
            item => item.IsMemoryPreview,
            item => item.EnableSmoothPlayback(),
            item => item.DisableSmoothPlayback(),
            item => item.DisableMemoryPlayback());

    private void ReleaseAllSmoothPlayback()
        => _previewWarmupController.ReleaseAllSmoothPlayback(
            _romLibrary.ToList(),
            item => item.IsMemoryPreview,
            item => item.DisableSmoothPlayback(),
            item => item.DisableMemoryPlayback());

    private void TrimLoadedPreviewCache(IReadOnlyList<RomLibraryItem> items, HashSet<string> keepAlivePaths, RomLibraryItem? priorityRom)
        => _previewWarmupController.TrimLoadedPreviewCache(
            items,
            keepAlivePaths,
            priorityRom,
            MaxLoadedPreviewCache,
            item => item.HasLoadedPreview,
            PathsEqual,
            item => item.ClearPreviewFrames());
}

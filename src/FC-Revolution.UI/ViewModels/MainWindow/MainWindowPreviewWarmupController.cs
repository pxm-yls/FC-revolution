using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowPreviewWarmupController
{
    private readonly MainWindowPreviewPlaybackPolicyController _playbackPolicyController;

    public MainWindowPreviewWarmupController(MainWindowPreviewPlaybackPolicyController playbackPolicyController)
    {
        _playbackPolicyController = playbackPolicyController;
    }

    public async Task WarmPreviewFramesAsync(
        CancellationToken token,
        IReadOnlyList<RomLibraryItem> items,
        RomLibraryItem? priorityRom,
        bool isShelfLayoutMode,
        bool isKaleidoscopeMode,
        int shelfVisibleRowCount,
        int shelfWarmExtraRows,
        int shelfColumns,
        int kaleidoscopePageSize,
        int maxWarmedPreviews,
        IReadOnlyList<RomLibraryItem> visibleShelfWarmTargets,
        IReadOnlyList<RomLibraryItem> visibleKaleidoscopeTargets,
        Func<string, string, bool> pathsEqual,
        Func<RomLibraryItem, bool> hasPreviewSelector,
        Func<RomLibraryItem, bool> hasLoadedPreviewSelector,
        Func<RomLibraryItem, bool> isPreviewCandidateSelector,
        Func<IReadOnlyList<RomLibraryItem>, HashSet<string>, RomLibraryItem?, Task> trimCacheAsync,
        Func<RomLibraryItem, CancellationToken, Task> warmItemAsync,
        Func<Task> updatePlaybackStateAsync,
        Func<Task> primeAnimatedPreviewsAsync)
    {
        var keepAlivePaths = _playbackPolicyController.BuildWarmKeepAlivePaths(
            isShelfLayoutMode,
            isKaleidoscopeMode,
            items,
            visibleShelfWarmTargets,
            visibleKaleidoscopeTargets,
            priorityRom,
            maxWarmedPreviews,
            pathsEqual);

        await trimCacheAsync(items, keepAlivePaths, priorityRom);

        var warmTargetLimit = _playbackPolicyController.BuildWarmTargetLimit(
            isShelfLayoutMode,
            isKaleidoscopeMode,
            shelfVisibleRowCount,
            shelfWarmExtraRows,
            shelfColumns,
            kaleidoscopePageSize,
            maxWarmedPreviews);
        var warmTargets = _playbackPolicyController.BuildWarmTargets(
            items,
            keepAlivePaths,
            warmTargetLimit,
            hasPreviewSelector,
            hasLoadedPreviewSelector,
            isPreviewCandidateSelector);

        var parallelism = Math.Max(1, maxWarmedPreviews);
        using var semaphore = new SemaphoreSlim(parallelism, parallelism);
        var tasks = warmTargets.Select(async item =>
        {
            await semaphore.WaitAsync(token);
            try
            {
                await warmItemAsync(item, token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                semaphore.Release();
            }
        });

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
        }

        await updatePlaybackStateAsync();
        await primeAnimatedPreviewsAsync();
    }

    public async Task PrimeAnimatedPreviewsAsync(
        bool isCarouselMode,
        bool isShelfLayoutMode,
        bool isKaleidoscopeMode,
        bool isShelfScrolling,
        RomLibraryItem? currentRom,
        RomLibraryItem? previousRom,
        RomLibraryItem? nextRom,
        IReadOnlyList<RomLibraryItem> shelfTargets,
        IReadOnlyList<RomLibraryItem> kaleidoscopeTargets,
        IReadOnlyList<RomLibraryItem> allRoms,
        int maxShelfSmoothPlayback,
        int kaleidoscopePageSize,
        int maxMemoryAnimatedPreviews,
        Func<RomLibraryItem, bool> isLoadedSelector,
        Func<RomLibraryItem, bool> isAnimatedSelector,
        Func<RomLibraryItem, bool> isMemoryPreviewSelector,
        Action<RomLibraryItem> enableSmoothPlayback,
        Action<RomLibraryItem> disableSmoothPlayback,
        Action<RomLibraryItem> disableMemoryPlayback)
    {
        if (isShelfLayoutMode && isShelfScrolling)
            return;

        var previewAnimationTargets = _playbackPolicyController.BuildPreviewAnimationTargets(
            isCarouselMode,
            isKaleidoscopeMode,
            isShelfScrolling,
            currentRom,
            previousRom,
            nextRom,
            shelfTargets,
            kaleidoscopeTargets,
            isAnimatedSelector);
        var targetPaths = _playbackPolicyController.BuildSmoothPlaybackTargetPaths(
            previewAnimationTargets,
            isShelfLayoutMode,
            isKaleidoscopeMode,
            maxShelfSmoothPlayback,
            kaleidoscopePageSize,
            maxMemoryAnimatedPreviews,
            isLoadedSelector,
            isAnimatedSelector);

        await Task.Run(() =>
        {
            foreach (var rom in allRoms)
            {
                if (!isLoadedSelector(rom))
                    continue;

                if (targetPaths.Contains(rom.Path))
                {
                    enableSmoothPlayback(rom);
                }
                else
                {
                    disableSmoothPlayback(rom);
                    if (isMemoryPreviewSelector(rom))
                        disableMemoryPlayback(rom);
                }
            }
        });
    }

    public void ReleaseAllSmoothPlayback(
        IReadOnlyList<RomLibraryItem> allRoms,
        Func<RomLibraryItem, bool> isMemoryPreviewSelector,
        Action<RomLibraryItem> disableSmoothPlayback,
        Action<RomLibraryItem> disableMemoryPlayback)
    {
        foreach (var rom in allRoms)
        {
            disableSmoothPlayback(rom);
            if (isMemoryPreviewSelector(rom))
                disableMemoryPlayback(rom);
        }
    }

    public void TrimLoadedPreviewCache(
        IReadOnlyList<RomLibraryItem> items,
        IReadOnlySet<string> keepAlivePaths,
        RomLibraryItem? priorityRom,
        int maxLoadedPreviewCache,
        Func<RomLibraryItem, bool> isLoadedSelector,
        Func<string, string, bool> pathsEqual,
        Action<RomLibraryItem> clearPreviewFrames)
    {
        var evictionCandidates = _playbackPolicyController.BuildTrimEvictionCandidates(
            items,
            keepAlivePaths,
            priorityRom,
            maxLoadedPreviewCache,
            isLoadedSelector,
            pathsEqual);
        foreach (var item in evictionCandidates)
            clearPreviewFrames(item);
    }
}

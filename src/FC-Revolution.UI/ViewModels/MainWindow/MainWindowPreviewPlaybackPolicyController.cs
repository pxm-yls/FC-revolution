using System;
using System.Collections.Generic;
using System.Linq;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowPreviewPlaybackPolicyController
{
    public HashSet<string> BuildWarmKeepAlivePaths(
        bool isShelfLayoutMode,
        bool isKaleidoscopeMode,
        IReadOnlyList<RomLibraryItem> items,
        IReadOnlyList<RomLibraryItem> visibleShelfWarmTargets,
        IReadOnlyList<RomLibraryItem> visibleKaleidoscopeTargets,
        RomLibraryItem? priorityRom,
        int maxWarmedPreviews,
        Func<string, string, bool> pathsEqual)
    {
        var keepAlivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (isShelfLayoutMode)
        {
            foreach (var item in visibleShelfWarmTargets)
                keepAlivePaths.Add(item.Path);
            return keepAlivePaths;
        }

        if (isKaleidoscopeMode)
        {
            foreach (var item in visibleKaleidoscopeTargets)
                keepAlivePaths.Add(item.Path);
            return keepAlivePaths;
        }

        if (priorityRom == null)
            return keepAlivePaths;

        var currentIndex = items
            .Select((item, index) => new { item, index })
            .FirstOrDefault(entry => pathsEqual(entry.item.Path, priorityRom.Path))?.index ?? -1;
        for (var offset = 0; offset < items.Count && keepAlivePaths.Count < maxWarmedPreviews; offset++)
        {
            foreach (var index in new[] { currentIndex + offset, currentIndex - offset })
            {
                if (index < 0 || index >= items.Count)
                    continue;

                keepAlivePaths.Add(items[index].Path);
                if (keepAlivePaths.Count >= maxWarmedPreviews)
                    break;
            }
        }

        return keepAlivePaths;
    }

    public int BuildWarmTargetLimit(
        bool isShelfLayoutMode,
        bool isKaleidoscopeMode,
        int shelfVisibleRowCount,
        int shelfWarmExtraRows,
        int shelfColumns,
        int kaleidoscopePageSize,
        int maxWarmedPreviews)
    {
        return isShelfLayoutMode
            ? Math.Max(1, (shelfVisibleRowCount + shelfWarmExtraRows * 2) * shelfColumns)
            : isKaleidoscopeMode
                ? kaleidoscopePageSize
                : maxWarmedPreviews;
    }

    public IReadOnlyList<RomLibraryItem> BuildWarmTargets(
        IReadOnlyList<RomLibraryItem> items,
        IReadOnlySet<string> keepAlivePaths,
        int warmTargetLimit,
        Func<RomLibraryItem, bool> hasPreviewSelector,
        Func<RomLibraryItem, bool> hasLoadedPreviewSelector,
        Func<RomLibraryItem, bool> isPreviewCandidateSelector)
    {
        return items
            .Where(item =>
                hasPreviewSelector(item) &&
                !hasLoadedPreviewSelector(item) &&
                isPreviewCandidateSelector(item))
            .OrderByDescending(item => keepAlivePaths.Contains(item.Path))
            .Take(warmTargetLimit)
            .ToList();
    }

    public IReadOnlyList<RomLibraryItem> BuildVisibleShelfPreviewTargets(
        IReadOnlyList<RomLibraryItem> romLibrary,
        int shelfVisibleStartRow,
        int shelfVisibleRowCount,
        int shelfColumns,
        int shelfWarmExtraRows,
        bool includeWarmRows)
    {
        if (romLibrary.Count == 0)
            return Array.Empty<RomLibraryItem>();

        var startRow = Math.Max(0, shelfVisibleStartRow - (includeWarmRows ? shelfWarmExtraRows : 0));
        var endRow = shelfVisibleStartRow + Math.Max(1, shelfVisibleRowCount) - 1 + (includeWarmRows ? shelfWarmExtraRows : 0);
        var startIndex = startRow * shelfColumns;
        var endIndexExclusive = Math.Min(romLibrary.Count, (endRow + 1) * shelfColumns);
        if (startIndex >= endIndexExclusive)
            return Array.Empty<RomLibraryItem>();

        return romLibrary
            .Skip(startIndex)
            .Take(endIndexExclusive - startIndex)
            .ToList();
    }

    public IReadOnlyList<RomLibraryItem> BuildVisibleKaleidoscopePreviewTargets(IEnumerable<RomLibraryItem?> slots) =>
        slots.Where(static rom => rom != null).Select(static rom => rom!).ToList();

    public IReadOnlyList<RomLibraryItem> BuildPreviewAnimationTargets(
        bool isCarouselMode,
        bool isKaleidoscopeMode,
        bool isShelfScrolling,
        RomLibraryItem? currentRom,
        RomLibraryItem? previousRom,
        RomLibraryItem? nextRom,
        IReadOnlyList<RomLibraryItem> shelfTargets,
        IReadOnlyList<RomLibraryItem> kaleidoscopeTargets,
        Func<RomLibraryItem, bool> isAnimatedSelector)
    {
        if (isCarouselMode)
        {
            if (currentRom == null)
                return Array.Empty<RomLibraryItem>();

            var targets = new List<RomLibraryItem>(3) { currentRom };
            if (previousRom != null)
                targets.Add(previousRom);
            if (nextRom != null)
                targets.Add(nextRom);
            return targets;
        }

        if (isKaleidoscopeMode)
            return kaleidoscopeTargets;

        if (isShelfScrolling)
            return Array.Empty<RomLibraryItem>();

        return shelfTargets.Where(isAnimatedSelector).ToList();
    }

    public HashSet<string> BuildSmoothPlaybackTargetPaths(
        IReadOnlyList<RomLibraryItem> previewAnimationTargets,
        bool isShelfLayoutMode,
        bool isKaleidoscopeMode,
        int maxShelfSmoothPlayback,
        int kaleidoscopePageSize,
        int maxMemoryAnimatedPreviews,
        Func<RomLibraryItem, bool> isLoadedSelector,
        Func<RomLibraryItem, bool> isAnimatedSelector)
    {
        var maxCount = isShelfLayoutMode
            ? maxShelfSmoothPlayback
            : isKaleidoscopeMode
                ? kaleidoscopePageSize
                : maxMemoryAnimatedPreviews;

        return previewAnimationTargets
            .Where(item => isLoadedSelector(item) && isAnimatedSelector(item))
            .Take(maxCount)
            .Select(item => item.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<RomLibraryItem> BuildTrimEvictionCandidates(
        IReadOnlyList<RomLibraryItem> items,
        IReadOnlySet<string> keepAlivePaths,
        RomLibraryItem? priorityRom,
        int maxLoadedPreviewCache,
        Func<RomLibraryItem, bool> isLoadedSelector,
        Func<string, string, bool> pathsEqual)
    {
        var loaded = items.Where(isLoadedSelector).ToList();
        if (loaded.Count <= maxLoadedPreviewCache)
            return Array.Empty<RomLibraryItem>();

        var currentIndex = priorityRom == null
            ? -1
            : items
                .Select((item, index) => new { item, index })
                .FirstOrDefault(entry => pathsEqual(entry.item.Path, priorityRom.Path))?.index ?? -1;

        var orderedCandidates = loaded
            .Where(item => !keepAlivePaths.Contains(item.Path))
            .OrderByDescending(item =>
            {
                if (currentIndex < 0)
                    return int.MaxValue;

                var index = items
                    .Select((candidate, idx) => new { candidate, idx })
                    .FirstOrDefault(entry => pathsEqual(entry.candidate.Path, item.Path))?.idx ?? int.MaxValue;
                return index == int.MaxValue ? int.MaxValue : Math.Abs(index - currentIndex);
            })
            .ToList();

        var toReleaseCount = Math.Max(0, loaded.Count - maxLoadedPreviewCache);
        return orderedCandidates.Take(toReleaseCount).ToList();
    }
}

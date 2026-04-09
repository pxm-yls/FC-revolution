using System;
using System.Collections.Generic;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct MainWindowKaleidoscopePageSelectionDecision(
    int PageIndex,
    bool KeepCurrentSelection,
    RomLibraryItem? FallbackSelection);

internal sealed class MainWindowLibraryNavigationController
{
    public RomLibraryItem? GetNeighbor(
        IReadOnlyList<RomLibraryItem> visibleRomLibrary,
        RomLibraryItem? currentRom,
        int offset)
    {
        if (currentRom == null || offset == 0)
            return null;

        var currentIndex = IndexOf(visibleRomLibrary, currentRom);
        if (currentIndex < 0)
            return null;

        var targetIndex = currentIndex + offset;
        return targetIndex >= 0 && targetIndex < visibleRomLibrary.Count
            ? visibleRomLibrary[targetIndex]
            : null;
    }

    public string BuildShelfScrollSummary(
        int shelfSlotCount,
        IReadOnlyList<RomLibraryItem> visibleRomLibrary,
        RomLibraryItem? currentRom,
        int shelfColumns,
        int shelfRowsPerPage)
    {
        if (shelfColumns <= 0)
            throw new ArgumentOutOfRangeException(nameof(shelfColumns));
        if (shelfRowsPerPage <= 0)
            throw new ArgumentOutOfRangeException(nameof(shelfRowsPerPage));
        if (shelfSlotCount == 0)
            return "第 0 / 0 页";

        var itemsPerPage = shelfColumns * shelfRowsPerPage;
        var totalPages = Math.Max(1, (int)Math.Ceiling(shelfSlotCount / (double)itemsPerPage));
        var currentIndex = currentRom == null
            ? 0
            : Math.Max(0, IndexOf(visibleRomLibrary, currentRom));
        var currentPage = Math.Min(totalPages, currentIndex / itemsPerPage + 1);
        return $"第 {currentPage} / {totalPages} 页";
    }

    public int NormalizeKaleidoscopePageIndex(int romCount, int pageSize, int requestedIndex)
    {
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize));

        var pageCount = (int)Math.Ceiling(romCount / (double)pageSize);
        if (pageCount <= 0)
            return 0;

        return Math.Clamp(requestedIndex, 0, pageCount - 1);
    }

    public int? ResolveKaleidoscopePageForCurrentRom(
        IReadOnlyList<RomLibraryItem> visibleRomLibrary,
        RomLibraryItem? currentRom,
        int pageSize)
    {
        if (currentRom == null || visibleRomLibrary.Count == 0)
            return null;

        var currentIndex = IndexOf(visibleRomLibrary, currentRom);
        if (currentIndex < 0)
            return null;

        return currentIndex / pageSize;
    }

    public MainWindowKaleidoscopePageSelectionDecision DecideKaleidoscopePageSelection(
        IReadOnlyList<RomLibraryItem> visibleRomLibrary,
        int pageSize,
        int requestedPageIndex,
        RomLibraryItem? currentRom,
        bool preserveSelection)
    {
        if (visibleRomLibrary.Count == 0)
            return new MainWindowKaleidoscopePageSelectionDecision(0, KeepCurrentSelection: false, FallbackSelection: null);

        var normalizedPageIndex = NormalizeKaleidoscopePageIndex(visibleRomLibrary.Count, pageSize, requestedPageIndex);
        if (preserveSelection && currentRom != null)
        {
            var currentIndex = IndexOf(visibleRomLibrary, currentRom);
            if (currentIndex >= 0 && currentIndex / pageSize == normalizedPageIndex)
            {
                return new MainWindowKaleidoscopePageSelectionDecision(
                    normalizedPageIndex,
                    KeepCurrentSelection: true,
                    FallbackSelection: null);
            }
        }

        var firstIndex = normalizedPageIndex * pageSize;
        var fallbackSelection = firstIndex >= 0 && firstIndex < visibleRomLibrary.Count
            ? visibleRomLibrary[firstIndex]
            : null;
        return new MainWindowKaleidoscopePageSelectionDecision(
            normalizedPageIndex,
            KeepCurrentSelection: false,
            fallbackSelection);
    }

    private static int IndexOf(IReadOnlyList<RomLibraryItem> items, RomLibraryItem target)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], target))
                return i;
        }

        return -1;
    }
}

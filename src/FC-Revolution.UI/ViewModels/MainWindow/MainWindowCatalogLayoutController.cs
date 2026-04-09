using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowCatalogLayoutController
{
    private static readonly (double X, double Y, double Width, double Height)[] KaleidoscopeLayouts =
    [
        (500, 8, 192, 128),
        (680, 132, 156, 122),
        (680, 346, 156, 122),
        (500, 464, 192, 128),
        (248, 464, 192, 128),
        (104, 346, 156, 122),
        (104, 132, 156, 122),
        (248, 8, 192, 128)
    ];

    public List<RomLibraryItem> BuildVisibleItems(
        IReadOnlyList<RomLibraryItem> allRomLibrary,
        string librarySearchText,
        RomSortField sortField,
        bool sortDescending)
    {
        var visibleItems = allRomLibrary
            .Where(item => MatchesLibrarySearch(item, librarySearchText))
            .ToList();
        ApplySort(visibleItems, sortField, sortDescending);
        return visibleItems;
    }

    public (IReadOnlyList<ShelfSlotItem> Slots, IReadOnlyList<ShelfRowItem> Rows) BuildShelfLayout(
        IReadOnlyList<RomLibraryItem> visibleRomLibrary,
        int shelfColumns)
    {
        var slots = new List<ShelfSlotItem>();
        var rows = new List<ShelfRowItem>();

        for (var i = 0; i < visibleRomLibrary.Count; i++)
            slots.Add(new ShelfSlotItem(i, visibleRomLibrary[i]));

        var remainder = slots.Count % shelfColumns;
        if (remainder != 0)
        {
            for (var i = remainder; i < shelfColumns; i++)
                slots.Add(new ShelfSlotItem(slots.Count, null));
        }

        var rowCount = (int)Math.Ceiling(slots.Count / (double)shelfColumns);
        for (var row = 0; row < rowCount; row++)
            rows.Add(new ShelfRowItem(row));

        return (slots, rows);
    }

    public int NormalizeKaleidoscopePageIndex(int romCount, int kaleidoscopePageSize, int requestedIndex)
    {
        var pageCount = (int)Math.Ceiling(romCount / (double)kaleidoscopePageSize);
        if (pageCount <= 0)
            return 0;

        return Math.Clamp(requestedIndex, 0, pageCount - 1);
    }

    public IReadOnlyList<KaleidoscopePageItem> BuildKaleidoscopePages(
        int romCount,
        int kaleidoscopePageSize,
        int currentPageIndex)
    {
        var pageCount = (int)Math.Ceiling(romCount / (double)kaleidoscopePageSize);
        var pages = new List<KaleidoscopePageItem>(pageCount);
        for (var i = 0; i < pageCount; i++)
            pages.Add(new KaleidoscopePageItem(i, (i + 1).ToString()) { IsCurrent = i == currentPageIndex });

        return pages;
    }

    public IReadOnlyList<KaleidoscopeSlotItem> BuildKaleidoscopeSlots(
        IReadOnlyList<RomLibraryItem> visibleRomLibrary,
        int kaleidoscopePageSize,
        int currentPageIndex)
    {
        var slots = new List<KaleidoscopeSlotItem>(kaleidoscopePageSize);
        if (visibleRomLibrary.Count == 0)
            return slots;

        var startIndex = currentPageIndex * kaleidoscopePageSize;
        for (var i = 0; i < kaleidoscopePageSize; i++)
        {
            var romIndex = startIndex + i;
            var rom = romIndex < visibleRomLibrary.Count ? visibleRomLibrary[romIndex] : null;
            var layout = KaleidoscopeLayouts[i];
            slots.Add(new KaleidoscopeSlotItem(i, rom, layout.X, layout.Y, layout.Width, layout.Height));
        }

        return slots;
    }

    public bool MatchesLibrarySearch(RomLibraryItem item, string librarySearchText)
    {
        if (string.IsNullOrWhiteSpace(librarySearchText))
            return true;

        var query = librarySearchText.Trim();
        return item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               Path.GetFileNameWithoutExtension(item.Name).Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public void ApplySort(List<RomLibraryItem> items, RomSortField sortField, bool sortDescending)
    {
        IOrderedEnumerable<RomLibraryItem> ordered = sortField switch
        {
            RomSortField.Size => items.OrderBy(item => item.FileSizeBytes),
            RomSortField.ImportedAt => items.OrderBy(item => item.ImportedAtUtc),
            _ => items.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase),
        };

        if (sortField != RomSortField.Name)
            ordered = ordered.ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase);

        var result = (sortDescending ? ordered.Reverse() : ordered).ToList();
        items.Clear();
        items.AddRange(result);
    }
}

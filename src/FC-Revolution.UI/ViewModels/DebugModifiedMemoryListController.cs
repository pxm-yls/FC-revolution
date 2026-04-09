using System;
using System.Collections.Generic;
using System.Linq;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct DebugModifiedMemoryPageState(
    int PageIndex,
    int PageCount,
    IReadOnlyList<ModifiedMemoryEntry> VisibleEntries);

internal readonly record struct DebugModifiedMemoryUpsertResult(
    ModifiedMemoryEntry Entry,
    int NextPageIndex);

internal static class DebugModifiedMemoryListController
{
    public static int GetPageCount(int entryCount, int pageSize)
    {
        var normalizedPageSize = Math.Max(1, pageSize);
        var normalizedEntryCount = Math.Max(0, entryCount);
        return Math.Max(1, (normalizedEntryCount + normalizedPageSize - 1) / normalizedPageSize);
    }

    public static DebugModifiedMemoryPageState BuildPageState(
        IReadOnlyList<ModifiedMemoryEntry> entries,
        int pageIndex,
        int pageSize)
    {
        var pageCount = GetPageCount(entries.Count, pageSize);
        var clampedPageIndex = Math.Clamp(pageIndex, 0, Math.Max(pageCount - 1, 0));
        var normalizedPageSize = Math.Max(1, pageSize);
        var visibleEntries = entries
            .Skip(clampedPageIndex * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();
        return new DebugModifiedMemoryPageState(clampedPageIndex, pageCount, visibleEntries);
    }

    public static DebugModifiedMemoryUpsertResult UpsertEntry(
        IList<ModifiedMemoryEntry> entries,
        ushort address,
        byte value)
    {
        var existing = entries.FirstOrDefault(item => item.Address == address);
        if (existing == null)
        {
            var inserted = new ModifiedMemoryEntry
            {
                Address = address,
                DisplayAddress = $"${address:X4}",
                Value = value.ToString("X2")
            };
            entries.Insert(0, inserted);
            return new DebugModifiedMemoryUpsertResult(inserted, NextPageIndex: 0);
        }

        existing.Value = value.ToString("X2");
        return new DebugModifiedMemoryUpsertResult(existing, NextPageIndex: 0);
    }

    public static bool RemoveEntry(ICollection<ModifiedMemoryEntry> entries, ModifiedMemoryEntry entry) =>
        entries.Remove(entry);

    public static void ReplaceEntries(
        ICollection<ModifiedMemoryEntry> target,
        IEnumerable<ModifiedMemoryEntry> source)
    {
        target.Clear();
        foreach (var entry in source)
            target.Add(entry);
    }
}

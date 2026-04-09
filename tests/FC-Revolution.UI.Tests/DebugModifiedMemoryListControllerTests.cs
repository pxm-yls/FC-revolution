using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class DebugModifiedMemoryListControllerTests
{
    [Fact]
    public void BuildPageState_ClampsPageIndexAndBuildsVisibleSlice()
    {
        var entries = Enumerable.Range(0, 11)
            .Select(index => CreateEntry((ushort)index, index.ToString("X2")))
            .ToList();

        var state = DebugModifiedMemoryListController.BuildPageState(entries, pageIndex: 5, pageSize: 10);

        Assert.Equal(2, state.PageCount);
        Assert.Equal(1, state.PageIndex);
        var visible = Assert.Single(state.VisibleEntries);
        Assert.Equal((ushort)0x000A, visible.Address);
    }

    [Fact]
    public void UpsertEntry_InsertsNewEntryAtFront_AndResetsToFirstPage()
    {
        var entries = new List<ModifiedMemoryEntry>
        {
            CreateEntry(0x0010, "10")
        };

        var result = DebugModifiedMemoryListController.UpsertEntry(entries, 0x0020, 0x7F);

        Assert.Equal(0, result.NextPageIndex);
        Assert.Equal((ushort)0x0020, result.Entry.Address);
        Assert.Equal("7F", result.Entry.Value);
        Assert.Equal(2, entries.Count);
        Assert.Equal((ushort)0x0020, entries[0].Address);
    }

    [Fact]
    public void UpsertEntry_UpdatesExistingEntryWithoutReordering()
    {
        var first = CreateEntry(0x0001, "11");
        var second = CreateEntry(0x0002, "22");
        var entries = new List<ModifiedMemoryEntry> { first, second };

        var result = DebugModifiedMemoryListController.UpsertEntry(entries, 0x0002, 0xAA);

        Assert.Equal((ushort)0x0002, result.Entry.Address);
        Assert.Equal("AA", second.Value);
        Assert.Same(first, entries[0]);
        Assert.Same(second, entries[1]);
    }

    [Fact]
    public void RemoveAndReplaceEntries_ApplyCollectionMutations()
    {
        var first = CreateEntry(0x0001, "11");
        var second = CreateEntry(0x0002, "22");
        var entries = new List<ModifiedMemoryEntry> { first, second };

        var removed = DebugModifiedMemoryListController.RemoveEntry(entries, first);
        var removedAgain = DebugModifiedMemoryListController.RemoveEntry(entries, first);

        Assert.True(removed);
        Assert.False(removedAgain);
        Assert.Single(entries);
        Assert.Same(second, entries[0]);

        var loaded = new[]
        {
            CreateEntry(0x1000, "AB"),
            CreateEntry(0x1001, "CD")
        };
        DebugModifiedMemoryListController.ReplaceEntries(entries, loaded);

        Assert.Collection(
            entries,
            entry => Assert.Equal((ushort)0x1000, entry.Address),
            entry => Assert.Equal((ushort)0x1001, entry.Address));
    }

    [Fact]
    public void GetPageCount_ReturnsAtLeastOne_ForEmptyEntries()
    {
        Assert.Equal(1, DebugModifiedMemoryListController.GetPageCount(0, pageSize: 10));
    }

    private static ModifiedMemoryEntry CreateEntry(ushort address, string value) =>
        new()
        {
            Address = address,
            DisplayAddress = $"${address:X4}",
            Value = value
        };
}

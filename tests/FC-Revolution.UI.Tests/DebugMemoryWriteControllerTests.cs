using System.Collections.Generic;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class DebugMemoryWriteControllerTests
{
    [Fact]
    public void BuildWriteSuccessState_ComputesPageHighlightAndStatus()
    {
        var state = DebugMemoryWriteController.BuildWriteSuccessState(0x01AF, 0x7F, DebugViewModel.MemoryPageSize);

        Assert.Equal(3, state.MemoryPageIndex);
        Assert.Equal("4", state.MemoryPageInput);
        Assert.Equal((ushort)0x01AF, state.HighlightedAddress);
        Assert.Equal("已修改 $01AF = $7F", state.EditStatus);
    }

    [Fact]
    public void TrackModifiedEntry_InsertsNewEntryAndBuildsUnlockedRuntimeEntry()
    {
        var entries = new List<ModifiedMemoryEntry>();

        var result = DebugMemoryWriteController.TrackModifiedEntry(entries, 0x0010, 0xAB);

        var entry = Assert.Single(entries);
        Assert.Equal((ushort)0x0010, entry.Address);
        Assert.Equal("$0010", entry.DisplayAddress);
        Assert.Equal("AB", entry.Value);
        Assert.False(entry.IsLocked);
        Assert.Equal(0, result.NextModifiedMemoryPageIndex);
        Assert.Equal(new ModifiedMemoryRuntimeEntry(0x0010, 0xAB, false), result.RuntimeEntry);
    }

    [Fact]
    public void TrackModifiedEntry_UpdatesExistingEntryAndPreservesLockState()
    {
        var existing = new ModifiedMemoryEntry
        {
            Address = 0x0010,
            DisplayAddress = "$0010",
            Value = "01",
            IsLocked = true
        };
        var entries = new List<ModifiedMemoryEntry> { existing };

        var result = DebugMemoryWriteController.TrackModifiedEntry(entries, 0x0010, 0xCD);

        var entry = Assert.Single(entries);
        Assert.Same(existing, entry);
        Assert.Equal("CD", entry.Value);
        Assert.True(entry.IsLocked);
        Assert.Equal(0, result.NextModifiedMemoryPageIndex);
        Assert.Equal(new ModifiedMemoryRuntimeEntry(0x0010, 0xCD, true), result.RuntimeEntry);
    }

    [Fact]
    public void BuildToggleLockDecision_ReturnsLockedRuntimeEntryAndStatus()
    {
        var entry = new ModifiedMemoryEntry
        {
            Address = 0x0020,
            DisplayAddress = "$0020",
            Value = "7F",
            IsLocked = false
        };

        var decision = DebugMemoryWriteController.BuildToggleLockDecision(entry, 0x7F);

        Assert.True(decision.NextIsLocked);
        Assert.Equal(new ModifiedMemoryRuntimeEntry(0x0020, 0x7F, true), decision.RuntimeEntry);
        Assert.Equal("已锁定 $0020，后续会持续写回 $7F", decision.EditStatus);
    }

    [Fact]
    public void BuildToggleLockDecision_ReturnsUnlockedStatusWhenEntryWasLocked()
    {
        var entry = new ModifiedMemoryEntry
        {
            Address = 0x0020,
            DisplayAddress = "$0020",
            Value = "7F",
            IsLocked = true
        };

        var decision = DebugMemoryWriteController.BuildToggleLockDecision(entry, 0x7F);

        Assert.False(decision.NextIsLocked);
        Assert.Equal(new ModifiedMemoryRuntimeEntry(0x0020, 0x7F, false), decision.RuntimeEntry);
        Assert.Equal("已解除锁定 $0020", decision.EditStatus);
    }

    [Fact]
    public void BuildRemoveDecision_ReturnsRemovedAddressAndStatus()
    {
        var entry = new ModifiedMemoryEntry
        {
            Address = 0x0030,
            DisplayAddress = "$0030",
            Value = "10"
        };

        var decision = DebugMemoryWriteController.BuildRemoveDecision(entry);

        Assert.Equal((ushort)0x0030, decision.RemovedAddress);
        Assert.Equal("已移除保存项 $0030", decision.EditStatus);
    }
}

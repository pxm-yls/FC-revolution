using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowModifiedMemoryLockStateControllerTests
{
    [Fact]
    public void ParseModifiedMemoryEntries_SkipsInvalidEntries_AndPreservesValues()
    {
        var parsed = GameWindowModifiedMemoryLockStateController.ParseModifiedMemoryEntries(
            [
                new RomConfigMemoryEntry { Address = "0010", Value = "0A", IsLocked = true },
                new RomConfigMemoryEntry { Address = "oops", Value = "7F", IsLocked = false },
                new RomConfigMemoryEntry { Address = "0020", Value = "GG", IsLocked = true },
                new RomConfigMemoryEntry { Address = "0030", Value = "1f", IsLocked = false }
            ]);

        Assert.Collection(
            parsed,
            entry =>
            {
                Assert.Equal((ushort)0x0010, entry.Address);
                Assert.Equal((byte)0x0A, entry.Value);
                Assert.True(entry.IsLocked);
            },
            entry =>
            {
                Assert.Equal((ushort)0x0030, entry.Address);
                Assert.Equal((byte)0x1F, entry.Value);
                Assert.False(entry.IsLocked);
            });
    }

    [Fact]
    public void BuildUpsertDecision_MapsLockedAndUnlockedEntries()
    {
        var lockedDecision = GameWindowModifiedMemoryLockStateController.BuildUpsertDecision(
            new ModifiedMemoryRuntimeEntry(0x0010, 0xAA, IsLocked: true));
        var unlockedDecision = GameWindowModifiedMemoryLockStateController.BuildUpsertDecision(
            new ModifiedMemoryRuntimeEntry(0x0020, 0xBB, IsLocked: false));

        Assert.Equal(GameWindowModifiedMemoryLockUpsertAction.Upsert, lockedDecision.Action);
        Assert.Equal((ushort)0x0010, lockedDecision.Address);
        Assert.Equal((byte)0xAA, lockedDecision.Value);
        Assert.True(lockedDecision.ShouldWriteValueImmediately);

        Assert.Equal(GameWindowModifiedMemoryLockUpsertAction.Remove, unlockedDecision.Action);
        Assert.Equal((ushort)0x0020, unlockedDecision.Address);
        Assert.Equal((byte)0xBB, unlockedDecision.Value);
        Assert.False(unlockedDecision.ShouldWriteValueImmediately);
    }

    [Fact]
    public void BuildRemoveAndReplaceDecision_TargetLockedEntriesOnly()
    {
        var remove = GameWindowModifiedMemoryLockStateController.BuildRemoveDecision(0x00FF);
        Assert.Equal((ushort)0x00FF, remove.Address);

        var replace = GameWindowModifiedMemoryLockStateController.BuildReplaceDecision(
            [
                new ModifiedMemoryRuntimeEntry(0x0010, 0x11, IsLocked: true),
                new ModifiedMemoryRuntimeEntry(0x0020, 0x22, IsLocked: false),
                new ModifiedMemoryRuntimeEntry(0x0030, 0x33, IsLocked: true)
            ]);

        Assert.Collection(
            replace.LockedEntries,
            entry => Assert.Equal((ushort)0x0010, entry.Address),
            entry => Assert.Equal((ushort)0x0030, entry.Address));
    }

    [Fact]
    public void BuildAutoApplyDecision_HonorsAutoApplyFlag_AndBuildsLockedSubset()
    {
        var disabled = GameWindowModifiedMemoryLockStateController.BuildAutoApplyDecision(
            new RomConfigProfile
            {
                AutoApplyModifiedMemoryOnLaunch = false,
                ModifiedMemory =
                [
                    new RomConfigMemoryEntry { Address = "0010", Value = "AA", IsLocked = true }
                ]
            });
        Assert.False(disabled.ShouldApply);
        Assert.Empty(disabled.RuntimeEntries);
        Assert.Empty(disabled.LockedEntries);

        var enabled = GameWindowModifiedMemoryLockStateController.BuildAutoApplyDecision(
            new RomConfigProfile
            {
                AutoApplyModifiedMemoryOnLaunch = true,
                ModifiedMemory =
                [
                    new RomConfigMemoryEntry { Address = "0010", Value = "AA", IsLocked = true },
                    new RomConfigMemoryEntry { Address = "0020", Value = "BB", IsLocked = false }
                ]
            });
        Assert.True(enabled.ShouldApply);
        Assert.Equal(2, enabled.RuntimeEntries.Count);
        var locked = Assert.Single(enabled.LockedEntries);
        Assert.Equal((ushort)0x0010, locked.Address);
    }
}

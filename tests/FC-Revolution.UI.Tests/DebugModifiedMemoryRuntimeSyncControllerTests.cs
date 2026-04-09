using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class DebugModifiedMemoryRuntimeSyncControllerTests
{
    [Fact]
    public void BuildRuntimeEntry_ReturnsExpectedEntry()
    {
        var runtimeEntry = DebugModifiedMemoryRuntimeSyncController.BuildRuntimeEntry(0x1234, 0xAB, isLocked: true);

        Assert.Equal((ushort)0x1234, runtimeEntry.Address);
        Assert.Equal((byte)0xAB, runtimeEntry.Value);
        Assert.True(runtimeEntry.IsLocked);
    }

    [Fact]
    public void TryBuildRuntimeEntry_ReturnsFalse_WhenValueInvalid()
    {
        var entry = new ModifiedMemoryEntry
        {
            Address = 0x0010,
            DisplayAddress = "$0010",
            Value = "GG",
            IsLocked = true
        };

        var parsed = DebugModifiedMemoryRuntimeSyncController.TryBuildRuntimeEntry(entry, out var runtimeEntry);

        Assert.False(parsed);
        Assert.Equal(default, runtimeEntry);
    }

    [Fact]
    public void BuildRuntimeEntries_SkipsInvalidValues_AndPreservesOrderAndLockState()
    {
        var entries = new[]
        {
            new ModifiedMemoryEntry
            {
                Address = 0x0010,
                DisplayAddress = "$0010",
                Value = "0a",
                IsLocked = true
            },
            new ModifiedMemoryEntry
            {
                Address = 0x0020,
                DisplayAddress = "$0020",
                Value = "ZZ",
                IsLocked = false
            },
            new ModifiedMemoryEntry
            {
                Address = 0x0030,
                DisplayAddress = "$0030",
                Value = "$7f",
                IsLocked = false
            }
        };

        var runtimeEntries = DebugModifiedMemoryRuntimeSyncController.BuildRuntimeEntries(entries);

        Assert.Collection(
            runtimeEntries,
            entry =>
            {
                Assert.Equal((ushort)0x0010, entry.Address);
                Assert.Equal((byte)0x0A, entry.Value);
                Assert.True(entry.IsLocked);
            },
            entry =>
            {
                Assert.Equal((ushort)0x0030, entry.Address);
                Assert.Equal((byte)0x7F, entry.Value);
                Assert.False(entry.IsLocked);
            });
    }
}

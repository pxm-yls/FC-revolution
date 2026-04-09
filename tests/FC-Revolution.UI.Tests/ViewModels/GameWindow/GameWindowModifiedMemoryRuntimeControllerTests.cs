using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowModifiedMemoryRuntimeControllerTests
{
    [Fact]
    public void ApplySavedProfile_WhenForeignProfile_AppliesDecisionAndUpdatesWarningStatus()
    {
        GameWindowModifiedMemoryAutoApplyDecision? appliedDecision = null;
        var statusText = string.Empty;
        var controller = new GameWindowModifiedMemoryRuntimeController(
            decision => appliedDecision = decision,
            _ => { },
            _ => { },
            _ => { },
            "/roms/contra.nes",
            status => statusText = status);
        var loadResult = new RomConfigLoadResult(
            new RomConfigProfile
            {
                ModifiedMemory =
                [
                    new RomConfigMemoryEntry { Address = "4016", Value = "7F", IsLocked = true },
                    new RomConfigMemoryEntry { Address = "4017", Value = "01", IsLocked = false }
                ]
            },
            HasProfileKindMismatch: false,
            IsForeignMachineProfile: true,
            IsFutureVersionProfile: false);

        controller.ApplySavedProfile(loadResult);

        Assert.True(appliedDecision.HasValue);
        Assert.True(appliedDecision.Value.ShouldApply);
        Assert.Equal(2, appliedDecision.Value.RuntimeEntries.Count);
        Assert.Single(appliedDecision.Value.LockedEntries);
        Assert.Equal((ushort)0x4016, appliedDecision.Value.LockedEntries[0].Address);
        Assert.Equal("运行中: contra.nes | 警告：当前 .fcr 来自其他设备", statusText);
    }

    [Fact]
    public void ApplySavedProfile_WhenAutoApplyDisabled_ProjectsDisabledDecisionWithoutWarning()
    {
        GameWindowModifiedMemoryAutoApplyDecision? appliedDecision = null;
        var statusText = string.Empty;
        var controller = new GameWindowModifiedMemoryRuntimeController(
            decision => appliedDecision = decision,
            _ => { },
            _ => { },
            _ => { },
            "/roms/contra.nes",
            status => statusText = status);
        var loadResult = new RomConfigLoadResult(
            new RomConfigProfile
            {
                AutoApplyModifiedMemoryOnLaunch = false,
                ModifiedMemory = [new RomConfigMemoryEntry { Address = "4016", Value = "7F", IsLocked = true }]
            },
            HasProfileKindMismatch: false,
            IsForeignMachineProfile: false,
            IsFutureVersionProfile: false);

        controller.ApplySavedProfile(loadResult);

        Assert.True(appliedDecision.HasValue);
        Assert.False(appliedDecision.Value.ShouldApply);
        Assert.Empty(appliedDecision.Value.RuntimeEntries);
        Assert.Empty(appliedDecision.Value.LockedEntries);
        Assert.Equal(string.Empty, statusText);
    }

    [Fact]
    public void UpsertAndRemoveRuntimeEntry_BuildExpectedDecisions()
    {
        GameWindowModifiedMemoryLockUpsertDecision? upsertDecision = null;
        GameWindowModifiedMemoryLockRemoveDecision? removeDecision = null;
        var controller = new GameWindowModifiedMemoryRuntimeController(
            _ => { },
            decision => upsertDecision = decision,
            decision => removeDecision = decision,
            _ => { },
            "/roms/contra.nes",
            _ => { });

        controller.UpsertRuntimeEntry(new ModifiedMemoryRuntimeEntry(0x4016, 0x7F, IsLocked: true));
        controller.RemoveRuntimeEntry(0x4017);

        Assert.True(upsertDecision.HasValue);
        Assert.Equal(GameWindowModifiedMemoryLockUpsertAction.Upsert, upsertDecision.Value.Action);
        Assert.Equal((ushort)0x4016, upsertDecision.Value.Address);
        Assert.Equal((byte)0x7F, upsertDecision.Value.Value);
        Assert.True(upsertDecision.Value.ShouldWriteValueImmediately);
        Assert.True(removeDecision.HasValue);
        Assert.Equal((ushort)0x4017, removeDecision.Value.Address);
    }

    [Fact]
    public void ReplaceRuntimeEntries_FiltersToLockedEntries()
    {
        GameWindowModifiedMemoryLockReplaceDecision? replaceDecision = null;
        var controller = new GameWindowModifiedMemoryRuntimeController(
            _ => { },
            _ => { },
            _ => { },
            decision => replaceDecision = decision,
            "/roms/contra.nes",
            _ => { });

        controller.ReplaceRuntimeEntries(
        [
            new ModifiedMemoryRuntimeEntry(0x4016, 0x7F, IsLocked: true),
            new ModifiedMemoryRuntimeEntry(0x4017, 0x01, IsLocked: false),
            new ModifiedMemoryRuntimeEntry(0x4018, 0x02, IsLocked: true)
        ]);

        Assert.True(replaceDecision.HasValue);
        Assert.Equal(2, replaceDecision.Value.LockedEntries.Count);
        Assert.Equal((ushort)0x4016, replaceDecision.Value.LockedEntries[0].Address);
        Assert.Equal((ushort)0x4018, replaceDecision.Value.LockedEntries[1].Address);
    }
}

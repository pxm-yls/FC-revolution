using System;
using System.Collections.Generic;
using System.Globalization;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct DebugMemoryWriteSuccessState(
    int MemoryPageIndex,
    string MemoryPageInput,
    ushort HighlightedAddress,
    string EditStatus);

internal readonly record struct DebugMemoryWriteTrackingResult(
    int NextModifiedMemoryPageIndex,
    ModifiedMemoryRuntimeEntry RuntimeEntry);

internal readonly record struct DebugMemoryLockToggleDecision(
    bool NextIsLocked,
    ModifiedMemoryRuntimeEntry RuntimeEntry,
    string EditStatus);

internal readonly record struct DebugMemoryRemoveDecision(
    ushort RemovedAddress,
    string EditStatus);

internal static class DebugMemoryWriteController
{
    public static DebugMemoryWriteSuccessState BuildWriteSuccessState(ushort address, byte value, int memoryPageSize)
    {
        var normalizedMemoryPageSize = Math.Max(1, memoryPageSize);
        var memoryPageIndex = address / normalizedMemoryPageSize;

        return new DebugMemoryWriteSuccessState(
            memoryPageIndex,
            (memoryPageIndex + 1).ToString(CultureInfo.InvariantCulture),
            address,
            $"已修改 ${address:X4} = ${value:X2}");
    }

    public static DebugMemoryWriteTrackingResult TrackModifiedEntry(
        IList<ModifiedMemoryEntry> entries,
        ushort address,
        byte value)
    {
        var upsertResult = DebugModifiedMemoryListController.UpsertEntry(entries, address, value);

        return new DebugMemoryWriteTrackingResult(
            upsertResult.NextPageIndex,
            DebugModifiedMemoryRuntimeSyncController.BuildRuntimeEntry(
                upsertResult.Entry.Address,
                value,
                upsertResult.Entry.IsLocked));
    }

    public static DebugMemoryLockToggleDecision BuildToggleLockDecision(ModifiedMemoryEntry entry, byte value)
    {
        var nextIsLocked = !entry.IsLocked;

        return new DebugMemoryLockToggleDecision(
            nextIsLocked,
            DebugModifiedMemoryRuntimeSyncController.BuildRuntimeEntry(entry.Address, value, nextIsLocked),
            nextIsLocked
                ? $"已锁定 ${entry.Address:X4}，后续会持续写回 ${value:X2}"
                : $"已解除锁定 ${entry.Address:X4}");
    }

    public static DebugMemoryRemoveDecision BuildRemoveDecision(ModifiedMemoryEntry entry) =>
        new(entry.Address, $"已移除保存项 ${entry.Address:X4}");
}

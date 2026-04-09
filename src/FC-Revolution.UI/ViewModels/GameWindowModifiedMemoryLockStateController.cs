using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal enum GameWindowModifiedMemoryLockUpsertAction
{
    Remove,
    Upsert
}

internal readonly record struct GameWindowModifiedMemoryLockUpsertDecision(
    GameWindowModifiedMemoryLockUpsertAction Action,
    ushort Address,
    byte Value,
    bool ShouldWriteValueImmediately);

internal readonly record struct GameWindowModifiedMemoryLockRemoveDecision(ushort Address);

internal readonly record struct GameWindowModifiedMemoryLockReplaceDecision(
    IReadOnlyList<ModifiedMemoryRuntimeEntry> LockedEntries);

internal readonly record struct GameWindowModifiedMemoryAutoApplyDecision(
    bool ShouldApply,
    IReadOnlyList<ModifiedMemoryRuntimeEntry> RuntimeEntries,
    IReadOnlyList<ModifiedMemoryRuntimeEntry> LockedEntries);

internal static class GameWindowModifiedMemoryLockStateController
{
    public static GameWindowModifiedMemoryAutoApplyDecision BuildAutoApplyDecision(RomConfigProfile profile)
    {
        if (!profile.AutoApplyModifiedMemoryOnLaunch)
        {
            return new GameWindowModifiedMemoryAutoApplyDecision(
                ShouldApply: false,
                RuntimeEntries: [],
                LockedEntries: []);
        }

        var runtimeEntries = ParseModifiedMemoryEntries(profile.ModifiedMemory);
        var lockedEntries = BuildLockedEntries(runtimeEntries);
        return new GameWindowModifiedMemoryAutoApplyDecision(
            ShouldApply: true,
            RuntimeEntries: runtimeEntries,
            LockedEntries: lockedEntries);
    }

    public static GameWindowModifiedMemoryLockUpsertDecision BuildUpsertDecision(ModifiedMemoryRuntimeEntry entry)
    {
        return entry.IsLocked
            ? new GameWindowModifiedMemoryLockUpsertDecision(
                Action: GameWindowModifiedMemoryLockUpsertAction.Upsert,
                Address: entry.Address,
                Value: entry.Value,
                ShouldWriteValueImmediately: true)
            : new GameWindowModifiedMemoryLockUpsertDecision(
                Action: GameWindowModifiedMemoryLockUpsertAction.Remove,
                Address: entry.Address,
                Value: entry.Value,
                ShouldWriteValueImmediately: false);
    }

    public static GameWindowModifiedMemoryLockRemoveDecision BuildRemoveDecision(ushort address) =>
        new(address);

    public static GameWindowModifiedMemoryLockReplaceDecision BuildReplaceDecision(
        IReadOnlyList<ModifiedMemoryRuntimeEntry> entries) =>
        new(BuildLockedEntries(entries));

    public static List<ModifiedMemoryRuntimeEntry> ParseModifiedMemoryEntries(IEnumerable<RomConfigMemoryEntry> entries)
    {
        var runtimeEntries = new List<ModifiedMemoryRuntimeEntry>();
        foreach (var entry in entries)
        {
            if (!ushort.TryParse(entry.Address, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address) ||
                !byte.TryParse(entry.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            runtimeEntries.Add(new ModifiedMemoryRuntimeEntry(address, value, entry.IsLocked));
        }

        return runtimeEntries;
    }

    private static IReadOnlyList<ModifiedMemoryRuntimeEntry> BuildLockedEntries(
        IEnumerable<ModifiedMemoryRuntimeEntry> entries) =>
        entries.Where(entry => entry.IsLocked).ToList();
}

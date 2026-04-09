using System.Collections.Generic;
using System.Globalization;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal static class DebugModifiedMemoryRuntimeSyncController
{
    public static ModifiedMemoryRuntimeEntry BuildRuntimeEntry(ushort address, byte value, bool isLocked) =>
        new(address, value, isLocked);

    public static bool TryBuildRuntimeEntry(ModifiedMemoryEntry entry, out ModifiedMemoryRuntimeEntry runtimeEntry)
    {
        if (TryParseByte(entry.Value, out var value))
        {
            runtimeEntry = BuildRuntimeEntry(entry.Address, value, entry.IsLocked);
            return true;
        }

        runtimeEntry = default;
        return false;
    }

    public static List<ModifiedMemoryRuntimeEntry> BuildRuntimeEntries(IEnumerable<ModifiedMemoryEntry> entries)
    {
        var runtimeEntries = new List<ModifiedMemoryRuntimeEntry>();
        foreach (var entry in entries)
        {
            if (!TryBuildRuntimeEntry(entry, out var runtimeEntry))
                continue;

            runtimeEntries.Add(runtimeEntry);
        }

        return runtimeEntries;
    }

    private static bool TryParseByte(string text, out byte value) =>
        byte.TryParse(text.Trim().Replace("$", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
}

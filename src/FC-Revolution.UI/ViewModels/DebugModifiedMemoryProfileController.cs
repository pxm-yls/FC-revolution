using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct DebugModifiedMemoryProfileLoadResult(
    IReadOnlyList<ModifiedMemoryEntry> Entries,
    RomConfigLoadResult LoadResult);

internal static class DebugModifiedMemoryProfileController
{
    public static bool TryLoad(string romPath, out DebugModifiedMemoryProfileLoadResult result)
    {
        result = default;
        if (!File.Exists(RomConfigProfile.GetProfilePath(romPath)))
            return false;

        try
        {
            var loadResult = RomConfigProfile.LoadValidated(romPath);
            var entries = loadResult.Profile.ModifiedMemory
                .Where(entry => TryParseAddress(entry.Address, out _) && TryParseByte(entry.Value, out _))
                .Select(entry =>
                {
                    var address = ushort.Parse(entry.Address, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    return new ModifiedMemoryEntry
                    {
                        Address = address,
                        DisplayAddress = $"${address:X4}",
                        Value = entry.Value.ToUpperInvariant(),
                        IsLocked = entry.IsLocked
                    };
                })
                .ToList();

            result = new DebugModifiedMemoryProfileLoadResult(entries, loadResult);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Save(string romPath, IEnumerable<ModifiedMemoryEntry> entries)
    {
        try
        {
            var profile = RomConfigProfile.LoadValidated(romPath).Profile;
            profile.ModifiedMemory = entries
                .Select(entry => new RomConfigMemoryEntry
                {
                    Address = entry.Address.ToString("X4"),
                    Value = entry.Value.ToUpperInvariant(),
                    IsLocked = entry.IsLocked
                })
                .ToList();
            RomConfigProfile.Save(romPath, profile);
        }
        catch
        {
        }
    }

    public static string BuildProfileStatus(string baseStatus, RomConfigLoadResult loadResult)
    {
        if (loadResult.HasProfileKindMismatch)
            return $"{baseStatus}，但文件类型不是 FC-Revolution 专用配置";

        if (loadResult.IsForeignMachineProfile)
            return $"{baseStatus}，注意：该 .fcr 来自其他设备，可能存在风险";

        if (loadResult.IsFutureVersionProfile)
            return $"{baseStatus}，注意：该 .fcr 版本高于当前程序";

        return baseStatus;
    }

    private static bool TryParseAddress(string text, out ushort address) =>
        ushort.TryParse(text.Trim().Replace("$", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address);

    private static bool TryParseByte(string text, out byte value) =>
        byte.TryParse(text.Trim().Replace("$", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
}

using System;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.Infrastructure;

internal readonly record struct RomMapperInfo(int Number, string Name, bool IsSupported)
{
    public string DisplayLabel => Number < 0 ? Name : IsSupported ? $"{Number} {Name}" : $"{Number} 未支持";
}

internal static class RomMapperInspector
{
    private const string UnavailableDisplayName = "Mapper 信息不可用";

    public static RomMapperInfo Inspect(string romPath) =>
        Inspect(romPath, LegacyFeatureRuntime.Current);

    internal static RomMapperInfo Inspect(string romPath, ILegacyFeatureRuntime legacyFeatureRuntime)
    {
        ArgumentNullException.ThrowIfNull(legacyFeatureRuntime);
        if (!legacyFeatureRuntime.TryGetRomMapperInfoInspector(out var inspector, out _))
            return new RomMapperInfo(-1, UnavailableDisplayName, IsSupported: false);

        var mapper = inspector.Inspect(romPath);
        return new RomMapperInfo(mapper.Number, mapper.Name, mapper.IsSupported);
    }
}

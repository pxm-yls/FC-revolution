using FCRevolution.FC.LegacyAdapters;

namespace FC_Revolution.UI.Infrastructure;

internal readonly record struct RomMapperInfo(int Number, string Name, bool IsSupported)
{
    public string DisplayLabel => IsSupported ? $"{Number} {Name}" : $"{Number} 未支持";
}

internal static class RomMapperInspector
{
    public static RomMapperInfo Inspect(string romPath)
    {
        var mapper = NesRomInspector.Inspect(romPath);
        return new RomMapperInfo(mapper.Number, mapper.Name, mapper.IsSupported);
    }
}

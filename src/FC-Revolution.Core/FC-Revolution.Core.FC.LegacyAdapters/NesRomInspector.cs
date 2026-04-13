using FCRevolution.Core.Mappers;

namespace FCRevolution.Core.FC.LegacyAdapters;

public readonly record struct LegacyMapperInfo(int Number, string Name, bool IsSupported);

public static class NesRomInspector
{
    public static LegacyMapperInfo Inspect(string romPath)
    {
        var headerBytes = new byte[16];
        using var stream = File.OpenRead(romPath);
        stream.ReadExactly(headerBytes);
        var header = new InesHeader(headerBytes);
        var isSupported = MapperFactory.RegisteredMappers.TryGetValue(header.MapperNumber, out var mapperName);
        return new LegacyMapperInfo(
            header.MapperNumber,
            isSupported ? mapperName! : "未支持/未知核心",
            isSupported);
    }
}

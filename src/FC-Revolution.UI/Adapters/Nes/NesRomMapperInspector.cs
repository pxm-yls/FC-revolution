using System.IO;
using FCRevolution.Core.Mappers;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.Adapters.Nes;

internal static class NesRomMapperInspector
{
    public static RomMapperInfo Inspect(string romPath)
    {
        var headerBytes = new byte[16];
        using var stream = File.OpenRead(romPath);
        stream.ReadExactly(headerBytes);
        var header = new InesHeader(headerBytes);
        var isSupported = MapperFactory.RegisteredMappers.TryGetValue(header.MapperNumber, out var mapperName);
        return new RomMapperInfo(
            header.MapperNumber,
            isSupported ? mapperName! : "未支持/未知核心",
            isSupported);
    }
}

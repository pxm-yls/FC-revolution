using System;
using System.IO;
using FCRevolution.Core.Mappers;

namespace FC_Revolution.UI.Infrastructure;

internal readonly record struct RomMapperInfo(int Number, string Name, bool IsSupported)
{
    public string DisplayLabel => IsSupported ? $"{Number} {Name}" : $"{Number} 未支持";
}

internal static class RomMapperInspector
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

namespace FCRevolution.Core.Mappers;

public static class MapperFactory
{
    public static ICartridge Create(byte[] romData)
    {
        var header = new InesHeader(romData);
        if (MapperProfileRegistry.TryGet(header.MapperNumber, out var profile))
            return new MapperCartridge(romData, header, profile);

        throw new NotSupportedException($"Mapper {header.MapperNumber} is not yet supported.");
    }

    public static IReadOnlyDictionary<int, string> RegisteredMappers => MapperProfileRegistry.RegisteredMappers;
}

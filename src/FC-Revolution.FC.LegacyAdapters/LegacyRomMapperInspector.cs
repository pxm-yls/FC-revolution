using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.FC.LegacyAdapters;

public sealed class LegacyRomMapperInspector : IRomMapperInfoInspector
{
    public CoreRomMapperInfo Inspect(string romPath)
    {
        var mapper = NesRomInspector.Inspect(romPath);
        return new CoreRomMapperInfo(mapper.Number, mapper.Name, mapper.IsSupported);
    }
}

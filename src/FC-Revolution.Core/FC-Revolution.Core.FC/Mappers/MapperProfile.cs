namespace FCRevolution.Core.Mappers;

public enum MapperFamilyKind
{
    Nrom,
    Mmc1,
    UxRom,
    CnRom,
    Mmc3,
    Vrc24,
    Vrc3,
    Sunsoft4,
    Jy90,
    Mapper15,
    DiscreteLatch,
    AddressLatch,
    Nanjing,
    Mapper246,
}

public sealed record MapperProfile(
    int Number,
    string Name,
    MapperFamilyKind Family,
    int PrgRamSize = 8192,
    int ChrRamSize = 8192,
    object? Settings = null);

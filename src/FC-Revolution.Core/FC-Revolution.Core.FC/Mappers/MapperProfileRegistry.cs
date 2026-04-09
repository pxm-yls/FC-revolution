using System.Collections.ObjectModel;

namespace FCRevolution.Core.Mappers;

internal static class MapperProfileRegistry
{
    private static readonly IReadOnlyDictionary<int, MapperProfile> Profiles = BuildProfiles();

    public static IReadOnlyDictionary<int, string> RegisteredMappers { get; } =
        new ReadOnlyDictionary<int, string>(
            Profiles.ToDictionary(pair => pair.Key, pair => pair.Value.Name));

    public static bool TryGet(int mapperNumber, out MapperProfile profile)
    {
        if (Profiles.TryGetValue(mapperNumber, out var registered))
        {
            profile = registered;
            return true;
        }

        profile = null!;
        return false;
    }

    private static IReadOnlyDictionary<int, MapperProfile> BuildProfiles()
    {
        MapperProfile[] profiles =
        [
            new(0, "NROM", MapperFamilyKind.Nrom),
            new(1, "MMC1 (SxROM)", MapperFamilyKind.Mmc1, ChrRamSize: 131072),
            new(2, "UxROM", MapperFamilyKind.UxRom),
            new(3, "CNROM", MapperFamilyKind.CnRom),
            new(4, "MMC3 (TxROM)", MapperFamilyKind.Mmc3),
            new(15, "100-in-1", MapperFamilyKind.Mapper15),
            new(25, "Konami VRC2/VRC4 D", MapperFamilyKind.Vrc24),
            new(68, "Sunsoft Mapper #4", MapperFamilyKind.Sunsoft4),
            new(73, "Konami VRC3", MapperFamilyKind.Vrc3),
            new(74, "TW MMC3+VRAM Rev. A", MapperFamilyKind.Mmc3),
            new(87, "74*139/74 DISCRETE", MapperFamilyKind.DiscreteLatch, Settings: DiscreteLatchVariant.Mapper87),
            new(90, "HUMMER/JY BOARD", MapperFamilyKind.Jy90),
            new(163, "Nanjing 163", MapperFamilyKind.Nanjing),
            new(164, "Nanjing 164", MapperFamilyKind.Nanjing),
            new(240, "Mapper 240", MapperFamilyKind.DiscreteLatch, Settings: DiscreteLatchVariant.Mapper240),
            new(242, "Mapper 242", MapperFamilyKind.AddressLatch),
            new(245, "Mapper 245", MapperFamilyKind.Mmc3),
            new(246, "Fong Shen Bang", MapperFamilyKind.Mapper246, PrgRamSize: 2048),
        ];

        return new ReadOnlyDictionary<int, MapperProfile>(
            profiles.ToDictionary(profile => profile.Number));
    }
}

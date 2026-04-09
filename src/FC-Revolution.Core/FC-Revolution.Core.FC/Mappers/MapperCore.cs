namespace FCRevolution.Core.Mappers;

internal abstract class MapperCore
{
    protected MapperCore(MapperCartridge cartridge, InesHeader header, MapperProfile profile)
    {
        Cartridge = cartridge;
        Header = header;
        Profile = profile;
    }

    protected MapperCartridge Cartridge { get; }
    protected InesHeader Header { get; }
    protected MapperProfile Profile { get; }

    protected byte[] PrgRom => Cartridge.PrgRom;
    protected byte[] ChrMem => Cartridge.ChrMem;
    protected byte[] PrgRam => Cartridge.PrgRam;
    protected bool ChrIsRam => Cartridge.ChrIsRam;
    protected int PrgRomBanks16K => Cartridge.PrgRomBanks16K;
    protected int PrgBanks8K => Cartridge.PrgBanks8K;
    protected int ChrBanks1K => Cartridge.ChrBanks1K;

    protected MirroringMode CurrentMirroring
    {
        get => Cartridge.CurrentMirroring;
        set => Cartridge.CurrentMirroring = value;
    }

    protected bool IsPrgRamAddress(ushort address) => Cartridge.IsPrgRamAddress(address);
    protected byte ReadPrgRam(ushort address) => Cartridge.ReadPrgRam(address);
    protected void WritePrgRam(ushort address, byte data) => Cartridge.WritePrgRam(address, data);

    protected bool TryReadPrgRam(ushort address, out byte value)
    {
        if (IsPrgRamAddress(address)) { value = ReadPrgRam(address); return true; }
        value = 0;
        return false;
    }

    protected bool TryWritePrgRam(ushort address, byte data)
    {
        if (!IsPrgRamAddress(address)) return false;
        WritePrgRam(address, data);
        return true;
    }
    protected byte ReadChr(ushort address) => Cartridge.ReadChr(address);
    protected byte ReadChrAt(int index) => Cartridge.ReadChrAt(index);
    protected void WriteChr(ushort address, byte data) => Cartridge.WriteChr(address, data);
    protected void WriteChrAt(int index, byte data) => Cartridge.WriteChrAt(index, data);

    public virtual MirroringMode Mirroring => CurrentMirroring;
    public virtual bool IrqActive => false;

    public abstract byte CpuRead(ushort address);
    public abstract void CpuWrite(ushort address, byte data);
    public virtual byte PpuRead(ushort address) => address < 0x2000 ? ReadChr(address) : (byte)0;
    public virtual void PpuWrite(ushort address, byte data) { if (address < 0x2000) WriteChr(address, data); }
    public abstract byte[] SerializeState();
    public abstract void DeserializeState(byte[] state);

    public virtual void Reset() { }
    public virtual void Clock() { }
    public virtual void SignalScanline() { }
}

internal static class MapperCoreFactory
{
    public static MapperCore Create(MapperCartridge cartridge, InesHeader header, MapperProfile profile) =>
        profile.Family switch
        {
            MapperFamilyKind.Nrom => new NromMapperCore(cartridge, header, profile),
            MapperFamilyKind.Mmc1 => new Mmc1MapperCore(cartridge, header, profile),
            MapperFamilyKind.UxRom => new UxRomMapperCore(cartridge, header, profile),
            MapperFamilyKind.CnRom => new CnromMapperCore(cartridge, header, profile),
            MapperFamilyKind.Mmc3 => new Mmc3MapperCore(cartridge, header, profile),
            MapperFamilyKind.Vrc24 => new Vrc24MapperCore(cartridge, header, profile),
            MapperFamilyKind.Vrc3 => new Vrc3MapperCore(cartridge, header, profile),
            MapperFamilyKind.Sunsoft4 => new Sunsoft4MapperCore(cartridge, header, profile),
            MapperFamilyKind.Jy90 => new Jy90MapperCore(cartridge, header, profile),
            MapperFamilyKind.Mapper15 => new Mapper15Core(cartridge, header, profile),
            MapperFamilyKind.DiscreteLatch => new DiscreteLatchMapperCore(cartridge, header, profile),
            MapperFamilyKind.AddressLatch => new AddressLatchMapperCore(cartridge, header, profile),
            MapperFamilyKind.Nanjing => new NanjingMapperCore(cartridge, header, profile),
            MapperFamilyKind.Mapper246 => new Mapper246Core(cartridge, header, profile),
            _ => throw new NotSupportedException($"Mapper family {profile.Family} is not implemented."),
        };
}

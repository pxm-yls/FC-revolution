namespace FCRevolution.Core.Mappers;

public enum MirroringMode { Horizontal, Vertical, SingleLower, SingleUpper, FourScreen }

public interface ICartridge : IEmulationComponent, IStateSerializable
{
    int MapperNumber { get; }
    MirroringMode Mirroring { get; }

    byte CpuRead(ushort address);
    void CpuWrite(ushort address, byte data);

    byte PpuRead(ushort address);
    void PpuWrite(ushort address, byte data);

    void SignalScanline();
    bool IrqActive { get; }
}

public readonly struct InesHeader
{
    public readonly int PrgRomBanks;   // 16KB units
    public readonly int ChrRomBanks;   // 8KB units (0 = CHR RAM)
    public readonly int MapperNumber;
    public readonly MirroringMode Mirroring;
    public readonly bool HasBatteryRam;
    public readonly bool HasTrainer;

    public InesHeader(byte[] rom)
    {
        if (rom.Length < 16 || rom[0] != 'N' || rom[1] != 'E' || rom[2] != 'S' || rom[3] != 0x1A)
            throw new InvalidDataException("Not a valid iNES ROM.");

        PrgRomBanks  = rom[4];
        ChrRomBanks  = rom[5];
        HasBatteryRam = (rom[6] & 0x02) != 0;
        HasTrainer    = (rom[6] & 0x04) != 0;
        Mirroring     = (rom[6] & 0x01) != 0 ? MirroringMode.Vertical : MirroringMode.Horizontal;
        if ((rom[6] & 0x08) != 0) Mirroring = MirroringMode.FourScreen;
        MapperNumber  = (rom[7] & 0xF0) | (rom[6] >> 4);
    }
}

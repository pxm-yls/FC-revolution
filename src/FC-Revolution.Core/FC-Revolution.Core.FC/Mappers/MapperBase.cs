namespace FCRevolution.Core.Mappers;

public abstract class MapperBase : ICartridge
{
    internal readonly byte[] PrgRom;
    internal readonly byte[] ChrMem;
    internal readonly byte[] PrgRam;
    internal readonly bool ChrIsRam;
    internal readonly int PrgRomBanks16K;
    internal readonly int PrgBanks8K;
    internal readonly int ChrBanks1K;

    protected MapperBase(byte[] romData, InesHeader header, int prgRamSize = 8192, int chrRamSize = 8192)
    {
        var prgSize = header.PrgRomBanks * 16384;
        var chrSize = header.ChrRomBanks * 8192;
        var trainerOffset = header.HasTrainer ? 512 : 0;

        PrgRom = new byte[prgSize];
        Array.Copy(romData, 16 + trainerOffset, PrgRom, 0, prgSize);
        PrgRomBanks16K = header.PrgRomBanks;
        PrgBanks8K = Math.Max(1, prgSize / 8192);

        if (chrSize > 0)
        {
            ChrIsRam = false;
            ChrMem = new byte[chrSize];
            Array.Copy(romData, 16 + trainerOffset + prgSize, ChrMem, 0, chrSize);
        }
        else
        {
            ChrIsRam = true;
            ChrMem = new byte[Math.Max(1024, chrRamSize)];
        }

        ChrBanks1K = Math.Max(1, ChrMem.Length / 1024);
        PrgRam = new byte[Math.Max(0, prgRamSize)];
        CurrentMirroring = header.Mirroring;
    }

    internal MirroringMode CurrentMirroring { get; set; }

    public abstract int MapperNumber { get; }
    public virtual MirroringMode Mirroring => CurrentMirroring;
    public virtual bool IrqActive { get; protected set; }

    internal bool IsPrgRamAddress(ushort address) =>
        PrgRam.Length > 0 && address >= 0x6000 && address < 0x8000;

    internal byte ReadPrgRam(ushort address) => PrgRam[address - 0x6000];

    internal void WritePrgRam(ushort address, byte data) => PrgRam[address - 0x6000] = data;

    internal byte ReadChr(ushort address) => ReadChrAt(address);

    internal byte ReadChrAt(int index) => ChrMem[index % ChrMem.Length];

    internal void WriteChr(ushort address, byte data) => WriteChrAt(address, data);

    internal void WriteChrAt(int index, byte data)
    {
        if (ChrIsRam)
            ChrMem[index % ChrMem.Length] = data;
    }

    public abstract byte CpuRead(ushort address);
    public abstract void CpuWrite(ushort address, byte data);
    public abstract byte PpuRead(ushort address);
    public abstract void PpuWrite(ushort address, byte data);
    public abstract byte[] SerializeState();
    public abstract void DeserializeState(byte[] state);

    public virtual void Reset() { }
    public virtual void Clock() { }
    public virtual void SignalScanline() { }
}

namespace FCRevolution.Core.Mappers;

internal sealed class NromMapperCore : MapperCore
{
    private readonly bool _prgMirror;

    public NromMapperCore(MapperCartridge cartridge, InesHeader header, MapperProfile profile)
        : base(cartridge, header, profile)
    {
        _prgMirror = header.PrgRomBanks == 1;
    }

    public override byte CpuRead(ushort address)
    {
        if (address >= 0x8000)
        {
            int idx = address - 0x8000;
            if (_prgMirror)
                idx &= 0x3FFF;

            return PrgRom[idx % PrgRom.Length];
        }

        return 0;
    }

    public override void CpuWrite(ushort address, byte data) { }

    public override byte[] SerializeState() => (byte[])ChrMem.Clone();

    public override void DeserializeState(byte[] state)
    {
        if (ChrIsRam)
            Array.Copy(state, ChrMem, Math.Min(state.Length, ChrMem.Length));
    }
}

internal sealed class UxRomMapperCore : MapperCore
{
    private int _prgBankLo;
    private readonly int _prgBankHi;

    public UxRomMapperCore(MapperCartridge cartridge, InesHeader header, MapperProfile profile)
        : base(cartridge, header, profile)
    {
        _prgBankHi = header.PrgRomBanks - 1;
    }

    public override byte CpuRead(ushort address)
    {
        if (TryReadPrgRam(address, out var ram)) return ram;

        if (address >= 0xC000)
            return PrgRom[_prgBankHi * 16384 + (address & 0x3FFF)];

        if (address >= 0x8000)
            return PrgRom[_prgBankLo * 16384 + (address & 0x3FFF)];

        return 0;
    }

    public override void CpuWrite(ushort address, byte data)
    {
        if (TryWritePrgRam(address, data)) return;

        if (address >= 0x8000)
            _prgBankLo = data & 0x0F;
    }

    public override void Reset() => _prgBankLo = 0;
    public override byte[] SerializeState() => [(byte)_prgBankLo];

    public override void DeserializeState(byte[] state)
    {
        if (state.Length > 0)
            _prgBankLo = state[0];
    }
}

internal sealed class CnromMapperCore : MapperCore
{
    private int _chrBank;

    public CnromMapperCore(MapperCartridge cartridge, InesHeader header, MapperProfile profile)
        : base(cartridge, header, profile)
    {
    }

    public override byte CpuRead(ushort address)
    {
        if (TryReadPrgRam(address, out var ram)) return ram;

        if (address >= 0x8000)
            return PrgRom[(address - 0x8000) % PrgRom.Length];

        return 0;
    }

    public override void CpuWrite(ushort address, byte data)
    {
        if (TryWritePrgRam(address, data)) return;

        if (address >= 0x8000)
            _chrBank = data & 0x03;
    }

    public override byte PpuRead(ushort address) => address < 0x2000 ? ReadChrAt(_chrBank * 8192 + address) : (byte)0;

    public override void PpuWrite(ushort address, byte data)
    {
        if (address < 0x2000)
            WriteChrAt(_chrBank * 8192 + address, data);
    }

    public override void Reset() => _chrBank = 0;
    public override byte[] SerializeState() => [(byte)_chrBank];

    public override void DeserializeState(byte[] state)
    {
        if (state.Length > 0)
            _chrBank = state[0];
    }
}

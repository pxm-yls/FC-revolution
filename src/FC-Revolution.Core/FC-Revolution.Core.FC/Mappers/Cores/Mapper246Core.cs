namespace FCRevolution.Core.Mappers;

internal sealed class Mapper246Core : MapperCore
{
    private readonly byte[] _regs = new byte[8];

    public Mapper246Core(MapperCartridge cartridge, InesHeader header, MapperProfile profile)
        : base(cartridge, header, profile)
    {
        Reset();
    }

    public override byte CpuRead(ushort address)
    {
        if (address >= 0x6800 && address < 0x7000 && PrgRam.Length > 0)
            return PrgRam[address - 0x6800];

        if (address < 0x8000)
            return 0;

        int slot = (address - 0x8000) / 0x2000;
        int bank = _regs[slot];
        return PrgRom[(bank * 0x2000 + (address & 0x1FFF)) % PrgRom.Length];
    }

    public override void CpuWrite(ushort address, byte data)
    {
        if (address >= 0x6000 && address < 0x6800)
        {
            _regs[address & 0x0007] = data;
            return;
        }

        if (address >= 0x6800 && address < 0x7000 && PrgRam.Length > 0)
            PrgRam[address - 0x6800] = data;
    }

    public override byte PpuRead(ushort address)
    {
        if (address >= 0x2000)
            return 0;

        int slot = address / 0x0800;
        int bank = _regs[4 + slot];
        return ReadChrAt(bank * 0x0800 + (address & 0x07FF));
    }

    public override void PpuWrite(ushort address, byte data)
    {
        if (address >= 0x2000)
            return;

        int slot = address / 0x0800;
        int bank = _regs[4 + slot];
        WriteChrAt(bank * 0x0800 + (address & 0x07FF), data);
    }

    public override void Reset()
    {
        for (int i = 0; i < 4; i++)
            _regs[i] = 0xFF;

        for (int i = 4; i < _regs.Length; i++)
            _regs[i] = 0;
    }

    public override byte[] SerializeState()
    {
        var buffer = new byte[_regs.Length + PrgRam.Length];
        Array.Copy(_regs, 0, buffer, 0, _regs.Length);
        if (PrgRam.Length > 0)
            Array.Copy(PrgRam, 0, buffer, _regs.Length, PrgRam.Length);
        return buffer;
    }

    public override void DeserializeState(byte[] state)
    {
        if (state.Length < _regs.Length)
            return;

        Array.Copy(state, 0, _regs, 0, _regs.Length);
        if (state.Length >= _regs.Length + PrgRam.Length)
            Array.Copy(state, _regs.Length, PrgRam, 0, PrgRam.Length);
    }
}

namespace FCRevolution.Core.Mappers;

internal sealed class Mmc1MapperCore : MapperCore
{
    private byte _shiftReg = 0x10;

    private int _control = 0x0C;
    private int _chrBank0;
    private int _chrBank1;
    private int _prgBank;

    public Mmc1MapperCore(MapperCartridge cartridge, InesHeader header, MapperProfile profile)
        : base(cartridge, header, profile)
    {
        UpdateMirroring();
    }

    public override byte CpuRead(ushort address)
    {
        if (TryReadPrgRam(address, out var ram)) return ram;

        if (address >= 0x8000)
        {
            int prgMode = (_control >> 2) & 3;
            int banks = PrgRom.Length / 16384;
            int bank;
            int off;

            if (prgMode <= 1)
            {
                bank = _prgBank & 0xFE;
                off = address >= 0xC000 ? 1 : 0;
            }
            else if (prgMode == 2)
            {
                bank = address < 0xC000 ? 0 : _prgBank;
                off = 0;
            }
            else
            {
                bank = address < 0xC000 ? _prgBank : banks - 1;
                off = 0;
            }

            int idx = ((bank + off) % banks) * 16384 + (address & 0x3FFF);
            return PrgRom[idx];
        }

        return 0;
    }

    public override void CpuWrite(ushort address, byte data)
    {
        if (TryWritePrgRam(address, data)) return;

        if (address < 0x8000)
            return;

        if ((data & 0x80) != 0)
        {
            _shiftReg = 0x10;
            _control |= 0x0C;
            UpdateMirroring();
            return;
        }

        bool complete = (_shiftReg & 0x01) != 0;
        _shiftReg = (byte)((_shiftReg >> 1) | ((data & 1) << 4));
        if (!complete)
            return;

        int reg = (address >> 13) & 3;
        switch (reg)
        {
            case 0:
                _control = _shiftReg & 0x1F;
                UpdateMirroring();
                break;
            case 1:
                _chrBank0 = _shiftReg & 0x1F;
                break;
            case 2:
                _chrBank1 = _shiftReg & 0x1F;
                break;
            case 3:
                _prgBank = _shiftReg & 0x0F;
                break;
        }

        _shiftReg = 0x10;
    }

    public override byte PpuRead(ushort address)
    {
        if (address >= 0x2000)
            return 0;

        int chrMode = (_control >> 4) & 1;
        int idx = chrMode == 0
            ? (_chrBank0 & 0x1E) * 4096 + address
            : (address < 0x1000 ? _chrBank0 : _chrBank1) * 4096 + (address & 0x0FFF);

        return ReadChrAt(idx);
    }

    public override void PpuWrite(ushort address, byte data)
    {
        if (address < 0x2000)
            WriteChr(address, data);
    }

    public override void Reset()
    {
        _shiftReg = 0x10;
        _control = 0x0C;
        UpdateMirroring();
    }

    public override byte[] SerializeState()
    {
        var buf = new byte[5 + PrgRam.Length];
        buf[0] = _shiftReg;
        buf[1] = (byte)_control;
        buf[2] = (byte)_chrBank0;
        buf[3] = (byte)_chrBank1;
        buf[4] = (byte)_prgBank;
        Array.Copy(PrgRam, 0, buf, 5, PrgRam.Length);
        return buf;
    }

    public override void DeserializeState(byte[] state)
    {
        if (state.Length < 5)
            return;

        _shiftReg = state[0];
        _control = state[1];
        UpdateMirroring();
        _chrBank0 = state[2];
        _chrBank1 = state[3];
        _prgBank = state[4];

        if (state.Length >= 5 + PrgRam.Length)
            Array.Copy(state, 5, PrgRam, 0, PrgRam.Length);
    }

    private void UpdateMirroring()
    {
        CurrentMirroring = (_control & 3) switch
        {
            0 => MirroringMode.SingleLower,
            1 => MirroringMode.SingleUpper,
            2 => MirroringMode.Vertical,
            _ => MirroringMode.Horizontal,
        };
    }
}

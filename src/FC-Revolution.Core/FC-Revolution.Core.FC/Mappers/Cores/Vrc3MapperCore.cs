namespace FCRevolution.Core.Mappers;

internal sealed class Vrc3MapperCore : MapperCore, ICpuCycleDrivenMapper
{
    private byte _prgBank;
    private bool _irqAutoEnable;
    private bool _irq8BitMode;
    private bool _irqEnabled;
    private ushort _irqReload;
    private ushort _irqCount;
    private bool _irqActive;

    public Vrc3MapperCore(MapperCartridge cartridge, InesHeader header, MapperProfile profile)
        : base(cartridge, header, profile)
    {
    }

    public override bool IrqActive => _irqActive;

    public override byte CpuRead(ushort address)
    {
        if (TryReadPrgRam(address, out var ram)) return ram;

        if (address >= 0xC000)
        {
            int bank = PrgRomBanks16K - 1;
            return PrgRom[(bank * 0x4000) + (address & 0x3FFF)];
        }

        if (address >= 0x8000)
            return PrgRom[((_prgBank % PrgRomBanks16K) * 0x4000) + (address & 0x3FFF)];

        return 0;
    }

    public override void CpuWrite(ushort address, byte data)
    {
        if (TryWritePrgRam(address, data)) return;

        switch (address & 0xF000)
        {
            case 0x8000:
                _irqReload = (ushort)((_irqReload & 0xFFF0) | ((data & 0x0F) << 0));
                break;
            case 0x9000:
                _irqReload = (ushort)((_irqReload & 0xFF0F) | ((data & 0x0F) << 4));
                break;
            case 0xA000:
                _irqReload = (ushort)((_irqReload & 0xF0FF) | ((data & 0x0F) << 8));
                break;
            case 0xB000:
                _irqReload = (ushort)((_irqReload & 0x0FFF) | ((data & 0x0F) << 12));
                break;
            case 0xC000:
                _irq8BitMode = (data & 0x04) != 0;
                _irqAutoEnable = (data & 0x01) != 0;
                _irqEnabled = (data & 0x02) != 0;
                if (_irqEnabled)
                {
                    _irqCount = _irq8BitMode
                        ? (ushort)((_irqCount & 0xFF00) | (_irqReload & 0x00FF))
                        : _irqReload;
                }
                _irqActive = false;
                break;
            case 0xD000:
                _irqActive = false;
                _irqEnabled = _irqAutoEnable;
                break;
            case 0xF000:
                _prgBank = data;
                break;
        }
    }

    public void AdvanceCpuCycles(int cycles)
    {
        if (!_irqEnabled)
            return;

        for (int i = 0; i < cycles; i++)
        {
            if (_irq8BitMode)
            {
                ushort temp = (ushort)(_irqCount & 0x00FF);
                if (temp == 0x00FF)
                {
                    _irqCount = (ushort)(_irqReload | (_irqReload & 0x00FF));
                    _irqActive = true;
                }
                else
                {
                    temp++;
                    _irqCount = (ushort)((_irqCount & 0xFF00) | temp);
                }
            }
            else if (_irqCount == 0xFFFF)
            {
                _irqCount = _irqReload;
                _irqActive = true;
            }
            else
            {
                _irqCount++;
            }
        }
    }

    public override void Reset()
    {
        _prgBank = 0;
        _irqAutoEnable = false;
        _irq8BitMode = false;
        _irqEnabled = false;
        _irqReload = 0;
        _irqCount = 0;
        _irqActive = false;
    }

    public override byte[] SerializeState()
    {
        var buffer = new byte[8 + PrgRam.Length + (ChrIsRam ? ChrMem.Length : 0)];
        buffer[0] = _prgBank;
        buffer[1] = (byte)(_irqAutoEnable ? 1 : 0);
        buffer[2] = (byte)(_irq8BitMode ? 1 : 0);
        buffer[3] = (byte)(_irqEnabled ? 1 : 0);
        buffer[4] = (byte)(_irqReload & 0xFF);
        buffer[5] = (byte)(_irqReload >> 8);
        buffer[6] = (byte)(_irqCount & 0xFF);
        buffer[7] = (byte)(_irqCount >> 8);

        int offset = 8;
        Array.Copy(PrgRam, 0, buffer, offset, PrgRam.Length);
        offset += PrgRam.Length;
        if (ChrIsRam)
            Array.Copy(ChrMem, 0, buffer, offset, ChrMem.Length);
        return buffer;
    }

    public override void DeserializeState(byte[] state)
    {
        if (state.Length < 8)
            return;

        _prgBank = state[0];
        _irqAutoEnable = state[1] != 0;
        _irq8BitMode = state[2] != 0;
        _irqEnabled = state[3] != 0;
        _irqReload = (ushort)(state[4] | (state[5] << 8));
        _irqCount = (ushort)(state[6] | (state[7] << 8));

        int offset = 8;
        if (state.Length >= offset + PrgRam.Length)
        {
            Array.Copy(state, offset, PrgRam, 0, PrgRam.Length);
            offset += PrgRam.Length;
        }

        if (ChrIsRam && state.Length >= offset + ChrMem.Length)
            Array.Copy(state, offset, ChrMem, 0, ChrMem.Length);
    }
}

namespace FCRevolution.Core.Mappers;

internal sealed class Mmc3MapperCore : MapperCore
{
    private const int BaseStateSize = 8 * 4 + 6;

    private readonly int[] _bankReg = new int[8];
    private readonly byte[] _mapper74ChrRam = new byte[2048];

    private int _bankSelect;
    private bool _prgMode;
    private bool _chrMode;

    private int _irqPeriod;
    private int _irqCounter;
    private bool _irqEnable;
    private bool _irqReload;
    private bool _irqActive;
    private byte _mapper245ChrLatch;

    public Mmc3MapperCore(MapperCartridge cartridge, InesHeader header, MapperProfile profile)
        : base(cartridge, header, profile)
    {
        CurrentMirroring = header.Mirroring;
        _bankReg[2] = 0;
        _bankReg[3] = 1;
        _bankReg[4] = 2;
        _bankReg[5] = 3;
        _bankReg[6] = 0;
        _bankReg[7] = 1;
    }

    public override bool IrqActive => _irqActive;

    public override byte CpuRead(ushort address)
    {
        if (TryReadPrgRam(address, out var ram)) return ram;

        if (address < 0x8000)
            return 0;

        int bank = GetPrgBank(address);
        return PrgRom[(bank * 8192 + (address & 0x1FFF)) % PrgRom.Length];
    }

    public override void CpuWrite(ushort address, byte data)
    {
        if (TryWritePrgRam(address, data)) return;

        if (address < 0x8000)
            return;

        switch (address & 0xE001)
        {
            case 0x8000:
                _bankSelect = data & 7;
                _prgMode = (data & 0x40) != 0;
                _chrMode = (data & 0x80) != 0;
                break;
            case 0x8001:
                _bankReg[_bankSelect] = data;
                if (Profile.Number == 245 && _bankSelect <= 5)
                    _mapper245ChrLatch = data;
                break;
            case 0xA000:
                CurrentMirroring = (data & 1) == 0 ? MirroringMode.Vertical : MirroringMode.Horizontal;
                break;
            case 0xC000:
                _irqPeriod = data;
                break;
            case 0xC001:
                _irqReload = true;
                break;
            case 0xE000:
                _irqEnable = false;
                _irqActive = false;
                break;
            case 0xE001:
                _irqEnable = true;
                break;
        }
    }

    public override byte PpuRead(ushort address)
    {
        if (address >= 0x2000)
            return 0;

        int bank = GetChrBank(address);
        int offset = address & 0x03FF;

        if (Profile.Number == 74 && (bank == 8 || bank == 9))
            return _mapper74ChrRam[((bank - 8) * 1024) + offset];

        if (Profile.Number == 245 && ChrIsRam)
            return ReadChr(address);

        return ReadChrAt(bank * 1024 + offset);
    }

    public override void PpuWrite(ushort address, byte data)
    {
        if (address >= 0x2000)
            return;

        int bank = GetChrBank(address);
        int offset = address & 0x03FF;

        if (Profile.Number == 74 && (bank == 8 || bank == 9))
        {
            _mapper74ChrRam[((bank - 8) * 1024) + offset] = data;
            return;
        }

        if (Profile.Number == 245 && ChrIsRam)
        {
            WriteChr(address, data);
            return;
        }

        WriteChrAt(bank * 1024 + offset, data);
    }

    public override void SignalScanline()
    {
        if (_irqCounter == 0 || _irqReload)
        {
            _irqCounter = _irqPeriod;
            _irqReload = false;
        }
        else
        {
            _irqCounter--;
        }

        if (_irqCounter == 0 && _irqEnable)
            _irqActive = true;
    }

    public override void Reset() => _irqActive = false;

    public override byte[] SerializeState()
    {
        int variantSize = Profile.Number switch
        {
            74 => _mapper74ChrRam.Length,
            245 => 1,
            _ => 0,
        };

        var buf = new byte[BaseStateSize + variantSize];
        int offset = 0;

        foreach (int reg in _bankReg)
        {
            BitConverter.TryWriteBytes(buf.AsSpan(offset), reg);
            offset += 4;
        }

        buf[offset++] = (byte)_bankSelect;
        buf[offset++] = (byte)(
            (_prgMode ? 1 : 0)
            | (_chrMode ? 2 : 0)
            | (_irqEnable ? 4 : 0)
            | (_irqReload ? 8 : 0)
            | (_irqActive ? 16 : 0));
        buf[offset++] = (byte)(_irqPeriod & 0xFF);
        buf[offset++] = (byte)((_irqPeriod >> 8) & 0xFF);
        buf[offset++] = (byte)(_irqCounter & 0xFF);
        buf[offset] = (byte)((_irqCounter >> 8) & 0xFF);

        offset = BaseStateSize;
        if (Profile.Number == 245)
            buf[offset] = _mapper245ChrLatch;
        else if (Profile.Number == 74)
            Array.Copy(_mapper74ChrRam, 0, buf, offset, _mapper74ChrRam.Length);

        return buf;
    }

    public override void DeserializeState(byte[] state)
    {
        if (state.Length < BaseStateSize)
            return;

        int offset = 0;
        for (int i = 0; i < 8; i++)
        {
            _bankReg[i] = BitConverter.ToInt32(state, offset);
            offset += 4;
        }

        _bankSelect = state[offset] & 7;
        byte flags = state[offset + 1];
        _prgMode = (flags & 1) != 0;
        _chrMode = (flags & 2) != 0;
        _irqEnable = (flags & 4) != 0;
        _irqReload = (flags & 8) != 0;
        _irqActive = (flags & 16) != 0;
        _irqPeriod = state[offset + 2] | (state[offset + 3] << 8);
        _irqCounter = state[offset + 4] | (state[offset + 5] << 8);

        offset = BaseStateSize;
        if (Profile.Number == 245 && state.Length > offset)
            _mapper245ChrLatch = state[offset];
        else if (Profile.Number == 74 && state.Length >= offset + _mapper74ChrRam.Length)
            Array.Copy(state, offset, _mapper74ChrRam, 0, _mapper74ChrRam.Length);
    }

    private int GetPrgBank(ushort address)
    {
        int slot = (address - 0x8000) / 0x2000;
        int bank = slot switch
        {
            0 => _prgMode ? PrgBanks8K - 2 : _bankReg[6],
            1 => _bankReg[7],
            2 => _prgMode ? _bankReg[6] : PrgBanks8K - 2,
            _ => PrgBanks8K - 1,
        };

        if (Profile.Number == 245)
            bank = (bank & 0x3F) | ((_mapper245ChrLatch & 0x02) << 5);

        return bank % PrgBanks8K;
    }

    private int GetChrBank(ushort address)
    {
        int slot = address / 0x400;
        int bank;
        if (_chrMode)
        {
            bank = slot switch
            {
                0 => _bankReg[2],
                1 => _bankReg[3],
                2 => _bankReg[4],
                3 => _bankReg[5],
                4 => _bankReg[0] & 0xFE,
                5 => (_bankReg[0] & 0xFE) + 1,
                6 => _bankReg[1] & 0xFE,
                _ => (_bankReg[1] & 0xFE) + 1,
            };
        }
        else
        {
            bank = slot switch
            {
                0 => _bankReg[0] & 0xFE,
                1 => (_bankReg[0] & 0xFE) + 1,
                2 => _bankReg[1] & 0xFE,
                3 => (_bankReg[1] & 0xFE) + 1,
                4 => _bankReg[2],
                5 => _bankReg[3],
                6 => _bankReg[4],
                _ => _bankReg[5],
            };
        }

        if (Profile.Number == 245 && !ChrIsRam)
            return bank & 0x07;

        return bank;
    }
}

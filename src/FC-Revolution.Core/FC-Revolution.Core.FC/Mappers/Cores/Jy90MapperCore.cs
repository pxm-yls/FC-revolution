namespace FCRevolution.Core.Mappers;

internal sealed class Jy90MapperCore : MapperCore, ICpuCycleDrivenMapper, IPpuAddressObserver
{
    private readonly byte[] _mul = [0xFF, 0xFF];
    private readonly byte[] _tkcom = new byte[4];
    private readonly byte[] _prgBanks = [0xFF, 0xFF, 0xFF, 0xFF];
    private readonly byte[] _chrLow = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
    private readonly byte[] _chrHigh = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
    private readonly byte[] _chrLatch = [0x00, 0x04];
    private readonly ushort[] _nameRegs = new ushort[4];

    private byte _regie = 0xFF;
    private byte _irqMode;
    private byte _irqPre;
    private byte _irqPreSize;
    private byte _irqCount;
    private byte _irqXor;
    private bool _irqEnabled;
    private bool _irqActive;
    private ushort _lastPpuAddress;

    public Jy90MapperCore(MapperCartridge cartridge, InesHeader header, MapperProfile profile)
        : base(cartridge, header, profile)
    {
        Reset();
    }

    public override bool IrqActive => _irqActive;

    public override byte CpuRead(ushort address)
    {
        switch (address & 0x5C03)
        {
            case 0x5800:
                return (byte)(_mul[0] * _mul[1]);
            case 0x5801:
                return (byte)((_mul[0] * _mul[1]) >> 8);
            case 0x5803:
                return _regie;
        }

        if (address >= 0x6000 && address < 0x8000 && (_tkcom[0] & 0x80) != 0)
            return ReadPrgAt8K(GetPrgBankFor6000(), address);

        if (address >= 0x8000)
            return ReadPrgAt8K(GetPrgBankForAddress(address), address);

        return 0;
    }

    public override void CpuWrite(ushort address, byte data)
    {
        switch (address & 0xFC00)
        {
            case 0x5800:
                WriteMathRegister(address, data);
                break;
            case 0x8000:
            case 0x8400:
            case 0x8800:
            case 0x8C00:
                _prgBanks[address & 0x03] = data;
                break;
            case 0x9000:
            case 0x9400:
            case 0x9800:
            case 0x9C00:
                _chrLow[address & 0x07] = data;
                break;
            case 0xA000:
            case 0xA400:
            case 0xA800:
            case 0xAC00:
                _chrHigh[address & 0x07] = data;
                break;
            case 0xB000:
            case 0xB400:
            case 0xB800:
            case 0xBC00:
                WriteNametableRegister(address, data);
                break;
            case 0xC000:
            case 0xC400:
            case 0xC800:
            case 0xCC00:
                WriteIrqRegister(address, data);
                break;
            case 0xD000:
            case 0xD400:
                _tkcom[address & 0x03] = data;
                UpdateMirroring();
                break;
        }

        if ((_irqMode & 0x03) == 0x03)
        {
            ClockCounter();
            ClockCounter();
        }
    }

    public override byte PpuRead(ushort address)
    {
        if (address >= 0x2000)
            return 0;

        int bank = GetChrBank(address);
        return ReadChrAt((bank * 0x0400) + (address & 0x03FF));
    }

    public override void PpuWrite(ushort address, byte data)
    {
        if (address < 0x2000)
        {
            int bank = GetChrBank(address);
            WriteChrAt((bank * 0x0400) + (address & 0x03FF), data);
        }
    }

    public void AdvanceCpuCycles(int cycles)
    {
        if ((_irqMode & 0x03) != 0x00)
            return;

        for (int i = 0; i < cycles; i++)
            ClockCounter();
    }

    public void ObservePpuAddress(ushort address)
    {
        if ((_irqMode & 0x03) == 0x02 && _lastPpuAddress != address)
        {
            ClockCounter();
            ClockCounter();
        }

        _lastPpuAddress = address;
        _chrLatch[0] = 0x00;
        _chrLatch[1] = 0x04;
    }

    public override void SignalScanline()
    {
        if ((_irqMode & 0x03) != 0x01)
            return;

        for (int i = 0; i < 8; i++)
            ClockCounter();
    }

    public override void Reset()
    {
        _irqMode = 0;
        _irqPre = 0;
        _irqPreSize = 0;
        _irqCount = 0;
        _irqXor = 0;
        _irqEnabled = false;
        _irqActive = false;
        _regie = 0xFF;
        _mul[0] = 0xFF;
        _mul[1] = 0xFF;
        Array.Clear(_tkcom);
        Array.Fill(_prgBanks, (byte)0xFF);
        Array.Fill(_chrLow, (byte)0xFF);
        Array.Fill(_chrHigh, (byte)0xFF);
        Array.Clear(_nameRegs);
        _chrLatch[0] = 0x00;
        _chrLatch[1] = 0x04;
        _lastPpuAddress = 0;
        UpdateMirroring();
    }

    public override byte[] SerializeState()
    {
        var buffer = new byte[40];
        int offset = 0;
        buffer[offset++] = _irqMode;
        buffer[offset++] = _irqPre;
        buffer[offset++] = _irqPreSize;
        buffer[offset++] = _irqCount;
        buffer[offset++] = _irqXor;
        buffer[offset++] = (byte)(_irqEnabled ? 1 : 0);
        buffer[offset++] = (byte)(_irqActive ? 1 : 0);
        buffer[offset++] = _regie;
        buffer[offset++] = _mul[0];
        buffer[offset++] = _mul[1];
        Array.Copy(_tkcom, 0, buffer, offset, _tkcom.Length);
        offset += _tkcom.Length;
        Array.Copy(_prgBanks, 0, buffer, offset, _prgBanks.Length);
        offset += _prgBanks.Length;
        Array.Copy(_chrLow, 0, buffer, offset, _chrLow.Length);
        offset += _chrLow.Length;
        Array.Copy(_chrHigh, 0, buffer, offset, _chrHigh.Length);
        offset += _chrHigh.Length;
        buffer[offset++] = _chrLatch[0];
        buffer[offset++] = _chrLatch[1];
        buffer[offset++] = (byte)(_lastPpuAddress & 0xFF);
        buffer[offset] = (byte)(_lastPpuAddress >> 8);
        return buffer;
    }

    public override void DeserializeState(byte[] state)
    {
        if (state.Length < 40)
            return;

        int offset = 0;
        _irqMode = state[offset++];
        _irqPre = state[offset++];
        _irqPreSize = state[offset++];
        _irqCount = state[offset++];
        _irqXor = state[offset++];
        _irqEnabled = state[offset++] != 0;
        _irqActive = state[offset++] != 0;
        _regie = state[offset++];
        _mul[0] = state[offset++];
        _mul[1] = state[offset++];
        Array.Copy(state, offset, _tkcom, 0, _tkcom.Length);
        offset += _tkcom.Length;
        Array.Copy(state, offset, _prgBanks, 0, _prgBanks.Length);
        offset += _prgBanks.Length;
        Array.Copy(state, offset, _chrLow, 0, _chrLow.Length);
        offset += _chrLow.Length;
        Array.Copy(state, offset, _chrHigh, 0, _chrHigh.Length);
        offset += _chrHigh.Length;
        _chrLatch[0] = state[offset++];
        _chrLatch[1] = state[offset++];
        _lastPpuAddress = (ushort)(state[offset++] | (state[offset] << 8));
        UpdateMirroring();
    }

    private byte ReadPrgAt8K(int bank, ushort address)
        => PrgRom[((bank % PrgBanks8K) * 0x2000) + (address & 0x1FFF)];

    private int GetPrgBankFor6000()
    {
        int bankMode = (_tkcom[3] & 0x06) << 5;
        int prgMode = _tkcom[0] & 0x07;
        return prgMode switch
        {
            0 or 4 => (((_prgBanks[3] << 2) + 3) & 0x3F) | bankMode,
            1 or 5 => (((_prgBanks[3] << 1) + 1) & 0x3F) | bankMode,
            _ => (_prgBanks[3] & 0x3F) | bankMode,
        };
    }

    private int GetPrgBankForAddress(ushort address)
    {
        int slot = (address - 0x8000) / 0x2000;
        int bankMode = (_tkcom[3] & 0x06) << 5;
        int prgMode = _tkcom[0] & 0x07;

        return prgMode switch
        {
            0 => (0x0F << 2) + slot + (((_tkcom[3] & 0x06) << 3)),
            1 => (slot < 2 ? ((_prgBanks[1] & 0x1F) << 1) + slot : (0x1F << 1) + (slot - 2)) + (((_tkcom[3] & 0x06) << 4)),
            2 or 3 => slot switch
            {
                0 => (_prgBanks[0] & 0x3F) | bankMode,
                1 => (_prgBanks[1] & 0x3F) | bankMode,
                2 => (_prgBanks[2] & 0x3F) | bankMode,
                _ => 0x3F | bankMode,
            },
            4 => (((_prgBanks[3] & 0x0F) << 2) + slot) | (((_tkcom[3] & 0x06) << 3)),
            5 => (slot < 2 ? ((_prgBanks[1] & 0x1F) << 1) + slot : ((_prgBanks[3] & 0x1F) << 1) + (slot - 2)) + (((_tkcom[3] & 0x06) << 4)),
            _ => slot switch
            {
                0 => (_prgBanks[0] & 0x3F) | bankMode,
                1 => (_prgBanks[1] & 0x3F) | bankMode,
                2 => (_prgBanks[2] & 0x3F) | bankMode,
                _ => (_prgBanks[3] & 0x3F) | bankMode,
            },
        };
    }

    private int GetChrBank(ushort address)
    {
        int chrMode = _tkcom[0] & 0x18;
        int bank = 0;
        int mask = 0xFFFF;

        if ((_tkcom[3] & 0x20) == 0)
        {
            bank = (_tkcom[3] & 0x01) | ((_tkcom[3] & 0x18) >> 2);
            switch (chrMode)
            {
                case 0x00:
                    bank <<= 5;
                    mask = 0x1F;
                    break;
                case 0x08:
                    bank <<= 6;
                    mask = 0x3F;
                    break;
                case 0x10:
                    bank <<= 7;
                    mask = 0x7F;
                    break;
                case 0x18:
                    bank <<= 8;
                    mask = 0xFF;
                    break;
            }
        }

        int slot = address / 0x0400;
        return chrMode switch
        {
            0x00 => (((_chrLow[0] | (_chrHigh[0] << 8)) & mask) | bank) + slot,
            0x08 => Get4KBChrBank(slot, bank, mask),
            0x10 => (((_chrLow[slot & ~1] | (_chrHigh[slot & ~1] << 8)) & mask) | bank) + (slot & 1),
            _ => ((_chrLow[slot] | (_chrHigh[slot] << 8)) & mask) | bank,
        };
    }

    private int Get4KBChrBank(int slot, int bank, int mask)
    {
        int regIndex = slot < 4 ? _chrLatch[0] : _chrLatch[1];
        int baseBank = ((_chrLow[regIndex] | (_chrHigh[regIndex] << 8)) & mask) | bank;
        return baseBank + (slot & 3);
    }

    private void WriteMathRegister(ushort address, byte data)
    {
        switch (address & 0x5C03)
        {
            case 0x5800:
                _mul[0] = data;
                break;
            case 0x5801:
                _mul[1] = data;
                break;
            case 0x5803:
                _regie = data;
                break;
        }
    }

    private void WriteNametableRegister(ushort address, byte data)
    {
        int index = address & 0x03;
        if ((address & 0x0004) != 0)
            _nameRegs[index] = (ushort)((_nameRegs[index] & 0x00FF) | (data << 8));
        else
            _nameRegs[index] = (ushort)((_nameRegs[index] & 0xFF00) | data);
    }

    private void WriteIrqRegister(ushort address, byte data)
    {
        switch (address & 0x0007)
        {
            case 0:
                _irqEnabled = (data & 0x01) != 0;
                if (!_irqEnabled)
                    _irqActive = false;
                break;
            case 1:
                _irqMode = data;
                break;
            case 2:
                _irqEnabled = false;
                _irqActive = false;
                break;
            case 3:
                _irqEnabled = true;
                break;
            case 4:
                _irqPre = (byte)(data ^ _irqXor);
                break;
            case 5:
                _irqCount = (byte)(data ^ _irqXor);
                break;
            case 6:
                _irqXor = data;
                break;
            case 7:
                _irqPreSize = data;
                break;
        }
    }

    private void ClockCounter()
    {
        byte preMask = (_irqMode & 0x04) != 0 ? (byte)0x07 : (byte)0xFF;
        int countMode = (_irqMode >> 6) & 0x03;

        if (countMode == 1)
        {
            _irqPre++;
            if ((_irqPre & preMask) == 0)
                ClockMainCounter();
        }
        else if (countMode == 2)
        {
            _irqPre--;
            if ((_irqPre & preMask) == preMask)
                ClockMainCounter();
        }
    }

    private void ClockMainCounter()
    {
        int countMode = (_irqMode >> 6) & 0x03;
        if (countMode == 1)
        {
            _irqCount++;
            if (_irqCount == 0 && _irqEnabled)
                _irqActive = true;
        }
        else if (countMode == 2)
        {
            _irqCount--;
            if (_irqCount == 0xFF && _irqEnabled)
                _irqActive = true;
        }
    }

    private void UpdateMirroring()
    {
        CurrentMirroring = (_tkcom[1] & 0x03) switch
        {
            0 => MirroringMode.Vertical,
            1 => MirroringMode.Horizontal,
            2 => MirroringMode.SingleLower,
            _ => MirroringMode.SingleUpper,
        };
    }
}

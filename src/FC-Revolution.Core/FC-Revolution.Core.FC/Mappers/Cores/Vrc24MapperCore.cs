namespace FCRevolution.Core.Mappers;

internal sealed class Vrc24MapperCore : MapperCore, ICpuCycleDrivenMapper
{
    private const ushort Reg1Mask = 0x000A;
    private const ushort Reg2Mask = 0x0005;

    private readonly byte[] _prgRegs = new byte[2];
    private readonly byte[] _chrRegs = new byte[8];
    private readonly int[] _chrHigh = new int[8];

    private ushort _irqCount;
    private byte _irqLatch;
    private bool _irqEnabled;
    private bool _irqReloadMode;
    private bool _irqEnableAfterAck;
    private int _irqAccumulator;
    private bool _irqActive;

    private byte _regCommand;
    private byte _mirrorMode;

    public Vrc24MapperCore(MapperCartridge cartridge, InesHeader header, MapperProfile profile)
        : base(cartridge, header, profile)
    {
        CurrentMirroring = header.Mirroring;
    }

    public override bool IrqActive => _irqActive;

    public override byte CpuRead(ushort address)
    {
        if (TryReadPrgRam(address, out var ram)) return ram;

        if (address < 0x8000)
            return 0;

        int slot = (address - 0x8000) / 0x2000;
        int bank = slot switch
        {
            0 => (_regCommand & 0x02) != 0 ? PrgBanks8K - 2 : _prgRegs[0],
            1 => _prgRegs[1],
            2 => (_regCommand & 0x02) != 0 ? _prgRegs[0] : PrgBanks8K - 2,
            _ => PrgBanks8K - 1,
        };

        return PrgRom[((bank % PrgBanks8K) * 0x2000) + (address & 0x1FFF)];
    }

    public override void CpuWrite(ushort address, byte data)
    {
        if (TryWritePrgRam(address, data)) return;

        if (address < 0x8000)
            return;

        int mapped = (address & 0xF000)
            | (((address & Reg2Mask) != 0 ? 1 : 0) << 1)
            | ((address & Reg1Mask) != 0 ? 1 : 0);

        if (mapped >= 0xB000 && mapped <= 0xE003)
        {
            int chrIndex = ((mapped >> 1) & 1) | ((mapped - 0xB000) >> 11);
            int nibbleShift = (mapped & 1) << 2;
            _chrRegs[chrIndex] = (byte)((_chrRegs[chrIndex] & (0xF0 >> nibbleShift)) | ((data & 0x0F) << nibbleShift));
            if (nibbleShift != 0)
                _chrHigh[chrIndex] = (data & 0x10) << 4;
            return;
        }

        switch (mapped & 0xF003)
        {
            case 0x8000:
            case 0x8001:
            case 0x8002:
            case 0x8003:
                _prgRegs[0] = (byte)(data & 0x1F);
                break;
            case 0xA000:
            case 0xA001:
            case 0xA002:
            case 0xA003:
                _prgRegs[1] = (byte)(data & 0x1F);
                break;
            case 0x9000:
            case 0x9001:
                if (data != 0xFF)
                {
                    _mirrorMode = data;
                    UpdateMirroring();
                }
                break;
            case 0x9002:
            case 0x9003:
                _regCommand = data;
                break;
            case 0xF000:
                _irqActive = false;
                _irqLatch &= 0xF0;
                _irqLatch |= (byte)(data & 0x0F);
                break;
            case 0xF001:
                _irqActive = false;
                _irqLatch &= 0x0F;
                _irqLatch |= (byte)(data << 4);
                break;
            case 0xF002:
                _irqActive = false;
                _irqAccumulator = 0;
                _irqCount = _irqLatch;
                _irqReloadMode = (data & 0x04) != 0;
                _irqEnabled = (data & 0x02) != 0;
                _irqEnableAfterAck = (data & 0x01) != 0;
                break;
            case 0xF003:
                _irqActive = false;
                _irqEnabled = _irqEnableAfterAck;
                break;
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
        if (!_irqEnabled)
            return;

        if (_irqReloadMode)
        {
            _irqAccumulator += cycles;
            while (_irqAccumulator > 0)
            {
                _irqAccumulator--;
                TickIrqCounter();
            }
        }
        else
        {
            _irqAccumulator += cycles * 3;
            while (_irqAccumulator >= 341)
            {
                _irqAccumulator -= 341;
                TickIrqCounter();
            }
        }
    }

    public override void Reset()
    {
        _irqActive = false;
        _irqEnabled = false;
        _irqReloadMode = false;
        _irqEnableAfterAck = false;
        _irqAccumulator = 0;
        _irqCount = 0;
        _irqLatch = 0;
        _regCommand = 0;
        _mirrorMode = 0;
        Array.Clear(_prgRegs);
        Array.Clear(_chrRegs);
        Array.Clear(_chrHigh);
        UpdateMirroring();
    }

    public override byte[] SerializeState()
    {
        var buffer = new byte[32 + PrgRam.Length];
        int offset = 0;
        buffer[offset++] = _prgRegs[0];
        buffer[offset++] = _prgRegs[1];
        Array.Copy(_chrRegs, 0, buffer, offset, _chrRegs.Length);
        offset += _chrRegs.Length;
        for (int i = 0; i < _chrHigh.Length; i++)
            buffer[offset++] = (byte)_chrHigh[i];
        buffer[offset++] = (byte)(_irqCount & 0xFF);
        buffer[offset++] = (byte)(_irqCount >> 8);
        buffer[offset++] = _irqLatch;
        buffer[offset++] = (byte)(_irqEnabled ? 1 : 0);
        buffer[offset++] = (byte)(_irqReloadMode ? 1 : 0);
        buffer[offset++] = (byte)(_irqEnableAfterAck ? 1 : 0);
        buffer[offset++] = (byte)(_irqAccumulator & 0xFF);
        buffer[offset++] = (byte)((_irqAccumulator >> 8) & 0xFF);
        buffer[offset++] = _regCommand;
        buffer[offset++] = _mirrorMode;
        buffer[offset++] = (byte)(_irqActive ? 1 : 0);
        Array.Copy(PrgRam, 0, buffer, offset, PrgRam.Length);
        return buffer;
    }

    public override void DeserializeState(byte[] state)
    {
        if (state.Length < 32)
            return;

        int offset = 0;
        _prgRegs[0] = state[offset++];
        _prgRegs[1] = state[offset++];
        Array.Copy(state, offset, _chrRegs, 0, _chrRegs.Length);
        offset += _chrRegs.Length;
        for (int i = 0; i < _chrHigh.Length; i++)
            _chrHigh[i] = state[offset++];
        _irqCount = (ushort)(state[offset++] | (state[offset++] << 8));
        _irqLatch = state[offset++];
        _irqEnabled = state[offset++] != 0;
        _irqReloadMode = state[offset++] != 0;
        _irqEnableAfterAck = state[offset++] != 0;
        _irqAccumulator = state[offset++] | (state[offset++] << 8);
        _regCommand = state[offset++];
        _mirrorMode = state[offset++];
        _irqActive = state[offset++] != 0;

        if (state.Length >= offset + PrgRam.Length)
            Array.Copy(state, offset, PrgRam, 0, PrgRam.Length);

        UpdateMirroring();
    }

    private int GetChrBank(ushort address)
    {
        int slot = address / 0x0400;
        return (_chrHigh[slot] | _chrRegs[slot]) % ChrBanks1K;
    }

    private void TickIrqCounter()
    {
        _irqCount++;
        if ((_irqCount & 0x0100) != 0)
        {
            _irqActive = true;
            _irqCount = _irqLatch;
        }
    }

    private void UpdateMirroring()
    {
        CurrentMirroring = (_mirrorMode & 0x03) switch
        {
            0 => MirroringMode.Vertical,
            1 => MirroringMode.Horizontal,
            2 => MirroringMode.SingleLower,
            _ => MirroringMode.SingleUpper,
        };
    }
}

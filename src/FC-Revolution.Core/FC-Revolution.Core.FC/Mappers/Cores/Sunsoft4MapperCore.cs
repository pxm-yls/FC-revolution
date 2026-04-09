namespace FCRevolution.Core.Mappers;

internal sealed class Sunsoft4MapperCore : MapperCore, IPpuNametableProvider
{
    private readonly byte[] _chrRegs = new byte[4];

    private byte _prgReg;
    private byte _secondaryChip;
    private byte _activePrgChip;
    private byte _nt1;
    private byte _nt2;
    private byte _mirrorControl;
    private int _readCounter;

    public Sunsoft4MapperCore(MapperCartridge cartridge, InesHeader header, MapperProfile profile)
        : base(cartridge, header, profile)
    {
        CurrentMirroring = header.Mirroring;
    }

    public override byte CpuRead(ushort address)
    {
        if (IsPrgRamAddress(address))
        {
            if (address == 0x6000)
                HandleReadCounter();

            return ReadPrgRam(address);
        }

        if (address >= 0x8000 && address < 0xC000)
        {
            HandleReadCounter();
            int bank = GetSelectablePrgBank();
            return PrgRom[((bank * 0x4000) + (address & 0x3FFF)) % PrgRom.Length];
        }

        if (address >= 0xC000)
        {
            int bank = PrgRomBanks16K - 1;
            return PrgRom[(bank * 0x4000) + (address & 0x3FFF)];
        }

        return 0;
    }

    public override void CpuWrite(ushort address, byte data)
    {
        if (address == 0x6000 && IsPrgRamAddress(address))
        {
            if (data == 0)
            {
                _readCounter = 0;
                _activePrgChip = GetConfiguredPrgChip();
            }

            WritePrgRam(address, data);
            return;
        }

        if (IsPrgRamAddress(address))
        {
            WritePrgRam(address, data);
            return;
        }

        switch (address & 0xF000)
        {
            case 0x8000:
            case 0x9000:
            case 0xA000:
            case 0xB000:
                _chrRegs[(address >> 12) & 0x03] = data;
                break;
            case 0xC000:
                _nt1 = data;
                break;
            case 0xD000:
                _nt2 = data;
                break;
            case 0xE000:
                _mirrorControl = data;
                UpdateMirroring();
                break;
            case 0xF000:
                _prgReg = (byte)(data & 0x07);
                _secondaryChip = (byte)((((data >> 3) & 0x01) ^ 1) & 0x01);
                _activePrgChip = GetConfiguredPrgChip();
                break;
        }
    }

    public override byte PpuRead(ushort address)
    {
        if (address >= 0x2000)
            return 0;

        int slot = address / 0x0800;
        int bank = _chrRegs[slot];
        return ReadChrAt((bank * 0x0800) + (address & 0x07FF));
    }

    public override void PpuWrite(ushort address, byte data)
    {
        if (address < 0x2000)
        {
            int slot = address / 0x0800;
            int bank = _chrRegs[slot];
            WriteChrAt((bank * 0x0800) + (address & 0x07FF), data);
        }
    }

    public override void Reset()
    {
        Array.Clear(_chrRegs);
        _prgReg = 0;
        _secondaryChip = 0;
        _activePrgChip = 0;
        _nt1 = 0;
        _nt2 = 0;
        _mirrorControl = 0;
        _readCounter = 0;
        UpdateMirroring();
    }

    public override byte[] SerializeState()
    {
        var buffer = new byte[10 + PrgRam.Length];
        buffer[0] = _chrRegs[0];
        buffer[1] = _chrRegs[1];
        buffer[2] = _chrRegs[2];
        buffer[3] = _chrRegs[3];
        buffer[4] = _prgReg;
        buffer[5] = _secondaryChip;
        buffer[6] = _activePrgChip;
        buffer[7] = _nt1;
        buffer[8] = _nt2;
        buffer[9] = _mirrorControl;
        Array.Copy(PrgRam, 0, buffer, 10, PrgRam.Length);
        return buffer;
    }

    public override void DeserializeState(byte[] state)
    {
        if (state.Length < 10)
            return;

        _chrRegs[0] = state[0];
        _chrRegs[1] = state[1];
        _chrRegs[2] = state[2];
        _chrRegs[3] = state[3];
        _prgReg = state[4];
        _secondaryChip = state[5];
        _activePrgChip = state[6];
        _nt1 = state[7];
        _nt2 = state[8];
        _mirrorControl = state[9];

        if (state.Length >= 10 + PrgRam.Length)
            Array.Copy(state, 10, PrgRam, 0, PrgRam.Length);

        UpdateMirroring();
    }

    public bool TryReadNametable(ushort address, out byte data)
    {
        if (!UsesChrBackedNametables())
        {
            data = 0;
            return false;
        }

        int bank = GetNametableChrBank(address);
        data = ReadChrAt((bank * 0x0400) + (address & 0x03FF));
        return true;
    }

    public bool TryWriteNametable(ushort address, byte data)
        => UsesChrBackedNametables();

    private int GetSelectablePrgBank()
    {
        int bank = _prgReg;
        if (PrgRomBanks16K > 8)
            bank += _activePrgChip * 8;
        return bank % PrgRomBanks16K;
    }

    private byte GetConfiguredPrgChip()
        => (byte)(PrgRomBanks16K > 8 ? _secondaryChip : 0);

    private bool UsesChrBackedNametables()
        => !ChrIsRam && (_mirrorControl & 0x10) != 0;

    private int GetNametableChrBank(ushort address)
    {
        int table = ((address - 0x2000) >> 10) & 0x03;
        byte selected = (_mirrorControl & 0x03) switch
        {
            0 => (table == 0 || table == 2) ? _nt1 : _nt2,
            1 => table <= 1 ? _nt1 : _nt2,
            2 => _nt1,
            _ => _nt2,
        };

        return (selected | 0x80) % ChrBanks1K;
    }

    private void HandleReadCounter()
    {
        _readCounter++;
        if (_readCounter == 1784)
            _activePrgChip = 0;
    }

    private void UpdateMirroring()
    {
        if (UsesChrBackedNametables())
            return;

        CurrentMirroring = (_mirrorControl & 0x03) switch
        {
            0 => MirroringMode.Vertical,
            1 => MirroringMode.Horizontal,
            2 => MirroringMode.SingleLower,
            _ => MirroringMode.SingleUpper,
        };
    }
}

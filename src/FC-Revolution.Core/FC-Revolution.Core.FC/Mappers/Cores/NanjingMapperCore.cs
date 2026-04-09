namespace FCRevolution.Core.Mappers;

internal sealed class NanjingMapperCore : MapperCore
{
    private readonly int _variant;
    private readonly byte[] _regs = new byte[4];

    private byte _lastStrobe = 1;
    private bool _trigger;
    private int _visibleScanline;
    private int? _forcedPrgBank32;
    private int _chrPage4K;

    public NanjingMapperCore(MapperCartridge cartridge, InesHeader header, MapperProfile profile)
        : base(cartridge, header, profile)
    {
        _variant = profile.Number;
        ResetRegisters();
    }

    public override byte CpuRead(ushort address)
    {
        if (address >= 0x5000 && address < 0x6000 && _variant == 163)
            return ReadLowRegister(address);

        if (TryReadPrgRam(address, out var ram)) return ram;

        if (address < 0x8000)
            return 0;

        int bank = _forcedPrgBank32 ?? ((_regs[0] << 4) | (_regs[1] & 0x0F));
        return PrgRom[((bank * 0x8000) + (address & 0x7FFF)) % PrgRom.Length];
    }

    public override void CpuWrite(ushort address, byte data)
    {
        if (TryWritePrgRam(address, data)) return;

        if (address < 0x5000 || address >= 0x6000)
            return;

        if (_variant == 163)
            WriteMapper163(address, data);
        else
            WriteMapper164(address, data);
    }

    public override byte PpuRead(ushort address)
    {
        if (address >= 0x2000)
            return 0;

        if (_variant == 163)
            return ReadChrAt((_chrPage4K * 0x1000) + (address & 0x0FFF));

        return ReadChr(address);
    }

    public override void PpuWrite(ushort address, byte data)
    {
        if (address >= 0x2000)
            return;

        if (_variant == 163)
            WriteChrAt((_chrPage4K * 0x1000) + (address & 0x0FFF), data);
        else
            WriteChr(address, data);
    }

    public override void SignalScanline()
    {
        if (_variant == 163 && (_regs[1] & 0x80) != 0)
        {
            if (_visibleScanline == 127)
                _chrPage4K = 1;
            else if (_visibleScanline == 239)
                _chrPage4K = 0;
        }

        _visibleScanline++;
        if (_visibleScanline >= 240)
            _visibleScanline = 0;
    }

    public override void Reset()
    {
        ResetRegisters();
    }

    public override byte[] SerializeState()
    {
        int size = 8 + PrgRam.Length + (ChrIsRam ? ChrMem.Length : 0);
        var buffer = new byte[size];
        buffer[0] = _regs[0];
        buffer[1] = _regs[1];
        buffer[2] = _regs[2];
        buffer[3] = _regs[3];
        buffer[4] = _lastStrobe;
        buffer[5] = (byte)(_trigger ? 1 : 0);
        buffer[6] = (byte)_visibleScanline;
        buffer[7] = (byte)_chrPage4K;

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

        _regs[0] = state[0];
        _regs[1] = state[1];
        _regs[2] = state[2];
        _regs[3] = state[3];
        _lastStrobe = state[4];
        _trigger = state[5] != 0;
        _visibleScanline = state[6] % 240;
        _chrPage4K = state[7] & 1;

        int offset = 8;
        if (state.Length >= offset + PrgRam.Length)
        {
            Array.Copy(state, offset, PrgRam, 0, PrgRam.Length);
            offset += PrgRam.Length;
        }

        if (ChrIsRam && state.Length >= offset + ChrMem.Length)
            Array.Copy(state, offset, ChrMem, 0, ChrMem.Length);

        if (_variant != 163)
            _forcedPrgBank32 = null;
    }

    private byte ReadLowRegister(ushort address)
    {
        return (address & 0x7700) switch
        {
            0x5100 => (byte)((_regs[2] | _regs[0] | _regs[1] | _regs[3]) ^ 0xFF),
            0x5500 => _trigger ? (byte)(_regs[2] | _regs[1]) : (byte)0,
            _ => 4,
        };
    }

    private void WriteMapper164(ushort address, byte data)
    {
        switch (address & 0x7300)
        {
            case 0x5100:
                _regs[0] = data;
                _forcedPrgBank32 = null;
                break;
            case 0x5000:
                _regs[1] = data;
                _forcedPrgBank32 = null;
                break;
            case 0x5300:
                _regs[2] = data;
                break;
            case 0x5200:
                _regs[3] = data;
                _forcedPrgBank32 = null;
                break;
        }
    }

    private void WriteMapper163(ushort address, byte data)
    {
        if (address == 0x5101)
        {
            if (_lastStrobe != 0 && data == 0)
                _trigger = !_trigger;

            _lastStrobe = data;
            return;
        }

        if (address == 0x5100 && data == 6)
        {
            _forcedPrgBank32 = 3;
            return;
        }

        switch (address & 0x7300)
        {
            case 0x5200:
                _regs[0] = data;
                _forcedPrgBank32 = null;
                break;
            case 0x5000:
                _regs[1] = data;
                _forcedPrgBank32 = null;
                if ((_regs[1] & 0x80) == 0 && _visibleScanline < 128)
                    _chrPage4K = 0;
                break;
            case 0x5300:
                _regs[2] = data;
                break;
            case 0x5100:
                _regs[3] = data;
                _forcedPrgBank32 = null;
                break;
        }
    }

    private void ResetRegisters()
    {
        Array.Clear(_regs);
        _lastStrobe = 1;
        _trigger = false;
        _visibleScanline = 0;
        _forcedPrgBank32 = null;
        _chrPage4K = 0;

        if (_variant == 164)
            _regs[1] = 0xFF;
    }
}

namespace FCRevolution.Core.Mappers;

internal enum DiscreteLatchVariant
{
    Mapper87,
    Mapper240,
}

internal sealed class DiscreteLatchMapperCore : MapperCore
{
    private readonly DiscreteLatchVariant _variant;
    private byte _latched;

    public DiscreteLatchMapperCore(MapperCartridge cartridge, InesHeader header, MapperProfile profile)
        : base(cartridge, header, profile)
    {
        _variant = profile.Settings as DiscreteLatchVariant? ?? throw new InvalidOperationException($"Mapper {profile.Number} 缺少离散锁存配置。");
        _latched = _variant == DiscreteLatchVariant.Mapper87 ? (byte)0xFF : (byte)0x00;
    }

    public override byte CpuRead(ushort address)
    {
        if (_variant == DiscreteLatchVariant.Mapper240 && IsPrgRamAddress(address))
            return ReadPrgRam(address);

        if (address < 0x8000)
            return 0;

        return _variant switch
        {
            DiscreteLatchVariant.Mapper87 => PrgRom[(address - 0x8000) % PrgRom.Length],
            DiscreteLatchVariant.Mapper240 => PrgRom[((((_latched >> 4) & 0x0F) * 0x8000) + (address & 0x7FFF)) % PrgRom.Length],
            _ => 0,
        };
    }

    public override void CpuWrite(ushort address, byte data)
    {
        if (_variant == DiscreteLatchVariant.Mapper240 && IsPrgRamAddress(address))
        {
            WritePrgRam(address, data);
            return;
        }

        bool handled = _variant switch
        {
            DiscreteLatchVariant.Mapper87 => address >= 0x6000,
            DiscreteLatchVariant.Mapper240 => address >= 0x4020 && address <= 0x5FFF,
            _ => false,
        };

        if (handled)
            _latched = data;
    }

    public override byte PpuRead(ushort address)
    {
        if (address >= 0x2000)
            return 0;

        int index = _variant switch
        {
            DiscreteLatchVariant.Mapper87 => ((((_latched >> 1) & 1) | ((_latched << 1) & 2)) * 0x2000) + address,
            DiscreteLatchVariant.Mapper240 => ((_latched & 0x0F) * 0x2000) + address,
            _ => address,
        };

        return ReadChrAt(index);
    }

    public override void PpuWrite(ushort address, byte data)
    {
        if (address >= 0x2000)
            return;

        int index = _variant switch
        {
            DiscreteLatchVariant.Mapper87 => ((((_latched >> 1) & 1) | ((_latched << 1) & 2)) * 0x2000) + address,
            DiscreteLatchVariant.Mapper240 => ((_latched & 0x0F) * 0x2000) + address,
            _ => address,
        };

        WriteChrAt(index, data);
    }

    public override void Reset()
    {
        _latched = _variant == DiscreteLatchVariant.Mapper87 ? (byte)0xFF : (byte)0x00;
    }

    public override byte[] SerializeState()
    {
        int size = 1 + (_variant == DiscreteLatchVariant.Mapper240 ? PrgRam.Length : 0);
        var buffer = new byte[size];
        buffer[0] = _latched;
        if (_variant == DiscreteLatchVariant.Mapper240 && PrgRam.Length > 0)
            Array.Copy(PrgRam, 0, buffer, 1, PrgRam.Length);
        return buffer;
    }

    public override void DeserializeState(byte[] state)
    {
        if (state.Length == 0)
            return;

        _latched = state[0];
        if (_variant == DiscreteLatchVariant.Mapper240 && state.Length >= 1 + PrgRam.Length)
            Array.Copy(state, 1, PrgRam, 0, PrgRam.Length);
    }
}

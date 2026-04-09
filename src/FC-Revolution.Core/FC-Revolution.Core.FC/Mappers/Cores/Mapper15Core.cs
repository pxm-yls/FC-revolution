namespace FCRevolution.Core.Mappers;

internal sealed class Mapper15Core : MapperCore
{
    private ushort _latchedAddress = 0x8000;
    private byte _latchedData;

    public Mapper15Core(MapperCartridge cartridge, InesHeader header, MapperProfile profile)
        : base(cartridge, header, profile)
    {
        UpdateMirroring();
    }

    public override byte CpuRead(ushort address)
    {
        if (TryReadPrgRam(address, out var ram)) return ram;

        if (address < 0x8000)
            return 0;

        int slot = (address - 0x8000) / 0x2000;
        int bank = GetPrgBank(slot);
        return PrgRom[(bank * 8192 + (address & 0x1FFF)) % PrgRom.Length];
    }

    public override void CpuWrite(ushort address, byte data)
    {
        if (TryWritePrgRam(address, data)) return;

        if (address < 0x8000)
            return;

        _latchedAddress = address;
        _latchedData = data;
        UpdateMirroring();
    }

    public override byte PpuRead(ushort address) => address < 0x2000 ? ReadChr(address) : (byte)0;

    public override void PpuWrite(ushort address, byte data)
    {
        if (address < 0x2000 && (_latchedAddress & 0x0003) != 3)
            WriteChr(address, data);
    }

    public override void Reset()
    {
        _latchedAddress = 0x8000;
        _latchedData = 0;
        UpdateMirroring();
    }

    public override byte[] SerializeState()
    {
        int size = 3 + PrgRam.Length + (ChrIsRam ? ChrMem.Length : 0);
        var buffer = new byte[size];
        buffer[0] = (byte)(_latchedAddress & 0xFF);
        buffer[1] = (byte)(_latchedAddress >> 8);
        buffer[2] = _latchedData;

        int offset = 3;
        if (PrgRam.Length > 0)
        {
            Array.Copy(PrgRam, 0, buffer, offset, PrgRam.Length);
            offset += PrgRam.Length;
        }

        if (ChrIsRam)
            Array.Copy(ChrMem, 0, buffer, offset, ChrMem.Length);

        return buffer;
    }

    public override void DeserializeState(byte[] state)
    {
        if (state.Length < 3)
            return;

        _latchedAddress = (ushort)(state[0] | (state[1] << 8));
        _latchedData = state[2];

        int offset = 3;
        if (state.Length >= offset + PrgRam.Length)
        {
            Array.Copy(state, offset, PrgRam, 0, PrgRam.Length);
            offset += PrgRam.Length;
        }

        if (ChrIsRam && state.Length >= offset + ChrMem.Length)
            Array.Copy(state, offset, ChrMem, 0, ChrMem.Length);

        UpdateMirroring();
    }

    private int GetPrgBank(int slot)
    {
        return (_latchedAddress & 0x0003) switch
        {
            0 => ((_latchedData & 0x3F) << 1) + slot,
            2 => ((_latchedData & 0x3F) << 1) + (_latchedData >> 7),
            1 => ((slot & 1) + (((slot >= 2 ? (_latchedData & 0x3F) | 0x07 : (_latchedData & 0x3F)) << 1))),
            _ => (slot & 1) + ((_latchedData & 0x3F) << 1),
        } % PrgBanks8K;
    }

    private void UpdateMirroring()
    {
        CurrentMirroring = ((_latchedData >> 6) & 1) == 0
            ? MirroringMode.Vertical
            : MirroringMode.Horizontal;
    }
}

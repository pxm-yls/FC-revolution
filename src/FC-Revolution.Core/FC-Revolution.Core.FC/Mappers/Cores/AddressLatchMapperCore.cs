namespace FCRevolution.Core.Mappers;

internal sealed class AddressLatchMapperCore : MapperCore
{
    private ushort _latchedAddress;

    public AddressLatchMapperCore(MapperCartridge cartridge, InesHeader header, MapperProfile profile)
        : base(cartridge, header, profile)
    {
        UpdateMirroring();
    }

    public override byte CpuRead(ushort address)
    {
        if (TryReadPrgRam(address, out var ram)) return ram;

        if (address < 0x8000)
            return 0;

        int bank = (_latchedAddress >> 3) & 0x0F;
        return PrgRom[((bank * 0x8000) + (address & 0x7FFF)) % PrgRom.Length];
    }

    public override void CpuWrite(ushort address, byte data)
    {
        if (TryWritePrgRam(address, data)) return;

        if (address < 0x8000)
            return;

        _latchedAddress = address;
        UpdateMirroring();
    }

    public override void Reset()
    {
        _latchedAddress = 0;
        UpdateMirroring();
    }

    public override byte[] SerializeState()
    {
        var buffer = new byte[2 + PrgRam.Length + (ChrIsRam ? ChrMem.Length : 0)];
        buffer[0] = (byte)(_latchedAddress & 0xFF);
        buffer[1] = (byte)(_latchedAddress >> 8);
        Array.Copy(PrgRam, 0, buffer, 2, PrgRam.Length);
        if (ChrIsRam)
            Array.Copy(ChrMem, 0, buffer, 2 + PrgRam.Length, ChrMem.Length);
        return buffer;
    }

    public override void DeserializeState(byte[] state)
    {
        if (state.Length < 2)
            return;

        _latchedAddress = (ushort)(state[0] | (state[1] << 8));
        if (state.Length >= 2 + PrgRam.Length)
            Array.Copy(state, 2, PrgRam, 0, PrgRam.Length);
        if (ChrIsRam && state.Length >= 2 + PrgRam.Length + ChrMem.Length)
            Array.Copy(state, 2 + PrgRam.Length, ChrMem, 0, ChrMem.Length);

        UpdateMirroring();
    }

    private void UpdateMirroring()
    {
        CurrentMirroring = ((_latchedAddress >> 1) & 1) == 0
            ? MirroringMode.Vertical
            : MirroringMode.Horizontal;
    }
}

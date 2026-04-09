namespace FCRevolution.Core.Mappers;

public sealed class MapperCartridge : MapperBase, ICpuCycleDrivenMapper, IPpuAddressObserver, IPpuNametableProvider
{
    private readonly MapperProfile _profile;
    private readonly MapperCore _core;

    public MapperCartridge(byte[] romData, InesHeader header, MapperProfile profile)
        : base(romData, header, profile.PrgRamSize, profile.ChrRamSize)
    {
        _profile = profile;
        _core = MapperCoreFactory.Create(this, header, profile);
    }

    public string MapperName => _profile.Name;

    internal IExtraAudioChannel? ExtraAudioChannel => _core as IExtraAudioChannel;
    internal ICpuCycleDrivenMapper? CpuCycleDrivenMapper => _core as ICpuCycleDrivenMapper;
    internal IPpuAddressObserver? PpuAddressObserver => _core as IPpuAddressObserver;
    internal IPpuNametableProvider? PpuNametableProvider => _core as IPpuNametableProvider;

    public override int MapperNumber => _profile.Number;
    public override MirroringMode Mirroring => _core.Mirroring;
    public override bool IrqActive => _core.IrqActive;

    public override byte CpuRead(ushort address) => _core.CpuRead(address);
    public override void CpuWrite(ushort address, byte data) => _core.CpuWrite(address, data);
    public override byte PpuRead(ushort address) => _core.PpuRead(address);
    public override void PpuWrite(ushort address, byte data) => _core.PpuWrite(address, data);
    public override byte[] SerializeState() => _core.SerializeState();
    public override void DeserializeState(byte[] state) => _core.DeserializeState(state);
    public override void Reset() => _core.Reset();
    public override void Clock() => _core.Clock();
    public override void SignalScanline() => _core.SignalScanline();

    public void AdvanceCpuCycles(int cycles) => CpuCycleDrivenMapper?.AdvanceCpuCycles(cycles);

    public void ObservePpuAddress(ushort address) => PpuAddressObserver?.ObservePpuAddress(address);

    public bool TryReadNametable(ushort address, out byte data)
    {
        if (PpuNametableProvider != null)
            return PpuNametableProvider.TryReadNametable(address, out data);

        data = 0;
        return false;
    }

    public bool TryWriteNametable(ushort address, byte data)
        => PpuNametableProvider != null && PpuNametableProvider.TryWriteNametable(address, data);
}

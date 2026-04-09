namespace FCRevolution.Core.Mappers;

/// <summary>
/// Optional capability for mapper cores that must advance internal state
/// using CPU cycle counts instead of scanline-only callbacks.
/// </summary>
public interface ICpuCycleDrivenMapper
{
    void AdvanceCpuCycles(int cycles);
}

/// <summary>
/// Optional capability for mapper cores that need to observe fine-grained
/// PPU address activity, such as A12 edges or fetch patterns.
/// </summary>
public interface IPpuAddressObserver
{
    void ObservePpuAddress(ushort address);
}

/// <summary>
/// Optional capability for mapper cores that override nametable access
/// instead of relying only on mirroring into internal PPU VRAM.
/// Address is expected in the $2000-$3EFF range.
/// </summary>
public interface IPpuNametableProvider
{
    bool TryReadNametable(ushort address, out byte data);
    bool TryWriteNametable(ushort address, byte data);
}

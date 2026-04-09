namespace FCRevolution.Emulation.Abstractions;

public sealed class CoreDebugState
{
    public byte A { get; init; }

    public byte X { get; init; }

    public byte Y { get; init; }

    public byte S { get; init; }

    public ushort PC { get; init; }

    public byte P { get; init; }

    public ulong TotalCycles { get; init; }

    public int PpuScanline { get; init; }

    public int PpuCycle { get; init; }

    public long PpuFrame { get; init; }

    public byte PpuCtrl { get; init; }

    public byte PpuMask { get; init; }

    public byte PpuStatus { get; init; }

    public string FlagLine { get; init; } = string.Empty;

    public string CycleLine { get; init; } = string.Empty;
}

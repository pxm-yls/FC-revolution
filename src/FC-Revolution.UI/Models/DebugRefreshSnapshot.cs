using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.Models;

public sealed class DebugRefreshSnapshot
{
    public CoreDebugState State { get; init; } = new();

    public ushort MemoryPageStart { get; init; }

    public byte[] MemoryPage { get; init; } = [];

    public ushort StackPageStart { get; init; }

    public byte[] StackPage { get; init; } = [];

    public ushort ZeroPageStart { get; init; }

    public byte[] ZeroPage { get; init; } = [];

    public ushort DisasmStart { get; init; }

    public byte[] Disasm { get; init; } = [];
}

public sealed class DebugRefreshRequest
{
    public int MemoryPageIndex { get; init; }

    public int StackPageIndex { get; init; }

    public int ZeroPageSliceIndex { get; init; }

    public int DisasmPageIndex { get; init; }

    public bool CaptureRegisters { get; init; }

    public bool CapturePpu { get; init; }

    public bool CaptureMemoryPage { get; init; }

    public bool CaptureStack { get; init; }

    public bool CaptureZeroPage { get; init; }

    public bool CaptureDisasm { get; init; }

    public bool RequiresState => CaptureRegisters || CapturePpu || CaptureDisasm;

    public bool RequiresAnyCapture =>
        CaptureRegisters ||
        CapturePpu ||
        CaptureMemoryPage ||
        CaptureStack ||
        CaptureZeroPage ||
        CaptureDisasm;
}

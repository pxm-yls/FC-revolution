using FCRevolution.Core.CPU;
using FCRevolution.Core.PPU;

namespace FCRevolution.Core.Debug;

/// <summary>Snapshot of CPU + PPU state for debug display, captured each frame.</summary>
public sealed class DebugState
{
    // ── CPU ───────────────────────────────────────────────────────────────
    public byte   A  { get; init; }
    public byte   X  { get; init; }
    public byte   Y  { get; init; }
    public byte   S  { get; init; }
    public ushort PC { get; init; }
    public StatusFlags P { get; init; }

    public ulong TotalCycles { get; init; }

    // ── PPU ───────────────────────────────────────────────────────────────
    public int  PpuScanline { get; init; }
    public int  PpuCycle    { get; init; }
    public long PpuFrame    { get; init; }
    public PpuControl PpuCtrl   { get; init; }
    public PpuMask    PpuMask   { get; init; }
    public PpuStatus  PpuStatus { get; init; }

    // ── Formatted helpers ─────────────────────────────────────────────────
    public string CpuLine =>
        $"A:{A:X2}  X:{X:X2}  Y:{Y:X2}  S:{S:X2}  PC:{PC:X4}  P:{(byte)P:X2}";

    public string FlagLine =>
        $"N:{Bit(StatusFlags.Negative)} V:{Bit(StatusFlags.Overflow)} -:{Bit(StatusFlags.Unused)} " +
        $"B:{Bit(StatusFlags.Break)} D:{Bit(StatusFlags.Decimal)} I:{Bit(StatusFlags.IRQDisable)} " +
        $"Z:{Bit(StatusFlags.Zero)} C:{Bit(StatusFlags.Carry)}";

    public string PpuLine =>
        $"SL:{PpuScanline,4}  CY:{PpuCycle,4}  FR:{PpuFrame}";

    public string CycleLine => $"CPU总周期: {TotalCycles:N0}";

    private int Bit(StatusFlags f) => P.HasFlag(f) ? 1 : 0;

    public static DebugState Capture(NesConsole nes) => new()
    {
        A  = nes.Cpu.A,
        X  = nes.Cpu.X,
        Y  = nes.Cpu.Y,
        S  = nes.Cpu.S,
        PC = nes.Cpu.PC,
        P  = nes.Cpu.P,
        TotalCycles = nes.Cpu.TotalCycles,
        PpuScanline = nes.Ppu.Scanline,
        PpuCycle    = nes.Ppu.Cycle,
        PpuFrame    = nes.Ppu.Frame,
        PpuCtrl     = nes.Ppu.Control,
        PpuMask     = nes.Ppu.Mask,
        PpuStatus   = nes.Ppu.Status,
    };
}

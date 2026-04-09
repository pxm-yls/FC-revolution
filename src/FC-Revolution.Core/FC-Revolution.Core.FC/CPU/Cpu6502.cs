using FCRevolution.Core.Bus;

namespace FCRevolution.Core.CPU;

public sealed partial class Cpu6502 : IEmulationComponent, IStateSerializable
{
    // ── Registers ─────────────────────────────────────────────────────────
    public byte A  { get; set; }       // Accumulator
    public byte X  { get; set; }       // X Index
    public byte Y  { get; set; }       // Y Index
    public byte S  { get; set; } = 0xFD; // Stack Pointer
    public ushort PC { get; set; }     // Program Counter
    public StatusFlags P { get; set; } = StatusFlags.Unused | StatusFlags.IRQDisable;

    // ── Interrupt lines ────────────────────────────────────────────────────
    public bool NmiPending  { get; set; }
    public bool IrqPending  { get; set; }

    // ── Cycle accounting ──────────────────────────────────────────────────
    public ulong TotalCycles { get; private set; }
    private int _extraCycles;

    // ── Bus ───────────────────────────────────────────────────────────────
    private readonly IBus _bus;

    // ── Lookup tables (populated in ctor) ─────────────────────────────────
    private readonly Action[] _instructionTable = new Action[256];
    private readonly int[] _cycleTable = new int[256];

    // Working state for each instruction fetch
    private ushort _absAddr;
    private byte   _relAddr;
    private bool   _isAccumulator;

    public Cpu6502(IBus bus)
    {
        _bus = bus;
        BuildLookupTables();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Public interface
    // ─────────────────────────────────────────────────────────────────────

    public void Reset()
    {
        ushort lo = Read(0xFFFC);
        ushort hi = Read(0xFFFD);
        PC = (ushort)((hi << 8) | lo);
        A = X = Y = 0; S = 0xFD;
        P = StatusFlags.Unused | StatusFlags.IRQDisable;
    }

    public void Clock() => ExecuteStep();

    public int ExecuteStep()
    {
        if (NmiPending) { NmiPending = false; HandleNmi(); return 7; }
        if (IrqPending && !P.HasFlag(StatusFlags.IRQDisable)) { IrqPending = false; HandleIrq(); return 7; }
        _extraCycles = 0; _isAccumulator = false;
        byte opcode = Read(PC++);
        _addrModeDispatch[opcode]();
        _instructionTable[opcode]();
        SetFlag(StatusFlags.Unused, true);
        int cy = _cycleTable[opcode] + _extraCycles;
        TotalCycles += (ulong)cy;
        return cy;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  State serialization
    // ─────────────────────────────────────────────────────────────────────

    public byte[] SerializeState()
    {
        var buf = new byte[17];
        buf[0] = A; buf[1] = X; buf[2] = Y; buf[3] = S;
        buf[4] = (byte)(PC & 0xFF); buf[5] = (byte)(PC >> 8);
        buf[6] = (byte)P;
        buf[7] = NmiPending ? (byte)1 : (byte)0;
        buf[8] = IrqPending ? (byte)1 : (byte)0;
        // TotalCycles (8 bytes, indices 9-16)
        ulong tc = TotalCycles;
        for (int i = 0; i < 8; i++) { buf[9 + i] = (byte)(tc & 0xFF); tc >>= 8; }
        return buf;
    }

    public void DeserializeState(byte[] state)
    {
        A = state[0]; X = state[1]; Y = state[2]; S = state[3];
        PC = (ushort)(state[4] | (state[5] << 8));
        P = (StatusFlags)state[6];
        NmiPending = state[7] != 0;
        IrqPending = state[8] != 0;
        ulong tc = 0;
        for (int i = 7; i >= 0; i--) tc = (tc << 8) | state[9 + i];
        TotalCycles = tc;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Interrupt handlers
    // ─────────────────────────────────────────────────────────────────────

    private void HandleNmi()
    {
        StackPush((byte)(PC >> 8)); StackPush((byte)(PC & 0xFF));
        SetFlag(StatusFlags.Break, false); SetFlag(StatusFlags.Unused, true); SetFlag(StatusFlags.IRQDisable, true);
        StackPush((byte)P);
        PC = (ushort)(Read(0xFFFA) | (Read(0xFFFB) << 8));
        TotalCycles += 7;
    }

    private void HandleIrq()
    {
        StackPush((byte)(PC >> 8)); StackPush((byte)(PC & 0xFF));
        SetFlag(StatusFlags.Break, false); SetFlag(StatusFlags.Unused, true); SetFlag(StatusFlags.IRQDisable, true);
        StackPush((byte)P);
        PC = (ushort)(Read(0xFFFE) | (Read(0xFFFF) << 8));
        TotalCycles += 7;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────

    private byte Read(ushort addr) => _bus.Read(addr);
    private void Write(ushort addr, byte data) => _bus.Write(addr, data);

    private void StackPush(byte val) => Write((ushort)(0x0100 | S--), val);
    private byte StackPop() => Read((ushort)(0x0100 | ++S));

    private void SetFlag(StatusFlags flag, bool value)
    {
        if (value) P |= flag;
        else P &= ~flag;
    }

    private bool GetFlag(StatusFlags flag) => P.HasFlag(flag);

    private byte Fetch() => Read(_absAddr);
    private static bool PageCrossed(ushort a, ushort b) => (a & 0xFF00) != (b & 0xFF00);

}

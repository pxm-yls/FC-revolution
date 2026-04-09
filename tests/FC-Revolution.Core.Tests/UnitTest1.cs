using FCRevolution.Core.Bus;
using FCRevolution.Core.CPU;

namespace FC_Revolution.Core.Tests;

// ── Minimal mock bus ──────────────────────────────────────────────────────
internal sealed class TestBus : IBus
{
    public readonly byte[] Mem = new byte[65536];
    public byte Read(ushort a) => Mem[a];
    public void Write(ushort a, byte d) => Mem[a] = d;
    public void LoadAt(ushort addr, params byte[] code)
    {
        Array.Copy(code, 0, Mem, addr, code.Length);
        // Set reset vector to addr
        Mem[0xFFFC] = (byte)(addr & 0xFF);
        Mem[0xFFFD] = (byte)(addr >> 8);
    }
}

public class Cpu6502Tests
{
    private static (Cpu6502 cpu, TestBus bus) Make(ushort origin, params byte[] code)
    {
        var bus = new TestBus();
        bus.LoadAt(origin, code);
        var cpu = new Cpu6502(bus);
        cpu.Reset();
        return (cpu, bus);
    }

    // ── LDA ───────────────────────────────────────────────────────────────

    [Fact]
    public void LDA_IMM_loadsValueIntoA()
    {
        var (cpu, _) = Make(0x8000, 0xA9, 0x42); // LDA #$42
        cpu.ExecuteStep();
        Assert.Equal(0x42, cpu.A);
        Assert.False(cpu.P.HasFlag(StatusFlags.Zero));
        Assert.False(cpu.P.HasFlag(StatusFlags.Negative));
    }

    [Fact]
    public void LDA_IMM_setsZeroFlag()
    {
        var (cpu, _) = Make(0x8000, 0xA9, 0x00); // LDA #$00
        cpu.ExecuteStep();
        Assert.Equal(0x00, cpu.A);
        Assert.True(cpu.P.HasFlag(StatusFlags.Zero));
    }

    [Fact]
    public void LDA_IMM_setsNegativeFlag()
    {
        var (cpu, _) = Make(0x8000, 0xA9, 0x80); // LDA #$80
        cpu.ExecuteStep();
        Assert.True(cpu.P.HasFlag(StatusFlags.Negative));
    }

    // ── STA / LDA roundtrip ───────────────────────────────────────────────

    [Fact]
    public void STA_ZP_storesAccumulator()
    {
        var (cpu, bus) = Make(0x8000,
            0xA9, 0x55,  // LDA #$55
            0x85, 0x10); // STA $10
        cpu.ExecuteStep();
        cpu.ExecuteStep();
        Assert.Equal(0x55, bus.Mem[0x10]);
    }

    // ── ADC ───────────────────────────────────────────────────────────────

    [Fact]
    public void ADC_IMM_addsWithoutCarry()
    {
        var (cpu, _) = Make(0x8000,
            0xA9, 0x10,  // LDA #$10
            0x69, 0x20); // ADC #$20
        cpu.ExecuteStep();
        cpu.ExecuteStep();
        Assert.Equal(0x30, cpu.A);
        Assert.False(cpu.P.HasFlag(StatusFlags.Carry));
    }

    [Fact]
    public void ADC_IMM_setsCarryOnOverflow()
    {
        var (cpu, _) = Make(0x8000,
            0xA9, 0xFF,  // LDA #$FF
            0x69, 0x01); // ADC #$01
        cpu.ExecuteStep();
        cpu.ExecuteStep();
        Assert.Equal(0x00, cpu.A);
        Assert.True(cpu.P.HasFlag(StatusFlags.Carry));
        Assert.True(cpu.P.HasFlag(StatusFlags.Zero));
    }

    // ── SBC ───────────────────────────────────────────────────────────────

    [Fact]
    public void SBC_IMM_subtractsWithBorrow()
    {
        var (cpu, _) = Make(0x8000,
            0x38,        // SEC
            0xA9, 0x10,  // LDA #$10
            0xE9, 0x05); // SBC #$05
        cpu.ExecuteStep();
        cpu.ExecuteStep();
        cpu.ExecuteStep();
        Assert.Equal(0x0B, cpu.A);
        Assert.True(cpu.P.HasFlag(StatusFlags.Carry));
    }

    // ── INX / DEX ─────────────────────────────────────────────────────────

    [Fact]
    public void INX_incrementsX()
    {
        var (cpu, _) = Make(0x8000,
            0xA2, 0x0F,  // LDX #$0F
            0xE8);       // INX
        cpu.ExecuteStep();
        cpu.ExecuteStep();
        Assert.Equal(0x10, cpu.X);
    }

    [Fact]
    public void DEX_decrementsX()
    {
        var (cpu, _) = Make(0x8000,
            0xA2, 0x10,  // LDX #$10
            0xCA);       // DEX
        cpu.ExecuteStep();
        cpu.ExecuteStep();
        Assert.Equal(0x0F, cpu.X);
    }

    // ── JMP ───────────────────────────────────────────────────────────────

    [Fact]
    public void JMP_ABS_setsPC()
    {
        var (cpu, bus) = Make(0x8000, 0x4C, 0x00, 0x90); // JMP $9000
        bus.Mem[0x9000] = 0xEA; // NOP
        cpu.ExecuteStep();
        Assert.Equal(0x9000, cpu.PC);
    }

    // ── JSR / RTS ─────────────────────────────────────────────────────────

    [Fact]
    public void JSR_RTS_returnsCorrectly()
    {
        var (cpu, bus) = Make(0x8000,
            0x20, 0x10, 0x80,  // JSR $8010
            0xEA);             // NOP (at $8003)
        bus.Mem[0x8010] = 0x60; // RTS
        cpu.ExecuteStep(); // JSR -> PC = $8010
        Assert.Equal(0x8010, cpu.PC);
        cpu.ExecuteStep(); // RTS -> PC = $8003
        Assert.Equal(0x8003, cpu.PC);
    }

    // ── Branch ────────────────────────────────────────────────────────────

    [Fact]
    public void BEQ_branches_whenZeroSet()
    {
        var (cpu, bus) = Make(0x8000,
            0xA9, 0x00,  // LDA #$00  (Z=1)
            0xF0, 0x02); // BEQ +2  -> jumps to $8006
        bus.Mem[0x8006] = 0xEA;
        cpu.ExecuteStep(); // LDA
        cpu.ExecuteStep(); // BEQ
        Assert.Equal(0x8006, cpu.PC);
    }

    [Fact]
    public void BNE_doesNotBranch_whenZeroSet()
    {
        var (cpu, _) = Make(0x8000,
            0xA9, 0x00,  // LDA #$00  (Z=1)
            0xD0, 0x02); // BNE +2  -> should NOT branch
        cpu.ExecuteStep();
        cpu.ExecuteStep();
        Assert.Equal(0x8004, cpu.PC);
    }

    // ── Stack ─────────────────────────────────────────────────────────────

    [Fact]
    public void PHA_PLA_roundtrip()
    {
        var (cpu, _) = Make(0x8000,
            0xA9, 0xAB,  // LDA #$AB
            0x48,        // PHA
            0xA9, 0x00,  // LDA #$00
            0x68);       // PLA
        cpu.ExecuteStep();
        cpu.ExecuteStep();
        cpu.ExecuteStep();
        cpu.ExecuteStep();
        Assert.Equal(0xAB, cpu.A);
    }

    // ── ASL accumulator ───────────────────────────────────────────────────

    [Fact]
    public void ASL_ACC_shiftsLeft()
    {
        var (cpu, _) = Make(0x8000,
            0xA9, 0x41,  // LDA #$41 = 0100_0001
            0x0A);       // ASL A    = 1000_0010
        cpu.ExecuteStep();
        cpu.ExecuteStep();
        Assert.Equal(0x82, cpu.A);
        Assert.False(cpu.P.HasFlag(StatusFlags.Carry));
        Assert.True(cpu.P.HasFlag(StatusFlags.Negative));
    }

    // ── NMI ───────────────────────────────────────────────────────────────

    [Fact]
    public void NMI_vectorsCorrectly()
    {
        var (cpu, bus) = Make(0x8000, 0xEA); // NOP
        bus.Mem[0xFFFA] = 0x00;
        bus.Mem[0xFFFB] = 0x90; // NMI vector -> $9000
        bus.Mem[0x9000] = 0xEA;

        cpu.NmiPending = true;
        cpu.ExecuteStep(); // handles NMI
        Assert.Equal(0x9000, cpu.PC);
        Assert.True(cpu.P.HasFlag(StatusFlags.IRQDisable));
    }

    // ── Cycle count ───────────────────────────────────────────────────────

    [Fact]
    public void LDA_IMM_takes2Cycles()
    {
        var (cpu, _) = Make(0x8000, 0xA9, 0x01);
        int cy = cpu.ExecuteStep();
        Assert.Equal(2, cy);
    }

    [Fact]
    public void JMP_ABS_takes3Cycles()
    {
        var (cpu, bus) = Make(0x8000, 0x4C, 0x00, 0x90);
        bus.Mem[0x9000] = 0xEA;
        int cy = cpu.ExecuteStep();
        Assert.Equal(3, cy);
    }
}

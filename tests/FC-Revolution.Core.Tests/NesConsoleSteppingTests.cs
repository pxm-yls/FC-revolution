using FCRevolution.Core;
using FCRevolution.Core.CPU;

namespace FC_Revolution.Core.Tests;

public class NesConsoleSteppingTests
{
    [Fact]
    public void RunFrame_And_StepInstruction_ToNextFrame_ProduceEquivalentCpuVisibleState()
    {
        var rom = CreateMinimalNromRom(
            0xE6, 0x00,       // INC $00
            0xA5, 0x00,       // LDA $00
            0x8D, 0x00, 0x02, // STA $0200
            0x4C, 0x00, 0x80  // JMP $8000
        );

        var runFrameConsole = new NesConsole();
        runFrameConsole.LoadRom(rom);

        var stepInstructionConsole = new NesConsole();
        stepInstructionConsole.LoadRom(rom);

        runFrameConsole.RunFrame();

        long startFrame = stepInstructionConsole.Ppu.Frame;
        while (stepInstructionConsole.Ppu.Frame == startFrame)
            stepInstructionConsole.StepInstruction();

        Assert.Equal(runFrameConsole.Ppu.Frame, stepInstructionConsole.Ppu.Frame);
        Assert.Equal(runFrameConsole.CpuCycles, stepInstructionConsole.CpuCycles);
        AssertCpuStateEqual(runFrameConsole.Cpu, stepInstructionConsole.Cpu);
        Assert.Equal(runFrameConsole.Bus.GetRam(), stepInstructionConsole.Bus.GetRam());
    }

    [Fact]
    public void StepClock_HaltsCpuDuringDma_And_ResumesAfterTransferWindow()
    {
        var rom = CreateMinimalNromRom(
            0xA9, 0x02,       // LDA #$02
            0x8D, 0x14, 0x40, // STA $4014 (start OAM DMA from page $02)
            0xE6, 0x10,       // INC $10
            0x4C, 0x07, 0x80  // JMP $8007
        );

        var nes = new NesConsole();
        nes.LoadRom(rom);

        nes.StepInstruction(); // LDA #$02
        nes.StepInstruction(); // STA $4014, DMA becomes active

        Assert.True(nes.Bus.DmaActive);
        Assert.Equal(0x8005, nes.Cpu.PC);
        Assert.Equal(6L, nes.CpuCycles);

        long cyclesBeforeDmaWindow = nes.CpuCycles;
        ushort pcBeforeDmaWindow = nes.Cpu.PC;

        for (int i = 0; i < 512; i++)
            nes.StepClock();

        Assert.True(nes.Bus.DmaActive);
        Assert.Equal(pcBeforeDmaWindow, nes.Cpu.PC);
        Assert.Equal(cyclesBeforeDmaWindow, nes.CpuCycles);

        nes.StepClock(); // Clears DMA pending state, CPU still halted this call

        Assert.False(nes.Bus.DmaActive);
        Assert.Equal(pcBeforeDmaWindow, nes.Cpu.PC);
        Assert.Equal(cyclesBeforeDmaWindow, nes.CpuCycles);

        nes.StepClock(); // CPU resumes and executes INC $10

        Assert.Equal((ushort)0x8007, nes.Cpu.PC);
        Assert.True(nes.CpuCycles > cyclesBeforeDmaWindow);
        Assert.Equal((byte)0x01, nes.Bus.GetRam()[0x0010]);
    }

    [Fact]
    public void StepInstruction_WhenDmaIsActive_DoesNotExecuteNextOpcodeUntilTransferCompletes()
    {
        var rom = CreateMinimalNromRom(
            0xA9, 0x02,       // LDA #$02
            0x8D, 0x14, 0x40, // STA $4014 (start OAM DMA from page $02)
            0xE6, 0x10,       // INC $10
            0x4C, 0x07, 0x80  // JMP $8007
        );

        var nes = new NesConsole();
        nes.LoadRom(rom);

        nes.StepInstruction(); // LDA #$02
        nes.StepInstruction(); // STA $4014, DMA becomes active

        Assert.True(nes.Bus.DmaActive);
        long cpuCyclesBeforeDmaWindow = nes.CpuCycles;
        ushort pcBeforeDmaWindow = nes.Cpu.PC;

        for (int i = 0; i < 512; i++)
        {
            int dmaSliceResult = nes.StepInstruction();
            Assert.Equal(0, dmaSliceResult);
            Assert.True(nes.Bus.DmaActive);
            Assert.Equal(pcBeforeDmaWindow, nes.Cpu.PC);
            Assert.Equal(cpuCyclesBeforeDmaWindow, nes.CpuCycles);
            Assert.Equal((byte)0x00, nes.Bus.GetRam()[0x0010]);
        }

        int finalDmaSliceResult = nes.StepInstruction();
        Assert.Equal(0, finalDmaSliceResult);
        Assert.False(nes.Bus.DmaActive);
        Assert.Equal(pcBeforeDmaWindow, nes.Cpu.PC);
        Assert.Equal(cpuCyclesBeforeDmaWindow, nes.CpuCycles);
        Assert.Equal((byte)0x00, nes.Bus.GetRam()[0x0010]);

        int resumedInstructionCycles = nes.StepInstruction();
        Assert.True(resumedInstructionCycles > 0);
        Assert.Equal((ushort)0x8007, nes.Cpu.PC);
        Assert.Equal((byte)0x01, nes.Bus.GetRam()[0x0010]);
    }

    [Fact]
    public void StepInstruction_WhenNmiAndIrqPending_HandlesNmiFirst_AndDefersIrqUntilIrqEnableCleared()
    {
        var rom = CreateMinimalNromRom(
            new ushort[] { 0x9000, 0x8000, 0xA000 },
            0xEA // NOP at $8000
        );

        int prgStart = 16;
        rom[prgStart + 0x1000] = 0x58; // $9000: CLI
        rom[prgStart + 0x1001] = 0xEA; // $9001: NOP
        rom[prgStart + 0x2000] = 0xEA; // $A000: IRQ handler NOP

        var nes = new NesConsole();
        nes.LoadRom(rom);

        nes.Cpu.P &= ~StatusFlags.IRQDisable; // Allow IRQ if chosen
        nes.Cpu.IrqPending = true;
        nes.Ppu.NmiPending = true;

        int nmiCycles = nes.StepInstruction();

        Assert.Equal(7, nmiCycles);
        Assert.Equal((ushort)0x9000, nes.Cpu.PC);
        Assert.True(nes.Cpu.IrqPending);
        Assert.True(nes.Cpu.P.HasFlag(StatusFlags.IRQDisable));
        Assert.False(nes.Ppu.NmiPending);

        int cliCycles = nes.StepInstruction(); // Executes CLI at $9000
        Assert.Equal(2, cliCycles);
        Assert.Equal((ushort)0x9001, nes.Cpu.PC);
        Assert.False(nes.Cpu.P.HasFlag(StatusFlags.IRQDisable));

        int irqCycles = nes.StepInstruction(); // Pending IRQ now delivers
        Assert.Equal(7, irqCycles);
        Assert.Equal((ushort)0xA000, nes.Cpu.PC);
        Assert.False(nes.Cpu.IrqPending);
    }

    [Fact]
    public void Cpu_LdaAbsX_UsesExtraCycleOnlyWhenPageCrossed()
    {
        static Cpu6502 BuildCpuWithProgram(byte low)
        {
            var bus = new TestBus();
            bus.LoadAt(0x8000,
                0xA2, 0x01,       // LDX #$01
                0xBD, low, 0x20); // LDA $20xx,X
            bus.Mem[0x20FF] = 0x11;
            bus.Mem[0x2100] = 0x22;

            var cpu = new Cpu6502(bus);
            cpu.Reset();
            return cpu;
        }

        var nonCrossCpu = BuildCpuWithProgram(0xFE); // $20FE + X => $20FF (no cross)
        Assert.Equal(2, nonCrossCpu.ExecuteStep());  // LDX
        int nonCrossCycles = nonCrossCpu.ExecuteStep();
        Assert.Equal(4, nonCrossCycles);
        Assert.Equal((byte)0x11, nonCrossCpu.A);

        var crossCpu = BuildCpuWithProgram(0xFF);    // $20FF + X => $2100 (cross)
        Assert.Equal(2, crossCpu.ExecuteStep());     // LDX
        int crossCycles = crossCpu.ExecuteStep();
        Assert.Equal(5, crossCycles);
        Assert.Equal((byte)0x22, crossCpu.A);
    }

    [Fact]
    public void RunFrame_WhenNmiAndIrqArePending_HandlesNmiBeforeIrq()
    {
        var rom = CreateMinimalNromRom(
            new ushort[] { 0x9000, 0x8000, 0xA000 },
            0x58,             // $8000: CLI
            0x4C, 0x00, 0x80  // $8001: JMP $8000
        );

        int prgStart = 16;
        rom[prgStart + 0x1000] = 0xE6; // $9000: INC $20
        rom[prgStart + 0x1001] = 0x20;
        rom[prgStart + 0x1002] = 0xA5; // $9002: LDA $20
        rom[prgStart + 0x1003] = 0x20;
        rom[prgStart + 0x1004] = 0x85; // $9004: STA $21 (NMI order marker)
        rom[prgStart + 0x1005] = 0x21;
        rom[prgStart + 0x1006] = 0x40; // $9006: RTI

        rom[prgStart + 0x2000] = 0xE6; // $A000: INC $20
        rom[prgStart + 0x2001] = 0x20;
        rom[prgStart + 0x2002] = 0xA5; // $A002: LDA $20
        rom[prgStart + 0x2003] = 0x20;
        rom[prgStart + 0x2004] = 0x85; // $A004: STA $22 (IRQ order marker)
        rom[prgStart + 0x2005] = 0x22;
        rom[prgStart + 0x2006] = 0x40; // $A006: RTI

        var nes = new NesConsole();
        nes.LoadRom(rom);

        // Prevent APU frame IRQ from introducing extra IRQ handling in this frame window.
        nes.Apu.WriteRegister(0x4017, 0x40);
        nes.Cpu.IrqPending = true;
        nes.Ppu.NmiPending = true;

        nes.RunFrame();

        var ram = nes.Bus.GetRam();
        Assert.Equal((byte)0x02, ram[0x20]); // both handlers ran exactly once
        Assert.Equal((byte)0x01, ram[0x21]); // NMI observed first count
        Assert.Equal((byte)0x02, ram[0x22]); // IRQ observed second count
    }

    [Fact]
    public void RunFrame_RaisesFrameReadyExactlyOncePerCall()
    {
        var rom = CreateMinimalNromRom(
            0xEA,             // NOP
            0x4C, 0x00, 0x80  // JMP $8000
        );

        var nes = new NesConsole();
        nes.LoadRom(rom);

        int frameReadyCount = 0;
        nes.FrameReady += _ => frameReadyCount++;

        nes.RunFrame();
        Assert.Equal(1, frameReadyCount);

        nes.RunFrame();
        Assert.Equal(2, frameReadyCount);
    }

    [Fact]
    public void StepClock_RaisesFrameReadyExactlyOnce_WhenFrameCompletesWithinThreePpuTicks()
    {
        var rom = CreateMinimalNromRom(
            0xEA,             // NOP
            0x4C, 0x00, 0x80  // JMP $8000
        );

        var nes = new NesConsole();
        nes.LoadRom(rom);

        int frameReadyCount = 0;
        nes.FrameReady += _ => frameReadyCount++;

        nes.Ppu.FrameComplete = false;
        nes.Ppu.Scanline = 260;
        nes.Ppu.Cycle = 339;

        nes.StepClock();

        Assert.Equal(1, frameReadyCount);
        Assert.True(nes.Ppu.FrameComplete);

        nes.StepClock();
        Assert.Equal(1, frameReadyCount);
    }

    [Fact]
    public void StepClock_AdvancesAtInstructionBoundaries_InNonDmaPath()
    {
        var rom = CreateMinimalNromRom(
            0xA9, 0x42, // LDA #$42
            0x85, 0x10, // STA $10
            0xE8,       // INX
            0x4C, 0x05, 0x80 // JMP $8005
        );

        var nes = new NesConsole();
        nes.LoadRom(rom);

        Assert.Equal((ushort)0x8000, nes.Cpu.PC);
        Assert.Equal(0, nes.Ppu.Cycle);
        Assert.Equal(-1, nes.Ppu.Scanline);
        Assert.Equal((byte)0x00, nes.Bus.GetRam()[0x0010]);

        nes.StepClock(); // LDA #$42
        Assert.Equal((ushort)0x8002, nes.Cpu.PC);
        Assert.Equal((byte)0x42, nes.Cpu.A);
        Assert.Equal((byte)0x00, nes.Bus.GetRam()[0x0010]);
        Assert.Equal(3, nes.Ppu.Cycle);
        Assert.Equal(-1, nes.Ppu.Scanline);

        nes.StepClock(); // STA $10
        Assert.Equal((ushort)0x8004, nes.Cpu.PC);
        Assert.Equal((byte)0x42, nes.Bus.GetRam()[0x0010]);
        Assert.Equal(6, nes.Ppu.Cycle);
        Assert.Equal(-1, nes.Ppu.Scanline);

        nes.StepClock(); // INX
        Assert.Equal((ushort)0x8005, nes.Cpu.PC);
        Assert.Equal((byte)0x01, nes.Cpu.X);
        Assert.Equal(9, nes.Ppu.Cycle);
        Assert.Equal(-1, nes.Ppu.Scanline);
    }

    private static void AssertCpuStateEqual(Cpu6502 expected, Cpu6502 actual)
    {
        Assert.Equal(expected.A, actual.A);
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.S, actual.S);
        Assert.Equal(expected.P, actual.P);
        Assert.Equal(expected.PC, actual.PC);
        Assert.Equal(expected.TotalCycles, actual.TotalCycles);
    }

    private static byte[] CreateMinimalNromRom(params byte[] program)
        => CreateMinimalNromRom(new ushort[] { 0x8000, 0x8000, 0x8000 }, program);

    private static byte[] CreateMinimalNromRom(ushort[] vectors, params byte[] program)
    {
        if (vectors.Length != 3)
            throw new ArgumentException("vectors must contain NMI, RESET, IRQ addresses");

        var rom = new byte[16 + 16384 + 8192];
        rom[0] = (byte)'N';
        rom[1] = (byte)'E';
        rom[2] = (byte)'S';
        rom[3] = 0x1A;
        rom[4] = 1; // 16KB PRG
        rom[5] = 1; // 8KB CHR

        int prgStart = 16;
        Array.Copy(program, 0, rom, prgStart, program.Length);

        // NROM-128 vectors are in the mirrored top of the single 16KB PRG bank.
        rom[prgStart + 0x3FFA] = (byte)(vectors[0] & 0xFF);
        rom[prgStart + 0x3FFB] = (byte)(vectors[0] >> 8);
        rom[prgStart + 0x3FFC] = (byte)(vectors[1] & 0xFF);
        rom[prgStart + 0x3FFD] = (byte)(vectors[1] >> 8);
        rom[prgStart + 0x3FFE] = (byte)(vectors[2] & 0xFF);
        rom[prgStart + 0x3FFF] = (byte)(vectors[2] >> 8);

        return rom;
    }
}

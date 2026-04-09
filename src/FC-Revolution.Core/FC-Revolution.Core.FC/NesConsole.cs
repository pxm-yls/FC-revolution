using FCRevolution.Core.APU;
using FCRevolution.Core.Bus;
using FCRevolution.Core.CPU;
using FCRevolution.Core.Input;
using FCRevolution.Core.Mappers;
using FCRevolution.Core.PPU;
using FCRevolution.Core.State;
using FCRevolution.Core.Timeline;

namespace FCRevolution.Core;

public sealed class NesConsole
{
    public readonly Cpu6502  Cpu;
    public readonly Ppu2C02  Ppu;
    public readonly Apu2A03  Apu;
    public readonly NesBus   Bus;

    public ICartridge? Cartridge { get; private set; }

    public bool Running  { get; private set; }
    public long CpuCycles { get; private set; }

    public readonly TimelineController Timeline;

    // ── Frame complete callback ──────────────────────────────────────────
    public event Action<uint[]>? FrameReady;

    // ── Audio chunk callback ─────────────────────────────────────────────
    public event Action<float[]>? AudioChunkReady;

    private IExtraAudioChannel? ExtraAudioChannel =>
        Cartridge is MapperCartridge mapperCartridge
            ? mapperCartridge.ExtraAudioChannel
            : Cartridge as IExtraAudioChannel;

    private ICpuCycleDrivenMapper? CpuCycleDrivenMapper =>
        Cartridge is MapperCartridge mapperCartridge
            ? mapperCartridge.CpuCycleDrivenMapper
            : Cartridge as ICpuCycleDrivenMapper;

    public NesConsole()
    {
        Bus = new NesBus();
        Cpu = new Cpu6502(Bus);
        Ppu = new Ppu2C02();
        Apu = new Apu2A03();

        Bus.Ppu = Ppu;
        Bus.Apu = Apu;
        Apu.SampleBatchReady += chunk => AudioChunkReady?.Invoke(chunk);
        Apu.Dmc.DmaRead  = addr => Bus.Read(addr);
        Timeline = new TimelineController(this);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  ROM management
    // ─────────────────────────────────────────────────────────────────────

    public void LoadRom(string path) => LoadRom(File.ReadAllBytes(path));

    public void LoadRom(byte[] data)
    {
        Cartridge = MapperFactory.Create(data);
        Bus.Cartridge = Cartridge;
        Ppu.InsertCartridge(Cartridge);
        var extraAudio = ExtraAudioChannel;
        Apu.ExtraAudioSampleProvider = extraAudio == null ? null : () => extraAudio.ExtraAudioSample;
        Reset();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Control
    // ─────────────────────────────────────────────────────────────────────

    public void Reset()
    {
        Cartridge?.Reset();
        ExtraAudioChannel?.ResetExtraAudio();
        Ppu.Reset();
        Apu.Reset();
        Cpu.Reset();
        CpuCycles = 0;
        Running = true;
        Timeline.Reset();
    }

    public void Start()  => Running = true;
    public void Pause()  => Running = false;
    public void Stop()   => Running = false;

    // ─────────────────────────────────────────────────────────────────────
    //  Execution
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Run one full video frame (~29780 CPU cycles). Returns the frame buffer.</summary>
    public uint[] RunFrame()
    {
        if (!Running || Cartridge == null) return Ppu.FrameBuffer;

        Ppu.FrameComplete = false;

        while (true)
        {
            ExecuteInstructionOrAdvanceDmaSlice(stopOnFrameComplete: true, out var frameCompleted);
            if (frameCompleted) goto frameDone;
        }

        frameDone:
        Timeline.OnFrameComplete(Ppu.FrameBuffer);
        FrameReady?.Invoke(Ppu.FrameBuffer);
        return Ppu.FrameBuffer;
    }

    /// <summary>Execute a single master clock. PPU runs 3x per CPU cycle.</summary>
    public void StepClock()
    {
        ClockPpuAndHandleNmiAndFrameComplete(
            ppuCycles: 3,
            clearFrameCompleteBeforeEachTick: true,
            stopOnFrameComplete: false,
            out _);
        PropagatePendingInterruptsBeforeInstruction();

        // OAM DMA suspends CPU for 513-514 cycles
        if (Bus.DmaActive)
        {
            Bus.ClockDma();
        }
        else
        {
            ExecuteStepClockCpuPath();
        }

        if (Ppu.FrameComplete)
            FrameReady?.Invoke(Ppu.FrameBuffer);
    }

    /// <summary>Step one CPU instruction (debug use). Propagates NMI/IRQ from PPU/APU.</summary>
    public int StepInstruction()
    {
        return ExecuteInstructionOrAdvanceDmaSlice(stopOnFrameComplete: false, out _);
    }

    private int ExecuteInstructionOrAdvanceDmaSlice(bool stopOnFrameComplete, out bool frameCompleted)
    {
        if (Bus.DmaActive)
        {
            Bus.ClockDma();
            ClockPpuAndHandleNmiAndFrameComplete(
                ppuCycles: 3,
                clearFrameCompleteBeforeEachTick: false,
                stopOnFrameComplete: stopOnFrameComplete,
                out frameCompleted);
            return 0;
        }

        return ExecuteInstructionSkeleton(stopOnFrameComplete, out frameCompleted);
    }

    private int ExecuteInstructionSkeleton(bool stopOnFrameComplete, out bool frameCompleted)
    {
        int cpuCycles = ExecuteCpuStepWithPendingInterruptPropagation(countCpuCyclesByInstructionCycles: true);

        AdvanceApuExtraAudioAndMapper(cpuCycles);
        ClockPpuAndHandleNmiAndFrameComplete(
            ppuCycles: cpuCycles * 3,
            clearFrameCompleteBeforeEachTick: false,
            stopOnFrameComplete: stopOnFrameComplete,
            out frameCompleted);

        return cpuCycles;
    }

    private void PropagatePendingInterruptsBeforeInstruction()
    {
        if (Ppu.NmiPending) { Ppu.NmiPending = false; Cpu.NmiPending = true; }
        if (Apu.IrqActive || (Cartridge?.IrqActive ?? false)) Cpu.IrqPending = true;
    }

    private void AdvanceApuExtraAudioAndMapper(int cpuCycles)
    {
        for (int i = 0; i < cpuCycles; i++)
        {
            Apu.Clock();
            ExtraAudioChannel?.ClockExtraAudio();
            CpuCycleDrivenMapper?.AdvanceCpuCycles(1);
        }
    }

    private void ClockPpuAndHandleNmiAndFrameComplete(
        int ppuCycles,
        bool clearFrameCompleteBeforeEachTick,
        bool stopOnFrameComplete,
        out bool frameCompleted)
    {
        frameCompleted = false;
        bool anyFrameComplete = false;

        for (int i = 0; i < ppuCycles; i++)
        {
            if (clearFrameCompleteBeforeEachTick)
                Ppu.FrameComplete = false;

            Ppu.Clock();
            if (Ppu.NmiPending) { Ppu.NmiPending = false; Cpu.NmiPending = true; }

            anyFrameComplete |= Ppu.FrameComplete;

            if (stopOnFrameComplete && anyFrameComplete)
            {
                frameCompleted = true;
                Ppu.FrameComplete = true;
                return;
            }
        }

        frameCompleted = anyFrameComplete;
        Ppu.FrameComplete = anyFrameComplete;
    }

    private int ExecuteCpuStepWithPendingInterruptPropagation(bool countCpuCyclesByInstructionCycles)
    {
        PropagatePendingInterruptsBeforeInstruction();
        int cpuCycles = Cpu.ExecuteStep();
        CpuCycles += countCpuCyclesByInstructionCycles ? cpuCycles : 1;
        return cpuCycles;
    }

    private void ExecuteStepClockCpuPath()
    {
        AdvanceApuExtraAudioAndMapper(1);
        ExecuteCpuStepWithPendingInterruptPropagation(countCpuCyclesByInstructionCycles: false);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Input
    // ─────────────────────────────────────────────────────────────────────

    public void SetButton(int player, NesButton btn, bool pressed)
    {
        var ctrl = player == 0 ? Bus.Controller1 : Bus.Controller2;
        ctrl.SetButton(btn, pressed);
    }

    public void SetControllerMask(int player, byte buttonsMask)
    {
        var buttons = new[]
        {
            NesButton.A,
            NesButton.B,
            NesButton.Select,
            NesButton.Start,
            NesButton.Up,
            NesButton.Down,
            NesButton.Left,
            NesButton.Right,
        };

        foreach (var button in buttons)
            SetButton(player, button, (buttonsMask & (byte)button) != 0);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  State save/load
    // ─────────────────────────────────────────────────────────────────────

    public StateSnapshotData CaptureSnapshot(uint[]? frameBuffer = null) => new()
    {
        Frame = Ppu.Frame,
        Timestamp = CpuCycles / 1789773.0,
        CpuState = Cpu.SerializeState(),
        PpuState = Ppu.SerializeState(),
        RamState = (byte[])Bus.GetRam().Clone(),
        CartState = Cartridge?.SerializeState() ?? [],
        ApuState = Apu.SerializeState(),
        Thumbnail = frameBuffer == null ? null : FrameSnapshot.MakeThumbnail(frameBuffer),
    };

    public void LoadSnapshot(StateSnapshotData snapshot)
    {
        Cpu.DeserializeState(snapshot.CpuState);
        Ppu.DeserializeState(snapshot.PpuState);
        Array.Copy(snapshot.RamState, Bus.GetRam(), Math.Min(snapshot.RamState.Length, 2048));
        if (Cartridge != null && snapshot.CartState.Length > 0)
            Cartridge.DeserializeState(snapshot.CartState);
        if (snapshot.ApuState.Length > 0)
            Apu.DeserializeState(snapshot.ApuState);
    }

    public byte[] SaveState()
        => StateSnapshotSerializer.Serialize(CaptureSnapshot(), includeThumbnail: false);

    public void LoadState(byte[] state)
    {
        if (StateSnapshotSerializer.HasHeader(state))
        {
            LoadSnapshot(StateSnapshotSerializer.Deserialize(state));
            return;
        }

        int off = 0;
        byte[] Next() { int len = BitConverter.ToInt32(state, off); off += 4; var d = state[off..(off+len)]; off += len; return d; }
        LoadSnapshot(new StateSnapshotData
        {
            CpuState = Next(),
            PpuState = Next(),
            RamState = Next(),
            ApuState = Next(),
            CartState = Cartridge != null && off < state.Length ? Next() : [],
        });
    }
}

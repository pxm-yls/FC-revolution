using FCRevolution.Core.APU.Channels;

namespace FCRevolution.Core.APU;

public sealed class Apu2A03 : IEmulationComponent
{
    public readonly PulseChannel   Pulse1   = new(false);
    public readonly PulseChannel   Pulse2   = new(true);
    public readonly TriangleChannel Triangle = new();
    public readonly NoiseChannel   Noise    = new();
    public readonly DmcChannel     Dmc      = new();

    public bool IrqActive => Dmc.IrqActive || _frameIrq;

    private int  _clock;
    private bool _mode5Step;   // false=4-step, true=5-step
    private bool _irqInhibit;
    private bool _frameIrq;
    private int  _frameStep;
    private int  _frameTimer;

    // NES APU frame counter step timings (CPU cycles)
    // 4-step: quarter@7457, half+q@14913, quarter@22371, half+q+IRQ@29829
    // 5-step: quarter@7457, half+q@14913, quarter@22371, nothing@29829, half+q@37281
    private static readonly int[] Steps4 = { 7457, 14913, 22371, 29829 };
    private static readonly int[] Steps5 = { 7457, 14913, 22371, 29829, 37281 };

    private const int CpuCyclesPerSample = 40; // ~44100 Hz at 1.789 MHz
    private const int SamplesPerChunk = 882;   // ~20 ms at 44.7 kHz
    private int _sampleTimer;
    public event Action<float[]>? SampleBatchReady;
    public Func<float>? ExtraAudioSampleProvider { get; set; }
    private readonly float[] _sampleChunk = new float[SamplesPerChunk];
    private int _sampleChunkPos;

    public float GetOutputSample()
    {
        float p1 = Pulse1.GetSample();
        float p2 = Pulse2.GetSample();
        float tri = Triangle.GetSample();
        float noise = Noise.GetSample();
        float dmc = Dmc.GetSample();

        float pulseOut  = 95.88f / (8128f / (p1 + p2) + 100f);
        float tndOut    = 159.79f / (1f / (tri / 8227f + noise / 12241f + dmc / 22638f) + 100f);
        return pulseOut + tndOut + (ExtraAudioSampleProvider?.Invoke() ?? 0f);
    }

    public void Clock()
    {
        // Triangle clocks every CPU cycle, others every 2
        Triangle.Clock();
        if ((_clock & 1) == 0)
        {
            Pulse1.Clock(); Pulse2.Clock();
            Noise.Clock();  Dmc.Clock();
        }

        // Frame counter (step-based, cycle-accurate)
        _frameTimer++;
        int[] steps = _mode5Step ? Steps5 : Steps4;
        if (_frameStep < steps.Length && _frameTimer >= steps[_frameStep])
        {
            ClockFrameCounter();
            _frameStep++;
            if (_frameStep >= steps.Length)
            {
                _frameStep = 0;
                _frameTimer = 0;
            }
        }

        // Sample output
        _sampleTimer++;
        if (_sampleTimer >= CpuCyclesPerSample)
        {
            _sampleTimer = 0;
            var sample = GetOutputSample();
            _sampleChunk[_sampleChunkPos++] = sample;
            if (_sampleChunkPos >= _sampleChunk.Length)
                FlushSampleChunk();
        }

        _clock++;
    }

    private void ClockFrameCounter()
    {
        // Determine clock type by current step index
        bool quarter, half;
        if (_mode5Step)
        {
            quarter = _frameStep == 0 || _frameStep == 1 || _frameStep == 2 || _frameStep == 4;
            half    = _frameStep == 1 || _frameStep == 4;
        }
        else
        {
            quarter = _frameStep == 0 || _frameStep == 1 || _frameStep == 2 || _frameStep == 3;
            half    = _frameStep == 1 || _frameStep == 3;
        }

        if (quarter)
        {
            Pulse1.ClockEnvelope(); Pulse2.ClockEnvelope();
            Triangle.ClockLinear(); Noise.ClockEnvelope();
        }
        if (half)
        {
            Pulse1.ClockLength(); Pulse1.ClockSweep();
            Pulse2.ClockLength(); Pulse2.ClockSweep();
            Triangle.ClockLength();
            Noise.ClockLength();
        }
        if (!_mode5Step && _frameStep == 3 && !_irqInhibit)
            _frameIrq = true;
    }

    public byte ReadRegister(ushort address)
    {
        if (address == 0x4015)
        {
            byte val = 0;
            if (Pulse1.Enabled)   val |= 0x01;
            if (Pulse2.Enabled)   val |= 0x02;
            if (Triangle.Enabled) val |= 0x04;
            if (Noise.Enabled)    val |= 0x08;
            if (_frameIrq)        val |= 0x40;
            _frameIrq = false;
            return val;
        }
        return 0;
    }

    public void WriteRegister(ushort address, byte data)
    {
        switch (address)
        {
            case 0x4000: Pulse1.WriteReg0(data);    break;
            case 0x4001: Pulse1.WriteReg1(data);    break;
            case 0x4002: Pulse1.WriteReg2(data);    break;
            case 0x4003: Pulse1.WriteReg3(data);    break;
            case 0x4004: Pulse2.WriteReg0(data);    break;
            case 0x4005: Pulse2.WriteReg1(data);    break;
            case 0x4006: Pulse2.WriteReg2(data);    break;
            case 0x4007: Pulse2.WriteReg3(data);    break;
            case 0x4008: Triangle.WriteReg0(data);  break;
            case 0x400A: Triangle.WriteReg2(data);  break;
            case 0x400B: Triangle.WriteReg3(data);  break;
            case 0x400C: Noise.WriteReg0(data);     break;
            case 0x400E: Noise.WriteReg2(data);     break;
            case 0x400F: Noise.WriteReg3(data);     break;
            case 0x4010: Dmc.WriteReg0(data);       break;
            case 0x4011: Dmc.WriteReg1(data);       break;
            case 0x4012: Dmc.WriteReg2(data);       break;
            case 0x4013: Dmc.WriteReg3(data);       break;
            case 0x4015:
                Pulse1.Enabled   = (data & 0x01) != 0;
                Pulse2.Enabled   = (data & 0x02) != 0;
                Triangle.Enabled = (data & 0x04) != 0;
                Noise.Enabled    = (data & 0x08) != 0;
                Dmc.Enabled      = (data & 0x10) != 0;
                if (!Dmc.Enabled) Dmc.ClearIrq();
                break;
            case 0x4017:
                _mode5Step  = (data & 0x80) != 0;
                _irqInhibit = (data & 0x40) != 0;
                if (_irqInhibit) _frameIrq = false;
                _frameTimer = 0; _frameStep = 0;
                if (_mode5Step) { ClockFrameCounter(); _frameStep = 0; }
                break;
        }
    }

    public void Reset()
    {
        _clock = 0; _frameStep = 0; _frameTimer = 0;
        _frameIrq = false; _irqInhibit = false; _mode5Step = false;
        _sampleTimer = 0;
        _sampleChunkPos = 0;
        WriteRegister(0x4015, 0);
    }

    private void FlushSampleChunk()
    {
        if (_sampleChunkPos == 0 || SampleBatchReady == null)
        {
            _sampleChunkPos = 0;
            return;
        }

        var chunk = new float[_sampleChunkPos];
        Array.Copy(_sampleChunk, chunk, _sampleChunkPos);
        _sampleChunkPos = 0;
        SampleBatchReady.Invoke(chunk);
    }

    public byte[] SerializeState()
    {
        // Header: 7 bytes of frame-counter state + _clock (4 bytes) + _sampleTimer (4 bytes)
        var hdr = new byte[15];
        hdr[0] = (byte)((_mode5Step ? 1 : 0) | (_irqInhibit ? 2 : 0) | (_frameIrq ? 4 : 0));
        hdr[1] = (byte)(_frameStep & 0xFF);
        hdr[2] = (byte)((_frameTimer      ) & 0xFF); hdr[3]  = (byte)((_frameTimer >> 8)  & 0xFF);
        hdr[4] = (byte)((_frameTimer >> 16) & 0xFF); hdr[5]  = (byte)((_frameTimer >> 24) & 0xFF);
        hdr[6] = (byte)(_sampleTimer & 0xFF);
        BitConverter.TryWriteBytes(hdr.AsSpan(7),  _clock);
        // Concat channel states
        var p1 = Pulse1.SerializeState();
        var p2 = Pulse2.SerializeState();
        var tr = Triangle.SerializeState();
        var ns = Noise.SerializeState();
        var dm = Dmc.SerializeState();
        var buf = new byte[hdr.Length + p1.Length + p2.Length + tr.Length + ns.Length + dm.Length];
        int o = 0;
        hdr.CopyTo(buf, o); o += hdr.Length;
        p1.CopyTo(buf, o);  o += p1.Length;
        p2.CopyTo(buf, o);  o += p2.Length;
        tr.CopyTo(buf, o);  o += tr.Length;
        ns.CopyTo(buf, o);  o += ns.Length;
        dm.CopyTo(buf, o);
        return buf;
    }

    public void DeserializeState(byte[] state)
    {
        if (state.Length < 15) return;
        byte f    = state[0];
        _mode5Step  = (f & 1) != 0; _irqInhibit = (f & 2) != 0; _frameIrq = (f & 4) != 0;
        _frameStep  = state[1];
        _frameTimer = state[2] | (state[3] << 8) | (state[4] << 16) | (state[5] << 24);
        _sampleTimer = state[6];
        _clock      = BitConverter.ToInt32(state, 7);
        int o = 15;
        Pulse1.DeserializeState(state[o..(o + 15)]);  o += 15;
        Pulse2.DeserializeState(state[o..(o + 15)]);  o += 15;
        Triangle.DeserializeState(state[o..(o + 9)]); o += 9;
        Noise.DeserializeState(state[o..(o + 12)]);   o += 12;
        Dmc.DeserializeState(state[o..]);
    }
}

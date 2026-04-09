using FCRevolution.Core.APU;

namespace FC_Revolution.Core.Tests;

public class ApuFrameCounterTests
{
    private static Apu2A03 MakeApu()
    {
        var apu = new Apu2A03();
        apu.Reset();
        return apu;
    }

    /// <summary>IRQ fires in 4-step mode at cycle 29829.</summary>
    [Fact]
    public void FourStep_IrqFires_By30000Cycles()
    {
        var apu = MakeApu();
        bool irqFired = false;
        for (int i = 0; i < 30000; i++)
        {
            apu.Clock();
            if (apu.IrqActive) { irqFired = true; break; }
        }
        Assert.True(irqFired);
    }

    /// <summary>IRQ does NOT fire before step 4 (first 29828 cycles).</summary>
    [Fact]
    public void FourStep_IrqNotFired_Before29829()
    {
        var apu = MakeApu();
        bool irqFired = false;
        for (int i = 0; i < 29828; i++)
        {
            apu.Clock();
            if (apu.IrqActive) { irqFired = true; break; }
        }
        Assert.False(irqFired);
    }

    /// <summary>5-step mode never fires the frame IRQ.</summary>
    [Fact]
    public void FiveStep_NoIrq_Generated()
    {
        var apu = MakeApu();
        apu.WriteRegister(0x4017, 0x80); // 5-step mode
        bool irqFired = false;
        for (int i = 0; i < 40000; i++)
        {
            apu.Clock();
            if (apu.IrqActive) { irqFired = true; break; }
        }
        Assert.False(irqFired);
    }

    /// <summary>IRQ inhibit prevents frame IRQ.</summary>
    [Fact]
    public void IrqInhibit_PreventsIrq()
    {
        var apu = MakeApu();
        apu.WriteRegister(0x4017, 0x40); // IRQ inhibit
        bool irqFired = false;
        for (int i = 0; i < 40000; i++)
        {
            apu.Clock();
            if (apu.IrqActive) { irqFired = true; break; }
        }
        Assert.False(irqFired);
    }

    /// <summary>Reading $4015 clears frame IRQ flag.</summary>
    [Fact]
    public void ReadStatus_ClearsFrameIrq()
    {
        var apu = MakeApu();
        for (int i = 0; i < 30000; i++) apu.Clock();
        Assert.True(apu.IrqActive);
        apu.ReadRegister(0x4015); // clears _frameIrq
        Assert.False(apu.IrqActive);
    }

    /// <summary>SampleBatchReady emits full audio chunks at the configured cadence.</summary>
    [Fact]
    public void SampleBatchReady_EmitsFullChunksAtExpectedRate()
    {
        var apu = MakeApu();
        var chunkCount = 0;
        var sampleCount = 0;
        apu.SampleBatchReady += chunk =>
        {
            chunkCount++;
            sampleCount += chunk.Length;
        };

        const int samplesPerChunk = 882;
        const int chunkTarget = 5;
        var cpuCycles = 40 * samplesPerChunk * chunkTarget;
        for (int i = 0; i < cpuCycles; i++) apu.Clock();

        Assert.Equal(chunkTarget, chunkCount);
        Assert.Equal(samplesPerChunk * chunkTarget, sampleCount);
    }

    /// <summary>Output sample is in valid [0, 2] range with silent channels.</summary>
    [Fact]
    public void GetOutputSample_SilentChannels_IsNearZero()
    {
        var apu = MakeApu();
        float sample = apu.GetOutputSample();
        Assert.True(sample >= 0f && sample < 0.01f, $"Expected near-zero, got {sample}");
    }

    /// <summary>Output sample is in valid [0, 2] range with active channels.</summary>
    [Fact]
    public void GetOutputSample_ActiveChannels_InRange()
    {
        var apu = MakeApu();
        apu.WriteRegister(0x4015, 0x0F); // enable pulse+triangle+noise
        apu.WriteRegister(0x4000, 0xBF); // Pulse1: 50% duty, constant vol=15
        apu.WriteRegister(0x4004, 0xBF); // Pulse2: same
        float sample = apu.GetOutputSample();
        Assert.True(sample >= 0f && sample <= 2f, $"Sample {sample} out of [0, 2]");
    }

    /// <summary>SerializeState/DeserializeState roundtrip preserves frame counter state.</summary>
    [Fact]
    public void Serialize_Roundtrip_PreservesFrameCounterState()
    {
        var apu = MakeApu();
        // Run to just before step 2 so frame timer and step are nonzero
        for (int i = 0; i < 10000; i++) apu.Clock();

        var state = apu.SerializeState();

        var apu2 = MakeApu();
        apu2.DeserializeState(state);

        // Both apus should fire IRQ at the same cycle from this point
        bool irq1 = false, irq2 = false;
        for (int i = 0; i < 30000; i++)
        {
            apu.Clock();  if (apu.IrqActive  && !irq1)  irq1  = true;
            apu2.Clock(); if (apu2.IrqActive && !irq2) irq2 = true;
            if (irq1 && irq2) break;
        }
        Assert.Equal(irq1, irq2);
    }

    /// <summary>SerializeState/DeserializeState roundtrip preserves enabled channel state.</summary>
    [Fact]
    public void Serialize_Roundtrip_PreservesChannelEnabled()
    {
        var apu = MakeApu();
        apu.WriteRegister(0x4015, 0x0F); // enable P1+P2+Tri+Noise
        apu.WriteRegister(0x4000, 0xBF); // Pulse1 vol=15

        var state = apu.SerializeState();
        var apu2 = MakeApu();
        apu2.DeserializeState(state);

        Assert.True(apu2.Pulse1.Enabled);
        Assert.True(apu2.Pulse2.Enabled);
        Assert.True(apu2.Triangle.Enabled);
        Assert.True(apu2.Noise.Enabled);
        Assert.False(apu2.Dmc.Enabled);
    }
}

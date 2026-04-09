using FCRevolution.Core.PPU;

namespace FC_Revolution.Core.Tests;

public class PpuTimingTests
{
    [Fact]
    public void OddFrame_WithRenderingEnabled_SkipsOnePreRenderCycle()
    {
        var oddFramePpu = new Ppu2C02();
        oddFramePpu.Reset();
        oddFramePpu.WriteRegister(0x2001, (byte)PpuMask.ShowBg);
        oddFramePpu.Frame = 1;
        oddFramePpu.Scanline = -1;
        oddFramePpu.Cycle = 339;

        oddFramePpu.Clock();

        Assert.Equal(0, oddFramePpu.Cycle);
        Assert.Equal(0, oddFramePpu.Scanline);

        var evenFramePpu = new Ppu2C02();
        evenFramePpu.Reset();
        evenFramePpu.WriteRegister(0x2001, (byte)PpuMask.ShowBg);
        evenFramePpu.Frame = 0;
        evenFramePpu.Scanline = -1;
        evenFramePpu.Cycle = 339;

        evenFramePpu.Clock();
        Assert.Equal(340, evenFramePpu.Cycle);
        Assert.Equal(-1, evenFramePpu.Scanline);

        evenFramePpu.Clock();
        Assert.Equal(0, evenFramePpu.Cycle);
        Assert.Equal(0, evenFramePpu.Scanline);
    }

    [Fact]
    public void Vblank_EntryAndClear_FollowsTiming_AndNmiEnableGate()
    {
        var ppu = new Ppu2C02();
        ppu.Reset();

        ppu.Scanline = 241;
        ppu.Cycle = 1;
        ppu.Clock();

        Assert.True(ppu.Status.HasFlag(PpuStatus.VerticalBlank));
        Assert.False(ppu.NmiPending);

        ppu.Scanline = -1;
        ppu.Cycle = 1;
        ppu.Clock();

        Assert.False(ppu.Status.HasFlag(PpuStatus.VerticalBlank));

        ppu.WriteRegister(0x2000, (byte)PpuControl.NmiEnable);
        ppu.NmiPending = false;
        ppu.Scanline = 241;
        ppu.Cycle = 1;
        ppu.Clock();

        Assert.True(ppu.Status.HasFlag(PpuStatus.VerticalBlank));
        Assert.True(ppu.NmiPending);

        byte status = ppu.ReadRegister(0x2002);
        Assert.True((status & (byte)PpuStatus.VerticalBlank) != 0);
        Assert.False(ppu.Status.HasFlag(PpuStatus.VerticalBlank));
    }

    [Fact]
    public void SpriteOverflow_IsSetAtSpriteEvaluationBoundary_WhenMoreThanEightSpritesMatchScanline()
    {
        static Ppu2C02 BuildPpuWithSpritesOnScanline(int visibleSpriteCount)
        {
            var ppu = new Ppu2C02();
            ppu.Reset();
            ppu.WriteRegister(0x2001, (byte)PpuMask.ShowSprites);
            ppu.Scanline = 0;
            ppu.Cycle = 257; // EvaluateSprites timing point

            for (int i = 0; i < 64; i++)
                ppu.Oam[i * 4] = 0xFF; // move all sprites off the target scanline

            for (int i = 0; i < visibleSpriteCount; i++)
            {
                int baseIndex = i * 4;
                ppu.Oam[baseIndex] = 0;         // Y: visible on scanline 0 in this implementation
                ppu.Oam[baseIndex + 1] = 0x01;  // tile
                ppu.Oam[baseIndex + 2] = 0x00;  // attr
                ppu.Oam[baseIndex + 3] = (byte)(i * 8);
            }

            return ppu;
        }

        var exactlyEight = BuildPpuWithSpritesOnScanline(8);
        exactlyEight.Clock();
        Assert.False(exactlyEight.Status.HasFlag(PpuStatus.SpriteOverflow));

        var nineSprites = BuildPpuWithSpritesOnScanline(9);
        nineSprites.Clock();
        Assert.True(nineSprites.Status.HasFlag(PpuStatus.SpriteOverflow));
    }

    [Fact]
    public void SerializeState_DeserializeState_RoundTripsPublicState()
    {
        var source = new Ppu2C02();
        source.Reset();
        source.WriteRegister(0x2000, (byte)PpuControl.NmiEnable);
        source.WriteRegister(0x2001, (byte)(PpuMask.ShowBg | PpuMask.ShowSprites));
        source.Scanline = 241;
        source.Cycle = 1;
        source.Clock();

        for (int i = 0; i < source.Vram.Length; i++)
            source.Vram[i] = (byte)(i * 3);
        for (int i = 0; i < source.PaletteRam.Length; i++)
            source.PaletteRam[i] = (byte)(i + 7);
        for (int i = 0; i < source.Oam.Length; i++)
            source.Oam[i] = (byte)(255 - i);

        source.Scanline = 37;
        source.Cycle = 128;
        source.Frame = 123456789L;

        var restored = new Ppu2C02();
        restored.Reset();
        restored.DeserializeState(source.SerializeState());

        Assert.Equal(source.Control, restored.Control);
        Assert.Equal(source.Mask, restored.Mask);
        Assert.Equal(source.Status, restored.Status);
        Assert.Equal(source.Scanline, restored.Scanline);
        Assert.Equal(source.Cycle, restored.Cycle);
        Assert.Equal(source.Frame, restored.Frame);
        Assert.Equal(source.Vram, restored.Vram);
        Assert.Equal(source.PaletteRam, restored.PaletteRam);
        Assert.Equal(source.Oam, restored.Oam);
    }
}

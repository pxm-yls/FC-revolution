using FCRevolution.Core;
using FCRevolution.Core.Mappers;
using FCRevolution.Core.PPU;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Common;

namespace FC_Revolution.Rendering.Tests;

public sealed class RenderDataExtractorTests
{
    [Fact]
    public void Extract_BatchWithPreviousFrame_RemainsStable_AndWithinAllocationBudget()
    {
        var ppu = new Ppu2C02();
        var cartridge = new TestCartridge(MirroringMode.Vertical);
        cartridge.PatternTable[0x0123] = 0xAB;
        cartridge.PatternTable[0x1FFE] = 0xCD;
        ppu.InsertCartridge(cartridge);

        ppu.Vram[4] = 0x44;
        ppu.Vram[0x03C1] = 0b_00_00_00_10;
        ppu.PaletteRam[1] = 0x21;
        ppu.PaletteRam[0x1F] = 0x30;

        ppu.Oam[0] = 20;
        ppu.Oam[1] = 7;
        ppu.Oam[2] = 0x41;
        ppu.Oam[3] = 30;
        ppu.Oam[4] = 120;
        ppu.Oam[5] = 15;
        ppu.Oam[6] = 0x83;
        ppu.Oam[7] = 88;

        SetScrollState(ppu, fineX: 3, fineY: 2, coarseX: 5, coarseY: 4, nametableSelect: 1);

        var previous = new FrameMetadata(
            sprites:
            [
                new SpriteEntry { Y = 18, TileId = 7, Attrs = 0x41, X = 24 },
                new SpriteEntry { Y = 122, TileId = 15, Attrs = 0x83, X = 90 }
            ]);

        var extractor = new RenderDataExtractor();

        // Warm-up to reduce one-time runtime noise in allocation sampling.
        _ = extractor.Extract(ppu.CaptureRenderStateSnapshot(), previousFrame: previous);
        FrameMetadata baseline = extractor.Extract(ppu.CaptureRenderStateSnapshot(), previousFrame: previous);

        const int iterations = 200;
        const long allocationCeilingBytes = 40 * 1024 * 1024;

        long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            FrameMetadata current = extractor.Extract(ppu.CaptureRenderStateSnapshot(), previousFrame: previous);
            AssertMetadataEquivalent(baseline, current);
        }
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;

        Assert.True(
            allocatedBytes <= allocationCeilingBytes,
            $"Extract batch allocated {allocatedBytes} bytes, expected <= {allocationCeilingBytes} bytes.");
    }

    [Fact]
    public void Extract_CapturesScrollVisibleTilesAndSprites()
    {
        var ppu = new Ppu2C02();
        var cartridge = new TestCartridge(MirroringMode.Vertical);
        cartridge.PatternTable[0x0123] = 0xAB;
        ppu.InsertCartridge(cartridge);
        ppu.Vram[4] = 0x44;
        ppu.Vram[0x03C1] = 0b_00_00_00_10;
        ppu.Oam[0] = 20;
        ppu.Oam[1] = 7;
        ppu.Oam[2] = 0x41;
        ppu.Oam[3] = 30;
        ppu.PaletteRam[1] = 0x21;

        SetScrollState(ppu, fineX: 1, fineY: 0, coarseX: 4, coarseY: 0, nametableSelect: 0);

        var extractor = new RenderDataExtractor();
        FrameMetadata metadata = extractor.Extract(ppu.CaptureRenderStateSnapshot(), screenWidth: 16, screenHeight: 8);

        Assert.Equal(1, metadata.FineScrollX);
        Assert.Equal(4, metadata.CoarseScrollX);
        Assert.Equal(0, metadata.NametableSelect);
        Assert.Equal(MirroringMode.Vertical, metadata.MirrorMode);
        Assert.Equal(64, metadata.Sprites.Length);
        Assert.Equal((byte)20, metadata.Sprites[0].Y);
        Assert.Equal((byte)7, metadata.Sprites[0].TileId);
        Assert.Equal((byte)0x41, metadata.Sprites[0].Attrs);
        Assert.Equal((byte)30, metadata.Sprites[0].X);
        Assert.Equal(0xAB, metadata.PatternTable[0x0123]);
        Assert.Equal(0xFF4C9AEC, metadata.Palette[1]);

        Assert.Equal(3, metadata.VisibleTiles.Count);
        Assert.Equal((-1, 0, (byte)0x44), (metadata.VisibleTiles[0].ScreenX, metadata.VisibleTiles[0].ScreenY, metadata.VisibleTiles[0].TileId));
        Assert.Equal((byte)2, metadata.VisibleTiles[0].PaletteId);
    }

    [Fact]
    public void Extract_GeneratesMotionVectors_FromPreviousFrame()
    {
        var ppu = new Ppu2C02();
        ppu.InsertCartridge(new TestCartridge(MirroringMode.Horizontal));
        ppu.Oam[0] = 11;
        ppu.Oam[1] = 5;
        ppu.Oam[2] = 0;
        ppu.Oam[3] = 24;
        SetScrollState(ppu, fineX: 5, fineY: 1, coarseX: 2, coarseY: 1, nametableSelect: 0);

        var previous = new FrameMetadata(
            sprites:
            [
                new SpriteEntry { Y = 9, TileId = 5, Attrs = 0, X = 14 }
            ],
            fineScrollX: 1,
            fineScrollY: 2,
            coarseScrollX: 1,
            coarseScrollY: 0,
            nametableSelect: 0);

        var extractor = new RenderDataExtractor();
        FrameMetadata metadata = extractor.Extract(ppu.CaptureRenderStateSnapshot(), previousFrame: previous);

        Assert.Equal(64, metadata.MotionVectors.Length);
        Assert.Equal(new System.Numerics.Vector2(-12f, -7f), metadata.BackgroundMotionVector);
        Assert.Equal(new System.Numerics.Vector2(10f, 2f), metadata.MotionVectors[0]);
    }

    [Fact]
    public void Extract_ScalesMotionVectors_ForCurrentRenderResolution()
    {
        var ppu = new Ppu2C02();
        ppu.InsertCartridge(new TestCartridge(MirroringMode.Horizontal));
        ppu.Oam[0] = 11;
        ppu.Oam[1] = 5;
        ppu.Oam[2] = 0;
        ppu.Oam[3] = 24;
        SetScrollState(ppu, fineX: 5, fineY: 1, coarseX: 2, coarseY: 1, nametableSelect: 0);

        var previous = new FrameMetadata(
            sprites:
            [
                new SpriteEntry { Y = 9, TileId = 5, Attrs = 0, X = 14 }
            ],
            fineScrollX: 1,
            fineScrollY: 2,
            coarseScrollX: 1,
            coarseScrollY: 0,
            nametableSelect: 0);

        var extractor = new RenderDataExtractor();
        FrameMetadata metadata = extractor.Extract(
            ppu.CaptureRenderStateSnapshot(),
            previousFrame: previous,
            screenWidth: 512,
            screenHeight: 120);

        Assert.Equal(64, metadata.MotionVectors.Length);
        Assert.Equal(new System.Numerics.Vector2(-24f, -3.5f), metadata.BackgroundMotionVector);
        Assert.Equal(new System.Numerics.Vector2(20f, 1f), metadata.MotionVectors[0]);
    }

    private static void SetScrollState(Ppu2C02 ppu, int fineX, int fineY, int coarseX, int coarseY, int nametableSelect)
    {
        ppu.WriteRegister(0x2005, (byte)((coarseX << 3) | fineX));
        ppu.WriteRegister(0x2005, (byte)((coarseY << 3) | fineY));

        ushort v = (ushort)(
            ((fineY & 0x07) << 12) |
            ((nametableSelect & 0x03) << 10) |
            ((coarseY & 0x1F) << 5) |
            (coarseX & 0x1F));

        ppu.WriteRegister(0x2006, (byte)(v >> 8));
        ppu.WriteRegister(0x2006, (byte)v);
    }

    private static void AssertMetadataEquivalent(FrameMetadata expected, FrameMetadata actual)
    {
        Assert.Equal(expected.FineScrollX, actual.FineScrollX);
        Assert.Equal(expected.FineScrollY, actual.FineScrollY);
        Assert.Equal(expected.CoarseScrollX, actual.CoarseScrollX);
        Assert.Equal(expected.CoarseScrollY, actual.CoarseScrollY);
        Assert.Equal(expected.NametableSelect, actual.NametableSelect);
        Assert.Equal(expected.MirrorMode, actual.MirrorMode);
        Assert.Equal(expected.UseBackgroundPatternTableHighBank, actual.UseBackgroundPatternTableHighBank);
        Assert.Equal(expected.UseSpritePatternTableHighBank, actual.UseSpritePatternTableHighBank);
        Assert.Equal(expected.Use8x16Sprites, actual.Use8x16Sprites);
        Assert.Equal(expected.ShowBackground, actual.ShowBackground);
        Assert.Equal(expected.ShowSprites, actual.ShowSprites);
        Assert.Equal(expected.ShowBackgroundLeft8, actual.ShowBackgroundLeft8);
        Assert.Equal(expected.ShowSpritesLeft8, actual.ShowSpritesLeft8);
        Assert.Equal(expected.BackgroundMotionVector, actual.BackgroundMotionVector);

        Assert.True(expected.Sprites.SequenceEqual(actual.Sprites));
        Assert.True(expected.Nametable.SequenceEqual(actual.Nametable));
        Assert.True(expected.PatternTable.SequenceEqual(actual.PatternTable));
        Assert.True(expected.Palette.SequenceEqual(actual.Palette));
        Assert.True(expected.MotionVectors.SequenceEqual(actual.MotionVectors));

        Assert.Equal(expected.VisibleTiles.Count, actual.VisibleTiles.Count);
        AssertEqualTile(expected.VisibleTiles[0], actual.VisibleTiles[0]);
        AssertEqualTile(
            expected.VisibleTiles[expected.VisibleTiles.Count / 2],
            actual.VisibleTiles[actual.VisibleTiles.Count / 2]);
        AssertEqualTile(expected.VisibleTiles[^1], actual.VisibleTiles[^1]);
    }

    private static void AssertEqualTile(VisibleTile expected, VisibleTile actual)
    {
        Assert.Equal(expected.ScreenX, actual.ScreenX);
        Assert.Equal(expected.ScreenY, actual.ScreenY);
        Assert.Equal(expected.TileId, actual.TileId);
        Assert.Equal(expected.PaletteId, actual.PaletteId);
        Assert.Equal(expected.LogicalNametableIndex, actual.LogicalNametableIndex);
        Assert.Equal(expected.PhysicalNametableIndex, actual.PhysicalNametableIndex);
        Assert.Equal(expected.TileX, actual.TileX);
        Assert.Equal(expected.TileY, actual.TileY);
        Assert.Equal(expected.ClipTop, actual.ClipTop);
        Assert.Equal(expected.ClipBottom, actual.ClipBottom);
        Assert.Equal(expected.UseBackgroundPatternTableHighBank, actual.UseBackgroundPatternTableHighBank);
    }

    private sealed class TestCartridge : ICartridge
    {
        public TestCartridge(MirroringMode mirroring)
        {
            Mirroring = mirroring;
        }

        public byte[] PatternTable { get; } = new byte[0x2000];

        public int MapperNumber => 0;

        public MirroringMode Mirroring { get; }

        public bool IrqActive => false;

        public byte CpuRead(ushort address) => 0;

        public void CpuWrite(ushort address, byte data)
        {
        }

        public byte PpuRead(ushort address) => address < PatternTable.Length ? PatternTable[address] : (byte)0;

        public void PpuWrite(ushort address, byte data)
        {
            if (address < PatternTable.Length)
                PatternTable[address] = data;
        }

        public void SignalScanline()
        {
        }

        public void Reset()
        {
        }

        public void Clock()
        {
        }

        public byte[] SerializeState() => [];

        public void DeserializeState(byte[] state)
        {
        }
    }
}

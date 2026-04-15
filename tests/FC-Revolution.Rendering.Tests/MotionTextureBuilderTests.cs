using System.Numerics;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Common;

namespace FC_Revolution.Rendering.Tests;

public sealed class MotionTextureBuilderTests
{
    [Fact]
    public void Build_AttachesMotionTexture_AndFillsBackgroundMotionOutsideMaskedLeft8()
    {
        var metadata = new FrameMetadata(
            backgroundMotionVector: new Vector2(3f, -2f),
            showBackground: true,
            showSprites: false,
            showBackgroundInFirstTileColumn: false,
            showSpritesInFirstTileColumn: true);

        LayeredFrameData frameData = LayeredFrameBuilder.Build(metadata, 16, 2);

        Assert.NotNull(frameData.MotionTexture);
        Assert.Equal(16, frameData.MotionTexture!.Width);
        Assert.Equal(2, frameData.MotionTexture.Height);
        Assert.Equal(16 * 2 * 2, frameData.MotionTexture.PackedVectors.Length);
        Assert.Equal(16 * 2 * sizeof(ushort) * 2, frameData.MotionTexture.PackedBytes.Length);
        Assert.Equal(Vector2.Zero, frameData.MotionTexture.GetVector(0, 0));
        Assert.Equal(Vector2.Zero, frameData.MotionTexture.GetVector(7, 1));
        Assert.Equal(new Vector2(3f, -2f), frameData.MotionTexture.GetVector(8, 0));
        Assert.Equal(new Vector2(3f, -2f), frameData.MotionTexture.GetVector(15, 1));
    }

    [Fact]
    public void BuildMotionTexture_UsesOpaqueSpritePixels_AndEarlierOamWinsOverlap()
    {
        var metadata = new FrameMetadata(
            sprites:
            [
                new SpriteEntry { X = 0, Y = 0, TileId = 1, Attrs = 0x00 },
                new SpriteEntry { X = 0, Y = 0, TileId = 2, Attrs = 0x00 }
            ],
            tileGraphicsBytes: BuildPatternTable(
                (1, BuildTile((0, 0, 1))),
                (2, BuildSolidTile(1))),
            backgroundMotionVector: new Vector2(1f, 1f),
            motionVectors:
            [
                new Vector2(10f, 1f),
                new Vector2(20f, 2f)
            ],
            showBackground: true,
            showSprites: true,
            showBackgroundInFirstTileColumn: true,
            showSpritesInFirstTileColumn: true);

        MotionTextureData texture = LayeredFrameBuilder.BuildMotionTexture(metadata, 8, 8);

        Assert.Equal(new Vector2(10f, 1f), texture.GetVector(0, 0));
        Assert.Equal(new Vector2(20f, 2f), texture.GetVector(1, 0));
        Assert.Equal(new Vector2(20f, 2f), texture.GetVector(7, 7));
    }

    [Fact]
    public void BuildMotionTexture_KeepsBackgroundMotion_WhenSpriteIsBehindOpaqueBackground()
    {
        var metadata = new FrameMetadata(
            sprites:
            [
                new SpriteEntry { X = 0, Y = 0, TileId = 1, Attrs = 0x20 }
            ],
            tileGraphicsBytes: BuildPatternTable(
                (0, BuildTile((0, 0, 1))),
                (1, BuildSolidTile(1))),
            backgroundMotionVector: new Vector2(1f, 0f),
            motionVectors:
            [
                new Vector2(9f, 0f)
            ],
            visibleTiles:
            [
                new VisibleTile { ScreenX = 0, ScreenY = 0, TileId = 0, PaletteId = 0 }
            ],
            showBackground: true,
            showSprites: true,
            showBackgroundInFirstTileColumn: true,
            showSpritesInFirstTileColumn: true);

        MotionTextureData texture = LayeredFrameBuilder.BuildMotionTexture(metadata, 8, 8);

        Assert.Equal(new Vector2(1f, 0f), texture.GetVector(0, 0));
        Assert.Equal(new Vector2(9f, 0f), texture.GetVector(1, 0));
    }

    [Fact]
    public void BuildMotionTexture_ClipsSpriteToFrame_AndHonorsSpriteLeft8Mask()
    {
        var metadata = new FrameMetadata(
            sprites:
            [
                new SpriteEntry { X = 6, Y = 0, TileId = 1, Attrs = 0x00 }
            ],
            tileGraphicsBytes: BuildPatternTable((1, BuildSolidTile(1))),
            backgroundMotionVector: new Vector2(1f, 1f),
            motionVectors:
            [
                new Vector2(5f, 5f)
            ],
            showBackground: true,
            showSprites: true,
            showBackgroundInFirstTileColumn: true,
            showSpritesInFirstTileColumn: false);

        MotionTextureData texture = LayeredFrameBuilder.BuildMotionTexture(metadata, 10, 2);

        Assert.Equal(new Vector2(1f, 1f), texture.GetVector(6, 0));
        Assert.Equal(new Vector2(1f, 1f), texture.GetVector(7, 1));
        Assert.Equal(new Vector2(5f, 5f), texture.GetVector(8, 0));
        Assert.Equal(new Vector2(5f, 5f), texture.GetVector(9, 1));
    }

    private static byte[] BuildPatternTable(params (int TileId, byte[] TileData)[] tiles)
    {
        var patternTable = new byte[0x2000];
        foreach (var (tileId, tileData) in tiles)
            Buffer.BlockCopy(tileData, 0, patternTable, tileId * 16, tileData.Length);

        return patternTable;
    }

    private static byte[] BuildSolidTile(byte colorIndex)
    {
        var tile = new byte[16];
        for (int y = 0; y < 8; y++)
            SetTilePixel(tile, 0, y, 8, colorIndex);

        return tile;
    }

    private static byte[] BuildTile(params (int X, int Y, byte ColorIndex)[] pixels)
    {
        var tile = new byte[16];
        foreach (var (x, y, colorIndex) in pixels)
            SetTilePixel(tile, x, y, 1, colorIndex);

        return tile;
    }

    private static void SetTilePixel(byte[] tile, int startX, int y, int width, byte colorIndex)
    {
        for (int x = startX; x < startX + width; x++)
        {
            int shift = 7 - x;
            if ((colorIndex & 0x01) != 0)
                tile[y] |= (byte)(1 << shift);

            if ((colorIndex & 0x02) != 0)
                tile[y + 8] |= (byte)(1 << shift);
        }
    }
}

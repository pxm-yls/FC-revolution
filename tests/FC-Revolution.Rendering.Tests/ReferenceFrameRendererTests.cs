using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Common;
using FCRevolution.Rendering.Diagnostics;

namespace FC_Revolution.Rendering.Tests;

public sealed class ReferenceFrameRendererTests
{
    [Fact]
    public void Render_CompositesSpritesUsingOamOrderAndPriority()
    {
        var metadata = new FrameMetadata(
            sprites:
            [
                new SpriteEntry { X = 0, Y = 0, TileId = 1, Attrs = 0x20 },
                new SpriteEntry { X = 0, Y = 0, TileId = 2, Attrs = 0x00 }
            ],
            patternTable: BuildPatternTable(
                (1, BuildSolidTile(1)),
                (2, BuildSolidTile(2))),
            palette:
            [
                0xFF101010u, 0xFF202020u, 0xFF303030u, 0xFF404040u,
                0xFF505050u, 0xFF606060u, 0xFF707070u, 0xFF808080u,
                0xFF909090u, 0xFFA0A0A0u, 0xFFB0B0B0u, 0xFFC0C0C0u,
                0xFFD0D0D0u, 0xFFE0E0E0u, 0xFFF0F0F0u, 0xFFFFFFFFu,
                0xFF110000u, 0xFF220000u, 0xFF330000u, 0xFF440000u,
                0xFF001100u, 0xFF002200u, 0xFF003300u, 0xFF004400u,
                0xFF000011u, 0xFF000022u, 0xFF000033u, 0xFF000044u,
                0xFF111111u, 0xFF222222u, 0xFF333333u, 0xFF444444u
            ],
            visibleTiles:
            [
                new VisibleTile { ScreenX = 0, ScreenY = 0, TileId = 0, PaletteId = 0 }
            ],
            showBackground: true,
            showSprites: true,
            showBackgroundLeft8: true,
            showSpritesLeft8: true);

        uint[] frame = ReferenceFrameRenderer.Render(metadata, 8, 8);

        Assert.All(frame, pixel => Assert.Equal(0xFF220000u, pixel));
    }

    [Fact]
    public void Render_HonorsBackgroundLeft8Mask()
    {
        var metadata = new FrameMetadata(
            patternTable: BuildPatternTable((0, BuildSolidTile(1))),
            palette:
            [
                0xFF010101u, 0xFF020202u, 0xFF030303u, 0xFF040404u,
                0xFF111111u, 0xFF121212u, 0xFF131313u, 0xFF141414u,
                0xFF212121u, 0xFF222222u, 0xFF232323u, 0xFF242424u,
                0xFF313131u, 0xFF323232u, 0xFF333333u, 0xFF343434u,
                0xFF414141u, 0xFF424242u, 0xFF434343u, 0xFF444444u,
                0xFF515151u, 0xFF525252u, 0xFF535353u, 0xFF545454u,
                0xFF616161u, 0xFF626262u, 0xFF636363u, 0xFF646464u,
                0xFF717171u, 0xFF727272u, 0xFF737373u, 0xFF747474u
            ],
            visibleTiles:
            [
                new VisibleTile { ScreenX = 0, ScreenY = 0, TileId = 0, PaletteId = 0 },
                new VisibleTile { ScreenX = 8, ScreenY = 0, TileId = 0, PaletteId = 0 }
            ],
            showBackground: true,
            showSprites: false,
            showBackgroundLeft8: false,
            showSpritesLeft8: true);

        uint[] frame = ReferenceFrameRenderer.Render(metadata, 16, 8);

        Assert.All(frame[..8], pixel => Assert.Equal(0xFF010101u, pixel));
        Assert.All(frame[8..16], pixel => Assert.Equal(0xFF020202u, pixel));
    }

    [Fact]
    public void Build_Expands8x16Sprites_IntoTwoTilesWithFlipAwareOrdering()
    {
        var metadata = new FrameMetadata(
            sprites:
            [
                new SpriteEntry { X = 24, Y = 40, TileId = 0x05, Attrs = 0xC2 }
            ],
            use8x16Sprites: true);

        LayeredFrameData frameData = LayeredFrameBuilder.Build(metadata, 32, 32);

        Assert.Equal(2, frameData.Sprites.Length);
        Assert.Equal(24f, frameData.Sprites[0].ScreenX);
        Assert.Equal(40f, frameData.Sprites[0].ScreenY);
        Assert.Equal(261u, frameData.Sprites[0].TileId);
        Assert.Equal(1u, frameData.Sprites[0].FlipH);
        Assert.Equal(1u, frameData.Sprites[0].FlipV);
        Assert.Equal(24f, frameData.Sprites[1].ScreenX);
        Assert.Equal(48f, frameData.Sprites[1].ScreenY);
        Assert.Equal(260u, frameData.Sprites[1].TileId);
        Assert.Equal(1u, frameData.Sprites[1].FlipH);
        Assert.Equal(1u, frameData.Sprites[1].FlipV);
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
        byte plane0 = (byte)((colorIndex & 0x01) != 0 ? 0xFF : 0x00);
        byte plane1 = (byte)((colorIndex & 0x02) != 0 ? 0xFF : 0x00);

        for (int row = 0; row < 8; row++)
        {
            tile[row] = plane0;
            tile[row + 8] = plane1;
        }

        return tile;
    }
}

using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Common;

namespace FCRevolution.Rendering.Diagnostics;

public static class ReferenceFrameRenderer
{
    public static uint[] Render(
        IFrameMetadata metadata,
        int frameWidth = FrameRenderDefaults.Width,
        int frameHeight = FrameRenderDefaults.Height)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return Render(LayeredFrameBuilder.Build(metadata, frameWidth, frameHeight));
    }

    public static uint[] Render(LayeredFrameData frameData)
    {
        ArgumentNullException.ThrowIfNull(frameData);

        int pixelCount = frameData.FrameWidth * frameData.FrameHeight;
        uint backdropColor = frameData.Palette.Length > 0 ? frameData.Palette[0] : 0xFF000000u;
        var backgroundColors = new uint[pixelCount];
        var spriteColors = new uint[pixelCount];
        var backgroundOpaque = new bool[pixelCount];
        var spriteState = new byte[pixelCount];
        var output = new uint[pixelCount];

        Array.Fill(backgroundColors, backdropColor);

        if (frameData.ShowBackground)
            RenderBackground(frameData, backgroundColors, backgroundOpaque);

        if (frameData.ShowSprites)
            RenderSprites(frameData, spriteColors, spriteState);

        for (int i = 0; i < pixelCount; i++)
        {
            bool hasSprite = spriteState[i] != 0;
            bool spriteBehindBackground = spriteState[i] == 2;
            output[i] = hasSprite && !(backgroundOpaque[i] && spriteBehindBackground)
                ? spriteColors[i]
                : backgroundColors[i];
        }

        return output;
    }

    private static void RenderBackground(LayeredFrameData frameData, uint[] backgroundColors, bool[] backgroundOpaque)
    {
        foreach (var tile in frameData.BackgroundTiles)
        {
            int originX = (int)tile.ScreenX;
            int originY = (int)tile.ScreenY;
            int clipTop = (int)tile.ClipTop;
            int clipBottom = tile.ClipBottom <= tile.ClipTop ? frameData.FrameHeight : (int)tile.ClipBottom;

            for (int localY = 0; localY < LayeredFrameBuilder.TileSize; localY++)
            {
                int pixelY = originY + localY;
                if ((uint)pixelY >= (uint)frameData.FrameHeight)
                    continue;

                for (int localX = 0; localX < LayeredFrameBuilder.TileSize; localX++)
                {
                    int pixelX = originX + localX;
                    if ((uint)pixelX >= (uint)frameData.FrameWidth)
                        continue;

                    if (!frameData.ShowBackgroundInFirstTileColumn && pixelX < LayeredFrameBuilder.TileSize)
                        continue;

                    if (pixelY < clipTop || pixelY >= clipBottom)
                        continue;

                    byte colorIndex = ReadAtlasColor(frameData.TileAtlas, tile.TileId, localX, localY);
                    if (colorIndex == 0)
                        continue;

                    int pixelIndex = (pixelY * frameData.FrameWidth) + pixelX;
                    backgroundColors[pixelIndex] = ReadPaletteColor(frameData.Palette, tile.PaletteBaseIndex, colorIndex);
                    backgroundOpaque[pixelIndex] = true;
                }
            }
        }
    }

    private static void RenderSprites(LayeredFrameData frameData, uint[] spriteColors, byte[] spriteState)
    {
        for (int spriteIndex = frameData.Sprites.Length - 1; spriteIndex >= 0; spriteIndex--)
        {
            var sprite = frameData.Sprites[spriteIndex];
            int originX = (int)sprite.ScreenX;
            int originY = (int)sprite.ScreenY;

            for (int localY = 0; localY < LayeredFrameBuilder.TileSize; localY++)
            {
                int sampleY = sprite.FlipV != 0 ? (LayeredFrameBuilder.TileSize - 1 - localY) : localY;
                int pixelY = originY + localY;
                if ((uint)pixelY >= (uint)frameData.FrameHeight)
                    continue;

                for (int localX = 0; localX < LayeredFrameBuilder.TileSize; localX++)
                {
                    int pixelX = originX + localX;
                    if ((uint)pixelX >= (uint)frameData.FrameWidth)
                        continue;

                    if (!frameData.ShowSpritesInFirstTileColumn && pixelX < LayeredFrameBuilder.TileSize)
                        continue;

                    int sampleX = sprite.FlipH != 0 ? (LayeredFrameBuilder.TileSize - 1 - localX) : localX;
                    byte colorIndex = ReadAtlasColor(frameData.TileAtlas, sprite.TileId, sampleX, sampleY);
                    if (colorIndex == 0)
                        continue;

                    int pixelIndex = (pixelY * frameData.FrameWidth) + pixelX;
                    spriteColors[pixelIndex] = ReadPaletteColor(frameData.Palette, sprite.PaletteBaseIndex, colorIndex);
                    spriteState[pixelIndex] = sprite.BehindBackground != 0 ? (byte)2 : (byte)1;
                }
            }
        }
    }

    private static byte ReadAtlasColor(byte[] chrAtlas, uint tileId, int x, int y)
    {
        int atlasTileRow = (int)tileId / LayeredFrameBuilder.TilesPerAtlasRow;
        int atlasTileColumn = (int)tileId % LayeredFrameBuilder.TilesPerAtlasRow;
        int atlasX = (atlasTileColumn * LayeredFrameBuilder.TileSize) + x;
        int atlasY = (atlasTileRow * LayeredFrameBuilder.TileSize) + y;
        int atlasIndex = (atlasY * LayeredFrameBuilder.AtlasWidth) + atlasX;
        return (uint)atlasIndex < (uint)chrAtlas.Length ? chrAtlas[atlasIndex] : (byte)0;
    }

    private static uint ReadPaletteColor(uint[] palette, uint paletteBaseIndex, byte colorIndex)
    {
        if (palette.Length == 0)
            return 0xFF000000u;

        int paletteIndex = ((int)paletteBaseIndex + colorIndex) & 0x1F;
        if ((paletteIndex & 0x13) == 0x10)
            paletteIndex &= 0x0F;

        return palette[paletteIndex % palette.Length];
    }
}

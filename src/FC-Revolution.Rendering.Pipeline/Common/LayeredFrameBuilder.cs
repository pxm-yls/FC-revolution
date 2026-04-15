using FCRevolution.Rendering.Abstractions;
using System.Numerics;

namespace FCRevolution.Rendering.Common;

public static class LayeredFrameBuilder
{
    public const int TileSize = 8;
    public const int TilesPerAtlasRow = 16;
    public const int AtlasWidth = TilesPerAtlasRow * TileSize;
    public const int AtlasHeight = (512 / TilesPerAtlasRow) * TileSize;
    public const int AtlasPixelCount = AtlasWidth * AtlasHeight;

    public static LayeredFrameData Build(
        IFrameMetadata metadata,
        int frameWidth = FrameRenderDefaults.Width,
        int frameHeight = FrameRenderDefaults.Height)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        byte[] tileAtlas = BuildTileAtlas(metadata.TileGraphicsBytes);
        SpriteRenderItem[] sprites = BuildSprites(metadata);
        MotionTextureData motionTexture = BuildMotionTexture(metadata, tileAtlas, sprites, frameWidth, frameHeight);

        return new LayeredFrameData(
            frameWidth,
            frameHeight,
            tileAtlas,
            metadata.Palette.ToArray(),
            BuildBackground(metadata, frameHeight),
            sprites,
            metadata.ShowBackground,
            metadata.ShowSprites,
            metadata.ShowBackgroundInFirstTileColumn,
            metadata.ShowSpritesInFirstTileColumn,
            motionTexture);
    }

    public static MotionTextureData BuildMotionTexture(
        IFrameMetadata metadata,
        int frameWidth = FrameRenderDefaults.Width,
        int frameHeight = FrameRenderDefaults.Height)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        byte[] tileAtlas = BuildTileAtlas(metadata.TileGraphicsBytes);
        SpriteRenderItem[] sprites = BuildSprites(metadata);
        return BuildMotionTexture(metadata, tileAtlas, sprites, frameWidth, frameHeight);
    }

    public static byte[] BuildTileAtlas(ReadOnlySpan<byte> tileGraphicsBytes)
    {
        var atlas = new byte[AtlasPixelCount];
        int tileCount = Math.Min(512, tileGraphicsBytes.Length / 16);

        for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            int tileRow = tileIndex / TilesPerAtlasRow;
            int tileColumn = tileIndex % TilesPerAtlasRow;
            int atlasBaseX = tileColumn * TileSize;
            int atlasBaseY = tileRow * TileSize;
            int tileOffset = tileIndex * 16;

            for (int row = 0; row < TileSize; row++)
            {
                byte plane0 = tileGraphicsBytes[tileOffset + row];
                byte plane1 = tileGraphicsBytes[tileOffset + row + 8];
                int atlasRowOffset = (atlasBaseY + row) * AtlasWidth;

                for (int column = 0; column < TileSize; column++)
                {
                    int shift = 7 - column;
                    byte colorIndex = (byte)((((plane1 >> shift) & 0x01) << 1) | ((plane0 >> shift) & 0x01));
                    atlas[atlasRowOffset + atlasBaseX + column] = colorIndex;
                }
            }
        }

        return atlas;
    }

    private static MotionTextureData BuildMotionTexture(
        IFrameMetadata metadata,
        byte[] tileAtlas,
        SpriteRenderItem[] sprites,
        int frameWidth,
        int frameHeight)
    {
        if (frameWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameWidth));

        if (frameHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameHeight));

        int pixelCount = checked(frameWidth * frameHeight);
        var packedVectors = new ushort[pixelCount * 2];
        var backgroundOpaque = new bool[pixelCount];

        FillBackgroundMotion(metadata, packedVectors, frameWidth, frameHeight);
        MarkBackgroundOpaquePixels(metadata, tileAtlas, backgroundOpaque, frameWidth, frameHeight);
        ApplySpriteMotion(metadata, tileAtlas, sprites, packedVectors, backgroundOpaque, frameWidth, frameHeight);

        return new MotionTextureData(frameWidth, frameHeight, packedVectors);
    }

    private static BackgroundTileRenderItem[] BuildBackground(IFrameMetadata metadata, int frameHeight)
    {
        var result = new BackgroundTileRenderItem[metadata.VisibleTiles.Count];

        for (int i = 0; i < metadata.VisibleTiles.Count; i++)
        {
            var tile = metadata.VisibleTiles[i];
            uint tileBankOffset = tile.UseUpperBackgroundTileBank ? 256u : 0u;
            float clipTop = tile.ClipTop;
            float clipBottom = tile.ClipBottom <= tile.ClipTop ? frameHeight : tile.ClipBottom;
            result[i] = new BackgroundTileRenderItem(
                tile.ScreenX,
                tile.ScreenY,
                tileBankOffset + tile.TileId,
                (uint)(tile.PaletteId << 2),
                clipTop,
                clipBottom);
        }

        return result;
    }

    private static void FillBackgroundMotion(
        IFrameMetadata metadata,
        ushort[] packedVectors,
        int frameWidth,
        int frameHeight)
    {
        if (!metadata.ShowBackground)
            return;

        Vector2 backgroundMotion = metadata.BackgroundMotionVector;
        for (int y = 0; y < frameHeight; y++)
        {
            int rowComponentIndex = y * frameWidth * 2;
            for (int x = 0; x < frameWidth; x++)
            {
                if (!metadata.ShowBackgroundInFirstTileColumn && x < TileSize)
                    continue;

                MotionTextureData.WriteVector(packedVectors, rowComponentIndex + (x * 2), backgroundMotion);
            }
        }
    }

    private static void MarkBackgroundOpaquePixels(
        IFrameMetadata metadata,
        byte[] tileAtlas,
        bool[] backgroundOpaque,
        int frameWidth,
        int frameHeight)
    {
        if (!metadata.ShowBackground)
            return;

        foreach (var tile in metadata.VisibleTiles)
        {
            int originX = tile.ScreenX;
            int originY = tile.ScreenY;
            int clipTop = tile.ClipTop;
            int clipBottom = tile.ClipBottom <= tile.ClipTop ? frameHeight : tile.ClipBottom;
            uint tileId = (tile.UseUpperBackgroundTileBank ? 256u : 0u) + tile.TileId;

            for (int localY = 0; localY < TileSize; localY++)
            {
                int pixelY = originY + localY;
                if ((uint)pixelY >= (uint)frameHeight)
                    continue;

                if (pixelY < clipTop || pixelY >= clipBottom)
                    continue;

                for (int localX = 0; localX < TileSize; localX++)
                {
                    int pixelX = originX + localX;
                    if ((uint)pixelX >= (uint)frameWidth)
                        continue;

                    if (!metadata.ShowBackgroundInFirstTileColumn && pixelX < TileSize)
                        continue;

                    if (ReadAtlasColor(tileAtlas, tileId, localX, localY) == 0)
                        continue;

                    backgroundOpaque[(pixelY * frameWidth) + pixelX] = true;
                }
            }
        }
    }

    private static SpriteRenderItem[] BuildSprites(IFrameMetadata metadata)
    {
        ReadOnlySpan<SpriteEntry> sprites = metadata.Sprites;
        bool useTallSprites = metadata.UseTallSprites;
        int spritesPerEntry = useTallSprites ? 2 : 1;
        var spriteItems = new SpriteRenderItem[sprites.Length * spritesPerEntry];
        int spriteItemIndex = 0;

        for (int spriteIndex = 0; spriteIndex < sprites.Length; spriteIndex++)
        {
            var sprite = sprites[spriteIndex];
            bool flipH = (sprite.Attrs & 0x40) != 0;
            bool flipV = (sprite.Attrs & 0x80) != 0;
            bool behindBackground = (sprite.Attrs & 0x20) != 0;
            uint paletteBaseIndex = (uint)(((sprite.Attrs & 0x03) + 4) << 2);

            if (!useTallSprites)
            {
                uint tileId = (metadata.UseUpperSpriteTileBank ? 256u : 0u) + sprite.TileId;
                spriteItems[spriteItemIndex++] = new SpriteRenderItem(
                    sprite.X,
                    sprite.Y,
                    tileId,
                    paletteBaseIndex,
                    flipH ? 1u : 0u,
                    flipV ? 1u : 0u,
                    behindBackground ? 1u : 0u,
                    (uint)spriteIndex);
                continue;
            }

            uint bankOffset = (uint)((sprite.TileId & 0x01) != 0 ? 256 : 0);
            uint topTile = bankOffset + (uint)(sprite.TileId & 0xFE);
            uint bottomTile = topTile + 1;

            if (!flipV)
            {
                spriteItems[spriteItemIndex++] = new SpriteRenderItem(
                    sprite.X,
                    sprite.Y,
                    topTile,
                    paletteBaseIndex,
                    flipH ? 1u : 0u,
                    0u,
                    behindBackground ? 1u : 0u,
                    (uint)spriteIndex);
                spriteItems[spriteItemIndex++] = new SpriteRenderItem(
                    sprite.X,
                    sprite.Y + TileSize,
                    bottomTile,
                    paletteBaseIndex,
                    flipH ? 1u : 0u,
                    0u,
                    behindBackground ? 1u : 0u,
                    (uint)spriteIndex);
                continue;
            }

            spriteItems[spriteItemIndex++] = new SpriteRenderItem(
                sprite.X,
                sprite.Y,
                bottomTile,
                paletteBaseIndex,
                flipH ? 1u : 0u,
                1u,
                behindBackground ? 1u : 0u,
                (uint)spriteIndex);
            spriteItems[spriteItemIndex++] = new SpriteRenderItem(
                sprite.X,
                sprite.Y + TileSize,
                topTile,
                paletteBaseIndex,
                flipH ? 1u : 0u,
                1u,
                behindBackground ? 1u : 0u,
                (uint)spriteIndex);
        }

        return spriteItems;
    }

    private static void ApplySpriteMotion(
        IFrameMetadata metadata,
        byte[] tileAtlas,
        SpriteRenderItem[] sprites,
        ushort[] packedVectors,
        bool[] backgroundOpaque,
        int frameWidth,
        int frameHeight)
    {
        if (!metadata.ShowSprites)
            return;

        ReadOnlySpan<Vector2> motionVectors = metadata.MotionVectors;

        for (int spriteIndex = sprites.Length - 1; spriteIndex >= 0; spriteIndex--)
        {
            var sprite = sprites[spriteIndex];
            Vector2 spriteMotion = sprite.OriginalSpriteIndex < motionVectors.Length
                ? motionVectors[(int)sprite.OriginalSpriteIndex]
                : Vector2.Zero;
            int originX = (int)sprite.ScreenX;
            int originY = (int)sprite.ScreenY;

            for (int localY = 0; localY < TileSize; localY++)
            {
                int sampleY = sprite.FlipV != 0 ? (TileSize - 1 - localY) : localY;
                int pixelY = originY + localY;
                if ((uint)pixelY >= (uint)frameHeight)
                    continue;

                for (int localX = 0; localX < TileSize; localX++)
                {
                    int pixelX = originX + localX;
                    if ((uint)pixelX >= (uint)frameWidth)
                        continue;

                    if (!metadata.ShowSpritesInFirstTileColumn && pixelX < TileSize)
                        continue;

                    int sampleX = sprite.FlipH != 0 ? (TileSize - 1 - localX) : localX;
                    if (ReadAtlasColor(tileAtlas, sprite.TileId, sampleX, sampleY) == 0)
                        continue;

                    int pixelIndex = (pixelY * frameWidth) + pixelX;
                    if (sprite.BehindBackground != 0 && backgroundOpaque[pixelIndex])
                        continue;

                    MotionTextureData.WriteVector(packedVectors, pixelIndex * 2, spriteMotion);
                }
            }
        }
    }

    private static byte ReadAtlasColor(byte[] chrAtlas, uint tileId, int x, int y)
    {
        int atlasTileRow = (int)tileId / TilesPerAtlasRow;
        int atlasTileColumn = (int)tileId % TilesPerAtlasRow;
        int atlasX = (atlasTileColumn * TileSize) + x;
        int atlasY = (atlasTileRow * TileSize) + y;
        int atlasIndex = (atlasY * AtlasWidth) + atlasX;
        return (uint)atlasIndex < (uint)chrAtlas.Length ? chrAtlas[atlasIndex] : (byte)0;
    }
}

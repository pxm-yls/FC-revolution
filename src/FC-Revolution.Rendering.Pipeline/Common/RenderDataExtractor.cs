using FCRevolution.Rendering.Abstractions;
using System.Numerics;

namespace FCRevolution.Rendering.Common;

public sealed class RenderDataExtractor : IRenderDataExtractor
{
    public FrameMetadata Extract(
        RenderStateSnapshot snapshot,
        IFrameMetadata? previousFrame = null,
        int screenWidth = 256,
        int screenHeight = 240)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        SpriteEntry[] sprites = ExtractSprites(snapshot.OamBytes);
        VisibleTile[] visibleTiles = ResolveVisibleTiles(snapshot, screenWidth, screenHeight);
        // Motion vectors are expressed in the current internal render pixel space,
        // so changes in internal render resolution scale the previous->current delta.
        float motionScaleX = screenWidth / 256f;
        float motionScaleY = screenHeight / 240f;
        Vector2 backgroundMotionVector = previousFrame is null
            ? Vector2.Zero
            : MotionVectorGenerator.GenerateBackgroundMotionVector(
                currentFineScrollX: snapshot.FineScrollX,
                currentFineScrollY: snapshot.FineScrollY,
                currentCoarseScrollX: snapshot.CoarseScrollX,
                currentCoarseScrollY: snapshot.CoarseScrollY,
                currentNametableSelect: snapshot.NametableSelect,
                previousFineScrollX: previousFrame.FineScrollX,
                previousFineScrollY: previousFrame.FineScrollY,
                previousCoarseScrollX: previousFrame.CoarseScrollX,
                previousCoarseScrollY: previousFrame.CoarseScrollY,
                previousNametableSelect: previousFrame.NametableSelect,
                scaleX: motionScaleX,
                scaleY: motionScaleY);

        return new FrameMetadata(
            sprites: sprites,
            nametable: snapshot.NametableBytes,
            patternTable: snapshot.PatternTableBytes,
            palette: snapshot.PaletteColors,
            backgroundMotionVector: backgroundMotionVector,
            motionVectors: previousFrame is null
                ? new Vector2[sprites.Length]
                : MotionVectorGenerator.GenerateSpriteMotionVectors(sprites, previousFrame.Sprites, motionScaleX, motionScaleY),
            visibleTiles: visibleTiles,
            mirrorMode: snapshot.MirroringMode,
            fineScrollX: snapshot.FineScrollX,
            fineScrollY: snapshot.FineScrollY,
            coarseScrollX: snapshot.CoarseScrollX,
            coarseScrollY: snapshot.CoarseScrollY,
            nametableSelect: snapshot.NametableSelect,
            useBackgroundPatternTableHighBank: snapshot.UseBackgroundPatternTableHighBank,
            useSpritePatternTableHighBank: snapshot.UseSpritePatternTableHighBank,
            use8x16Sprites: snapshot.Use8x16Sprites,
            showBackground: snapshot.ShowBackground,
            showSprites: snapshot.ShowSprites,
            showBackgroundLeft8: snapshot.ShowBackgroundLeft8,
            showSpritesLeft8: snapshot.ShowSpritesLeft8);
    }

    private static VisibleTile[] ResolveVisibleTiles(
        RenderStateSnapshot snapshot,
        int screenWidth,
        int screenHeight)
    {
        if (!snapshot.HasCapturedBackgroundScanlineStates)
        {
            return VisibleTileResolver.ResolveArray(
                snapshot.NametableBytes,
                snapshot.PatternTableBytes,
                snapshot.FineScrollX,
                snapshot.FineScrollY,
                snapshot.CoarseScrollX,
                snapshot.CoarseScrollY,
                snapshot.NametableSelect,
                snapshot.MirroringMode,
                screenWidth,
                screenHeight,
                useBackgroundPatternTableHighBank: snapshot.UseBackgroundPatternTableHighBank);
        }

        int scanlineCount = Math.Min(screenHeight, snapshot.BackgroundScanlineStates.Length);
        if (scanlineCount <= 0)
            return [];

        int totalVisibleTiles = 0;
        int stripStart = 0;
        BackgroundScanlineRenderState currentState = snapshot.BackgroundScanlineStates[0];

        for (int scanline = 1; scanline <= scanlineCount; scanline++)
        {
            bool reachedEnd = scanline == scanlineCount;
            bool sameState = !reachedEnd && snapshot.BackgroundScanlineStates[scanline].Equals(currentState);
            if (sameState)
                continue;

            if (currentState.ShowBackground)
            {
                int stripHeight = scanline - stripStart;
                totalVisibleTiles += VisibleTileResolver.GetVisibleTileCount(
                    snapshot.NametableBytes,
                    snapshot.PatternTableBytes,
                    currentState.FineScrollX,
                    currentState.FineScrollY,
                    currentState.CoarseScrollX,
                    currentState.CoarseScrollY,
                    currentState.NametableSelect,
                    snapshot.MirroringMode,
                    screenWidth,
                    stripHeight);
            }

            if (reachedEnd)
                break;

            stripStart = scanline;
            currentState = snapshot.BackgroundScanlineStates[scanline];
        }

        if (totalVisibleTiles == 0)
            return [];

        var visibleTiles = new VisibleTile[totalVisibleTiles];
        int writeIndex = 0;
        stripStart = 0;
        currentState = snapshot.BackgroundScanlineStates[0];

        for (int scanline = 1; scanline <= scanlineCount; scanline++)
        {
            bool reachedEnd = scanline == scanlineCount;
            bool sameState = !reachedEnd && snapshot.BackgroundScanlineStates[scanline].Equals(currentState);
            if (sameState)
                continue;

            if (currentState.ShowBackground)
            {
                int stripHeight = scanline - stripStart;
                writeIndex = VisibleTileResolver.ResolveInto(
                    visibleTiles,
                    writeIndex,
                    snapshot.NametableBytes,
                    snapshot.PatternTableBytes,
                    currentState.FineScrollX,
                    currentState.FineScrollY,
                    currentState.CoarseScrollX,
                    currentState.CoarseScrollY,
                    currentState.NametableSelect,
                    snapshot.MirroringMode,
                    screenWidth,
                    stripHeight,
                    screenOffsetY: stripStart,
                    clipTop: stripStart,
                    clipBottom: scanline,
                    useBackgroundPatternTableHighBank: currentState.UseBackgroundPatternTableHighBank);
            }

            if (reachedEnd)
                break;

            stripStart = scanline;
            currentState = snapshot.BackgroundScanlineStates[scanline];
        }

        return visibleTiles;
    }

    private static SpriteEntry[] ExtractSprites(ReadOnlySpan<byte> oamBytes)
    {
        int count = oamBytes.Length / 4;
        var sprites = new SpriteEntry[count];

        for (int i = 0; i < count; i++)
        {
            int offset = i * 4;
            sprites[i] = new SpriteEntry
            {
                Y = oamBytes[offset],
                TileId = oamBytes[offset + 1],
                Attrs = oamBytes[offset + 2],
                X = oamBytes[offset + 3]
            };
        }

        return sprites;
    }
}

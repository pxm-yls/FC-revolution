namespace FCRevolution.Rendering.Abstractions;

public readonly struct SpriteRenderItem
{
    public SpriteRenderItem(
        float screenX,
        float screenY,
        uint tileId,
        uint paletteBaseIndex,
        uint flipH,
        uint flipV,
        uint behindBackground,
        uint originalSpriteIndex)
    {
        ScreenX = screenX;
        ScreenY = screenY;
        TileId = tileId;
        PaletteBaseIndex = paletteBaseIndex;
        FlipH = flipH;
        FlipV = flipV;
        BehindBackground = behindBackground;
        OriginalSpriteIndex = originalSpriteIndex;
    }

    public readonly float ScreenX;

    public readonly float ScreenY;

    public readonly uint TileId;

    public readonly uint PaletteBaseIndex;

    public readonly uint FlipH;

    public readonly uint FlipV;

    public readonly uint BehindBackground;

    public readonly uint OriginalSpriteIndex;
}

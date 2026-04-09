namespace FCRevolution.Rendering.Abstractions;

public readonly struct BackgroundTileRenderItem
{
    public BackgroundTileRenderItem(
        float screenX,
        float screenY,
        uint tileId,
        uint paletteBaseIndex,
        float clipTop,
        float clipBottom)
    {
        ScreenX = screenX;
        ScreenY = screenY;
        TileId = tileId;
        PaletteBaseIndex = paletteBaseIndex;
        ClipTop = clipTop;
        ClipBottom = clipBottom;
    }

    public readonly float ScreenX;

    public readonly float ScreenY;

    public readonly uint TileId;

    public readonly uint PaletteBaseIndex;

    public readonly float ClipTop;

    public readonly float ClipBottom;
}

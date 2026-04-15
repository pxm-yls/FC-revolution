namespace FCRevolution.Rendering.Common;

public readonly record struct VisibleTile
{
    public int ScreenX { get; init; }

    public int ScreenY { get; init; }

    public byte TileId { get; init; }

    public byte PaletteId { get; init; }

    public int LogicalPlaneIndex { get; init; }

    public int PhysicalPlaneIndex { get; init; }

    public int TileX { get; init; }

    public int TileY { get; init; }

    public int ClipTop { get; init; }

    public int ClipBottom { get; init; }

    public bool UseUpperBackgroundTileBank { get; init; }
}

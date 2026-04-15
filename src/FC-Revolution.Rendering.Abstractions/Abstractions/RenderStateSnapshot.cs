namespace FCRevolution.Rendering.Abstractions;

public enum BackgroundPlaneLayoutMode
{
    SharedTopBottom,
    SharedLeftRight,
    SinglePlane0,
    SinglePlane1,
    IndependentPlanes
}

public readonly record struct BackgroundScanlineRenderState
{
    public int FineScrollX { get; init; }

    public int FineScrollY { get; init; }

    public int CoarseScrollX { get; init; }

    public int CoarseScrollY { get; init; }

    public int BackgroundPlaneSelect { get; init; }

    public bool UseUpperBackgroundTileBank { get; init; }

    public bool ShowBackground { get; init; }

    public bool ShowBackgroundInFirstTileColumn { get; init; }
}

public sealed class RenderStateSnapshot
{
    public required byte[] BackgroundPlaneBytes { get; init; }

    public required byte[] TileGraphicsBytes { get; init; }

    public required uint[] PaletteColors { get; init; }

    public required byte[] SpriteBytes { get; init; }

    public required BackgroundPlaneLayoutMode BackgroundPlaneLayout { get; init; }

    public required int FineScrollX { get; init; }

    public required int FineScrollY { get; init; }

    public required int CoarseScrollX { get; init; }

    public required int CoarseScrollY { get; init; }

    public required int BackgroundPlaneSelect { get; init; }

    public required bool UseUpperBackgroundTileBank { get; init; }

    public required bool UseUpperSpriteTileBank { get; init; }

    public required bool UseTallSprites { get; init; }

    public required bool ShowBackground { get; init; }

    public required bool ShowSprites { get; init; }

    public required bool ShowBackgroundInFirstTileColumn { get; init; }

    public required bool ShowSpritesInFirstTileColumn { get; init; }

    public required bool HasCapturedBackgroundScanlineStates { get; init; }

    public required BackgroundScanlineRenderState[] BackgroundScanlineStates { get; init; }
}

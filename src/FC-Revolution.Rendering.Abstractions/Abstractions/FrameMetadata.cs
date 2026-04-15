using System.Numerics;
using FCRevolution.Rendering.Common;

namespace FCRevolution.Rendering.Abstractions;

public sealed class FrameMetadata : IFrameMetadata
{
    private readonly SpriteEntry[] _sprites;
    private readonly byte[] _backgroundPlaneBytes;
    private readonly byte[] _tileGraphicsBytes;
    private readonly uint[] _palette;
    private readonly Vector2 _backgroundMotionVector;
    private readonly Vector2[] _motionVectors;
    private readonly VisibleTile[] _visibleTiles;

    public FrameMetadata(
        SpriteEntry[]? sprites = null,
        byte[]? backgroundPlaneBytes = null,
        byte[]? tileGraphicsBytes = null,
        uint[]? palette = null,
        Vector2 backgroundMotionVector = default,
        Vector2[]? motionVectors = null,
        VisibleTile[]? visibleTiles = null,
        BackgroundPlaneLayoutMode backgroundPlaneLayout = BackgroundPlaneLayoutMode.SharedTopBottom,
        int fineScrollX = 0,
        int fineScrollY = 0,
        int coarseScrollX = 0,
        int coarseScrollY = 0,
        int backgroundPlaneSelect = 0,
        bool useUpperBackgroundTileBank = false,
        bool useUpperSpriteTileBank = false,
        bool useTallSprites = false,
        bool showBackground = true,
        bool showSprites = true,
        bool showBackgroundInFirstTileColumn = true,
        bool showSpritesInFirstTileColumn = true)
    {
        _sprites = sprites ?? [];
        _backgroundPlaneBytes = backgroundPlaneBytes ?? [];
        _tileGraphicsBytes = tileGraphicsBytes ?? [];
        _palette = palette ?? [];
        _backgroundMotionVector = backgroundMotionVector;
        _motionVectors = motionVectors ?? [];
        _visibleTiles = visibleTiles ?? [];
        BackgroundPlaneLayout = backgroundPlaneLayout;
        FineScrollX = fineScrollX;
        FineScrollY = fineScrollY;
        CoarseScrollX = coarseScrollX;
        CoarseScrollY = coarseScrollY;
        BackgroundPlaneSelect = backgroundPlaneSelect;
        UseUpperBackgroundTileBank = useUpperBackgroundTileBank;
        UseUpperSpriteTileBank = useUpperSpriteTileBank;
        UseTallSprites = useTallSprites;
        ShowBackground = showBackground;
        ShowSprites = showSprites;
        ShowBackgroundInFirstTileColumn = showBackgroundInFirstTileColumn;
        ShowSpritesInFirstTileColumn = showSpritesInFirstTileColumn;
    }

    public ReadOnlySpan<SpriteEntry> Sprites => _sprites;

    public ReadOnlySpan<byte> BackgroundPlaneBytes => _backgroundPlaneBytes;

    public ReadOnlySpan<byte> TileGraphicsBytes => _tileGraphicsBytes;

    public ReadOnlySpan<uint> Palette => _palette;

    public Vector2 BackgroundMotionVector => _backgroundMotionVector;

    public ReadOnlySpan<Vector2> MotionVectors => _motionVectors;

    public IReadOnlyList<VisibleTile> VisibleTiles => _visibleTiles;

    public BackgroundPlaneLayoutMode BackgroundPlaneLayout { get; }

    public int FineScrollX { get; }

    public int FineScrollY { get; }

    public int CoarseScrollX { get; }

    public int CoarseScrollY { get; }

    public int BackgroundPlaneSelect { get; }

    public bool UseUpperBackgroundTileBank { get; }

    public bool UseUpperSpriteTileBank { get; }

    public bool UseTallSprites { get; }

    public bool ShowBackground { get; }

    public bool ShowSprites { get; }

    public bool ShowBackgroundInFirstTileColumn { get; }

    public bool ShowSpritesInFirstTileColumn { get; }
}

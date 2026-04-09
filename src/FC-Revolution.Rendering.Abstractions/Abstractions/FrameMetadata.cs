using System.Numerics;
using FCRevolution.Rendering.Common;

namespace FCRevolution.Rendering.Abstractions;

public sealed class FrameMetadata : IFrameMetadata
{
    private readonly SpriteEntry[] _sprites;
    private readonly byte[] _nametable;
    private readonly byte[] _patternTable;
    private readonly uint[] _palette;
    private readonly Vector2 _backgroundMotionVector;
    private readonly Vector2[] _motionVectors;
    private readonly VisibleTile[] _visibleTiles;

    public FrameMetadata(
        SpriteEntry[]? sprites = null,
        byte[]? nametable = null,
        byte[]? patternTable = null,
        uint[]? palette = null,
        Vector2 backgroundMotionVector = default,
        Vector2[]? motionVectors = null,
        VisibleTile[]? visibleTiles = null,
        FrameMirroringMode mirrorMode = FrameMirroringMode.Horizontal,
        int fineScrollX = 0,
        int fineScrollY = 0,
        int coarseScrollX = 0,
        int coarseScrollY = 0,
        int nametableSelect = 0,
        bool useBackgroundPatternTableHighBank = false,
        bool useSpritePatternTableHighBank = false,
        bool use8x16Sprites = false,
        bool showBackground = true,
        bool showSprites = true,
        bool showBackgroundLeft8 = true,
        bool showSpritesLeft8 = true)
    {
        _sprites = sprites ?? [];
        _nametable = nametable ?? [];
        _patternTable = patternTable ?? [];
        _palette = palette ?? [];
        _backgroundMotionVector = backgroundMotionVector;
        _motionVectors = motionVectors ?? [];
        _visibleTiles = visibleTiles ?? [];
        MirrorMode = mirrorMode;
        FineScrollX = fineScrollX;
        FineScrollY = fineScrollY;
        CoarseScrollX = coarseScrollX;
        CoarseScrollY = coarseScrollY;
        NametableSelect = nametableSelect;
        UseBackgroundPatternTableHighBank = useBackgroundPatternTableHighBank;
        UseSpritePatternTableHighBank = useSpritePatternTableHighBank;
        Use8x16Sprites = use8x16Sprites;
        ShowBackground = showBackground;
        ShowSprites = showSprites;
        ShowBackgroundLeft8 = showBackgroundLeft8;
        ShowSpritesLeft8 = showSpritesLeft8;
    }

    public ReadOnlySpan<SpriteEntry> Sprites => _sprites;

    public ReadOnlySpan<byte> Nametable => _nametable;

    public ReadOnlySpan<byte> PatternTable => _patternTable;

    public ReadOnlySpan<uint> Palette => _palette;

    public Vector2 BackgroundMotionVector => _backgroundMotionVector;

    public ReadOnlySpan<Vector2> MotionVectors => _motionVectors;

    public IReadOnlyList<VisibleTile> VisibleTiles => _visibleTiles;

    public FrameMirroringMode MirrorMode { get; }

    public int FineScrollX { get; }

    public int FineScrollY { get; }

    public int CoarseScrollX { get; }

    public int CoarseScrollY { get; }

    public int NametableSelect { get; }

    public bool UseBackgroundPatternTableHighBank { get; }

    public bool UseSpritePatternTableHighBank { get; }

    public bool Use8x16Sprites { get; }

    public bool ShowBackground { get; }

    public bool ShowSprites { get; }

    public bool ShowBackgroundLeft8 { get; }

    public bool ShowSpritesLeft8 { get; }
}

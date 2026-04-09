using System.Numerics;
using FCRevolution.Core.Mappers;
using FCRevolution.Rendering.Common;

namespace FCRevolution.Rendering.Abstractions;

public interface IFrameMetadata
{
    ReadOnlySpan<SpriteEntry> Sprites { get; }

    ReadOnlySpan<byte> Nametable { get; }

    ReadOnlySpan<byte> PatternTable { get; }

    ReadOnlySpan<uint> Palette { get; }

    /// <summary>
    /// Frame-global background motion in screen-space pixels (previous -> current),
    /// derived from scroll delta and scaled for the current render resolution.
    /// </summary>
    Vector2 BackgroundMotionVector { get; }

    /// <summary>
    /// Per-sprite motion in screen-space pixels (previous -> current),
    /// indexed by sprite slot and scaled for the current render resolution.
    /// </summary>
    ReadOnlySpan<Vector2> MotionVectors { get; }

    IReadOnlyList<VisibleTile> VisibleTiles { get; }

    MirroringMode MirrorMode { get; }

    int FineScrollX { get; }

    int FineScrollY { get; }

    int CoarseScrollX { get; }

    int CoarseScrollY { get; }

    int NametableSelect { get; }

    bool UseBackgroundPatternTableHighBank { get; }

    bool UseSpritePatternTableHighBank { get; }

    bool Use8x16Sprites { get; }

    bool ShowBackground { get; }

    bool ShowSprites { get; }

    bool ShowBackgroundLeft8 { get; }

    bool ShowSpritesLeft8 { get; }
}

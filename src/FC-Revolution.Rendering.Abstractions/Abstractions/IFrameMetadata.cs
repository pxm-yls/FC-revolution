using System.Numerics;
using FCRevolution.Rendering.Common;

namespace FCRevolution.Rendering.Abstractions;

public interface IFrameMetadata
{
    ReadOnlySpan<SpriteEntry> Sprites { get; }

    ReadOnlySpan<byte> BackgroundPlaneBytes { get; }

    ReadOnlySpan<byte> TileGraphicsBytes { get; }

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

    BackgroundPlaneLayoutMode BackgroundPlaneLayout { get; }

    int FineScrollX { get; }

    int FineScrollY { get; }

    int CoarseScrollX { get; }

    int CoarseScrollY { get; }

    int BackgroundPlaneSelect { get; }

    bool UseUpperBackgroundTileBank { get; }

    bool UseUpperSpriteTileBank { get; }

    bool UseTallSprites { get; }

    bool ShowBackground { get; }

    bool ShowSprites { get; }

    bool ShowBackgroundInFirstTileColumn { get; }

    bool ShowSpritesInFirstTileColumn { get; }
}

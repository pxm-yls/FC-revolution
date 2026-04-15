using System.Numerics;
using FCRevolution.Rendering.Abstractions;

namespace FCRevolution.Rendering.Common;

public static class MotionVectorGenerator
{
    private const int ScrollWidthPixels = 64 * 8;
    private const int ScrollHeightPixels = 60 * 8;

    /// <summary>
    /// Returns frame-global background motion in screen-space pixels (previous -> current).
    /// Positive X/Y means on-screen content moved right/down.
    /// </summary>
    public static Vector2 GenerateBackgroundMotionVector(
        IFrameMetadata current,
        IFrameMetadata? previous,
        float scaleX,
        float scaleY)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (previous is null)
            return Vector2.Zero;

        return GenerateBackgroundMotionVector(
            currentFineScrollX: current.FineScrollX,
            currentFineScrollY: current.FineScrollY,
            currentCoarseScrollX: current.CoarseScrollX,
            currentCoarseScrollY: current.CoarseScrollY,
            currentBackgroundPlaneSelect: current.BackgroundPlaneSelect,
            previousFineScrollX: previous.FineScrollX,
            previousFineScrollY: previous.FineScrollY,
            previousCoarseScrollX: previous.CoarseScrollX,
            previousCoarseScrollY: previous.CoarseScrollY,
            previousBackgroundPlaneSelect: previous.BackgroundPlaneSelect,
            scaleX: scaleX,
            scaleY: scaleY);
    }

    /// <summary>
    /// Returns frame-global background motion in screen-space pixels (previous -> current).
    /// Scroll itself is camera-space, so background motion is the inverse of scroll delta.
    /// </summary>
    public static Vector2 GenerateBackgroundMotionVector(
        int currentFineScrollX,
        int currentFineScrollY,
        int currentCoarseScrollX,
        int currentCoarseScrollY,
        int currentBackgroundPlaneSelect,
        int previousFineScrollX,
        int previousFineScrollY,
        int previousCoarseScrollX,
        int previousCoarseScrollY,
        int previousBackgroundPlaneSelect,
        float scaleX,
        float scaleY)
    {
        int currentScrollX = ComputeAbsoluteScrollX(currentFineScrollX, currentCoarseScrollX, currentBackgroundPlaneSelect);
        int currentScrollY = ComputeAbsoluteScrollY(currentFineScrollY, currentCoarseScrollY, currentBackgroundPlaneSelect);
        int previousScrollX = ComputeAbsoluteScrollX(previousFineScrollX, previousCoarseScrollX, previousBackgroundPlaneSelect);
        int previousScrollY = ComputeAbsoluteScrollY(previousFineScrollY, previousCoarseScrollY, previousBackgroundPlaneSelect);

        int scrollDeltaX = ComputeWrappedDelta(currentScrollX, previousScrollX, ScrollWidthPixels);
        int scrollDeltaY = ComputeWrappedDelta(currentScrollY, previousScrollY, ScrollHeightPixels);

        return new Vector2(
            -scrollDeltaX * scaleX,
            -scrollDeltaY * scaleY);
    }

    /// <summary>
    /// Returns per-sprite motion in screen-space pixels (previous -> current),
    /// indexed by sprite slot and scaled for the current render resolution.
    /// </summary>
    public static Vector2[] GenerateSpriteMotionVectors(
        IFrameMetadata current,
        IFrameMetadata? previous,
        float scaleX,
        float scaleY)
    {
        if (previous is null)
            return new Vector2[current.Sprites.Length];

        return GenerateSpriteMotionVectors(current.Sprites, previous.Sprites, scaleX, scaleY);
    }

    public static Vector2[] GenerateSpriteMotionVectors(
        ReadOnlySpan<SpriteEntry> current,
        ReadOnlySpan<SpriteEntry> previous,
        float scaleX,
        float scaleY)
    {
        int count = Math.Max(current.Length, previous.Length);
        var vectors = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            var currentSprite = i < current.Length ? current[i] : default;
            var previousSprite = i < previous.Length ? previous[i] : default;
            vectors[i] = new Vector2(
                (currentSprite.X - previousSprite.X) * scaleX,
                (currentSprite.Y - previousSprite.Y) * scaleY);
        }

        return vectors;
    }

    private static int ComputeAbsoluteScrollX(int fineScrollX, int coarseScrollX, int backgroundPlaneSelect)
    {
        int planeX = backgroundPlaneSelect & 0x01;
        return (planeX * 32 * 8) + (coarseScrollX * 8) + fineScrollX;
    }

    private static int ComputeAbsoluteScrollY(int fineScrollY, int coarseScrollY, int backgroundPlaneSelect)
    {
        int planeY = (backgroundPlaneSelect >> 1) & 0x01;
        return (planeY * 30 * 8) + (coarseScrollY * 8) + fineScrollY;
    }

    private static int ComputeWrappedDelta(int current, int previous, int period)
    {
        int delta = current - previous;
        int halfPeriod = period / 2;

        if (delta > halfPeriod)
            delta -= period;
        else if (delta < -halfPeriod)
            delta += period;

        return delta;
    }
}

using System.Numerics;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Common;

namespace FC_Revolution.Rendering.Tests;

public sealed class MotionVectorGeneratorTests
{
    [Fact]
    public void GenerateSpriteMotionVectors_UsesSpriteDeltaAndScale()
    {
        SpriteEntry[] previous =
        [
            new() { X = 10, Y = 6 }
        ];

        SpriteEntry[] current =
        [
            new() { X = 20, Y = 9 }
        ];

        Vector2[] vectors = MotionVectorGenerator.GenerateSpriteMotionVectors(current, previous, 2f, 3f);

        Assert.Single(vectors);
        Assert.Equal(new Vector2(20f, 9f), vectors[0]);
    }

    [Fact]
    public void GenerateSpriteMotionVectors_WithMissingPreviousFrame_ReturnsZeroVectors()
    {
        var current = new FrameMetadata(
            sprites:
            [
                new SpriteEntry { X = 20, Y = 9 },
                new SpriteEntry { X = 32, Y = 18 }
            ]);

        Vector2[] vectors = MotionVectorGenerator.GenerateSpriteMotionVectors(current, previous: null, 1f, 1f);

        Assert.Equal(2, vectors.Length);
        Assert.All(vectors, vector => Assert.Equal(Vector2.Zero, vector));
    }

    [Fact]
    public void GenerateBackgroundMotionVector_UsesInverseScrollDeltaAndScale()
    {
        var previous = new FrameMetadata(
            fineScrollX: 2,
            fineScrollY: 3,
            coarseScrollX: 5,
            coarseScrollY: 2,
            nametableSelect: 0);

        var current = new FrameMetadata(
            fineScrollX: 5,
            fineScrollY: 1,
            coarseScrollX: 6,
            coarseScrollY: 4,
            nametableSelect: 0);

        Vector2 vector = MotionVectorGenerator.GenerateBackgroundMotionVector(current, previous, scaleX: 2f, scaleY: 0.5f);

        Assert.Equal(new Vector2(-22f, -7f), vector);
    }

    [Fact]
    public void GenerateBackgroundMotionVector_AppliesWrappedScrollDelta()
    {
        var previous = new FrameMetadata(
            fineScrollX: 7,
            fineScrollY: 7,
            coarseScrollX: 31,
            coarseScrollY: 29,
            nametableSelect: 0b11);

        var current = new FrameMetadata(
            fineScrollX: 0,
            fineScrollY: 0,
            coarseScrollX: 0,
            coarseScrollY: 0,
            nametableSelect: 0);

        Vector2 vector = MotionVectorGenerator.GenerateBackgroundMotionVector(current, previous, scaleX: 1f, scaleY: 1f);

        Assert.Equal(new Vector2(-1f, -1f), vector);
    }
}

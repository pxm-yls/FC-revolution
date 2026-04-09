using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowRewindSequencePlannerTests
{
    [Fact]
    public void Build_UsesAtLeastOneStep_WhenSnapshotIntervalIsZero()
    {
        var queriedFrames = new List<long>();
        var snapshots = new Dictionary<long, CoreTimelineSnapshot>
        {
            [5] = CreateSnapshot(5),
            [4] = CreateSnapshot(4),
            [3] = CreateSnapshot(3)
        };

        var result = GameWindowRewindSequencePlanner.Build(
            currentFrame: 5,
            targetFrame: 3,
            snapshotInterval: 0,
            getNearest: frame =>
            {
                queriedFrames.Add(frame);
                return snapshots.TryGetValue(frame, out var snapshot) ? snapshot : null;
            });

        Assert.Equal([5L, 4L, 3L], queriedFrames);
        Assert.Equal([5L, 4L, 3L], result.Select(snapshot => snapshot.Frame));
    }

    [Fact]
    public void Build_DeduplicatesConsecutiveHitsToSameSnapshotFrame()
    {
        var shared = CreateSnapshot(4);

        var result = GameWindowRewindSequencePlanner.Build(
            currentFrame: 6,
            targetFrame: 2,
            snapshotInterval: 2,
            getNearest: frame => frame switch
            {
                6 => shared,
                4 => shared,
                2 => CreateSnapshot(2),
                _ => null
            });

        Assert.Equal([4L, 2L], result.Select(snapshot => snapshot.Frame));
    }

    [Fact]
    public void Build_WhenMainLoopFindsNothing_FallsBackToTargetFrameLookup()
    {
        var queriedFrames = new List<long>();
        var fallback = CreateSnapshot(1);
        var fallbackServed = false;

        var result = GameWindowRewindSequencePlanner.Build(
            currentFrame: 10,
            targetFrame: 4,
            snapshotInterval: 3,
            getNearest: frame =>
            {
                queriedFrames.Add(frame);
                if (frame == 4 && !fallbackServed)
                {
                    fallbackServed = true;
                    return null;
                }

                return frame == 4 ? fallback : null;
            });

        Assert.Equal([10L, 7L, 4L, 4L], queriedFrames);
        Assert.Single(result);
        Assert.Same(fallback, result[0]);
    }

    [Fact]
    public void Build_WhenFallbackMisses_ReturnsEmpty()
    {
        var result = GameWindowRewindSequencePlanner.Build(
            currentFrame: 8,
            targetFrame: 2,
            snapshotInterval: 3,
            getNearest: _ => null);

        Assert.Empty(result);
    }

    private static CoreTimelineSnapshot CreateSnapshot(long frame) => new()
    {
        Frame = frame,
        TimestampSeconds = frame / 60.0,
        Thumbnail = [],
        State = new CoreStateBlob
        {
            Format = "test/snapshot",
            Data = []
        }
    };
}

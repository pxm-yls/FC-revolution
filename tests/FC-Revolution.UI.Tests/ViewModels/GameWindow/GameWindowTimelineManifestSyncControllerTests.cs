using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowTimelineManifestSyncControllerTests
{
    [Fact]
    public void BuildSyncResult_WhenObservedWriteTimeIsNewer_RequestsManifestSync()
    {
        var knownWriteTimeUtc = new DateTime(2026, 4, 3, 9, 30, 0, DateTimeKind.Utc);
        var observedWriteTimeUtc = knownWriteTimeUtc.AddSeconds(2);

        var result = GameWindowTimelineManifestSyncController.BuildSyncResult(
            knownWriteTimeUtc,
            observedWriteTimeUtc);

        Assert.True(result.ShouldSyncManifest);
        Assert.Equal(observedWriteTimeUtc, result.ObservedWriteTimeUtc);
    }

    [Fact]
    public void BuildSyncResult_WhenObservedWriteTimeMatchesKnown_DoesNotSync()
    {
        var knownWriteTimeUtc = new DateTime(2026, 4, 3, 9, 30, 0, DateTimeKind.Utc);

        var result = GameWindowTimelineManifestSyncController.BuildSyncResult(
            knownWriteTimeUtc,
            knownWriteTimeUtc);

        Assert.False(result.ShouldSyncManifest);
        Assert.Equal(knownWriteTimeUtc, result.ObservedWriteTimeUtc);
    }

    [Fact]
    public void BuildSyncResult_WhenObservedWriteTimeIsOlder_DoesNotSync()
    {
        var knownWriteTimeUtc = new DateTime(2026, 4, 3, 9, 30, 0, DateTimeKind.Utc);
        var observedWriteTimeUtc = knownWriteTimeUtc.AddSeconds(-2);

        var result = GameWindowTimelineManifestSyncController.BuildSyncResult(
            knownWriteTimeUtc,
            observedWriteTimeUtc);

        Assert.False(result.ShouldSyncManifest);
        Assert.Equal(observedWriteTimeUtc, result.ObservedWriteTimeUtc);
    }
}

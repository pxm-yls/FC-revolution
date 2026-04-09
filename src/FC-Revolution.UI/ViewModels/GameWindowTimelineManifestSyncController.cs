using System;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowTimelineManifestSyncResult(
    bool ShouldSyncManifest,
    DateTime ObservedWriteTimeUtc);

internal static class GameWindowTimelineManifestSyncController
{
    public static GameWindowTimelineManifestSyncResult BuildSyncResult(
        DateTime knownWriteTimeUtc,
        DateTime observedWriteTimeUtc)
    {
        return new GameWindowTimelineManifestSyncResult(
            ShouldSyncManifest: observedWriteTimeUtc > knownWriteTimeUtc,
            ObservedWriteTimeUtc: observedWriteTimeUtc);
    }
}

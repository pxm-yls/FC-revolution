using System;
using System.Collections.Generic;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Adapters.LegacyTimeline;

namespace FC_Revolution.UI.ViewModels;

internal static class GameWindowRewindSequencePlanner
{
    public static List<CoreTimelineSnapshot> Build(
        long currentFrame,
        long targetFrame,
        int snapshotInterval,
        Func<long, CoreTimelineSnapshot?> getNearest)
    {
        var snapshots = new List<CoreTimelineSnapshot>();
        var step = Math.Max(1, snapshotInterval);
        long lastAdded = long.MaxValue;
        for (var frame = currentFrame; frame >= targetFrame; frame -= step)
        {
            var snapshot = getNearest(frame);
            if (snapshot == null)
                continue;

            var snapshotFrame = CoreTimelineModelBridge.ReadFrame(snapshot);
            if (snapshotFrame == lastAdded)
                continue;

            snapshots.Add(snapshot);
            lastAdded = snapshotFrame;
        }

        if (snapshots.Count == 0)
        {
            var fallback = getNearest(targetFrame);
            if (fallback != null)
                snapshots.Add(fallback);
        }

        return snapshots;
    }
}

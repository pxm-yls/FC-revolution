using FCRevolution.Core.Timeline.Persistence;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Adapters.LegacyTimeline;

internal static class GameWindowPreviewNodeFactory
{
    public static BranchPreviewNode Create(
        TimelineSnapshotRecord record,
        CoreTimelineSnapshot snapshot,
        int previewWidth,
        int previewHeight)
    {
        var frame = CoreTimelineModelBridge.ReadFrame(snapshot);
        var timestampSeconds = CoreTimelineModelBridge.ReadTimestampSeconds(snapshot);
        var thumbnail = CoreTimelineModelBridge.ReadThumbnail(snapshot);
        return new BranchPreviewNode
        {
            Id = record.SnapshotId,
            Frame = frame,
            TimestampSeconds = timestampSeconds,
            Title = record.Name ?? $"画面节点 {frame}",
            Bitmap = ThumbnailItem.CreateBitmap(thumbnail, previewWidth, previewHeight),
        };
    }
}

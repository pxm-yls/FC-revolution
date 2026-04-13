using FCRevolution.Core.FC.LegacyAdapters;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Adapters.LegacyTimeline;

internal static class GameWindowPreviewNodeFactory
{
    public static BranchPreviewNode Create(
        LegacyTimelinePreviewEntry previewEntry,
        int previewWidth,
        int previewHeight)
    {
        var snapshot = previewEntry.Snapshot;
        var frame = snapshot.Frame;
        var timestampSeconds = snapshot.TimestampSeconds;
        var thumbnail = snapshot.Thumbnail;
        return new BranchPreviewNode
        {
            Id = previewEntry.SnapshotId,
            Frame = frame,
            TimestampSeconds = timestampSeconds,
            Title = previewEntry.Name ?? $"画面节点 {frame}",
            Bitmap = ThumbnailItem.CreateBitmap(thumbnail, previewWidth, previewHeight),
        };
    }
}

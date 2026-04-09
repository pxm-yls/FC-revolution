using FCRevolution.Emulation.Abstractions;
using FCRevolution.Core.Timeline.Persistence;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class GameWindowPreviewNodeFactoryTests
{
    [Fact]
    public void Create_MapsRecordFields_AndBuildsBitmapWithRequestedSize()
    {
        var record = new TimelineSnapshotRecord
        {
            SnapshotId = Guid.NewGuid(),
            Frame = 321,
            TimestampSeconds = 5.35,
            Name = "自定义节点标题"
        };
        var snapshot = new CoreTimelineSnapshot
        {
            Frame = 321,
            TimestampSeconds = 5.35,
            Thumbnail = new uint[64 * 60],
            State = new CoreStateBlob
            {
                Format = "test/snapshot",
                Data = []
            }
        };

        var node = GameWindowPreviewNodeFactory.Create(record, snapshot, previewWidth: 256, previewHeight: 240);

        Assert.Equal(record.SnapshotId, node.Id);
        Assert.Equal(record.Frame, node.Frame);
        Assert.Equal(record.TimestampSeconds, node.TimestampSeconds);
        Assert.Equal("自定义节点标题", node.Title);
        Assert.NotNull(node.Bitmap);
        Assert.Equal(256, node.Bitmap!.PixelSize.Width);
        Assert.Equal(240, node.Bitmap.PixelSize.Height);
    }

    [Fact]
    public void Create_UsesFallbackTitle_WhenRecordNameMissing()
    {
        var record = new TimelineSnapshotRecord
        {
            SnapshotId = Guid.NewGuid(),
            Frame = 42,
            TimestampSeconds = 0.7,
            Name = null
        };
        var snapshot = new CoreTimelineSnapshot
        {
            Frame = 42,
            TimestampSeconds = 0.7,
            Thumbnail = new uint[64 * 60],
            State = new CoreStateBlob
            {
                Format = "test/snapshot",
                Data = []
            }
        };

        var node = GameWindowPreviewNodeFactory.Create(record, snapshot, previewWidth: 128, previewHeight: 96);

        Assert.Equal("画面节点 42", node.Title);
    }
}

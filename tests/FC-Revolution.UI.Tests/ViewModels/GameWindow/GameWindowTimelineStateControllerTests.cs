using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowTimelineStateControllerTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void BuildRefreshViewState_ComputesRefreshAllGating(
        bool force,
        bool isBranchGalleryVisible,
        bool expectedShouldRefreshAll)
    {
        var state = GameWindowTimelineStateController.BuildRefreshViewState(
            force,
            isBranchGalleryVisible,
            currentFrame: 120,
            newestFrame: 80,
            timestampSeconds: 1.25);

        Assert.Equal(expectedShouldRefreshAll, state.ShouldRefreshAll);
    }

    [Fact]
    public void BuildRefreshViewState_ProjectsCursorAndTexts_WhenNoSnapshotExists()
    {
        var state = GameWindowTimelineStateController.BuildRefreshViewState(
            force: false,
            isBranchGalleryVisible: true,
            currentFrame: 42,
            newestFrame: -1,
            timestampSeconds: 3.5);

        Assert.Equal(42, state.Cursor.CurrentFrame);
        Assert.Equal(3.5, state.Cursor.TimestampSeconds);
        Assert.Equal("时间线位置: 帧 42 | 尚无可视快照", state.TimelinePositionText);
        Assert.Equal("主轴固定在中线，当前时刻保持居中；右侧按钮可调整轴点与时间尺度，右键节点可载入、分支或生成画面节点", state.TimelineHintText);
    }

    [Fact]
    public void BuildRefreshViewState_ProjectsPositionText_WhenSnapshotExists()
    {
        var state = GameWindowTimelineStateController.BuildRefreshViewState(
            force: false,
            isBranchGalleryVisible: false,
            currentFrame: 240,
            newestFrame: 180,
            timestampSeconds: 6.25);

        Assert.Equal("时间线位置: 当前帧 240 | 最近快照帧 180", state.TimelinePositionText);
    }
}

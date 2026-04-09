namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowTimelineCursorViewState(
    long CurrentFrame,
    double TimestampSeconds);

internal readonly record struct GameWindowTimelineRefreshViewState(
    bool ShouldRefreshAll,
    GameWindowTimelineCursorViewState Cursor,
    string TimelinePositionText,
    string TimelineHintText);

internal static class GameWindowTimelineStateController
{
    private const string TimelineHint =
        "主轴固定在中线，当前时刻保持居中；右侧按钮可调整轴点与时间尺度，右键节点可载入、分支或生成画面节点";

    public static GameWindowTimelineRefreshViewState BuildRefreshViewState(
        bool force,
        bool isBranchGalleryVisible,
        long currentFrame,
        long newestFrame,
        double timestampSeconds)
    {
        var shouldRefreshAll = force || isBranchGalleryVisible;
        var positionText = newestFrame < 0
            ? $"时间线位置: 帧 {currentFrame} | 尚无可视快照"
            : $"时间线位置: 当前帧 {currentFrame} | 最近快照帧 {newestFrame}";

        return new GameWindowTimelineRefreshViewState(
            shouldRefreshAll,
            new GameWindowTimelineCursorViewState(currentFrame, timestampSeconds),
            positionText,
            TimelineHint);
    }
}

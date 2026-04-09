namespace FC_Revolution.UI.ViewModels;

internal sealed record PreviewStartupWarmupStartState(
    string CurrentStep,
    string PreviewStatus);

internal sealed record PreviewStartupWarmupCompletionState(
    string CurrentStep,
    string PreviewStatus,
    string LanStatus);

internal sealed class MainWindowPreviewStartupController
{
    public PreviewStartupWarmupStartState BuildWarmupStartState(string? currentRomDisplayName)
    {
        return new PreviewStartupWarmupStartState(
            CurrentStep: currentRomDisplayName == null
                ? "正在整理预览资源。"
                : $"正在预热 {currentRomDisplayName} 的预览动画。",
            PreviewStatus: "加载中");
    }

    public PreviewStartupWarmupCompletionState BuildWarmupCompletionState(int warmedPreviewCount, bool isLanArcadeEnabled)
    {
        return new PreviewStartupWarmupCompletionState(
            CurrentStep: warmedPreviewCount == 0
                ? "预览阶段完成，当前没有可加载的预览。"
                : $"预览阶段完成，已预热 {warmedPreviewCount} 个预览。",
            PreviewStatus: warmedPreviewCount == 0 ? "完成（无预览）" : $"完成（{warmedPreviewCount} 个）",
            LanStatus: isLanArcadeEnabled ? "准备启动" : "已跳过（已关闭）");
    }
}

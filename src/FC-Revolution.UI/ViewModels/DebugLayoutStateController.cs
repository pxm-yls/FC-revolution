using System;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct DebugLayoutViewState(
    bool HasVisibleLeftSections,
    bool HasVisibleRightSections,
    bool ShowLeftPane,
    bool ShowRightPane,
    int LeftPaneColumnSpan,
    int RightPaneColumn,
    int RightPaneColumnSpan,
    double PreferredWindowWidth,
    double PreferredWindowHeight,
    double PreferredMinWidth,
    double PreferredMinHeight,
    double MemoryCellFontSize,
    double CompactMemoryCellFontSize);

internal readonly record struct DebugPendingDisplaySettingsViewState(
    bool HasPendingDisplaySettingsChanges,
    string DisplaySettingsRestartHint);

internal static class DebugLayoutStateController
{
    private const string PendingChangesRestartHint = "新设置已保存，重启当前游戏后才会同步影响显示与统计。";
    private const string StableRestartHint = "这里修改的是下次启动配置，当前会话保持稳定不实时切换。";

    public static DebugLayoutViewState BuildLayoutViewState(DebugWindowDisplaySettingsProfile settings)
    {
        var hasVisibleLeftSections =
            settings.ShowRegisters ||
            settings.ShowPpu ||
            settings.ShowDisasm ||
            settings.ShowStack ||
            settings.ShowZeroPage;
        var hasVisibleRightSections =
            settings.ShowMemoryEditor ||
            settings.ShowMemoryPage ||
            settings.ShowModifiedMemory;
        var showLeftPane = hasVisibleLeftSections || !hasVisibleRightSections;
        var showRightPane = hasVisibleRightSections || !hasVisibleLeftSections;
        var leftPaneColumnSpan = showLeftPane && !showRightPane ? 2 : 1;
        var rightPaneColumn = showLeftPane && showRightPane ? 1 : 0;
        var rightPaneColumnSpan = showRightPane && !showLeftPane ? 2 : 1;
        var preferredWindowWidth = showLeftPane && showRightPane ? 1200 : showRightPane ? 960 : 900;
        var dominantSectionCount = Math.Max(GetVisibleLeftSectionCount(settings), GetVisibleRightSectionCount(settings));
        var preferredWindowHeight = Math.Clamp(430 + dominantSectionCount * 105, 620, 920);
        var preferredMinWidth = showLeftPane && showRightPane ? 960 : 760;
        var memoryCellFontSize = showLeftPane && showRightPane ? 10 : 11;
        var compactMemoryCellFontSize = showLeftPane && showRightPane ? 9 : 10;

        return new DebugLayoutViewState(
            hasVisibleLeftSections,
            hasVisibleRightSections,
            showLeftPane,
            showRightPane,
            leftPaneColumnSpan,
            rightPaneColumn,
            rightPaneColumnSpan,
            preferredWindowWidth,
            preferredWindowHeight,
            preferredMinWidth,
            560,
            memoryCellFontSize,
            compactMemoryCellFontSize);
    }

    public static DebugPendingDisplaySettingsViewState BuildPendingDisplaySettingsViewState(
        DebugWindowDisplaySettingsProfile activeSettings,
        DebugWindowDisplaySettingsProfile pendingSettings)
    {
        var hasPendingDisplaySettingsChanges =
            activeSettings.ShowRegisters != pendingSettings.ShowRegisters ||
            activeSettings.ShowPpu != pendingSettings.ShowPpu ||
            activeSettings.ShowDisasm != pendingSettings.ShowDisasm ||
            activeSettings.ShowStack != pendingSettings.ShowStack ||
            activeSettings.ShowZeroPage != pendingSettings.ShowZeroPage ||
            activeSettings.ShowMemoryEditor != pendingSettings.ShowMemoryEditor ||
            activeSettings.ShowMemoryPage != pendingSettings.ShowMemoryPage ||
            activeSettings.ShowModifiedMemory != pendingSettings.ShowModifiedMemory;

        return new DebugPendingDisplaySettingsViewState(
            hasPendingDisplaySettingsChanges,
            hasPendingDisplaySettingsChanges ? PendingChangesRestartHint : StableRestartHint);
    }

    private static int GetVisibleLeftSectionCount(DebugWindowDisplaySettingsProfile settings)
    {
        var count = 0;
        if (settings.ShowRegisters) count++;
        if (settings.ShowPpu) count++;
        if (settings.ShowDisasm) count++;
        if (settings.ShowStack) count++;
        if (settings.ShowZeroPage) count++;
        return count;
    }

    private static int GetVisibleRightSectionCount(DebugWindowDisplaySettingsProfile settings)
    {
        var count = 0;
        if (settings.ShowMemoryEditor) count++;
        if (settings.ShowMemoryPage) count++;
        if (settings.ShowModifiedMemory) count++;
        return count;
    }
}

using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class DebugLayoutStateControllerTests
{
    [Fact]
    public void BuildLayoutViewState_RightPaneOnly_UsesSinglePaneMetrics()
    {
        var state = DebugLayoutStateController.BuildLayoutViewState(new DebugWindowDisplaySettingsProfile
        {
            ShowMemoryEditor = true,
            ShowMemoryPage = true,
            ShowModifiedMemory = true
        });

        Assert.False(state.HasVisibleLeftSections);
        Assert.True(state.HasVisibleRightSections);
        Assert.False(state.ShowLeftPane);
        Assert.True(state.ShowRightPane);
        Assert.Equal(0, state.RightPaneColumn);
        Assert.Equal(2, state.RightPaneColumnSpan);
        Assert.Equal(960, state.PreferredWindowWidth);
        Assert.Equal(745, state.PreferredWindowHeight);
        Assert.Equal(760, state.PreferredMinWidth);
        Assert.Equal(11, state.MemoryCellFontSize);
        Assert.Equal(10, state.CompactMemoryCellFontSize);
    }

    [Fact]
    public void BuildLayoutViewState_DualPane_UsesCompactMetrics()
    {
        var state = DebugLayoutStateController.BuildLayoutViewState(new DebugWindowDisplaySettingsProfile
        {
            ShowRegisters = true,
            ShowMemoryEditor = true,
            ShowMemoryPage = true,
            ShowModifiedMemory = true
        });

        Assert.True(state.HasVisibleLeftSections);
        Assert.True(state.HasVisibleRightSections);
        Assert.True(state.ShowLeftPane);
        Assert.True(state.ShowRightPane);
        Assert.Equal(1, state.LeftPaneColumnSpan);
        Assert.Equal(1, state.RightPaneColumn);
        Assert.Equal(1, state.RightPaneColumnSpan);
        Assert.Equal(1200, state.PreferredWindowWidth);
        Assert.Equal(960, state.PreferredMinWidth);
        Assert.Equal(10, state.MemoryCellFontSize);
        Assert.Equal(9, state.CompactMemoryCellFontSize);
    }

    [Fact]
    public void BuildPendingDisplaySettingsViewState_UsesStableHint_WhenNoSettingsChange()
    {
        var activeSettings = new DebugWindowDisplaySettingsProfile
        {
            ShowRegisters = true,
            ShowMemoryEditor = true,
            ShowMemoryPage = true,
            ShowModifiedMemory = true
        };

        var state = DebugLayoutStateController.BuildPendingDisplaySettingsViewState(activeSettings, activeSettings.Clone());

        Assert.False(state.HasPendingDisplaySettingsChanges);
        Assert.Equal("这里修改的是下次启动配置，当前会话保持稳定不实时切换。", state.DisplaySettingsRestartHint);
    }

    [Fact]
    public void BuildPendingDisplaySettingsViewState_UsesRestartHint_WhenAnySettingChanges()
    {
        var activeSettings = new DebugWindowDisplaySettingsProfile
        {
            ShowRegisters = true,
            ShowMemoryEditor = true,
            ShowMemoryPage = true,
            ShowModifiedMemory = true
        };
        var pendingSettings = activeSettings.Clone();
        pendingSettings.ShowRegisters = false;

        var state = DebugLayoutStateController.BuildPendingDisplaySettingsViewState(activeSettings, pendingSettings);

        Assert.True(state.HasPendingDisplaySettingsChanges);
        Assert.Equal("新设置已保存，重启当前游戏后才会同步影响显示与统计。", state.DisplaySettingsRestartHint);
    }
}

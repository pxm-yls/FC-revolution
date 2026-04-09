using System;
using System.Collections.Generic;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct ResourceCleanupWorkflowResult(
    bool ShouldRefreshLibrary,
    string ResultText,
    string SummaryText);

internal sealed class MainWindowResourceCleanupWorkflowController
{
    private readonly Func<ResourceCleanupSelection, IEnumerable<RomLibraryItem>, ResourceCleanupResult> _executeCleanup;
    private readonly Func<ResourceCleanupSnapshot> _captureCleanupSnapshot;

    public MainWindowResourceCleanupWorkflowController(MainWindowResourceManagementController resourceManagementController)
        : this(resourceManagementController.ExecuteCleanup, resourceManagementController.CaptureCleanupSnapshot)
    {
    }

    internal MainWindowResourceCleanupWorkflowController(
        Func<ResourceCleanupSelection, IEnumerable<RomLibraryItem>, ResourceCleanupResult> executeCleanup,
        Func<ResourceCleanupSnapshot> captureCleanupSnapshot)
    {
        _executeCleanup = executeCleanup;
        _captureCleanupSnapshot = captureCleanupSnapshot;
    }

    public ResourceCleanupWorkflowResult ExecuteCleanup(
        ResourceCleanupSelection selection,
        IEnumerable<RomLibraryItem> romLibrary)
    {
        if (!selection.HasAnySelection)
        {
            return new ResourceCleanupWorkflowResult(
                ShouldRefreshLibrary: false,
                ResultText: "请至少勾选一项要清理的资源。",
                SummaryText: BuildCleanupSummary());
        }

        var result = _executeCleanup(selection, romLibrary);
        return new ResourceCleanupWorkflowResult(
            ShouldRefreshLibrary: true,
            ResultText: result.ToSummaryText(),
            SummaryText: BuildCleanupSummary());
    }

    public string BuildCleanupSummary()
    {
        var snapshot = _captureCleanupSnapshot();
        return $"当前资源统计：预览动画 {snapshot.PreviewCount} 个，缩略图/封面 {snapshot.ImageCount} 个，时间线存档文件 {snapshot.TimelineFileCount} 个，导出视频 {snapshot.ExportVideoCount} 个。";
    }
}

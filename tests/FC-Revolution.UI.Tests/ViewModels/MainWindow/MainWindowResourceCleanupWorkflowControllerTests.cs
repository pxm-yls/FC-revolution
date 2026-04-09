using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowResourceCleanupWorkflowControllerTests
{
    [Fact]
    public void ExecuteCleanup_WithoutSelection_ReturnsValidationText_AndDoesNotRequestRefresh()
    {
        var executeCalls = 0;
        var controller = new MainWindowResourceCleanupWorkflowController(
            (_, _) =>
            {
                executeCalls++;
                return new ResourceCleanupResult(1, 2, 3, 4);
            },
            () => new ResourceCleanupSnapshot(5, 6, 7, 8));

        var result = controller.ExecuteCleanup(
            new ResourceCleanupSelection(
                CleanupPreviewAnimations: false,
                CleanupThumbnails: false,
                CleanupTimelineSaves: false,
                CleanupExportVideos: false),
            []);

        Assert.False(result.ShouldRefreshLibrary);
        Assert.Equal("请至少勾选一项要清理的资源。", result.ResultText);
        Assert.Equal("当前资源统计：预览动画 5 个，缩略图/封面 6 个，时间线存档文件 7 个，导出视频 8 个。", result.SummaryText);
        Assert.Equal(0, executeCalls);
    }

    [Fact]
    public void ExecuteCleanup_WithSelection_FormatsResultAndRequestsRefresh()
    {
        ResourceCleanupSelection? capturedSelection = null;
        IReadOnlyList<RomLibraryItem>? capturedRomLibrary = null;
        var controller = new MainWindowResourceCleanupWorkflowController(
            (selection, romLibrary) =>
            {
                capturedSelection = selection;
                capturedRomLibrary = romLibrary.ToList();
                return new ResourceCleanupResult(2, 3, 4, 5);
            },
            () => new ResourceCleanupSnapshot(10, 11, 12, 13));
        var rom = new RomLibraryItem(
            "contra.nes",
            "/tmp/contra.nes",
            "/tmp/contra.mp4",
            hasPreview: true,
            fileSizeBytes: 1,
            importedAtUtc: DateTime.UtcNow);

        var result = controller.ExecuteCleanup(
            new ResourceCleanupSelection(
                CleanupPreviewAnimations: true,
                CleanupThumbnails: false,
                CleanupTimelineSaves: true,
                CleanupExportVideos: false),
            [rom]);

        Assert.True(result.ShouldRefreshLibrary);
        Assert.Equal("清理完成：预览动画 2 个，缩略图/封面 3 个，时间线存档文件 4 个，导出视频 5 个。", result.ResultText);
        Assert.Equal("当前资源统计：预览动画 10 个，缩略图/封面 11 个，时间线存档文件 12 个，导出视频 13 个。", result.SummaryText);
        Assert.True(capturedSelection.HasValue);
        Assert.True(capturedSelection.Value.CleanupPreviewAnimations);
        Assert.True(capturedSelection.Value.CleanupTimelineSaves);
        Assert.NotNull(capturedRomLibrary);
        Assert.Same(rom, Assert.Single(capturedRomLibrary!));
    }
}

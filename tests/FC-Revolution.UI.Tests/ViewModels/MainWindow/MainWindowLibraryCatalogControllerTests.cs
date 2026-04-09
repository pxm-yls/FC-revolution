using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowLibraryCatalogControllerTests
{
    [Fact]
    public void BuildVisibleSelection_PreservesPreferredRomWhenStillVisible()
    {
        var layoutController = new MainWindowCatalogLayoutController();
        var controller = new MainWindowLibraryCatalogController(layoutController);
        var allRoms = new List<RomLibraryItem>
        {
            CreateRom("contra.nes", 11),
            CreateRom("mario.nes", 22),
            CreateRom("zelda.nes", 33)
        };

        var preferredPath = allRoms[1].Path;
        var selection = controller.BuildVisibleSelection(
            allRoms,
            librarySearchText: "",
            sortField: RomSortField.Name,
            sortDescending: false,
            preferredPath,
            currentRomPath: null,
            fallbackRomPath: null,
            pathsEqual: (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(["contra", "mario", "zelda"], selection.VisibleItems.Select(item => item.DisplayName));
        Assert.NotNull(selection.PreferredRom);
        Assert.Equal("mario", selection.PreferredRom!.DisplayName);
    }

    [Fact]
    public void BuildEmptyLibraryState_WhenSearchHasNoResult_ReturnsSearchEmptyCopy()
    {
        var controller = new MainWindowLibraryCatalogController(new MainWindowCatalogLayoutController());

        var state = controller.BuildEmptyLibraryState(
            romDirectory: "/tmp/roms",
            totalRomCount: 7,
            librarySearchText: "  Ninja  ");

        Assert.Equal("没有匹配的 ROM", state.CurrentRomName);
        Assert.Equal("搜索“Ninja”没有命中，可尝试标题片段、文件名或清空搜索", state.CurrentRomPathText);
        Assert.Equal("当前筛选下没有可用预览", state.PreviewStatusText);
        Assert.Equal("当前搜索无结果，共 7 个游戏可用", state.StatusText);
    }

    [Fact]
    public void BuildLibrarySummary_WithSearch_UsesTotalAndVisibleCount()
    {
        var controller = new MainWindowLibraryCatalogController(new MainWindowCatalogLayoutController());

        var summary = controller.BuildLibrarySummary(
            romDirectory: "/tmp/roms",
            totalRomCount: 12,
            visibleRomCount: 3,
            hasLibrarySearchText: true,
            sortDescription: "名称 A-Z");

        Assert.Equal("已扫描 `/tmp/roms`，共 12 个 ROM，筛选后显示 3 个，当前排序 名称 A-Z", summary);
    }

    private static RomLibraryItem CreateRom(string fileName, long size)
    {
        return new RomLibraryItem(
            fileName,
            $"/tmp/{fileName}",
            $"/tmp/{Path.GetFileNameWithoutExtension(fileName)}.mp4",
            hasPreview: false,
            size,
            DateTime.UtcNow);
    }
}

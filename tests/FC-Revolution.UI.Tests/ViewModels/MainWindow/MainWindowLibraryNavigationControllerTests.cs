using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowLibraryNavigationControllerTests
{
    [Fact]
    public void GetNeighbor_ReturnsAdjacentRomOrNullAtBounds()
    {
        var controller = new MainWindowLibraryNavigationController();
        var roms = CreateRoms(3);

        Assert.Same(roms[0], controller.GetNeighbor(roms, roms[1], -1));
        Assert.Same(roms[2], controller.GetNeighbor(roms, roms[1], 1));
        Assert.Null(controller.GetNeighbor(roms, roms[0], -1));
        Assert.Null(controller.GetNeighbor(roms, roms[2], 1));
        Assert.Null(controller.GetNeighbor(roms, null, 1));
    }

    [Fact]
    public void ResolveKaleidoscopePageForCurrentRom_ComputesExpectedPage()
    {
        var controller = new MainWindowLibraryNavigationController();
        var roms = CreateRoms(18);

        var page = controller.ResolveKaleidoscopePageForCurrentRom(roms, roms[9], pageSize: 8);
        Assert.Equal(1, page);

        var detachedRom = CreateRom("detached.nes", 100);
        Assert.Null(controller.ResolveKaleidoscopePageForCurrentRom(roms, detachedRom, pageSize: 8));
    }

    [Fact]
    public void DecideKaleidoscopePageSelection_PreserveSelection_KeepsCurrentWhenStillOnTargetPage()
    {
        var controller = new MainWindowLibraryNavigationController();
        var roms = CreateRoms(12);

        var decision = controller.DecideKaleidoscopePageSelection(
            roms,
            pageSize: 8,
            requestedPageIndex: 1,
            currentRom: roms[9],
            preserveSelection: true);

        Assert.Equal(1, decision.PageIndex);
        Assert.True(decision.KeepCurrentSelection);
        Assert.Null(decision.FallbackSelection);
    }

    [Fact]
    public void DecideKaleidoscopePageSelection_WithoutPreserveSelection_SelectsFirstRomInTargetPage()
    {
        var controller = new MainWindowLibraryNavigationController();
        var roms = CreateRoms(12);

        var decision = controller.DecideKaleidoscopePageSelection(
            roms,
            pageSize: 8,
            requestedPageIndex: 99,
            currentRom: roms[9],
            preserveSelection: false);

        Assert.Equal(1, decision.PageIndex);
        Assert.False(decision.KeepCurrentSelection);
        Assert.Same(roms[8], decision.FallbackSelection);
    }

    [Fact]
    public void BuildShelfScrollSummary_ComputesCurrentAndTotalPages()
    {
        var controller = new MainWindowLibraryNavigationController();
        var roms = CreateRoms(10);

        var emptySummary = controller.BuildShelfScrollSummary(
            shelfSlotCount: 0,
            roms,
            currentRom: null,
            shelfColumns: 4,
            shelfRowsPerPage: 2);
        Assert.Equal("第 0 / 0 页", emptySummary);

        var summary = controller.BuildShelfScrollSummary(
            shelfSlotCount: 12,
            roms,
            currentRom: roms[9],
            shelfColumns: 4,
            shelfRowsPerPage: 2);
        Assert.Equal("第 2 / 2 页", summary);
    }

    private static List<RomLibraryItem> CreateRoms(int count)
    {
        var roms = new List<RomLibraryItem>(count);
        for (var index = 0; index < count; index++)
            roms.Add(CreateRom($"game-{index}.nes", index));

        return roms;
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

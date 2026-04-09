using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowCatalogLayoutControllerTests
{
    [Fact]
    public void BuildVisibleItems_FiltersAndSortsWithStableRules()
    {
        var controller = new MainWindowCatalogLayoutController();
        var roms = new List<RomLibraryItem>
        {
            CreateRom("b.nes", 200, new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
            CreateRom("a.nes", 100, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            CreateRom("zelda.nes", 150, new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc))
        };

        var byName = controller.BuildVisibleItems(roms, "", RomSortField.Name, sortDescending: false);
        Assert.Equal(["a", "b", "zelda"], byName.Select(item => item.DisplayName));

        var filtered = controller.BuildVisibleItems(roms, "zel", RomSortField.Name, sortDescending: false);
        Assert.Single(filtered);
        Assert.Equal("zelda", filtered[0].DisplayName);

        var bySizeDesc = controller.BuildVisibleItems(roms, "", RomSortField.Size, sortDescending: true);
        Assert.Equal([200L, 150L, 100L], bySizeDesc.Select(item => item.FileSizeBytes));
    }

    [Fact]
    public void BuildShelfLayout_PadsSlotsToFullRows()
    {
        var controller = new MainWindowCatalogLayoutController();
        var roms = new List<RomLibraryItem>
        {
            CreateRom("a.nes", 1, DateTime.UtcNow),
            CreateRom("b.nes", 1, DateTime.UtcNow),
            CreateRom("c.nes", 1, DateTime.UtcNow),
            CreateRom("d.nes", 1, DateTime.UtcNow),
            CreateRom("e.nes", 1, DateTime.UtcNow)
        };

        var (slots, rows) = controller.BuildShelfLayout(roms, shelfColumns: 4);
        Assert.Equal(8, slots.Count);
        Assert.Equal(2, rows.Count);
        Assert.Equal(3, slots.Count(item => item.IsEmpty));
    }

    [Fact]
    public void BuildKaleidoscopeLayout_NormalizesPageAndBuildsSlots()
    {
        var controller = new MainWindowCatalogLayoutController();
        var roms = Enumerable.Range(0, 10)
            .Select(index => CreateRom($"game-{index}.nes", index, DateTime.UtcNow.AddMinutes(index)))
            .ToList();

        var normalized = controller.NormalizeKaleidoscopePageIndex(roms.Count, kaleidoscopePageSize: 8, requestedIndex: 99);
        Assert.Equal(1, normalized);

        var pages = controller.BuildKaleidoscopePages(roms.Count, kaleidoscopePageSize: 8, currentPageIndex: normalized);
        Assert.Equal(2, pages.Count);
        Assert.True(pages[1].IsCurrent);

        var slots = controller.BuildKaleidoscopeSlots(roms, kaleidoscopePageSize: 8, currentPageIndex: normalized);
        Assert.Equal(8, slots.Count);
        Assert.Equal("game-8", slots[0].Rom?.DisplayName);
        Assert.Equal("game-9", slots[1].Rom?.DisplayName);
        Assert.Null(slots[2].Rom);
    }

    private static RomLibraryItem CreateRom(string fileName, long fileSizeBytes, DateTime importedAtUtc)
    {
        return new RomLibraryItem(
            fileName,
            $"/tmp/{fileName}",
            $"/tmp/{Path.GetFileNameWithoutExtension(fileName)}.mp4",
            hasPreview: false,
            fileSizeBytes,
            importedAtUtc);
    }
}

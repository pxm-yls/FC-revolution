using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowPreviewPlaybackPolicyControllerTests
{
    [Fact]
    public void BuildVisibleShelfPreviewTargets_ExpandsRangeWithWarmRows()
    {
        var controller = new MainWindowPreviewPlaybackPolicyController();
        var roms = Enumerable.Range(1, 12)
            .Select(index => CreateRom($"R{index}", $"/tmp/r{index}.nes"))
            .ToList();

        var targets = controller.BuildVisibleShelfPreviewTargets(
            roms,
            shelfVisibleStartRow: 1,
            shelfVisibleRowCount: 1,
            shelfColumns: 4,
            shelfWarmExtraRows: 1,
            includeWarmRows: true);

        Assert.Equal(12, targets.Count);
        Assert.Equal("/tmp/r1.nes", targets[0].Path);
        Assert.Equal("/tmp/r12.nes", targets[^1].Path);
    }

    [Fact]
    public void BuildPreviewAnimationTargets_ShelfModeFiltersByAnimatedSelector()
    {
        var controller = new MainWindowPreviewPlaybackPolicyController();
        var rom1 = CreateRom("R1", "/tmp/r1.nes");
        var rom2 = CreateRom("R2", "/tmp/r2.nes");
        var rom3 = CreateRom("R3", "/tmp/r3.nes");
        var animated = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rom1.Path, rom3.Path };

        var targets = controller.BuildPreviewAnimationTargets(
            isCarouselMode: false,
            isKaleidoscopeMode: false,
            isShelfScrolling: false,
            currentRom: null,
            previousRom: null,
            nextRom: null,
            shelfTargets: [rom1, rom2, rom3],
            kaleidoscopeTargets: [],
            isAnimatedSelector: item => animated.Contains(item.Path));

        Assert.Equal([rom1.Path, rom3.Path], targets.Select(item => item.Path).ToArray());
    }

    [Fact]
    public void BuildSmoothPlaybackTargetPaths_RespectsLayoutCap_AndLoadedAnimatedFilter()
    {
        var controller = new MainWindowPreviewPlaybackPolicyController();
        var rom1 = CreateRom("R1", "/tmp/r1.nes");
        var rom2 = CreateRom("R2", "/tmp/r2.nes");
        var rom3 = CreateRom("R3", "/tmp/r3.nes");
        var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rom1.Path, rom2.Path, rom3.Path };
        var animated = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rom1.Path, rom3.Path };

        var paths = controller.BuildSmoothPlaybackTargetPaths(
            previewAnimationTargets: [rom1, rom2, rom3],
            isShelfLayoutMode: false,
            isKaleidoscopeMode: false,
            maxShelfSmoothPlayback: 12,
            kaleidoscopePageSize: 8,
            maxMemoryAnimatedPreviews: 1,
            isLoadedSelector: item => loaded.Contains(item.Path),
            isAnimatedSelector: item => animated.Contains(item.Path));

        Assert.Single(paths);
        Assert.Contains(rom1.Path, paths);
    }

    [Fact]
    public void BuildTrimEvictionCandidates_PrioritizesFartherItems_AndSkipsKeepAlive()
    {
        var controller = new MainWindowPreviewPlaybackPolicyController();
        var roms = new[]
        {
            CreateRom("R1", "/tmp/r1.nes"),
            CreateRom("R2", "/tmp/r2.nes"),
            CreateRom("R3", "/tmp/r3.nes"),
            CreateRom("R4", "/tmp/r4.nes"),
            CreateRom("R5", "/tmp/r5.nes")
        };
        var loaded = roms.Select(item => item.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keepAlive = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/tmp/r1.nes" };
        var priority = roms[2];

        var evictions = controller.BuildTrimEvictionCandidates(
            roms,
            keepAlive,
            priorityRom: priority,
            maxLoadedPreviewCache: 3,
            isLoadedSelector: item => loaded.Contains(item.Path),
            pathsEqual: (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(2, evictions.Count);
        Assert.DoesNotContain(evictions, item => item.Path == "/tmp/r1.nes");
        Assert.Contains(evictions, item => item.Path == "/tmp/r5.nes");
    }

    [Fact]
    public void BuildWarmKeepAlivePaths_UsesShelfTargets_WhenShelfLayout()
    {
        var controller = new MainWindowPreviewPlaybackPolicyController();
        var roms = Enumerable.Range(1, 6)
            .Select(index => CreateRom($"R{index}", $"/tmp/r{index}.nes"))
            .ToList();

        var keepAlive = controller.BuildWarmKeepAlivePaths(
            isShelfLayoutMode: true,
            isKaleidoscopeMode: false,
            items: roms,
            visibleShelfWarmTargets: [roms[1], roms[3]],
            visibleKaleidoscopeTargets: [roms[4]],
            priorityRom: roms[5],
            maxWarmedPreviews: 3,
            pathsEqual: (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(2, keepAlive.Count);
        Assert.Contains("/tmp/r2.nes", keepAlive);
        Assert.Contains("/tmp/r4.nes", keepAlive);
    }

    [Fact]
    public void BuildWarmKeepAlivePaths_UsesKaleidoscopeTargets_WhenKaleidoscopeLayout()
    {
        var controller = new MainWindowPreviewPlaybackPolicyController();
        var roms = Enumerable.Range(1, 5)
            .Select(index => CreateRom($"R{index}", $"/tmp/r{index}.nes"))
            .ToList();

        var keepAlive = controller.BuildWarmKeepAlivePaths(
            isShelfLayoutMode: false,
            isKaleidoscopeMode: true,
            items: roms,
            visibleShelfWarmTargets: [roms[0], roms[1]],
            visibleKaleidoscopeTargets: [roms[2], roms[4]],
            priorityRom: roms[3],
            maxWarmedPreviews: 3,
            pathsEqual: (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(2, keepAlive.Count);
        Assert.Contains("/tmp/r3.nes", keepAlive);
        Assert.Contains("/tmp/r5.nes", keepAlive);
    }

    [Fact]
    public void BuildWarmKeepAlivePaths_UsesPriorityNeighborhood_WhenNonShelfAndNonKaleidoscope()
    {
        var controller = new MainWindowPreviewPlaybackPolicyController();
        var roms = Enumerable.Range(1, 7)
            .Select(index => CreateRom($"R{index}", $"/tmp/r{index}.nes"))
            .ToList();
        var priority = roms[3];

        var keepAlive = controller.BuildWarmKeepAlivePaths(
            isShelfLayoutMode: false,
            isKaleidoscopeMode: false,
            items: roms,
            visibleShelfWarmTargets: [],
            visibleKaleidoscopeTargets: [],
            priorityRom: priority,
            maxWarmedPreviews: 4,
            pathsEqual: (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(4, keepAlive.Count);
        Assert.Contains("/tmp/r4.nes", keepAlive);
        Assert.Contains("/tmp/r5.nes", keepAlive);
        Assert.Contains("/tmp/r3.nes", keepAlive);
        Assert.Contains("/tmp/r6.nes", keepAlive);
    }

    [Fact]
    public void BuildWarmTargetLimit_AndBuildWarmTargets_SelectsCandidateSubsetByPriority()
    {
        var controller = new MainWindowPreviewPlaybackPolicyController();
        var roms = Enumerable.Range(1, 6)
            .Select(index => CreateRom($"R{index}", $"/tmp/r{index}.nes"))
            .ToList();
        var keepAlive = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            roms[4].Path,
            roms[5].Path
        };
        var candidatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            roms[0].Path,
            roms[2].Path,
            roms[4].Path,
            roms[5].Path
        };
        var loadedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            roms[2].Path
        };
        var limit = controller.BuildWarmTargetLimit(
            isShelfLayoutMode: false,
            isKaleidoscopeMode: false,
            shelfVisibleRowCount: 1,
            shelfWarmExtraRows: 1,
            shelfColumns: 4,
            kaleidoscopePageSize: 8,
            maxWarmedPreviews: 3);

        var warmTargets = controller.BuildWarmTargets(
            roms,
            keepAlive,
            limit,
            hasPreviewSelector: _ => true,
            hasLoadedPreviewSelector: item => loadedPaths.Contains(item.Path),
            isPreviewCandidateSelector: item => candidatePaths.Contains(item.Path));

        Assert.Equal(3, limit);
        Assert.Equal(3, warmTargets.Count);
        Assert.Equal([roms[4].Path, roms[5].Path, roms[0].Path], warmTargets.Select(item => item.Path).ToArray());
    }

    private static RomLibraryItem CreateRom(string name, string path) =>
        new($"{name}.nes", path, "", hasPreview: true, fileSizeBytes: 1024, importedAtUtc: DateTime.UtcNow);
}

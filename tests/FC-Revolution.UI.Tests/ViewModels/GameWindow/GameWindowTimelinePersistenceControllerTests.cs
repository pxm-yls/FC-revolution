using System.IO;
using System.Linq;
using FCRevolution.FC.LegacyAdapters;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Adapters.LegacyTimeline;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class GameWindowTimelinePersistenceControllerTests
{
    [Fact]
    public void BuildPreviewNodes_ProjectsPersistedEntries_ToPreviewNodes()
    {
        var entry = new LegacyTimelinePreviewEntry(
            SnapshotId: Guid.NewGuid(),
            BranchId: Guid.NewGuid(),
            Frame: 128,
            TimestampSeconds: 3.2,
            CreatedAtUtc: DateTime.UtcNow,
            Name: "节点A",
            Snapshot: new CoreTimelineSnapshot
            {
                Frame = 128,
                TimestampSeconds = 3.2,
                Thumbnail = new uint[64 * 60],
                State = new CoreStateBlob
                {
                    Format = "test/snapshot",
                    Data = []
                }
            });

        var nodes = GameWindowTimelinePersistenceController.BuildPreviewNodes(
            [entry],
            previewWidth: 256,
            previewHeight: 240);

        var node = Assert.Single(nodes);
        Assert.Equal(entry.SnapshotId, node.Id);
        Assert.Equal(128, node.Frame);
        Assert.Equal(3.2, node.TimestampSeconds);
        Assert.Equal("节点A", node.Title);
        Assert.NotNull(node.Bitmap);
    }

    [Fact]
    public void BuildPersistPreviewNodePlan_UsesBranchSnapshot_WithoutNearestLookup()
    {
        var currentBranchId = Guid.NewGuid();
        var branchPointId = Guid.NewGuid();
        var branchSnapshot = new CoreTimelineSnapshot
        {
            Frame = 88,
            TimestampSeconds = 1.2,
            Thumbnail = new uint[64 * 60],
            State = new CoreStateBlob
            {
                Format = "test/snapshot",
                Data = []
            }
        };
        var nearestInvoked = false;

        var plan = GameWindowTimelinePersistenceController.BuildPersistPreviewNodePlan(
            currentBranchId,
            branchPointId,
            title: "手动节点",
            frame: 999,
            branchPointSnapshot: branchSnapshot,
            getNearestSnapshot: _ =>
            {
                nearestInvoked = true;
                return null;
            });

        Assert.True(plan.ShouldPersist);
        Assert.False(nearestInvoked);
        Assert.Equal(branchPointId, plan.BranchId);
        Assert.Equal("手动节点", plan.Title);
        Assert.Equal(88, plan.Snapshot!.Frame);
    }

    [Fact]
    public void BuildPersistPreviewNodePlan_FallsBackToNearestSnapshot_AndCurrentBranch()
    {
        var currentBranchId = Guid.NewGuid();
        long nearestRequestedFrame = -1;
        var nearestSnapshot = new CoreTimelineSnapshot
        {
            Frame = 320,
            TimestampSeconds = 5.4,
            Thumbnail = new uint[64 * 60],
            State = new CoreStateBlob
            {
                Format = "test/snapshot",
                Data = []
            }
        };

        var plan = GameWindowTimelinePersistenceController.BuildPersistPreviewNodePlan(
            currentBranchId,
            branchPointId: null,
            title: "fallback 节点",
            frame: 321,
            branchPointSnapshot: null,
            getNearestSnapshot: frame =>
            {
                nearestRequestedFrame = frame;
                return nearestSnapshot;
            });

        Assert.True(plan.ShouldPersist);
        Assert.Equal(321, nearestRequestedFrame);
        Assert.Equal(currentBranchId, plan.BranchId);
        Assert.Equal(320, plan.Snapshot!.Frame);
    }

    [Fact]
    public void BuildPersistPreviewNodePlan_WhenNoSnapshot_ReturnsNoOp()
    {
        var plan = GameWindowTimelinePersistenceController.BuildPersistPreviewNodePlan(
            Guid.NewGuid(),
            branchPointId: null,
            title: "empty",
            frame: 12,
            branchPointSnapshot: null,
            getNearestSnapshot: _ => null);

        Assert.False(plan.ShouldPersist);
        Assert.Null(plan.Snapshot);
    }

    [Fact]
    public void ReadManifestWriteTimeUtc_WhenManifestMissing_ReturnsDateTimeMinValue()
    {
        var romId = $"test-rom-{Guid.NewGuid():N}";
        var manifestPath = Path.Combine(LegacyTimelineStorageAdapter.GetRomDirectory(romId), "manifest.json");
        Assert.False(File.Exists(manifestPath));

        var writeTime = GameWindowTimelinePersistenceController.ReadManifestWriteTimeUtc(romId);

        Assert.Equal(DateTime.MinValue, writeTime);
    }

    [Fact]
    public void TryPersistPreviewNode_SavesPreviewNode_AndReturnsProjectedNode()
    {
        var repository = new LegacyTimelineRepositoryAdapter();
        var romId = $"ui-preview-{Guid.NewGuid():N}";

        try
        {
            var manifest = repository.LoadTimelineState(new CoreBranchTree(), romId, "Preview Persist Rom", "/tmp/preview-persist.nes").Manifest;
            var snapshot = CreateCoreTimelineSnapshot(frame: 180);
            var node = CreateBranchCanvasNode(
                "Boss Preview",
                snapshot.Frame,
                new CoreBranchPoint
                {
                    Id = Guid.NewGuid(),
                    Name = "Boss Branch",
                    RomPath = "/tmp/boss.nes",
                    Frame = snapshot.Frame,
                    TimestampSeconds = snapshot.TimestampSeconds,
                    Snapshot = snapshot,
                    CreatedAt = DateTime.UtcNow
                });

            var result = GameWindowTimelinePersistenceController.TryPersistPreviewNode(
                repository,
                manifest,
                romId,
                manifest.CurrentBranchId,
                node,
                _ => null,
                previewWidth: 256,
                previewHeight: 240);

            Assert.NotNull(result);
            Assert.Equal("Boss Preview", result.Value.PreviewNode.Title);
            Assert.Equal(180, result.Value.PreviewNode.Frame);
            Assert.True(result.Value.ManifestWriteTimeUtc > DateTime.MinValue);

            var loaded = repository.LoadPreviewNodes(manifest);
            var restored = Assert.Single(loaded);
            Assert.Equal("Boss Preview", restored.Name);
            Assert.Equal(180, restored.Snapshot.Frame);
        }
        finally
        {
            DeleteRomDirectory(romId);
        }
    }

    [Fact]
    public void LoadTimelineState_LoadsManifestBranchTree_AndPreviewNodes()
    {
        var repository = new LegacyTimelineRepositoryAdapter();
        var romId = $"ui-reload-{Guid.NewGuid():N}";
        var romPath = "/tmp/reload-test-rom.nes";

        try
        {
            var manifest = repository.LoadTimelineState(new CoreBranchTree(), romId, "Reload Rom", romPath).Manifest;
            var branchPoint = new CoreBranchPoint
            {
                Id = Guid.NewGuid(),
                Name = "Reload Branch",
                RomPath = romPath,
                Frame = 240,
                TimestampSeconds = 4.0,
                Snapshot = CreateCoreTimelineSnapshot(240),
                CreatedAt = DateTime.UtcNow
            };
            repository.PersistBranchPoint(manifest, romId, branchPoint, parentBranchId: null, romPath);
            repository.SavePreviewNode(
                manifest,
                romId,
                branchPoint.Id,
                Guid.NewGuid(),
                "Reload Preview",
                CreateCoreTimelineSnapshot(245));

            var tree = new CoreBranchTree();
            var loadState = GameWindowTimelinePersistenceController.LoadTimelineState(
                repository,
                tree,
                romId,
                "Reload Rom",
                previewWidth: 256,
                previewHeight: 240,
                romPath);

            Assert.Equal(manifest.CurrentBranchId, loadState.CurrentBranchId);
            Assert.True(loadState.ManifestWriteTimeUtc > DateTime.MinValue);

            var restoredRoot = Assert.Single(tree.Roots);
            Assert.Equal("Reload Branch", restoredRoot.Name);

            var previewNode = Assert.Single(loadState.PreviewNodes);
            Assert.Equal("Reload Preview", previewNode.Title);
            Assert.Equal(245, previewNode.Frame);
        }
        finally
        {
            DeleteRomDirectory(romId);
        }
    }

    [Fact]
    public void TryReloadTimelineState_WhenManifestWriteTimeIsNotNewer_ReturnsNull()
    {
        var repository = new LegacyTimelineRepositoryAdapter();
        var romId = $"ui-nosync-{Guid.NewGuid():N}";
        var romPath = "/tmp/no-sync-rom.nes";

        try
        {
            _ = repository.LoadTimelineState(new CoreBranchTree(), romId, "No Sync Rom", romPath);
            var knownWriteTimeUtc = GameWindowTimelinePersistenceController.ReadManifestWriteTimeUtc(romId);

            var tree = new CoreBranchTree();
            var reloadState = GameWindowTimelinePersistenceController.TryReloadTimelineState(
                repository,
                tree,
                knownWriteTimeUtc,
                romId,
                "No Sync Rom",
                romPath,
                previewWidth: 256,
                previewHeight: 240);

            Assert.Null(reloadState);
            Assert.Empty(tree.Roots);
        }
        finally
        {
            DeleteRomDirectory(romId);
        }
    }

    [Fact]
    public void TryReloadTimelineState_WhenManifestWriteTimeIsNewer_ReloadsTimelineState()
    {
        var repository = new LegacyTimelineRepositoryAdapter();
        var romId = $"ui-resync-{Guid.NewGuid():N}";
        var romPath = "/tmp/resync-test-rom.nes";

        try
        {
            var manifest = repository.LoadTimelineState(new CoreBranchTree(), romId, "Resync Rom", romPath).Manifest;
            var knownWriteTimeUtc = GameWindowTimelinePersistenceController.ReadManifestWriteTimeUtc(romId);
            var branchPoint = new CoreBranchPoint
            {
                Id = Guid.NewGuid(),
                Name = "Resync Branch",
                RomPath = romPath,
                Frame = 300,
                TimestampSeconds = 5.0,
                Snapshot = CreateCoreTimelineSnapshot(300),
                CreatedAt = DateTime.UtcNow
            };
            repository.PersistBranchPoint(manifest, romId, branchPoint, parentBranchId: null, romPath);
            repository.SavePreviewNode(
                manifest,
                romId,
                branchPoint.Id,
                Guid.NewGuid(),
                "Resync Preview",
                CreateCoreTimelineSnapshot(305));

            var tree = new CoreBranchTree();
            var reloadState = GameWindowTimelinePersistenceController.TryReloadTimelineState(
                repository,
                tree,
                knownWriteTimeUtc,
                romId,
                "Resync Rom",
                romPath,
                previewWidth: 256,
                previewHeight: 240);

            Assert.NotNull(reloadState);
            Assert.True(reloadState.Value.ManifestWriteTimeUtc >= knownWriteTimeUtc);
            Assert.Equal("Resync Branch", Assert.Single(tree.Roots).Name);
            Assert.Equal("Resync Preview", Assert.Single(reloadState.Value.PreviewNodes).Title);
        }
        finally
        {
            DeleteRomDirectory(romId);
        }
    }

    private static BranchCanvasNode CreateBranchCanvasNode(string title, long frame, CoreBranchPoint? branchPoint) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
            Subtitle = $"帧 {frame}",
            Frame = frame,
            CreatedAt = DateTime.UtcNow,
            X = 0,
            Y = 0,
            Width = 152,
            Height = 130,
            Bitmap = null,
            IsBranchNode = branchPoint != null,
            IsMainlineNode = branchPoint == null,
            BackgroundHex = "#000000",
            BorderBrushHex = "#FFFFFF",
            BorderThicknessValue = 1,
            BranchPoint = branchPoint
        };

    private static CoreTimelineSnapshot CreateCoreTimelineSnapshot(long frame) =>
        new()
        {
            Frame = frame,
            TimestampSeconds = frame / 60.0,
            Thumbnail = Enumerable.Repeat((uint)frame, 64 * 60).ToArray(),
            State = new CoreStateBlob
            {
                Format = "test/snapshot",
                Data = []
            }
        };

    private static void DeleteRomDirectory(string romId)
    {
        var romDirectory = LegacyTimelineStorageAdapter.GetRomDirectory(romId);
        if (Directory.Exists(romDirectory))
            Directory.Delete(romDirectory, recursive: true);
    }
}

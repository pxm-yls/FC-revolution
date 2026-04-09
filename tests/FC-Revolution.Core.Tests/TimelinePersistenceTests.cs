using FCRevolution.Core.State;
using FCRevolution.Core.Timeline;
using FCRevolution.Core.Timeline.Persistence;

namespace FC_Revolution.Core.Tests;

public class StateSnapshotSerializerTests
{
    [Fact]
    public void Serialize_ProducesStableBinaryLayout_ForKnownSnapshot()
    {
        var snapshot = new StateSnapshotData
        {
            Frame = 0x0102030405060708,
            Timestamp = 123.5,
            CpuState = [0x11, 0x12],
            PpuState = [0x21],
            RamState = [0x31, 0x32, 0x33],
            CartState = [],
            ApuState = [0x41],
            Thumbnail = [0x01020304u, 0xA0B0C0D0u],
        };

        var bytes = StateSnapshotSerializer.Serialize(snapshot, includeThumbnail: true);

        var expected = new List<byte>();
        expected.AddRange("FCRS"u8.ToArray());
        expected.Add(0x01); // version
        expected.Add(0x01); // thumbnail flag
        expected.AddRange(BitConverter.GetBytes(0x0102030405060708L));
        expected.AddRange(BitConverter.GetBytes(123.5));
        expected.AddRange(BitConverter.GetBytes(2)); expected.AddRange([0x11, 0x12]);      // CPU
        expected.AddRange(BitConverter.GetBytes(1)); expected.Add(0x21);                    // PPU
        expected.AddRange(BitConverter.GetBytes(3)); expected.AddRange([0x31, 0x32, 0x33]); // RAM
        expected.AddRange(BitConverter.GetBytes(0));                                         // CART
        expected.AddRange(BitConverter.GetBytes(1)); expected.Add(0x41);                    // APU
        expected.AddRange(BitConverter.GetBytes(2));                                         // thumbnail length
        expected.AddRange(BitConverter.GetBytes(0x01020304u));
        expected.AddRange(BitConverter.GetBytes(0xA0B0C0D0u));

        Assert.Equal(expected.ToArray(), bytes);
    }

    [Fact]
    public void SerializeDeserialize_RoundTripsSnapshot_WithThumbnail()
    {
        var snapshot = new StateSnapshotData
        {
            Frame = 123,
            Timestamp = 12.5,
            CpuState = [1, 2, 3],
            PpuState = [4, 5],
            RamState = [6, 7, 8, 9],
            CartState = [10, 11],
            ApuState = [12, 13, 14],
            Thumbnail = Enumerable.Range(0, 64 * 60).Select(i => (uint)i).ToArray(),
        };

        var bytes = StateSnapshotSerializer.Serialize(snapshot, includeThumbnail: true);
        var restored = StateSnapshotSerializer.Deserialize(bytes);

        Assert.True(StateSnapshotSerializer.HasHeader(bytes));
        Assert.Equal(snapshot.Frame, restored.Frame);
        Assert.Equal(snapshot.Timestamp, restored.Timestamp);
        Assert.Equal(snapshot.CpuState, restored.CpuState);
        Assert.Equal(snapshot.PpuState, restored.PpuState);
        Assert.Equal(snapshot.RamState, restored.RamState);
        Assert.Equal(snapshot.CartState, restored.CartState);
        Assert.Equal(snapshot.ApuState, restored.ApuState);
        Assert.Equal(snapshot.Thumbnail, restored.Thumbnail);
    }

    [Fact]
    public void SerializeDeserialize_RoundTripsSnapshot_WithoutThumbnail()
    {
        var snapshot = new StateSnapshotData
        {
            Frame = 7,
            Timestamp = 1.5,
            CpuState = [1],
            PpuState = [2],
            RamState = [3],
            CartState = [],
            ApuState = [4],
        };

        var bytes = StateSnapshotSerializer.Serialize(snapshot, includeThumbnail: false);
        var restored = StateSnapshotSerializer.Deserialize(bytes);

        Assert.Null(restored.Thumbnail);
        Assert.Equal(snapshot.Frame, restored.Frame);
        Assert.Equal(snapshot.ApuState, restored.ApuState);
    }
}

public class TimelineRepositoryTests
{
    [Fact]
    public void LoadOrCreate_CreatesMainBranchManifest()
    {
        var repository = new TimelineRepository();
        var romId = $"test-{Guid.NewGuid():N}";

        try
        {
            var manifest = repository.LoadOrCreate(romId, "Test Rom");
            var mainBranchId = TimelineStoragePaths.GetStableMainBranchId(romId);

            Assert.Equal(romId, manifest.RomId);
            Assert.Equal(mainBranchId, manifest.CurrentBranchId);
            Assert.Contains(manifest.Branches, branch => branch.BranchId == mainBranchId && branch.IsMainBranch);
            Assert.True(File.Exists(TimelineStoragePaths.GetManifestPath(romId)));
        }
        finally
        {
            var romDirectory = TimelineStoragePaths.GetRomDirectory(romId);
            if (Directory.Exists(romDirectory))
                Directory.Delete(romDirectory, recursive: true);
        }
    }

    [Fact]
    public void UpsertQuickSaveSnapshot_UpdatesBranchPointers()
    {
        var repository = new TimelineRepository();
        var romId = $"test-{Guid.NewGuid():N}";

        try
        {
            var manifest = repository.LoadOrCreate(romId, "QuickSave Rom");
            var branchId = manifest.CurrentBranchId;

            var record = repository.UpsertQuickSaveSnapshot(manifest, branchId, frame: 240, timestampSeconds: 4.0);
            repository.Save(manifest);

            var branch = Assert.Single(manifest.Branches, item => item.BranchId == branchId);
            Assert.Equal(record.SnapshotId, branch.QuickSaveSnapshotId);
            Assert.Equal(record.SnapshotId, branch.HeadSnapshotId);
            Assert.Equal(240, record.Frame);
            Assert.Equal(TimelineStoragePaths.GetQuickSavePath(romId, branchId), record.StateFile);
        }
        finally
        {
            var romDirectory = TimelineStoragePaths.GetRomDirectory(romId);
            if (Directory.Exists(romDirectory))
                Directory.Delete(romDirectory, recursive: true);
        }
    }

    [Fact]
    public void SaveBranchPoint_AndPopulateBranchTree_RestoresPersistedBranches()
    {
        var repository = new TimelineRepository();
        var romId = $"test-{Guid.NewGuid():N}";
        var romPath = "/tmp/test-rom.nes";

        try
        {
            var manifest = repository.LoadOrCreate(romId, "Branch Rom");
            var mainBranchId = manifest.CurrentBranchId;

            var rootBranch = new BranchPoint
            {
                Id = Guid.NewGuid(),
                Name = "Root Branch",
                RomPath = romPath,
                Frame = 120,
                Timestamp = 2.0,
                Snapshot = MakeFrameSnapshot(120),
                CreatedAt = DateTime.UtcNow,
            };
            repository.SaveBranchPoint(manifest, romId, rootBranch, parentBranchId: null);

            var childBranch = new BranchPoint
            {
                Id = Guid.NewGuid(),
                Name = "Child Branch",
                RomPath = romPath,
                Frame = 180,
                Timestamp = 3.0,
                Snapshot = MakeFrameSnapshot(180),
                CreatedAt = DateTime.UtcNow,
            };
            repository.SaveBranchPoint(manifest, romId, childBranch, parentBranchId: rootBranch.Id);

            var tree = new BranchTree();
            repository.PopulateBranchTree(tree, manifest, romId, romPath);

            var restoredRoot = Assert.Single(tree.Roots);
            Assert.Equal(rootBranch.Name, restoredRoot.Name);
            Assert.Equal(rootBranch.Frame, restoredRoot.Frame);
            Assert.Single(restoredRoot.Children);
            Assert.Equal(childBranch.Id, restoredRoot.Children[0].Id);
            Assert.DoesNotContain(manifest.Branches, branch => branch.BranchId == mainBranchId && !branch.IsMainBranch);
        }
        finally
        {
            var romDirectory = TimelineStoragePaths.GetRomDirectory(romId);
            if (Directory.Exists(romDirectory))
                Directory.Delete(romDirectory, recursive: true);
        }
    }

    [Fact]
    public void DeleteBranch_RemovesChildBranches_AndFiles()
    {
        var repository = new TimelineRepository();
        var romId = $"test-{Guid.NewGuid():N}";

        try
        {
            var manifest = repository.LoadOrCreate(romId, "Delete Rom");
            var rootBranch = new BranchPoint
            {
                Id = Guid.NewGuid(),
                Name = "Root",
                Frame = 90,
                Timestamp = 1.5,
                Snapshot = MakeFrameSnapshot(90),
                CreatedAt = DateTime.UtcNow,
            };
            var childBranch = new BranchPoint
            {
                Id = Guid.NewGuid(),
                Name = "Child",
                Frame = 120,
                Timestamp = 2.0,
                Snapshot = MakeFrameSnapshot(120),
                CreatedAt = DateTime.UtcNow,
            };

            repository.SaveBranchPoint(manifest, romId, rootBranch, null);
            repository.SaveBranchPoint(manifest, romId, childBranch, rootBranch.Id);

            var rootDirectory = TimelineStoragePaths.GetBranchDirectory(romId, rootBranch.Id);
            var childDirectory = TimelineStoragePaths.GetBranchDirectory(romId, childBranch.Id);
            Assert.True(Directory.Exists(rootDirectory));
            Assert.True(Directory.Exists(childDirectory));

            repository.DeleteBranch(manifest, romId, rootBranch.Id);

            Assert.DoesNotContain(manifest.Branches, branch => branch.BranchId == rootBranch.Id || branch.BranchId == childBranch.Id);
            Assert.False(Directory.Exists(rootDirectory));
            Assert.False(Directory.Exists(childDirectory));
        }
        finally
        {
            var romDirectory = TimelineStoragePaths.GetRomDirectory(romId);
            if (Directory.Exists(romDirectory))
                Directory.Delete(romDirectory, recursive: true);
        }
    }

    [Fact]
    public void SavePreviewNode_LoadPreviewNodes_AndDeletePreviewNode_RoundTripsPersistedPreviewMarkers()
    {
        var repository = new TimelineRepository();
        var romId = $"test-{Guid.NewGuid():N}";

        try
        {
            var manifest = repository.LoadOrCreate(romId, "Preview Node Rom");
            var branchId = manifest.CurrentBranchId;
            var previewNodeId = Guid.NewGuid();
            var snapshot = MakeFrameSnapshot(300);

            var record = repository.SavePreviewNode(
                manifest,
                romId,
                branchId,
                previewNodeId,
                "Boss Preview",
                snapshot);

            Assert.Equal("PreviewNode", record.Kind);
            Assert.True(File.Exists(TimelineStoragePaths.GetPreviewNodeSnapshotPath(romId, previewNodeId)));

            var loaded = repository.LoadPreviewNodes(manifest);
            var restored = Assert.Single(loaded);
            Assert.Equal(previewNodeId, restored.Record.SnapshotId);
            Assert.Equal(snapshot.Frame, restored.Snapshot.Frame);
            Assert.Equal(snapshot.Thumbnail, restored.Snapshot.Thumbnail);

            repository.DeletePreviewNode(manifest, romId, previewNodeId);

            Assert.DoesNotContain(manifest.Snapshots, item => item.SnapshotId == previewNodeId);
            Assert.False(File.Exists(TimelineStoragePaths.GetPreviewNodeSnapshotPath(romId, previewNodeId)));
        }
        finally
        {
            var romDirectory = TimelineStoragePaths.GetRomDirectory(romId);
            if (Directory.Exists(romDirectory))
                Directory.Delete(romDirectory, recursive: true);
        }
    }

    [Fact]
    public void RenamePreviewNode_UpdatesPersistedPreviewTitle()
    {
        var repository = new TimelineRepository();
        var romId = $"test-rename-{Guid.NewGuid():N}";

        try
        {
            var manifest = repository.LoadOrCreate(romId, "Preview Rename Rom");
            var branchId = manifest.CurrentBranchId;
            var previewNodeId = Guid.NewGuid();

            repository.SavePreviewNode(
                manifest,
                romId,
                branchId,
                previewNodeId,
                "旧标题",
                MakeFrameSnapshot(420));

            repository.RenamePreviewNode(manifest, previewNodeId, "Boss Rush Checkpoint");

            var reloaded = repository.LoadOrCreate(romId, "Preview Rename Rom");
            var record = Assert.Single(reloaded.Snapshots, item => item.Kind == "PreviewNode");
            Assert.Equal("Boss Rush Checkpoint", record.Name);
        }
        finally
        {
            var romDirectory = TimelineStoragePaths.GetRomDirectory(romId);
            if (Directory.Exists(romDirectory))
                Directory.Delete(romDirectory, recursive: true);
        }
    }

    private static FrameSnapshot MakeFrameSnapshot(long frame) => new()
    {
        Frame = frame,
        Timestamp = frame / 60.0,
        CpuState = [(byte)(frame & 0xFF), 1, 2, 3],
        PpuState = CreatePpuState(),
        RamState = [7, 8, 9, 10],
        CartState = [],
        ApuState = [11, 12],
        Thumbnail = Enumerable.Repeat((uint)frame, 64 * 60).ToArray(),
    };

    private static byte[] CreatePpuState()
    {
        const int frameBufferOffset = 2362;
        var state = new byte[frameBufferOffset + (256 * 240 * sizeof(uint))];
        for (var i = 0; i < 256 * 240; i++)
            BitConverter.GetBytes((uint)(0xFF000000 | (uint)i)).CopyTo(state, frameBufferOffset + i * sizeof(uint));

        return state;
    }
}

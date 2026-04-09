using FCRevolution.Core.Timeline;

namespace FC_Revolution.Core.Tests;

public class TimelineCacheTests
{
    private static FrameSnapshot MakeSnap(long frame) => new()
    {
        Frame     = frame,
        Timestamp = frame / 60.0,
        CpuState  = new byte[] { (byte)(frame & 0xFF), 0, 0, 0 },
        PpuState  = new byte[10],
        RamState  = new byte[8],
        CartState = Array.Empty<byte>(),
        Thumbnail = new uint[64 * 60],
    };

    [Fact]
    public void Push_StoresSnapshotInHot()
    {
        var cache = new TimelineCache(hotCapacity: 10);
        cache.Push(MakeSnap(1));
        Assert.Equal(1, cache.HotCount);
    }

    [Fact]
    public void Push_EvictsToWarmWhenHotFull()
    {
        var cache = new TimelineCache(hotCapacity: 3, warmCapacity: 100);
        cache.Push(MakeSnap(1));
        cache.Push(MakeSnap(2));
        cache.Push(MakeSnap(3));
        Assert.Equal(3, cache.HotCount);
        Assert.Equal(0, cache.WarmCount);

        cache.Push(MakeSnap(4)); // triggers eviction
        Assert.Equal(3, cache.HotCount);
        Assert.Equal(1, cache.WarmCount);
    }

    [Fact]
    public void GetNearest_ReturnsCorrectHotSnapshot()
    {
        var cache = new TimelineCache();
        cache.Push(MakeSnap(10));
        cache.Push(MakeSnap(20));
        cache.Push(MakeSnap(30));

        var s = cache.GetNearest(25);
        Assert.NotNull(s);
        Assert.Equal(20, s!.Frame);
    }

    [Fact]
    public void GetNearest_ReturnsNullWhenEmpty()
    {
        var cache = new TimelineCache();
        Assert.Null(cache.GetNearest(100));
    }

    [Fact]
    public void GetNearest_FindsWarmSnapshot()
    {
        var cache = new TimelineCache(hotCapacity: 2, warmCapacity: 100);
        cache.Push(MakeSnap(1));
        cache.Push(MakeSnap(2));
        cache.Push(MakeSnap(3)); // evicts frame 1 to warm
        cache.Push(MakeSnap(4)); // evicts frame 2 to warm

        // Hot has 3,4; Warm has 1,2
        var s = cache.GetNearest(2);
        Assert.NotNull(s);
        Assert.Equal(2, s!.Frame);
    }

    [Fact]
    public void WarmCapacity_TrimPreservesNearestAndThumbnailOrdering()
    {
        var cache = new TimelineCache(hotCapacity: 1, warmCapacity: 3);
        for (int frame = 1; frame <= 6; frame++)
            cache.Push(MakeSnap(frame));

        Assert.Equal(1, cache.HotCount);
        Assert.Equal(3, cache.WarmCount);

        // Hot has 6; warm keeps latest 3 evicted frames: 3,4,5.
        Assert.Equal(5, cache.GetNearest(5)!.Frame);
        Assert.Equal(3, cache.GetNearest(3)!.Frame);
        Assert.Null(cache.GetNearest(2));

        var thumbFrames = cache.GetThumbnails().Select(item => item.frame).ToList();
        Assert.Equal(new long[] { 6, 5, 4, 3 }, thumbFrames);
    }

    [Fact]
    public void WarmCache_PreservesApuState_ThroughLz4Roundtrip()
    {
        var apuPayload = new byte[] { 0xAB, 0xCD, 0xEF, 0x01, 0x23 };
        var snap = new FrameSnapshot
        {
            Frame     = 1,
            Timestamp = 1 / 60.0,
            CpuState  = new byte[17],
            PpuState  = new byte[10],
            RamState  = new byte[8],
            CartState = Array.Empty<byte>(),
            ApuState  = apuPayload,
            Thumbnail = new uint[64 * 60],
        };

        // hot=1 so pushing a second snap evicts frame 1 to warm
        var cache = new TimelineCache(hotCapacity: 1, warmCapacity: 100);
        cache.Push(snap);
        cache.Push(MakeSnap(2)); // evicts frame 1 to warm

        var restored = cache.GetNearest(1);
        Assert.NotNull(restored);
        Assert.Equal(apuPayload, restored!.ApuState);
    }

    [Fact]
    public void WarmCache_RepeatedRestore_RemainsStable()
    {
        var cache = new TimelineCache(hotCapacity: 1, warmCapacity: 100);
        cache.Push(MakeSnap(1));
        cache.Push(MakeSnap(2)); // evict frame 1 to warm

        for (var i = 0; i < 50; i++)
        {
            var restored = cache.GetNearest(1);
            Assert.NotNull(restored);
            Assert.Equal(1, restored!.Frame);
            Assert.Equal(1 / 60.0, restored.Timestamp);
        }
    }

    [Fact]
    public void Clear_EmptiesBothLayers()
    {
        var cache = new TimelineCache(hotCapacity: 2, warmCapacity: 100);
        cache.Push(MakeSnap(1));
        cache.Push(MakeSnap(2));
        cache.Push(MakeSnap(3));
        cache.Clear();
        Assert.Equal(0, cache.HotCount);
        Assert.Equal(0, cache.WarmCount);
    }

    [Fact]
    public void GetThumbnails_ReturnsMostRecentFirst()
    {
        var cache = new TimelineCache(hotCapacity: 10);
        cache.Push(MakeSnap(1));
        cache.Push(MakeSnap(5));
        cache.Push(MakeSnap(3));

        var thumbs = cache.GetThumbnails().ToList();
        Assert.Equal(3, thumbs.Count);
        Assert.Equal(3, thumbs[0].frame); // most recent pushed
    }

    [Fact]
    public void Thumbnail_Downsamples256x240To64x60()
    {
        var fb = new uint[256 * 240];
        for (int i = 0; i < fb.Length; i++) fb[i] = (uint)i;

        var thumb = FrameSnapshot.MakeThumbnail(fb);
        Assert.Equal(64 * 60, thumb.Length);
        Assert.Equal(fb[0], thumb[0]); // top-left matches
    }
}

public class BranchTreeTests
{
    [Fact]
    public void AddRoot_AddsToRoots()
    {
        var tree = new BranchTree();
        var bp = new BranchPoint { Name = "Root", Frame = 0, Snapshot = null! };
        tree.AddRoot(bp);
        Assert.Single(tree.Roots);
    }

    [Fact]
    public void Fork_AddsChildToParent()
    {
        var tree = new BranchTree();
        var root = tree.AddRoot(new BranchPoint { Name = "Root", Frame = 0, Snapshot = null! });
        var child = tree.Fork(root.Id, new BranchPoint { Name = "Fork1", Frame = 10, Snapshot = null! });

        Assert.Single(root.Children);
        Assert.Equal(child.Id, root.Children[0].Id);
    }

    [Fact]
    public void Fork_WithUnknownParent_AddsAsRoot()
    {
        var tree = new BranchTree();
        tree.Fork(Guid.NewGuid(), new BranchPoint { Name = "Orphan", Frame = 5, Snapshot = null! });
        Assert.Single(tree.Roots);
    }

    [Fact]
    public void Remove_DeletesBranch()
    {
        var tree = new BranchTree();
        var bp = tree.AddRoot(new BranchPoint { Name = "ToDelete", Frame = 0, Snapshot = null! });
        tree.Remove(bp.Id);
        Assert.Empty(tree.Roots);
        Assert.Null(tree.Find(bp.Id));
    }

    [Fact]
    public void AllBranches_ReturnsAllNodes()
    {
        var tree = new BranchTree();
        var root = tree.AddRoot(new BranchPoint { Name = "Root", Frame = 0, Snapshot = null! });
        tree.Fork(root.Id, new BranchPoint { Name = "Child", Frame = 10, Snapshot = null! });
        Assert.Equal(2, tree.AllBranches().Count());
    }
}

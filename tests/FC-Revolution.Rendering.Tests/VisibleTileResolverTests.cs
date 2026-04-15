using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Common;

namespace FC_Revolution.Rendering.Tests;

public sealed class VisibleTileResolverTests
{
    [Fact]
    public void Resolve_HorizontalScroll32_StartsAtExpectedTile()
    {
        byte[] backgroundPlanes = CombinePages(
            BuildPage((x, y) => (byte)((y * 32) + x)),
            BuildPage((x, y) => (byte)(200 + x)));

        List<VisibleTile> tiles = VisibleTileResolver.Resolve(
            backgroundPlanes,
            fineScrollX: 0,
            fineScrollY: 0,
            coarseScrollX: 4,
            coarseScrollY: 0,
            backgroundPlaneSelect: 0,
            backgroundPlaneLayout: BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth: 256,
            screenHeight: 240);

        VisibleTile topLeft = FindTile(tiles, 0, 0);

        Assert.Equal((byte)4, topLeft.TileId);
        Assert.Equal((byte)0, topLeft.PaletteId);
        Assert.Equal(0, topLeft.LogicalPlaneIndex);
        Assert.Equal(0, topLeft.PhysicalPlaneIndex);
    }

    [Fact]
    public void Resolve_FineScroll_KeepsLeadingPartialTile()
    {
        byte[] backgroundPlanes = CombinePages(
            BuildPage((x, y) => (byte)x),
            BuildPage((x, y) => (byte)(100 + x)));

        List<VisibleTile> tiles = VisibleTileResolver.Resolve(
            backgroundPlanes,
            fineScrollX: 3,
            fineScrollY: 0,
            coarseScrollX: 1,
            coarseScrollY: 0,
            backgroundPlaneSelect: 0,
            backgroundPlaneLayout: BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth: 16,
            screenHeight: 8);

        Assert.Equal(3, tiles.Count);
        Assert.Equal((-3, (byte)1), (tiles[0].ScreenX, tiles[0].TileId));
        Assert.Equal((5, (byte)2), (tiles[1].ScreenX, tiles[1].TileId));
        Assert.Equal((13, (byte)3), (tiles[2].ScreenX, tiles[2].TileId));
    }

    [Fact]
    public void Resolve_CrossesIntoAdjacentNametable_WhenScrollWraps()
    {
        byte[] backgroundPlanes = CombinePages(
            BuildPage((x, y) => (byte)x),
            BuildPage((x, y) => (byte)(200 + x)));

        List<VisibleTile> tiles = VisibleTileResolver.Resolve(
            backgroundPlanes,
            fineScrollX: 0,
            fineScrollY: 0,
            coarseScrollX: 31,
            coarseScrollY: 0,
            backgroundPlaneSelect: 0,
            backgroundPlaneLayout: BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth: 16,
            screenHeight: 8);

        Assert.Equal(2, tiles.Count);

        Assert.Equal((0, (byte)31, 0, 0), (tiles[0].ScreenX, tiles[0].TileId, tiles[0].LogicalPlaneIndex, tiles[0].PhysicalPlaneIndex));
        Assert.Equal((8, (byte)200, 1, 1), (tiles[1].ScreenX, tiles[1].TileId, tiles[1].LogicalPlaneIndex, tiles[1].PhysicalPlaneIndex));
    }

    [Theory]
    [InlineData(BackgroundPlaneLayoutMode.SharedTopBottom, 1, 11)]
    [InlineData(BackgroundPlaneLayoutMode.SharedTopBottom, 2, 22)]
    [InlineData(BackgroundPlaneLayoutMode.SharedLeftRight, 2, 11)]
    [InlineData(BackgroundPlaneLayoutMode.SharedLeftRight, 3, 22)]
    [InlineData(BackgroundPlaneLayoutMode.SinglePlane0, 3, 11)]
    [InlineData(BackgroundPlaneLayoutMode.SinglePlane1, 0, 22)]
    public void Resolve_MapsLogicalNametables_AccordingToMirroring(
        BackgroundPlaneLayoutMode backgroundPlaneLayout,
        int backgroundPlaneSelect,
        byte expectedTileId)
    {
        byte[] backgroundPlanes = CombinePages(
            BuildPage((x, y) => 11),
            BuildPage((x, y) => 22));

        List<VisibleTile> tiles = VisibleTileResolver.Resolve(
            backgroundPlanes,
            fineScrollX: 0,
            fineScrollY: 0,
            coarseScrollX: 0,
            coarseScrollY: 0,
            backgroundPlaneSelect: backgroundPlaneSelect,
            backgroundPlaneLayout: backgroundPlaneLayout,
            screenWidth: 8,
            screenHeight: 8);

        Assert.Single(tiles);
        Assert.Equal(expectedTileId, tiles[0].TileId);
    }

    [Fact]
    public void Resolve_ReadsBackgroundPalette_FromAttributeQuadrants()
    {
        byte[] firstPage = BuildPage((x, y) => (byte)((y * 32) + x));
        firstPage[0x03C0] = 0b_11_10_01_00;

        byte[] backgroundPlanes = CombinePages(firstPage, BuildPage((x, y) => 0));

        List<VisibleTile> tiles = VisibleTileResolver.Resolve(
            backgroundPlanes,
            fineScrollX: 0,
            fineScrollY: 0,
            coarseScrollX: 0,
            coarseScrollY: 0,
            backgroundPlaneSelect: 0,
            backgroundPlaneLayout: BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth: 32,
            screenHeight: 32);

        Assert.Equal((byte)0, FindTile(tiles, 0, 0).PaletteId);
        Assert.Equal((byte)1, FindTile(tiles, 16, 0).PaletteId);
        Assert.Equal((byte)2, FindTile(tiles, 0, 16).PaletteId);
        Assert.Equal((byte)3, FindTile(tiles, 16, 16).PaletteId);
    }

    [Fact]
    public void ResolveInto_AndResolveArray_MatchResolveOutput()
    {
        byte[] backgroundPlanes = CombinePages(
            BuildPage((x, y) => (byte)((y * 32) + x)),
            BuildPage((x, y) => (byte)(100 + x)));

        List<VisibleTile> expected = VisibleTileResolver.Resolve(
            backgroundPlanes,
            fineScrollX: 3,
            fineScrollY: 2,
            coarseScrollX: 5,
            coarseScrollY: 4,
            backgroundPlaneSelect: 0,
            backgroundPlaneLayout: BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth: 24,
            screenHeight: 16,
            screenOffsetY: 7,
            clipTop: 7,
            clipBottom: 23,
            useUpperBackgroundTileBank: true);

        VisibleTile[] resolvedArray = VisibleTileResolver.ResolveArray(
            backgroundPlanes,
            fineScrollX: 3,
            fineScrollY: 2,
            coarseScrollX: 5,
            coarseScrollY: 4,
            backgroundPlaneSelect: 0,
            backgroundPlaneLayout: BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth: 24,
            screenHeight: 16,
            screenOffsetY: 7,
            clipTop: 7,
            clipBottom: 23,
            useUpperBackgroundTileBank: true);

        var destination =
            new List<VisibleTile> { new VisibleTile { ScreenX = -999, ScreenY = -999, TileId = 0xEE, PaletteId = 3 } };
        VisibleTileResolver.ResolveInto(
            destination,
            backgroundPlanes,
            fineScrollX: 3,
            fineScrollY: 2,
            coarseScrollX: 5,
            coarseScrollY: 4,
            backgroundPlaneSelect: 0,
            backgroundPlaneLayout: BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth: 24,
            screenHeight: 16,
            screenOffsetY: 7,
            clipTop: 7,
            clipBottom: 23,
            useUpperBackgroundTileBank: true);

        Assert.Equal(expected.Count, resolvedArray.Length);
        Assert.Equal(expected.Count + 1, destination.Count);

        for (int i = 0; i < expected.Count; i++)
        {
            AssertEqualTile(expected[i], resolvedArray[i]);
            AssertEqualTile(expected[i], destination[i + 1]);
        }
    }

    [Fact]
    public void GetVisibleTileCount_AndArrayResolveInto_SupportContiguousStripWrites()
    {
        byte[] backgroundPlanes = CombinePages(
            BuildPage((x, y) => (byte)((y * 32) + x)),
            BuildPage((x, y) => (byte)(100 + x)));

        const int screenWidth = 24;
        const int firstStripHeight = 8;
        const int secondStripHeight = 16;

        int firstCount = VisibleTileResolver.GetVisibleTileCount(
            backgroundPlanes,
            fineScrollX: 1,
            fineScrollY: 0,
            coarseScrollX: 2,
            coarseScrollY: 3,
            backgroundPlaneSelect: 0,
            backgroundPlaneLayout: BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth: screenWidth,
            screenHeight: firstStripHeight);

        int secondCount = VisibleTileResolver.GetVisibleTileCount(
            backgroundPlanes,
            fineScrollX: 5,
            fineScrollY: 0,
            coarseScrollX: 2,
            coarseScrollY: 3,
            backgroundPlaneSelect: 0,
            backgroundPlaneLayout: BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth: screenWidth,
            screenHeight: secondStripHeight);

        var destination = new VisibleTile[firstCount + secondCount];
        int nextIndex = VisibleTileResolver.ResolveInto(
            destination,
            startIndex: 0,
            backgroundPlanes,
            fineScrollX: 1,
            fineScrollY: 0,
            coarseScrollX: 2,
            coarseScrollY: 3,
            backgroundPlaneSelect: 0,
            backgroundPlaneLayout: BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth: screenWidth,
            screenHeight: firstStripHeight,
            screenOffsetY: 0,
            clipTop: 0,
            clipBottom: firstStripHeight,
            useUpperBackgroundTileBank: false);
        nextIndex = VisibleTileResolver.ResolveInto(
            destination,
            nextIndex,
            backgroundPlanes,
            fineScrollX: 5,
            fineScrollY: 0,
            coarseScrollX: 2,
            coarseScrollY: 3,
            backgroundPlaneSelect: 0,
            backgroundPlaneLayout: BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth: screenWidth,
            screenHeight: secondStripHeight,
            screenOffsetY: firstStripHeight,
            clipTop: firstStripHeight,
            clipBottom: firstStripHeight + secondStripHeight,
            useUpperBackgroundTileBank: true);

        Assert.Equal(destination.Length, nextIndex);

        List<VisibleTile> firstExpected = VisibleTileResolver.Resolve(
            backgroundPlanes,
            fineScrollX: 1,
            fineScrollY: 0,
            coarseScrollX: 2,
            coarseScrollY: 3,
            backgroundPlaneSelect: 0,
            backgroundPlaneLayout: BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth: screenWidth,
            screenHeight: firstStripHeight,
            screenOffsetY: 0,
            clipTop: 0,
            clipBottom: firstStripHeight,
            useUpperBackgroundTileBank: false);
        List<VisibleTile> secondExpected = VisibleTileResolver.Resolve(
            backgroundPlanes,
            fineScrollX: 5,
            fineScrollY: 0,
            coarseScrollX: 2,
            coarseScrollY: 3,
            backgroundPlaneSelect: 0,
            backgroundPlaneLayout: BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth: screenWidth,
            screenHeight: secondStripHeight,
            screenOffsetY: firstStripHeight,
            clipTop: firstStripHeight,
            clipBottom: firstStripHeight + secondStripHeight,
            useUpperBackgroundTileBank: true);

        Assert.Equal(firstExpected.Count, firstCount);
        Assert.Equal(secondExpected.Count, secondCount);

        for (int i = 0; i < firstExpected.Count; i++)
            AssertEqualTile(firstExpected[i], destination[i]);

        for (int i = 0; i < secondExpected.Count; i++)
            AssertEqualTile(secondExpected[i], destination[firstExpected.Count + i]);
    }

    [Fact]
    public void ResolveInto_ArrayDestination_BatchProcessing_StaysWithinAllocationBudget_AndStable()
    {
        byte[] backgroundPlanes = CombinePages(
            BuildPage((x, y) => (byte)((y * 32) + x)),
            BuildPage((x, y) => (byte)(100 + x)));

        const int fineScrollX = 3;
        const int fineScrollY = 2;
        const int coarseScrollX = 5;
        const int coarseScrollY = 4;
        const int backgroundPlaneSelect = 0;
        const int screenWidth = 256;
        const int screenHeight = 240;
        const int iterations = 2000;
        const long allocationCeilingBytes = 512 * 1024;

        int expectedCount = VisibleTileResolver.GetVisibleTileCount(
            backgroundPlanes,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            backgroundPlaneSelect,
            BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth,
            screenHeight);
        VisibleTile[] expected = VisibleTileResolver.ResolveArray(
            backgroundPlanes,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            backgroundPlaneSelect,
            BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth,
            screenHeight);

        var destination = new VisibleTile[expectedCount];

        // Warm-up to reduce one-time JIT/allocation noise.
        int warmupCount = VisibleTileResolver.ResolveInto(
            destination,
            startIndex: 0,
            backgroundPlanes,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            backgroundPlaneSelect,
            BackgroundPlaneLayoutMode.SharedLeftRight,
            screenWidth,
            screenHeight);
        Assert.Equal(expectedCount, warmupCount);

        long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
        int resolvedCount = 0;
        for (int i = 0; i < iterations; i++)
        {
            resolvedCount = VisibleTileResolver.ResolveInto(
                destination,
                startIndex: 0,
                backgroundPlanes,
                fineScrollX,
                fineScrollY,
                coarseScrollX,
                coarseScrollY,
                backgroundPlaneSelect,
                BackgroundPlaneLayoutMode.SharedLeftRight,
                screenWidth,
                screenHeight);
        }
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;

        Assert.Equal(expectedCount, resolvedCount);
        Assert.True(allocatedBytes <= allocationCeilingBytes,
            $"Batch ResolveInto allocated {allocatedBytes} bytes, expected <= {allocationCeilingBytes} bytes.");

        // Sample-check output stability after repeated batch runs.
        AssertEqualTile(expected[0], destination[0]);
        AssertEqualTile(expected[expected.Length / 2], destination[destination.Length / 2]);
        AssertEqualTile(expected[^1], destination[^1]);
    }

    private static VisibleTile FindTile(IReadOnlyList<VisibleTile> tiles, int screenX, int screenY) =>
        tiles.Single(tile => tile.ScreenX == screenX && tile.ScreenY == screenY);

    private static void AssertEqualTile(VisibleTile expected, VisibleTile actual)
    {
        Assert.Equal(expected.ScreenX, actual.ScreenX);
        Assert.Equal(expected.ScreenY, actual.ScreenY);
        Assert.Equal(expected.TileId, actual.TileId);
        Assert.Equal(expected.PaletteId, actual.PaletteId);
        Assert.Equal(expected.LogicalPlaneIndex, actual.LogicalPlaneIndex);
        Assert.Equal(expected.PhysicalPlaneIndex, actual.PhysicalPlaneIndex);
        Assert.Equal(expected.TileX, actual.TileX);
        Assert.Equal(expected.TileY, actual.TileY);
        Assert.Equal(expected.ClipTop, actual.ClipTop);
        Assert.Equal(expected.ClipBottom, actual.ClipBottom);
        Assert.Equal(expected.UseUpperBackgroundTileBank, actual.UseUpperBackgroundTileBank);
    }

    private static byte[] CombinePages(params byte[][] pages)
    {
        var combined = new byte[pages.Sum(page => page.Length)];
        int offset = 0;
        foreach (byte[] page in pages)
        {
            Buffer.BlockCopy(page, 0, combined, offset, page.Length);
            offset += page.Length;
        }

        return combined;
    }

    private static byte[] BuildPage(Func<int, int, byte> tileFactory)
    {
        var page = new byte[1024];

        for (int y = 0; y < 30; y++)
        {
            for (int x = 0; x < 32; x++)
                page[(y * 32) + x] = tileFactory(x, y);
        }

        return page;
    }
}

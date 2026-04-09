using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class DebugPageStateControllerTests
{
    [Fact]
    public void BuildPageSummaries_FormatsExpectedRanges()
    {
        Assert.Equal("内存 $0080 - $00FF", DebugPageStateController.BuildMemoryPageSummary(1, DebugViewModel.MemoryPageSize));
        Assert.Equal("第 2 / 512 页", DebugPageStateController.BuildMemoryPageNumber(1, 0x10000 / DebugViewModel.MemoryPageSize));
        Assert.Equal("栈 $0140 - $017F", DebugPageStateController.BuildStackPageSummary(1, DebugViewModel.StackPageSize));
        Assert.Equal("零页 $0080 - $00BF", DebugPageStateController.BuildZeroPageSummary(2, DebugViewModel.ZeroPageSliceSize));
        Assert.Equal("反汇编页偏移 -2", DebugPageStateController.BuildDisasmSummary(-2));
    }

    [Theory]
    [InlineData(0, -1, 0, 3, 0, false)]
    [InlineData(1, -1, 0, 3, 0, true)]
    [InlineData(2, 1, 0, 3, 3, true)]
    [InlineData(3, 1, 0, 3, 3, false)]
    public void MoveBounded_ClampsAndReportsWhetherIndexChanged(
        int currentIndex,
        int delta,
        int minIndex,
        int maxIndex,
        int expectedIndex,
        bool expectedChanged)
    {
        var decision = DebugPageStateController.MoveBounded(currentIndex, delta, minIndex, maxIndex);

        Assert.Equal(expectedIndex, decision.NextIndex);
        Assert.Equal(expectedChanged, decision.Changed);
    }

    [Theory]
    [InlineData(-3, 1)]
    [InlineData(0, 1)]
    [InlineData(5, 5)]
    [InlineData(999, 8)]
    public void ClampPageNumber_RestrictsRequestedPageToValidRange(int pageNumber, int expected)
    {
        Assert.Equal(expected, DebugPageStateController.ClampPageNumber(pageNumber, 8));
    }
}

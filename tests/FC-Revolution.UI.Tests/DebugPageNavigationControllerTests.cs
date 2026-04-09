using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class DebugPageNavigationControllerTests
{
    [Fact]
    public void BuildMemoryPageMoveDecision_MovesWithinBoundsAndProjectsPageInput()
    {
        var decision = DebugPageNavigationController.BuildMemoryPageMoveDecision(
            currentIndex: 0,
            delta: 1,
            totalPages: 512);

        Assert.True(decision.Changed);
        Assert.Equal(1, decision.NextIndex);
        Assert.Equal("2", decision.MemoryPageInput);
    }

    [Fact]
    public void BuildMemoryPageMoveDecision_StopsAtLowerBound()
    {
        var decision = DebugPageNavigationController.BuildMemoryPageMoveDecision(
            currentIndex: 0,
            delta: -1,
            totalPages: 512);

        Assert.False(decision.Changed);
        Assert.Equal(0, decision.NextIndex);
        Assert.Equal("1", decision.MemoryPageInput);
    }

    [Fact]
    public void BuildMemoryPageJumpDecision_ProjectsIndexAndNormalizedInput()
    {
        var decision = DebugPageNavigationController.BuildMemoryPageJumpDecision(pageNumber: 7);

        Assert.Equal(6, decision.NextIndex);
        Assert.Equal("7", decision.MemoryPageInput);
    }

    [Fact]
    public void BuildBoundedPageMoveDecision_ClampsAndReportsChange()
    {
        var unchanged = DebugPageNavigationController.BuildBoundedPageMoveDecision(
            currentIndex: 0,
            delta: -1,
            minIndex: 0,
            maxIndex: 3);
        var changed = DebugPageNavigationController.BuildBoundedPageMoveDecision(
            currentIndex: 2,
            delta: 1,
            minIndex: 0,
            maxIndex: 3);

        Assert.False(unchanged.Changed);
        Assert.Equal(0, unchanged.NextIndex);
        Assert.True(changed.Changed);
        Assert.Equal(3, changed.NextIndex);
    }

    [Fact]
    public void BuildDisasmPageMoveDecision_RemainsUnbounded()
    {
        var decision = DebugPageNavigationController.BuildDisasmPageMoveDecision(
            currentIndex: -2,
            delta: -1);

        Assert.True(decision.Changed);
        Assert.Equal(-3, decision.NextIndex);
    }

    [Fact]
    public void BuildModifiedMemoryPageMoveDecision_RespectsBoundsWithoutForcingRefreshPath()
    {
        var blocked = DebugPageNavigationController.BuildModifiedMemoryPageMoveDecision(
            currentIndex: 0,
            delta: -1,
            pageCount: 2);
        var moved = DebugPageNavigationController.BuildModifiedMemoryPageMoveDecision(
            currentIndex: 0,
            delta: 1,
            pageCount: 2);
        var empty = DebugPageNavigationController.BuildModifiedMemoryPageMoveDecision(
            currentIndex: 0,
            delta: 1,
            pageCount: 0);

        Assert.False(blocked.Changed);
        Assert.Equal(0, blocked.NextIndex);
        Assert.True(moved.Changed);
        Assert.Equal(1, moved.NextIndex);
        Assert.False(empty.Changed);
        Assert.Equal(0, empty.NextIndex);
    }
}

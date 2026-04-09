using System.Globalization;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct DebugPageMoveDecision(
    bool Changed,
    int NextIndex);

internal readonly record struct DebugMemoryPageMoveDecision(
    bool Changed,
    int NextIndex,
    string MemoryPageInput);

internal readonly record struct DebugMemoryPageJumpDecision(
    int NextIndex,
    string MemoryPageInput);

internal static class DebugPageNavigationController
{
    public static DebugMemoryPageMoveDecision BuildMemoryPageMoveDecision(
        int currentIndex,
        int delta,
        int totalPages)
    {
        var move = DebugPageStateController.MoveBounded(
            currentIndex,
            delta,
            minIndex: 0,
            maxIndex: totalPages - 1);
        return new DebugMemoryPageMoveDecision(
            move.Changed,
            move.NextIndex,
            (move.NextIndex + 1).ToString(CultureInfo.InvariantCulture));
    }

    public static DebugMemoryPageJumpDecision BuildMemoryPageJumpDecision(int pageNumber) =>
        new(
            NextIndex: pageNumber - 1,
            MemoryPageInput: pageNumber.ToString(CultureInfo.InvariantCulture));

    public static DebugPageMoveDecision BuildBoundedPageMoveDecision(
        int currentIndex,
        int delta,
        int minIndex,
        int maxIndex)
    {
        var move = DebugPageStateController.MoveBounded(currentIndex, delta, minIndex, maxIndex);
        return new DebugPageMoveDecision(move.Changed, move.NextIndex);
    }

    public static DebugPageMoveDecision BuildDisasmPageMoveDecision(int currentIndex, int delta) =>
        new(Changed: delta != 0, NextIndex: currentIndex + delta);

    public static DebugPageMoveDecision BuildModifiedMemoryPageMoveDecision(
        int currentIndex,
        int delta,
        int pageCount)
    {
        if (delta == 0 || pageCount <= 0)
            return new DebugPageMoveDecision(Changed: false, NextIndex: currentIndex);

        var nextIndex = currentIndex + delta;
        if (nextIndex < 0 || nextIndex >= pageCount)
            return new DebugPageMoveDecision(Changed: false, NextIndex: currentIndex);

        return new DebugPageMoveDecision(Changed: true, NextIndex: nextIndex);
    }
}

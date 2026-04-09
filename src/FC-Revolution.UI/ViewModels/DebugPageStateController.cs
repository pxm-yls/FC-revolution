using System;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct DebugPageNavigationDecision(
    int NextIndex,
    bool Changed);

internal static class DebugPageStateController
{
    public static string BuildMemoryPageSummary(int memoryPageIndex, int memoryPageSize)
    {
        var start = memoryPageIndex * memoryPageSize;
        var end = Math.Min(0xFFFF, start + memoryPageSize - 1);
        return $"内存 ${start:X4} - ${end:X4}";
    }

    public static string BuildMemoryPageNumber(int memoryPageIndex, int totalPageCount) =>
        $"第 {memoryPageIndex + 1} / {totalPageCount} 页";

    public static string BuildStackPageSummary(int stackPageIndex, int stackPageSize)
    {
        var start = 0x0100 + stackPageIndex * stackPageSize;
        var end = start + stackPageSize - 1;
        return $"栈 ${start:X4} - ${end:X4}";
    }

    public static string BuildZeroPageSummary(int zeroPageSliceIndex, int zeroPageSliceSize)
    {
        var start = zeroPageSliceIndex * zeroPageSliceSize;
        var end = Math.Min(0x00FF, start + zeroPageSliceSize - 1);
        return $"零页 ${start:X4} - ${end:X4}";
    }

    public static string BuildDisasmSummary(int disasmPageIndex) =>
        $"反汇编页偏移 {disasmPageIndex:+#;-#;0}";

    public static DebugPageNavigationDecision MoveBounded(
        int currentIndex,
        int delta,
        int minIndex,
        int maxIndex)
    {
        var nextIndex = Math.Clamp(currentIndex + delta, minIndex, maxIndex);
        return new DebugPageNavigationDecision(nextIndex, nextIndex != currentIndex);
    }

    public static int ClampPageNumber(int pageNumber, int maxPageNumber) =>
        Math.Clamp(pageNumber, 1, maxPageNumber);
}

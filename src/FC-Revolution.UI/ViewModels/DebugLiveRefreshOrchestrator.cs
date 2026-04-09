using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct DebugRefreshCapturePlan(
    bool RequiresState,
    bool RequiresAnyCapture);

internal readonly record struct DebugRefreshAddressPlan(
    ushort MemoryPageStart,
    ushort StackPageStart,
    ushort ZeroPageStart,
    ushort DisasmStart);

internal readonly record struct DebugRefreshApplyPlan(
    bool ApplyRegisters,
    bool ApplyPpu,
    bool ApplyMemoryPage,
    bool ApplyStack,
    bool ApplyZeroPage,
    bool ApplyDisasm);

internal static class DebugLiveRefreshOrchestrator
{
    public static bool ShouldRunLiveRefreshTick(
        bool isDisposed,
        bool hasSessionFailure,
        bool isMemoryCellEditing,
        bool hasActiveRefreshSections) =>
        !isDisposed &&
        !hasSessionFailure &&
        !isMemoryCellEditing &&
        hasActiveRefreshSections;

    public static bool HasActiveRefreshSections(
        bool showRegisters,
        bool showPpu,
        bool showDisasm,
        bool showStack,
        bool showZeroPage,
        bool showMemoryPage) =>
        showRegisters ||
        showPpu ||
        showDisasm ||
        showStack ||
        showZeroPage ||
        showMemoryPage;

    public static bool ShouldScheduleRefresh(
        bool refreshScheduled,
        bool isDisposed,
        bool hasSessionFailure) =>
        !refreshScheduled &&
        !isDisposed &&
        !hasSessionFailure;

    public static DebugRefreshCapturePlan BuildCapturePlan(DebugRefreshRequest request) =>
        new(
            request.RequiresState,
            request.RequiresAnyCapture);

    public static DebugRefreshAddressPlan BuildAddressPlan(
        DebugRefreshRequest request,
        ushort programCounter,
        int memoryPageSize,
        int stackPageSize,
        int zeroPageSliceSize,
        int disasmPageSize)
    {
        var memoryPageStart = (ushort)(request.MemoryPageIndex * memoryPageSize);
        var stackPageStart = (ushort)(0x0100 + request.StackPageIndex * stackPageSize);
        var zeroPageStart = (ushort)(request.ZeroPageSliceIndex * zeroPageSliceSize);
        var disasmStart = unchecked((ushort)(programCounter + request.DisasmPageIndex * disasmPageSize));

        return new DebugRefreshAddressPlan(
            memoryPageStart,
            stackPageStart,
            zeroPageStart,
            disasmStart);
    }

    public static DebugRefreshApplyPlan BuildApplyPlan(
        bool showRegisters,
        bool showPpu,
        bool showMemoryPage,
        bool showStack,
        bool showZeroPage,
        bool showDisasm) =>
        new(
            showRegisters,
            showPpu,
            showMemoryPage,
            showStack,
            showZeroPage,
            showDisasm);

    public static ushort ResolveMemoryPageStart(int memoryPageIndex, int memoryPageSize) =>
        (ushort)(memoryPageIndex * memoryPageSize);

    public static bool ShouldScheduleRefreshAfterLocatorUpdate(bool updatedInPlace) => !updatedInPlace;
}

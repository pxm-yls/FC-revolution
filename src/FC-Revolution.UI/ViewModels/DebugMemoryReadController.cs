using System;
using System.Globalization;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct DebugMemoryReadSuccessState(
    string ValueInput,
    int MemoryPageIndex,
    string MemoryPageInput,
    ushort HighlightedAddress,
    string EditStatus);

internal static class DebugMemoryReadController
{
    public static DebugMemoryReadSuccessState BuildReadSuccessState(ushort address, byte value, int memoryPageSize)
    {
        var normalizedMemoryPageSize = Math.Max(1, memoryPageSize);
        var memoryPageIndex = address / normalizedMemoryPageSize;
        return new DebugMemoryReadSuccessState(
            value.ToString("X2"),
            memoryPageIndex,
            (memoryPageIndex + 1).ToString(CultureInfo.InvariantCulture),
            address,
            $"已查询 ${address:X4} = ${value:X2}");
    }
}

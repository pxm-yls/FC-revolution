using System.Collections.Generic;
using System.Collections.ObjectModel;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal static class DebugMemoryGridBuilder
{
    public static void BuildMemoryRows(
        ObservableCollection<MemoryPageRow> target,
        ushort startAddress,
        IReadOnlyList<byte> values,
        bool rowHeaderOffset,
        int columns,
        ushort? highlightAddress = null)
    {
        var rows = values.Count / columns;
        if (TryUpdateMemoryRowsInPlace(target, startAddress, values, columns, highlightAddress))
            return;

        target.Clear();
        for (var row = 0; row < rows; row++)
        {
            var rowBase = startAddress + row * columns;
            var cells = new List<MemoryCellItem>(columns);
            for (var column = 0; column < columns; column++)
            {
                var address = (ushort)(rowBase + column);
                var index = row * columns + column;
                var (isHighlighted, isRowLocatorHighlighted, isColumnLocatorHighlighted) = BuildHighlightFlags(
                    row,
                    column,
                    startAddress,
                    values.Count,
                    columns,
                    highlightAddress);
                cells.Add(new MemoryCellItem
                {
                    Address = address,
                    DisplayAddress = $"${address:X4}",
                    Value = values[index].ToString("X2"),
                    IsHighlighted = isHighlighted,
                    IsRowLocatorHighlighted = isRowLocatorHighlighted,
                    IsColumnLocatorHighlighted = isColumnLocatorHighlighted
                });
            }

            target.Add(new MemoryPageRow
            {
                RowHeader = rowHeaderOffset ? $"${rowBase:X4}" : $"R{row + 1}",
                Cells = cells
            });
        }
    }

    public static bool TryUpdateMemoryRowsInPlace(
        ObservableCollection<MemoryPageRow> target,
        ushort startAddress,
        IReadOnlyList<byte> values,
        int columns,
        ushort? highlightAddress)
    {
        var rows = values.Count / columns;
        if (target.Count != rows)
            return false;

        for (var row = 0; row < rows; row++)
        {
            if (target[row].Cells is not IList<MemoryCellItem> cells || cells.Count != columns)
                return false;

            var expectedRowBase = startAddress + row * columns;
            if (cells[0].Address != expectedRowBase)
                return false;
        }

        for (var row = 0; row < rows; row++)
        {
            var cells = (IList<MemoryCellItem>)target[row].Cells;
            for (var column = 0; column < columns; column++)
            {
                var index = row * columns + column;
                var cell = cells[column];
                var (isHighlighted, isRowLocatorHighlighted, isColumnLocatorHighlighted) = BuildHighlightFlags(
                    row,
                    column,
                    startAddress,
                    values.Count,
                    columns,
                    highlightAddress);
                cell.Value = values[index].ToString("X2");
                cell.IsHighlighted = isHighlighted;
                cell.IsRowLocatorHighlighted = isRowLocatorHighlighted;
                cell.IsColumnLocatorHighlighted = isColumnLocatorHighlighted;
            }
        }

        return true;
    }

    public static (bool IsHighlighted, bool IsRowLocatorHighlighted, bool IsColumnLocatorHighlighted) BuildHighlightFlags(
        int row,
        int column,
        ushort startAddress,
        int valueCount,
        int columns,
        ushort? highlightAddress)
    {
        if (!highlightAddress.HasValue)
            return (false, false, false);

        var highlightOffset = highlightAddress.Value - startAddress;
        if (highlightAddress.Value < startAddress || highlightOffset >= valueCount)
            return (false, false, false);

        var highlightedRow = highlightOffset / columns;
        var highlightedColumn = highlightOffset % columns;
        return (
            highlightOffset == row * columns + column,
            highlightedRow == row,
            highlightedColumn == column);
    }
}

using System.Collections.Generic;

namespace FC_Revolution.UI.Models;

public sealed class MemoryPageRow
{
    public required string RowHeader { get; init; }

    public required IReadOnlyList<MemoryCellItem> Cells { get; init; }
}

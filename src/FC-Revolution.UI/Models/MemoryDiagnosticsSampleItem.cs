namespace FC_Revolution.UI.Models;

public sealed class MemoryDiagnosticsSampleItem
{
    public required string TimeLabel { get; init; }

    public required string WorkingSetLabel { get; init; }

    public required string PrivateLabel { get; init; }

    public required string ManagedLabel { get; init; }

    public required string PreviewLabel { get; init; }

    public required string CacheLabel { get; init; }

    public required string LayoutLabel { get; init; }
}

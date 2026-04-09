namespace FCRevolution.Core.Timeline.Persistence;

public sealed class TimelineManifest
{
    public string RomId { get; set; } = "";
    public string RomDisplayName { get; set; } = "";
    public int Version { get; set; } = 1;
    public Guid CurrentBranchId { get; set; }
    public List<TimelineBranchRecord> Branches { get; set; } = [];
    public List<TimelineSnapshotRecord> Snapshots { get; set; } = [];
}

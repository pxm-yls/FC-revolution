namespace FCRevolution.Core.Timeline.Persistence;

public sealed class TimelineSnapshotRecord
{
    public Guid SnapshotId { get; set; }
    public Guid BranchId { get; set; }
    public Guid? ParentSnapshotId { get; set; }
    public long Frame { get; set; }
    public double TimestampSeconds { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Kind { get; set; } = "Timeline";
    public string? Name { get; set; }
    public string StateFile { get; set; } = "";
    public string? ThumbnailFile { get; set; }
}

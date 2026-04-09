namespace FCRevolution.Core.Timeline.Persistence;

public sealed class TimelineBranchRecord
{
    public Guid BranchId { get; set; }
    public string Name { get; set; } = "主线";
    public Guid? ParentBranchId { get; set; }
    public Guid? ForkedFromSnapshotId { get; set; }
    public long CreatedFromFrame { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? HeadSnapshotId { get; set; }
    public Guid? QuickSaveSnapshotId { get; set; }
    public bool IsMainBranch { get; set; }
}

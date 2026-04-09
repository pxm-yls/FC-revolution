namespace FCRevolution.Core.State;

using FCRevolution.Core.Timeline;

/// <summary>Unified emulator snapshot payload shared by save-state and timeline systems.</summary>
public sealed class StateSnapshotData
{
    public long Frame { get; init; }
    public double Timestamp { get; init; }
    public byte[] CpuState { get; init; } = [];
    public byte[] PpuState { get; init; } = [];
    public byte[] RamState { get; init; } = [];
    public byte[] CartState { get; init; } = [];
    public byte[] ApuState { get; init; } = [];
    public uint[]? Thumbnail { get; init; }

    public FrameSnapshot ToFrameSnapshot(uint[]? thumbnailOverride = null) => new()
    {
        Frame = Frame,
        Timestamp = Timestamp,
        CpuState = CpuState,
        PpuState = PpuState,
        RamState = RamState,
        CartState = CartState,
        ApuState = ApuState,
        Thumbnail = thumbnailOverride ?? Thumbnail ?? new uint[64 * 60],
    };

    public static StateSnapshotData FromFrameSnapshot(FrameSnapshot snapshot) => new()
    {
        Frame = snapshot.Frame,
        Timestamp = snapshot.Timestamp,
        CpuState = snapshot.CpuState,
        PpuState = snapshot.PpuState,
        RamState = snapshot.RamState,
        CartState = snapshot.CartState,
        ApuState = snapshot.ApuState,
        Thumbnail = snapshot.Thumbnail,
    };
}

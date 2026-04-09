using System;
using FCRevolution.Core.State;
using FCRevolution.Core.Timeline;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.ViewModels;

internal static class CoreTimelineModelBridge
{
    private const string DefaultSnapshotFormat = "nes/fcrs";

    public static CoreTimelineSnapshot ToCoreTimelineSnapshot(FrameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new CoreTimelineSnapshot
        {
            Frame = snapshot.Frame,
            TimestampSeconds = snapshot.Timestamp,
            Thumbnail = snapshot.Thumbnail,
            State = new CoreStateBlob
            {
                Format = DefaultSnapshotFormat,
                Data = StateSnapshotSerializer.Serialize(
                    StateSnapshotData.FromFrameSnapshot(snapshot),
                    includeThumbnail: true)
            }
        };
    }

    public static FrameSnapshot ToLegacyFrameSnapshot(CoreTimelineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.State.Data is { Length: > 0 } payload)
        {
            var parsed = StateSnapshotSerializer.Deserialize(payload).ToFrameSnapshot(thumbnailOverride: snapshot.Thumbnail);
            return new FrameSnapshot
            {
                Frame = parsed.Frame,
                Timestamp = parsed.Timestamp,
                CpuState = parsed.CpuState,
                PpuState = parsed.PpuState,
                RamState = parsed.RamState,
                CartState = parsed.CartState,
                ApuState = parsed.ApuState,
                Thumbnail = parsed.Thumbnail,
            };
        }

        return new FrameSnapshot
        {
            Frame = snapshot.Frame,
            Timestamp = snapshot.TimestampSeconds,
            Thumbnail = snapshot.Thumbnail,
        };
    }

    public static CoreBranchPoint ToCoreBranchPoint(BranchPoint branchPoint)
    {
        ArgumentNullException.ThrowIfNull(branchPoint);

        var coreBranchPoint = new CoreBranchPoint
        {
            Id = branchPoint.Id,
            Name = branchPoint.Name,
            RomPath = branchPoint.RomPath,
            Frame = branchPoint.Frame,
            TimestampSeconds = branchPoint.Timestamp,
            Snapshot = ToCoreTimelineSnapshot(branchPoint.Snapshot),
            CreatedAt = branchPoint.CreatedAt
        };

        foreach (var child in branchPoint.Children)
            coreBranchPoint.Children.Add(ToCoreBranchPoint(child));

        return coreBranchPoint;
    }

    public static BranchPoint ToLegacyBranchPoint(CoreBranchPoint branchPoint, string? romPath)
    {
        ArgumentNullException.ThrowIfNull(branchPoint);

        var legacyBranchPoint = new BranchPoint
        {
            Id = branchPoint.Id,
            Name = branchPoint.Name,
            RomPath = string.IsNullOrWhiteSpace(branchPoint.RomPath) ? romPath ?? string.Empty : branchPoint.RomPath,
            Frame = branchPoint.Frame,
            Timestamp = branchPoint.TimestampSeconds,
            Snapshot = ToLegacyFrameSnapshot(branchPoint.Snapshot),
            CreatedAt = branchPoint.CreatedAt,
        };

        foreach (var child in branchPoint.Children)
            legacyBranchPoint.Children.Add(ToLegacyBranchPoint(child, child.RomPath));

        return legacyBranchPoint;
    }

    public static long ReadFrame(CoreTimelineSnapshot snapshot) => snapshot.Frame;

    public static double ReadTimestampSeconds(CoreTimelineSnapshot snapshot) => snapshot.TimestampSeconds;

    public static uint[] ReadThumbnail(CoreTimelineSnapshot snapshot) => snapshot.Thumbnail;
}

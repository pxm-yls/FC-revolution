using System;
using System.Collections.Generic;
using System.Linq;
using FCRevolution.Core;
using FCRevolution.Core.Input;
using FCRevolution.Core.PPU;
using FCRevolution.Core.State;
using FCRevolution.Core.Timeline;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.Infrastructure;

public sealed class NesConsoleDebugSurfaceAdapter : ICoreDebugSurface
{
    private readonly NesConsole _console;

    public NesConsoleDebugSurfaceAdapter(NesConsole console)
    {
        _console = console;
    }

    public CoreDebugState CaptureDebugState() => CoreDebugState.FromLegacy(FCRevolution.Core.Debug.DebugState.Capture(_console));

    public byte ReadMemory(ushort address) => _console.Bus.Read(address);

    public void WriteMemory(ushort address, byte value) => _console.Bus.Write(address, value);

    public byte[] ReadMemoryBlock(ushort startAddress, int length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");

        var values = new byte[length];
        for (var index = 0; index < length; index++)
            values[index] = _console.Bus.Read(unchecked((ushort)(startAddress + index)));

        return values;
    }
}

public sealed class NesConsoleTimeTravelServiceAdapter : ITimeTravelService
{
    private const double CpuClockRate = 1789773.0;
    private readonly NesConsole _console;

    public NesConsoleTimeTravelServiceAdapter(NesConsole console)
    {
        _console = console;
    }

    public long CurrentFrame => _console.Ppu.Frame;

    public double CurrentTimestampSeconds => _console.CpuCycles / CpuClockRate;

    public int SnapshotInterval
    {
        get => _console.Timeline.SnapshotInterval;
        set => _console.Timeline.SnapshotInterval = value;
    }

    public int HotCacheCount => _console.Timeline.Cache.HotCount;

    public int WarmCacheCount => _console.Timeline.Cache.WarmCount;

    public long NewestFrame => _console.Timeline.Cache.NewestFrame;

    public CoreTimeTravelCacheInfo GetCacheInfo() => new(
        HotCacheCount,
        WarmCacheCount,
        NewestFrame,
        SnapshotInterval);

    public IReadOnlyList<CoreTimelineThumbnail> GetThumbnails() =>
        _console.Timeline.Cache.GetThumbnails()
            .Select(static entry => new CoreTimelineThumbnail(entry.frame, entry.thumb))
            .ToList();

    public CoreBranchPoint CreateBranch(string name, uint[] frameBuffer) =>
        MapBranchPoint(_console.Timeline.CreateBranch(name, frameBuffer));

    public void RestoreSnapshot(CoreTimelineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var decoded = StateSnapshotSerializer.Deserialize(snapshot.State.Data).ToFrameSnapshot(snapshot.Thumbnail);
        _console.LoadSnapshot(StateSnapshotData.FromFrameSnapshot(decoded));
    }

    public long SeekToFrame(long frame) => _console.Timeline.SeekToFrame(frame);

    public long RewindFrames(int frames) => _console.Timeline.RewindFrames(frames);

    public CoreTimelineSnapshot? GetNearestSnapshot(long frame)
    {
        var snapshot = _console.Timeline.Cache.GetNearest(frame);
        return snapshot is null ? null : MapSnapshot(snapshot);
    }

    public CoreStateBlob? GetNearestState(long frame, bool includeThumbnail = false)
    {
        var snapshot = _console.Timeline.Cache.GetNearest(frame);
        if (snapshot is null)
            return null;

        return new CoreStateBlob
        {
            Format = "nes/fcrs",
            Data = StateSnapshotSerializer.Serialize(
                StateSnapshotData.FromFrameSnapshot(snapshot),
                includeThumbnail)
        };
    }

    public void PauseRecording() => _console.Timeline.PauseRecording();

    public void ResumeRecording() => _console.Timeline.ResumeRecording();

    private static CoreBranchPoint MapBranchPoint(BranchPoint source)
    {
        var mapped = new CoreBranchPoint
        {
            Id = source.Id,
            Name = source.Name,
            RomPath = source.RomPath,
            Frame = source.Frame,
            TimestampSeconds = source.Timestamp,
            Snapshot = MapSnapshot(source.Snapshot),
            CreatedAt = source.CreatedAt
        };

        foreach (var child in source.Children)
            mapped.Children.Add(MapBranchPoint(child));

        return mapped;
    }

    private static CoreTimelineSnapshot MapSnapshot(FrameSnapshot source)
    {
        return new CoreTimelineSnapshot
        {
            Frame = source.Frame,
            TimestampSeconds = source.Timestamp,
            Thumbnail = source.Thumbnail,
            State = new CoreStateBlob
            {
                Format = "nes/fcrs",
                Data = StateSnapshotSerializer.Serialize(
                    StateSnapshotData.FromFrameSnapshot(source),
                    includeThumbnail: true)
            }
        };
    }
}

public sealed class NesConsoleInputStateWriterAdapter : ICoreInputStateWriter
{
    private readonly NesConsole _console;

    public NesConsoleInputStateWriterAdapter(NesConsole console)
    {
        _console = console;
    }

    public void SetInputState(string portId, string actionId, float value)
    {
        var player = portId switch
        {
            "p1" => 0,
            "p2" => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(portId), portId, "Unsupported input port id.")
        };

        var button = actionId.ToLowerInvariant() switch
        {
            "a" => NesButton.A,
            "b" => NesButton.B,
            "select" => NesButton.Select,
            "start" => NesButton.Start,
            "up" => NesButton.Up,
            "down" => NesButton.Down,
            "left" => NesButton.Left,
            "right" => NesButton.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(actionId), actionId, "Unsupported input action id.")
        };

        _console.SetButton(player, button, value >= 0.5f);
    }
}

public sealed class NesConsoleRenderStateProviderAdapter : INesRenderStateProvider
{
    private readonly NesConsole _console;

    public NesConsoleRenderStateProviderAdapter(NesConsole console)
    {
        _console = console;
    }

    public PpuRenderStateSnapshot CaptureRenderStateSnapshot() => _console.Ppu.CaptureRenderStateSnapshot();
}

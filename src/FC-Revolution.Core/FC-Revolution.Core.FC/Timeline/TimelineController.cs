using FCRevolution.Core.State;

namespace FCRevolution.Core.Timeline;

/// <summary>
/// Orchestrates timeline recording and rewind/fast-forward for a <see cref="NesConsole"/>.
/// Records a snapshot every <see cref="SnapshotInterval"/> frames.
/// </summary>
public sealed class TimelineController
{
    private readonly NesConsole _nes;
    private readonly TimelineCache _cache;

    public int  SnapshotInterval { get; set; } = 5;  // snapshot every N frames
    public bool IsRecording      { get; private set; } = true;

    private int _framesSinceSnapshot;

    public TimelineController(NesConsole nes, TimelineCache? cache = null)
    {
        _nes   = nes;
        _cache = cache ?? new TimelineCache();
    }

    public TimelineCache Cache => _cache;

    // ─────────────────────────────────────────────────────────────────────
    //  Recording
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Call after each <see cref="NesConsole.RunFrame"/> to record state periodically.</summary>
    public void OnFrameComplete(uint[] frameBuffer)
    {
        if (!IsRecording) return;

        _framesSinceSnapshot++;
        if (_framesSinceSnapshot < SnapshotInterval) return;
        _framesSinceSnapshot = 0;

        CaptureSnapshot(frameBuffer);
    }

    private void CaptureSnapshot(uint[] frameBuffer)
    {
        _cache.Push(_nes.CaptureSnapshot(frameBuffer).ToFrameSnapshot());
    }

    public void PauseRecording()  => IsRecording = false;
    public void ResumeRecording() => IsRecording = true;

    // ─────────────────────────────────────────────────────────────────────
    //  Rewind / Seek
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Restore the emulator to the nearest snapshot at or before <paramref name="targetFrame"/>.
    /// Returns the actual frame restored, or -1 if no snapshot is available.
    /// </summary>
    public long SeekToFrame(long targetFrame)
    {
        var snap = _cache.GetNearest(targetFrame);
        if (snap == null) return -1;

        RestoreSnapshot(snap);
        return snap.Frame;
    }

    /// <summary>Step back by <paramref name="frames"/> frames (best effort).</summary>
    public long RewindFrames(int frames)
    {
        long target = Math.Max(0, _nes.Ppu.Frame - frames);
        return SeekToFrame(target);
    }

    private void RestoreSnapshot(FrameSnapshot snap)
    {
        PauseRecording();
        try
        {
            _nes.LoadSnapshot(StateSnapshotData.FromFrameSnapshot(snap));
        }
        finally
        {
            ResumeRecording();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Branch management (used by Save Branch Gallery)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Create a named branch point at the current frame.</summary>
    public BranchPoint CreateBranch(string name, uint[] currentFrame)
    {
        CaptureSnapshot(currentFrame);
        var snap = _cache.GetNearest(_nes.Ppu.Frame)!;
        return new BranchPoint
        {
            Id        = Guid.NewGuid(),
            Name      = name,
            Frame     = snap.Frame,
            Timestamp = snap.Timestamp,
            Snapshot  = snap,
        };
    }

    public void Reset()
    {
        _cache.Clear();
        _framesSinceSnapshot = 0;
        IsRecording = true;
    }
}

using System.Buffers;
using K4os.Compression.LZ4;
using FCRevolution.Core.State;

namespace FCRevolution.Core.Timeline;

/// <summary>
/// Multi-layer timeline cache.
/// Hot  : last <see cref="HotCapacity"/> frames  – raw in memory, O(1) access.
/// Warm : last <see cref="WarmCapacity"/> frames – LZ4-compressed in memory.
/// Cold : older frames                           – not yet implemented (disk).
/// </summary>
public sealed class TimelineCache
{
    private sealed class WarmEntry
    {
        public required long Frame { get; init; }
        public required double Timestamp { get; init; }
        public required uint[] Thumbnail { get; init; }
        public required byte[] CompressedBody { get; init; }
        public required int OriginalBodySize { get; init; }
    }

    // ── Configuration ─────────────────────────────────────────────────────
    public int HotCapacity  { get; }
    public int WarmCapacity { get; }

    // ── Hot layer: circular buffer of raw snapshots ────────────────────────
    private readonly FrameSnapshot?[] _hot;
    private int _hotHead, _hotCount;

    // ── Warm layer: LZ4-compressed snapshots ──────────────────────────────
    private readonly List<WarmEntry> _warm;
    private int _warmStart;

    // ── Stats ──────────────────────────────────────────────────────────────
    public int  HotCount  => _hotCount;
    public int  WarmCount => _warm.Count - _warmStart;
    public long OldestHotFrame => _hotCount == 0 ? -1 : _hot[(_hotHead - _hotCount + HotCapacity) % HotCapacity]!.Frame;
    public long NewestFrame    => _hotCount == 0 ? -1 : _hot[(_hotHead - 1 + HotCapacity) % HotCapacity]!.Frame;

    public TimelineCache(int hotCapacity = 600, int warmCapacity = 3600)
    {
        HotCapacity  = hotCapacity;
        WarmCapacity = warmCapacity;
        _hot  = new FrameSnapshot?[hotCapacity];
        _warm = new List<WarmEntry>(warmCapacity + 16);
        _warmStart = 0;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Write
    // ─────────────────────────────────────────────────────────────────────

    public void Push(FrameSnapshot snap)
    {
        // Evict oldest hot entry to warm if hot is full
        if (_hotCount == HotCapacity)
        {
            var evicted = _hot[(_hotHead - _hotCount + HotCapacity) % HotCapacity]!;
            MoveToWarm(evicted);
            _hotCount--;
        }

        _hot[_hotHead] = snap;
        _hotHead = (_hotHead + 1) % HotCapacity;
        _hotCount++;
    }

    private void MoveToWarm(FrameSnapshot snap)
    {
        if (WarmCapacity <= 0)
            return;

        // Trim warm to capacity
        while (WarmCount >= WarmCapacity)
            TrimOldestWarmEntry();

        var raw = SerializeSnapshotBody(snap);
        var compressed = new byte[LZ4Codec.MaximumOutputSize(raw.Length)];
        var len = LZ4Codec.Encode(raw, 0, raw.Length, compressed, 0, compressed.Length, LZ4Level.L00_FAST);
        Array.Resize(ref compressed, len);
        var entry = new WarmEntry
        {
            Frame = snap.Frame,
            Timestamp = snap.Timestamp,
            Thumbnail = snap.Thumbnail,
            CompressedBody = compressed,
            OriginalBodySize = raw.Length
        };

        var index = FindWarmInsertIndex(entry.Frame);
        _warm.Insert(index, entry);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Read
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Get the most recent snapshot at or before <paramref name="targetFrame"/>.</summary>
    public FrameSnapshot? GetNearest(long targetFrame)
    {
        // Search hot (newest first)
        for (int i = 0; i < _hotCount; i++)
        {
            int idx = (_hotHead - 1 - i + HotCapacity) % HotCapacity;
            var s = _hot[idx]!;
            if (s.Frame <= targetFrame) return s;
        }

        // Search warm
        var warmIndex = FindWarmEntryAtOrBefore(targetFrame);
        return warmIndex >= 0 ? RestoreWarmSnapshot(_warm[warmIndex]) : null;
    }

    /// <summary>Returns thumbnails for UI display — all layers, newest first.</summary>
    public IEnumerable<(long frame, uint[] thumb)> GetThumbnails()
    {
        // Hot layer
        for (int i = 0; i < _hotCount; i++)
        {
            int idx = (_hotHead - 1 - i + HotCapacity) % HotCapacity;
            var s = _hot[idx]!;
            yield return (s.Frame, s.Thumbnail);
        }
        // Warm layer (newest first)
        for (var i = _warm.Count - 1; i >= 0; i--)
        {
            if (i < _warmStart)
                break;
            var entry = _warm[i];
            yield return (entry.Frame, entry.Thumbnail);
        }
    }

    public void Clear()
    {
        Array.Clear(_hot);
        _hotHead = 0;
        _hotCount = 0;
        _warm.Clear();
        _warmStart = 0;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Serialization helpers
    // ─────────────────────────────────────────────────────────────────────

    private static byte[] SerializeSnapshotBody(FrameSnapshot snapshot)
        => StateSnapshotSerializer.Serialize(StateSnapshotData.FromFrameSnapshot(snapshot), includeThumbnail: false);

    private FrameSnapshot RestoreWarmSnapshot(WarmEntry entry)
    {
        var rented = ArrayPool<byte>.Shared.Rent(entry.OriginalBodySize);
        try
        {
            var decoded = LZ4Codec.Decode(
                entry.CompressedBody,
                0,
                entry.CompressedBody.Length,
                rented,
                0,
                entry.OriginalBodySize);
            if (decoded != entry.OriginalBodySize)
                throw new InvalidDataException($"LZ4 decompression failed: expected {entry.OriginalBodySize}, got {decoded}");

            return StateSnapshotSerializer.Deserialize(rented).ToFrameSnapshot(entry.Thumbnail);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private int FindWarmInsertIndex(long frame)
    {
        var low = _warmStart;
        var high = _warm.Count;
        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (_warm[mid].Frame <= frame)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }

    private int FindWarmEntryAtOrBefore(long targetFrame)
    {
        var low = _warmStart;
        var high = _warm.Count - 1;
        var best = -1;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var frame = _warm[mid].Frame;
            if (frame <= targetFrame)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return best;
    }

    private void TrimOldestWarmEntry()
    {
        if (_warmStart >= _warm.Count)
            return;

        _warmStart++;
        CompactWarmIfNeeded();
    }

    private void CompactWarmIfNeeded()
    {
        // Periodically compact to release dead prefix without per-eviction shifting.
        if (_warmStart == 0)
            return;
        if (_warmStart < 256 && _warmStart < _warm.Count / 2)
            return;

        _warm.RemoveRange(0, _warmStart);
        _warmStart = 0;
    }
}

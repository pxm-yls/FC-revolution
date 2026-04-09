using System.Collections.ObjectModel;
using System.Text;
using FCRevolution.Core.Mappers;
using FCRevolution.Core.PPU;
using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.Core.Sample.Managed;

public sealed class SampleManagedCoreModule : IManagedCoreModule
{
    public const string CoreId = "fc.sample.managed";

    public CoreManifest Manifest { get; } = new(
        CoreId,
        "FC-Revolution Sample Managed Core",
        "sample",
        "0.1.0",
        CoreBinaryKinds.ManagedDotNet);

    public IEmulatorCoreFactory CreateFactory() => new SampleManagedCoreFactory(Manifest);
}

internal sealed class SampleManagedCoreFactory : IEmulatorCoreFactory
{
    private readonly CoreManifest _manifest;

    public SampleManagedCoreFactory(CoreManifest manifest)
    {
        _manifest = manifest;
    }

    public IEmulatorCoreSession CreateSession(CoreSessionCreateOptions options) => new SampleManagedCoreSession(_manifest);
}

internal sealed class SampleManagedCoreSession : IEmulatorCoreSession
{
    private const int FrameWidth = 256;
    private const int FrameHeight = 240;
    private const int AudioSampleRate = 44100;
    private const int AudioSamplesPerFrame = 735;
    private const int MaxSnapshots = 120;
    private const double FrameDurationSeconds = 1.0 / 60.0;
    private static readonly IReadOnlyDictionary<string, byte> ActionBitMap = new ReadOnlyDictionary<string, byte>(
        new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = 0x01,
            ["b"] = 0x02,
            ["select"] = 0x04,
            ["start"] = 0x08,
            ["up"] = 0x10,
            ["down"] = 0x20,
            ["left"] = 0x40,
            ["right"] = 0x80
        });

    private readonly byte[] _memory = new byte[ushort.MaxValue + 1];
    private readonly Dictionary<Type, object> _capabilitiesByType = new();
    private readonly Dictionary<string, float> _inputValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CoreTimelineSnapshot> _snapshots = [];
    private string _loadedMediaPath = string.Empty;
    private long _frame;
    private long _presentationIndex;
    private bool _paused;
    private int _snapshotInterval = 5;

    public SampleManagedCoreSession(CoreManifest manifest)
    {
        RuntimeInfo = new CoreRuntimeInfo(
            manifest.CoreId,
            manifest.DisplayName,
            manifest.SystemId,
            manifest.Version,
            manifest.BinaryKind);
        Capabilities = CoreCapabilitySet.From(
            CoreCapabilityIds.VideoFrame,
            CoreCapabilityIds.AudioOutput,
            CoreCapabilityIds.SaveState,
            CoreCapabilityIds.MediaLoad,
            CoreCapabilityIds.InputSchema,
            CoreCapabilityIds.InputState,
            CoreCapabilityIds.TimeTravel,
            CoreCapabilityIds.DebugMemory,
            CoreCapabilityIds.DebugRegisters,
            CoreCapabilityIds.SystemNesRenderState);
        InputSchema = SampleInputSchema.Instance;
        _capabilitiesByType[typeof(ICoreDebugSurface)] = new SampleCoreDebugSurface(this);
        _capabilitiesByType[typeof(ICoreInputStateWriter)] = new SampleCoreInputStateWriter(this);
        _capabilitiesByType[typeof(ITimeTravelService)] = new SampleTimeTravelService(this);
        _capabilitiesByType[typeof(INesRenderStateProvider)] = new SampleRenderStateProvider();
        ResetRuntimeState();
    }

    public event Action<VideoFramePacket>? VideoFrameReady;

    public event Action<AudioPacket>? AudioReady;

    public CoreRuntimeInfo RuntimeInfo { get; }

    public CoreCapabilitySet Capabilities { get; }

    public IInputSchema InputSchema { get; }

    public CoreLoadResult LoadMedia(CoreMediaLoadRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (!File.Exists(request.MediaPath))
            return CoreLoadResult.Fail($"Media file not found: {request.MediaPath}");

        _loadedMediaPath = request.MediaPath;
        ResetRuntimeState();
        PrimeMemoryFromMedia(request.MediaPath);
        CaptureSnapshot();
        PublishCurrentFrame();
        return CoreLoadResult.Ok();
    }

    public void Reset()
    {
        ResetRuntimeState();
        if (!string.IsNullOrWhiteSpace(_loadedMediaPath) && File.Exists(_loadedMediaPath))
            PrimeMemoryFromMedia(_loadedMediaPath);
        CaptureSnapshot();
        PublishCurrentFrame();
    }

    public void Pause() => _paused = true;

    public void Resume() => _paused = false;

    public CoreStepResult RunFrame()
    {
        try
        {
            AdvanceFrame(emitEvents: true, captureSnapshot: true);
            return CoreStepResult.Ok(_presentationIndex);
        }
        catch (Exception ex)
        {
            return CoreStepResult.Fail(ex.Message);
        }
    }

    public CoreStepResult StepInstruction() => RunFrame();

    public CoreStateBlob CaptureState(bool includeThumbnail = false) => new()
    {
        Format = "sample/fcrs",
        Data = SerializeState(includeThumbnail ? BuildFramePixels() : null)
    };

    public void RestoreState(CoreStateBlob state)
    {
        ArgumentNullException.ThrowIfNull(state);
        DeserializeState(state.Data);
        PublishCurrentFrame();
    }

    public bool TryGetCapability<TCapability>(out TCapability capability)
        where TCapability : class
    {
        if (_capabilitiesByType.TryGetValue(typeof(TCapability), out var resolved) &&
            resolved is TCapability typed)
        {
            capability = typed;
            return true;
        }

        capability = null!;
        return false;
    }

    public void Dispose()
    {
    }

    internal long CurrentFrame => _frame;

    internal double CurrentTimestampSeconds => _frame * FrameDurationSeconds;

    internal int SnapshotInterval
    {
        get => _snapshotInterval;
        set => _snapshotInterval = Math.Max(1, value);
    }

    internal int HotCacheCount => _snapshots.Count;

    internal int WarmCacheCount => 0;

    internal long NewestFrame => _snapshots.Count == 0 ? 0 : _snapshots[^1].Frame;

    internal IReadOnlyList<CoreTimelineThumbnail> GetThumbnails() =>
        _snapshots
            .Select(snapshot => new CoreTimelineThumbnail(snapshot.Frame, (uint[])snapshot.Thumbnail.Clone()))
            .ToList();

    internal CoreBranchPoint CreateBranch(string name, uint[] frameBuffer)
    {
        var snapshot = GetNearestSnapshot(_frame) ?? CaptureSnapshot(frameBuffer);
        return new CoreBranchPoint
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Sample Branch" : name,
            RomPath = _loadedMediaPath,
            Frame = snapshot.Frame,
            TimestampSeconds = snapshot.TimestampSeconds,
            Snapshot = snapshot,
            CreatedAt = DateTime.UtcNow
        };
    }

    internal void RestoreSnapshot(CoreTimelineSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        RestoreState(snapshot.State);
    }

    internal long SeekToFrame(long frame)
    {
        var normalized = Math.Max(0, frame);
        var snapshot = GetNearestSnapshot(normalized);
        if (snapshot is null)
        {
            ResetRuntimeState();
            if (!string.IsNullOrWhiteSpace(_loadedMediaPath) && File.Exists(_loadedMediaPath))
                PrimeMemoryFromMedia(_loadedMediaPath);
        }
        else
        {
            RestoreState(snapshot.State);
        }

        while (_frame < normalized)
            AdvanceFrame(emitEvents: false, captureSnapshot: false);

        PublishCurrentFrame();
        return _frame;
    }

    internal long RewindFrames(int frames) => SeekToFrame(Math.Max(0, _frame - Math.Max(0, frames)));

    internal CoreTimelineSnapshot? GetNearestSnapshot(long frame)
    {
        if (_snapshots.Count == 0)
            return null;

        return _snapshots
            .OrderBy(snapshot => Math.Abs(snapshot.Frame - frame))
            .ThenByDescending(snapshot => snapshot.Frame)
            .FirstOrDefault();
    }

    internal CoreStateBlob? GetNearestState(long frame, bool includeThumbnail = false)
    {
        var snapshot = GetNearestSnapshot(frame);
        if (snapshot is null)
            return null;

        return includeThumbnail
            ? new CoreStateBlob
            {
                Format = snapshot.State.Format,
                Data = SerializeState(snapshot.Thumbnail)
            }
            : snapshot.State;
    }

    internal CoreDebugState CaptureDebugState() => new()
    {
        A = (byte)(_frame & 0xFF),
        X = _memory[0x10],
        Y = _memory[0x11],
        S = 0xFF,
        PC = (ushort)(_frame & 0xFFFF),
        P = 0x24,
        TotalCycles = (ulong)(_frame * 341),
        PpuScanline = (int)(_frame % FrameHeight),
        PpuCycle = (int)((_frame * 3) % 341),
        PpuFrame = _frame,
        PpuCtrl = 0,
        PpuMask = 0,
        PpuStatus = 0,
        FlagLine = "Nv-bdizc",
        CycleLine = $"sample frame {_frame}"
    };

    internal byte ReadMemory(ushort address) => _memory[address];

    internal void WriteMemory(ushort address, byte value) => _memory[address] = value;

    internal byte[] ReadMemoryBlock(ushort startAddress, int length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");

        var values = new byte[length];
        for (var index = 0; index < length; index++)
            values[index] = _memory[unchecked((ushort)(startAddress + index))];
        return values;
    }

    internal void SetInputState(string portId, string actionId, float value)
    {
        var key = $"{portId}:{actionId}";
        _inputValues[key] = value;
        if (ActionBitMap.TryGetValue(actionId, out var bit))
        {
            var inputAddress = string.Equals(portId, "p2", StringComparison.OrdinalIgnoreCase) ? 0x4017 : 0x4016;
            if (value >= 0.5f)
                _memory[inputAddress] |= bit;
            else
                _memory[inputAddress] &= unchecked((byte)~bit);
        }
    }

    private void ResetRuntimeState()
    {
        _frame = 0;
        _presentationIndex = 0;
        _paused = false;
        Array.Clear(_memory);
        _inputValues.Clear();
        _snapshots.Clear();
    }

    private void PrimeMemoryFromMedia(string mediaPath)
    {
        var bytes = File.ReadAllBytes(mediaPath);
        if (bytes.Length == 0)
            return;

        var copyLength = Math.Min(bytes.Length, _memory.Length);
        Array.Copy(bytes, _memory, copyLength);
        _memory[0] = (byte)copyLength;
        _memory[1] = bytes[0];
        _memory[2] = bytes[^1];
    }

    private void AdvanceFrame(bool emitEvents, bool captureSnapshot)
    {
        if (!_paused)
            _frame++;

        _memory[0x20] = (byte)(_frame & 0xFF);
        _memory[0x21] = (byte)((_frame >> 8) & 0xFF);

        if (captureSnapshot && (_snapshots.Count == 0 || _frame % SnapshotInterval == 0))
            CaptureSnapshot();

        if (emitEvents)
            PublishCurrentFrame();
    }

    private void PublishCurrentFrame()
    {
        var pixels = BuildFramePixels();
        _presentationIndex++;
        VideoFrameReady?.Invoke(new VideoFramePacket
        {
            Pixels = pixels,
            Width = FrameWidth,
            Height = FrameHeight,
            PixelFormat = "argb32",
            PresentationIndex = _presentationIndex,
            TimestampSeconds = CurrentTimestampSeconds
        });
        AudioReady?.Invoke(new AudioPacket
        {
            Samples = new float[AudioSamplesPerFrame],
            SampleRate = AudioSampleRate,
            Channels = 1,
            SampleFormat = "f32",
            SampleCount = AudioSamplesPerFrame,
            TimestampSeconds = CurrentTimestampSeconds
        });
    }

    private uint[] BuildFramePixels()
    {
        var pixels = new uint[FrameWidth * FrameHeight];
        var mediaSeed = string.IsNullOrWhiteSpace(_loadedMediaPath)
            ? 0
            : Path.GetFileNameWithoutExtension(_loadedMediaPath).Aggregate(0, (current, ch) => current + ch);
        var inputSeed = _inputValues.Where(entry => entry.Value >= 0.5f).Aggregate(0, (current, entry) => current + entry.Key[0]);
        var redBase = (byte)((mediaSeed + _frame * 3) % 255);
        var greenBase = (byte)((inputSeed + _frame * 5) % 255);
        var blueBase = (byte)((96 + _frame * 7) % 255);

        for (var y = 0; y < FrameHeight; y++)
        {
            for (var x = 0; x < FrameWidth; x++)
            {
                var red = (byte)((redBase + x) % 255);
                var green = (byte)((greenBase + y) % 255);
                var blue = (byte)((blueBase + (x / 8) + (y / 8)) % 255);
                pixels[(y * FrameWidth) + x] = 0xFF000000u | ((uint)red << 16) | ((uint)green << 8) | blue;
            }
        }

        return pixels;
    }

    private CoreTimelineSnapshot CaptureSnapshot(uint[]? thumbnailOverride = null)
    {
        var snapshot = new CoreTimelineSnapshot
        {
            Frame = _frame,
            TimestampSeconds = CurrentTimestampSeconds,
            Thumbnail = thumbnailOverride is null ? BuildFramePixels() : (uint[])thumbnailOverride.Clone(),
            State = new CoreStateBlob
            {
                Format = "sample/fcrs",
                Data = SerializeState()
            }
        };

        _snapshots.RemoveAll(existing => existing.Frame == snapshot.Frame);
        _snapshots.Add(snapshot);
        if (_snapshots.Count > MaxSnapshots)
            _snapshots.RemoveRange(0, _snapshots.Count - MaxSnapshots);
        return snapshot;
    }

    private byte[] SerializeState(uint[]? thumbnail = null)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(1);
        writer.Write(_frame);
        writer.Write(_presentationIndex);
        writer.Write(_paused);
        writer.Write(_snapshotInterval);
        writer.Write(_loadedMediaPath ?? string.Empty);
        writer.Write(_memory.Length);
        writer.Write(_memory);
        writer.Write(_inputValues.Count);
        foreach (var entry in _inputValues.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            writer.Write(entry.Key);
            writer.Write(entry.Value);
        }

        var pixels = thumbnail ?? Array.Empty<uint>();
        writer.Write(pixels.Length);
        foreach (var pixel in pixels)
            writer.Write(pixel);

        writer.Flush();
        return stream.ToArray();
    }

    private void DeserializeState(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var version = reader.ReadInt32();
        if (version != 1)
            throw new InvalidOperationException($"Unsupported sample core state version: {version}");

        _frame = reader.ReadInt64();
        _presentationIndex = reader.ReadInt64();
        _paused = reader.ReadBoolean();
        _snapshotInterval = Math.Max(1, reader.ReadInt32());
        _loadedMediaPath = reader.ReadString();
        var memoryLength = reader.ReadInt32();
        var storedMemory = reader.ReadBytes(memoryLength);
        Array.Clear(_memory);
        Array.Copy(storedMemory, _memory, Math.Min(storedMemory.Length, _memory.Length));
        _inputValues.Clear();
        var inputCount = reader.ReadInt32();
        for (var index = 0; index < inputCount; index++)
            _inputValues[reader.ReadString()] = reader.ReadSingle();

        _ = reader.ReadInt32();
    }

    private sealed class SampleCoreDebugSurface : ICoreDebugSurface
    {
        private readonly SampleManagedCoreSession _session;

        public SampleCoreDebugSurface(SampleManagedCoreSession session)
        {
            _session = session;
        }

        public CoreDebugState CaptureDebugState() => _session.CaptureDebugState();

        public byte ReadMemory(ushort address) => _session.ReadMemory(address);

        public void WriteMemory(ushort address, byte value) => _session.WriteMemory(address, value);

        public byte[] ReadMemoryBlock(ushort startAddress, int length) => _session.ReadMemoryBlock(startAddress, length);
    }

    private sealed class SampleCoreInputStateWriter : ICoreInputStateWriter
    {
        private readonly SampleManagedCoreSession _session;

        public SampleCoreInputStateWriter(SampleManagedCoreSession session)
        {
            _session = session;
        }

        public void SetInputState(string portId, string actionId, float value) => _session.SetInputState(portId, actionId, value);
    }

    private sealed class SampleTimeTravelService : ITimeTravelService
    {
        private readonly SampleManagedCoreSession _session;

        public SampleTimeTravelService(SampleManagedCoreSession session)
        {
            _session = session;
        }

        public long CurrentFrame => _session.CurrentFrame;

        public double CurrentTimestampSeconds => _session.CurrentTimestampSeconds;

        public int SnapshotInterval
        {
            get => _session.SnapshotInterval;
            set => _session.SnapshotInterval = value;
        }

        public int HotCacheCount => _session.HotCacheCount;

        public int WarmCacheCount => _session.WarmCacheCount;

        public long NewestFrame => _session.NewestFrame;

        public CoreTimeTravelCacheInfo GetCacheInfo() => new(
            HotCacheCount,
            WarmCacheCount,
            NewestFrame,
            SnapshotInterval);

        public IReadOnlyList<CoreTimelineThumbnail> GetThumbnails() => _session.GetThumbnails();

        public CoreBranchPoint CreateBranch(string name, uint[] frameBuffer) => _session.CreateBranch(name, frameBuffer);

        public void RestoreSnapshot(CoreTimelineSnapshot snapshot) => _session.RestoreSnapshot(snapshot);

        public long SeekToFrame(long frame) => _session.SeekToFrame(frame);

        public long RewindFrames(int frames) => _session.RewindFrames(frames);

        public CoreTimelineSnapshot? GetNearestSnapshot(long frame) => _session.GetNearestSnapshot(frame);

        public CoreStateBlob? GetNearestState(long frame, bool includeThumbnail = false) => _session.GetNearestState(frame, includeThumbnail);

        public void PauseRecording()
        {
        }

        public void ResumeRecording()
        {
        }
    }

    private sealed class SampleRenderStateProvider : INesRenderStateProvider
    {
        public PpuRenderStateSnapshot CaptureRenderStateSnapshot() => new()
        {
            NametableBytes = new byte[2048],
            PatternTableBytes = new byte[8192],
            PaletteColors = new uint[32],
            OamBytes = new byte[256],
            MirroringMode = MirroringMode.Horizontal,
            FineScrollX = 0,
            FineScrollY = 0,
            CoarseScrollX = 0,
            CoarseScrollY = 0,
            NametableSelect = 0,
            UseBackgroundPatternTableHighBank = false,
            UseSpritePatternTableHighBank = false,
            Use8x16Sprites = false,
            ShowBackground = true,
            ShowSprites = true,
            ShowBackgroundLeft8 = true,
            ShowSpritesLeft8 = true,
            HasCapturedBackgroundScanlineStates = false,
            BackgroundScanlineStates = new PpuBackgroundScanlineState[FrameHeight]
        };
    }
}

internal sealed class SampleInputSchema : IInputSchema
{
    public static SampleInputSchema Instance { get; } = new();

    public IReadOnlyList<InputPortDescriptor> Ports { get; } =
    [
        new("p1", "Player 1", 0),
        new("p2", "Player 2", 1)
    ];

    public IReadOnlyList<InputActionDescriptor> Actions { get; } =
    [
        new("a", "A", "p1", InputValueKind.Digital),
        new("b", "B", "p1", InputValueKind.Digital),
        new("select", "Select", "p1", InputValueKind.Digital),
        new("start", "Start", "p1", InputValueKind.Digital),
        new("up", "Up", "p1", InputValueKind.Digital),
        new("down", "Down", "p1", InputValueKind.Digital),
        new("left", "Left", "p1", InputValueKind.Digital),
        new("right", "Right", "p1", InputValueKind.Digital),
        new("a", "A", "p2", InputValueKind.Digital),
        new("b", "B", "p2", InputValueKind.Digital),
        new("select", "Select", "p2", InputValueKind.Digital),
        new("start", "Start", "p2", InputValueKind.Digital),
        new("up", "Up", "p2", InputValueKind.Digital),
        new("down", "Down", "p2", InputValueKind.Digital),
        new("left", "Left", "p2", InputValueKind.Digital),
        new("right", "Right", "p2", InputValueKind.Digital)
    ];
}

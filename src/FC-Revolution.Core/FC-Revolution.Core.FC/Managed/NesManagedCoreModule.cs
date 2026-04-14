using FCRevolution.Core.Input;
using FCRevolution.Core.Debug;
using FCRevolution.Core.State;
using FCRevolution.Core.Timeline;
using FCRevolution.Core.PPU;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Core.Nes.Managed.Adapters.Nes;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Common;

namespace FCRevolution.Core.Nes.Managed;

public sealed class NesManagedCoreModule : IManagedCoreModule
{
    public const string CoreId = "fc.nes.managed";

    public CoreManifest Manifest { get; } = new(
        CoreId,
        "FC-Revolution NES Managed Core",
        "nes",
        "0.1.0",
        CoreBinaryKinds.ManagedDotNet)
    {
        SupportedMediaFilePatterns = ["*.nes"]
    };

    public IEmulatorCoreFactory CreateFactory() => new NesManagedCoreFactory(Manifest);
}

internal sealed class NesManagedCoreFactory : IEmulatorCoreFactory
{
    private readonly CoreManifest _manifest;

    public NesManagedCoreFactory(CoreManifest manifest)
    {
        _manifest = manifest;
    }

    public IEmulatorCoreSession CreateSession(CoreSessionCreateOptions options) => new NesManagedCoreSession(_manifest);
}

internal sealed class NesManagedCoreSession : IEmulatorCoreSession
{
    private const int AudioSampleRate = 44744;

    private readonly NesConsole _console = new();
    private readonly Dictionary<Type, object> _capabilitiesByType = new();
    private long _videoPresentationIndex;

    public NesManagedCoreSession(CoreManifest manifest)
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
            CoreCapabilityIds.Disassembly,
            CoreCapabilityIds.LayeredFrame);
        InputSchema = NesInputSchema.Instance;
        _capabilitiesByType[typeof(ICoreDebugSurface)] = new NesCoreDebugSurface(_console);
        _capabilitiesByType[typeof(ICoreInputStateWriter)] = new NesCoreInputStateWriter(_console);
        _capabilitiesByType[typeof(ITimeTravelService)] = new NesTimeTravelService(_console);
        _capabilitiesByType[typeof(ILayeredFrameProvider)] = new NesLayeredFrameProvider(_console);
        _console.FrameReady += HandleFrameReady;
        _console.AudioChunkReady += HandleAudioReady;
    }

    public event Action<VideoFramePacket>? VideoFrameReady;

    public event Action<AudioPacket>? AudioReady;

    public CoreRuntimeInfo RuntimeInfo { get; }

    public CoreCapabilitySet Capabilities { get; }

    public IInputSchema InputSchema { get; }

    public CoreLoadResult LoadMedia(CoreMediaLoadRequest request)
    {
        try
        {
            _console.LoadRom(request.MediaPath);
            return CoreLoadResult.Ok();
        }
        catch (Exception ex)
        {
            return CoreLoadResult.Fail(ex.Message);
        }
    }

    public void Reset() => _console.Reset();

    public void Pause() => _console.Pause();

    public void Resume() => _console.Start();

    public CoreStepResult RunFrame()
    {
        try
        {
            _console.RunFrame();
            return CoreStepResult.Ok(_videoPresentationIndex);
        }
        catch (Exception ex)
        {
            return CoreStepResult.Fail(ex.Message);
        }
    }

    public CoreStepResult StepInstruction()
    {
        try
        {
            _console.StepInstruction();
            return CoreStepResult.Ok(_videoPresentationIndex);
        }
        catch (Exception ex)
        {
            return CoreStepResult.Fail(ex.Message);
        }
    }

    public CoreStateBlob CaptureState(bool includeThumbnail = false) => new()
    {
        Format = "nes/fcrs",
        Data = includeThumbnail
            ? FCRevolution.Core.State.StateSnapshotSerializer.Serialize(_console.CaptureSnapshot(_console.Ppu.FrameBuffer), includeThumbnail: true)
            : _console.SaveState()
    };

    public void RestoreState(CoreStateBlob state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _console.LoadState(state.Data);
    }

    public bool TryGetCapability<TCapability>(out TCapability capability)
        where TCapability : class
    {
        if (_capabilitiesByType.TryGetValue(typeof(TCapability), out var resolved) &&
            resolved is TCapability typedCapability)
        {
            capability = typedCapability;
            return true;
        }

        capability = null!;
        return false;
    }

    public void Dispose()
    {
        _console.FrameReady -= HandleFrameReady;
        _console.AudioChunkReady -= HandleAudioReady;
    }

    private void HandleFrameReady(uint[] frameBuffer)
    {
        _videoPresentationIndex++;
        VideoFrameReady?.Invoke(new VideoFramePacket
        {
            Pixels = (uint[])frameBuffer.Clone(),
            Width = NesConstants.ScreenWidth,
            Height = NesConstants.ScreenHeight,
            PixelFormat = "argb32",
            PresentationIndex = _videoPresentationIndex,
            TimestampSeconds = _console.CpuCycles / 1789773.0
        });
    }

    private void HandleAudioReady(float[] samples)
    {
        AudioReady?.Invoke(new AudioPacket
        {
            Samples = (float[])samples.Clone(),
            SampleRate = AudioSampleRate,
            Channels = 1,
            SampleFormat = "f32",
            SampleCount = samples.Length,
            TimestampSeconds = _console.CpuCycles / 1789773.0
        });
    }

    private sealed class NesCoreDebugSurface : ICoreDebugSurface
    {
        private readonly NesConsole _console;

        public NesCoreDebugSurface(NesConsole console)
        {
            _console = console;
        }

        public CoreDebugState CaptureDebugState() => MapDebugState(DebugState.Capture(_console));

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

        private static CoreDebugState MapDebugState(DebugState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            return new CoreDebugState
            {
                InstructionPointer = state.PC,
                InstructionPointerLabel = "PC",
                Sections =
                [
                    new CoreDebugSection(
                        "cpu-registers",
                        "CPU Registers",
                        "registers",
                        [
                            new CoreDebugValue("A", $"{state.A:X2}"),
                            new CoreDebugValue("X", $"{state.X:X2}"),
                            new CoreDebugValue("Y", $"{state.Y:X2}"),
                            new CoreDebugValue("S", $"{state.S:X2}")
                        ]),
                    new CoreDebugSection(
                        "cpu-status",
                        "CPU Status",
                        "registers",
                        [
                            new CoreDebugValue("PC", $"{state.PC:X4}"),
                            new CoreDebugValue("P", $"{(byte)state.P:X2}"),
                            new CoreDebugValue("Flags", state.FlagLine.Replace("FLAGS ", string.Empty, StringComparison.OrdinalIgnoreCase)),
                            new CoreDebugValue("Cycles", state.CycleLine.Replace("CPU ", string.Empty, StringComparison.OrdinalIgnoreCase))
                        ]),
                    new CoreDebugSection(
                        "video-timing",
                        "Video Timing",
                        "video",
                        [
                            new CoreDebugValue("Scanline", state.PpuScanline.ToString()),
                            new CoreDebugValue("Dot", state.PpuCycle.ToString()),
                            new CoreDebugValue("Frame", state.PpuFrame.ToString())
                        ]),
                    new CoreDebugSection(
                        "video-status",
                        "Video Status",
                        "video",
                        [
                            new CoreDebugValue("CTRL", $"{(byte)state.PpuCtrl:X2}"),
                            new CoreDebugValue("MASK", $"{(byte)state.PpuMask:X2}"),
                            new CoreDebugValue("STAT", $"{(byte)state.PpuStatus:X2}")
                        ])
                ]
            };
        }
    }

    private sealed class NesTimeTravelService : ITimeTravelService
    {
        private const double CpuClockRate = 1789773.0;
        private readonly NesConsole _console;

        public NesTimeTravelService(NesConsole console)
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

    private sealed class NesCoreInputStateWriter : ICoreInputStateWriter
    {
        private readonly NesConsole _console;

        public NesCoreInputStateWriter(NesConsole console)
        {
            _console = console;
        }

        public void SetInputState(string portId, string actionId, float value)
        {
            if (!TryResolvePlayer(portId, out var player) ||
                !TryResolveButton(actionId, out var button))
            {
                return;
            }

            _console.SetButton(player, button, value >= 0.5f);
        }

        private static bool TryResolvePlayer(string portId, out int player)
        {
            switch (portId.Trim().ToLowerInvariant())
            {
                case "p1":
                    player = 0;
                    return true;
                case "p2":
                    player = 1;
                    return true;
                default:
                    player = -1;
                    return false;
            }
        }

        private static bool TryResolveButton(string actionId, out NesButton button)
        {
            switch (actionId.Trim().ToLowerInvariant())
            {
                case "a":
                case "x":
                case "turboa":
                    button = NesButton.A;
                    return true;
                case "b":
                case "y":
                case "turbob":
                    button = NesButton.B;
                    return true;
                case "select":
                    button = NesButton.Select;
                    return true;
                case "start":
                    button = NesButton.Start;
                    return true;
                case "up":
                    button = NesButton.Up;
                    return true;
                case "down":
                    button = NesButton.Down;
                    return true;
                case "left":
                    button = NesButton.Left;
                    return true;
                case "right":
                    button = NesButton.Right;
                    return true;
                default:
                    button = default;
                    return false;
            }
        }
    }

    private sealed class NesLayeredFrameProvider : ILayeredFrameProvider
    {
        private readonly NesConsole _console;
        private IFrameMetadata? _previousFrameMetadata;

        public NesLayeredFrameProvider(NesConsole console)
        {
            _console = console;
        }

        public LayeredFrameData CaptureLayeredFrame()
        {
            var metadata = new RenderDataExtractor().Extract(
                NesRenderStateAdapter.Map(_console.Ppu.CaptureRenderStateSnapshot()),
                _previousFrameMetadata);
            _previousFrameMetadata = metadata;
            return LayeredFrameBuilder.Build(metadata);
        }

        public void ResetTemporalHistory()
        {
            _previousFrameMetadata = null;
        }
    }
    private sealed class NesInputSchema : IInputSchema
    {
        public static NesInputSchema Instance { get; } = new();

        public IReadOnlyList<InputPortDescriptor> Ports { get; } =
        [
            new("p1", "Player 1", 0),
            new("p2", "Player 2", 1)
        ];

        public IReadOnlyList<InputActionDescriptor> Actions { get; }

        private NesInputSchema()
        {
            Actions = BuildActions(Ports);
        }

        private static IReadOnlyList<InputActionDescriptor> BuildActions(IReadOnlyList<InputPortDescriptor> ports)
        {
            var actions = new List<InputActionDescriptor>();
            foreach (var port in ports)
            {
                actions.Add(new InputActionDescriptor("a", "A", port.PortId, InputValueKind.Digital, LegacyBitMask: 0x01));
                actions.Add(new InputActionDescriptor("b", "B", port.PortId, InputValueKind.Digital, LegacyBitMask: 0x02));
                actions.Add(new InputActionDescriptor("x", "X (Turbo A)", port.PortId, InputValueKind.Digital, CanonicalActionId: "a", IsBindable: false, LegacyBitMask: 0x01));
                actions.Add(new InputActionDescriptor("y", "Y (Turbo B)", port.PortId, InputValueKind.Digital, CanonicalActionId: "b", IsBindable: false, LegacyBitMask: 0x02));
                actions.Add(new InputActionDescriptor("select", "Select", port.PortId, InputValueKind.Digital, LegacyBitMask: 0x04));
                actions.Add(new InputActionDescriptor("start", "Start", port.PortId, InputValueKind.Digital, LegacyBitMask: 0x08));
                actions.Add(new InputActionDescriptor("up", "Up", port.PortId, InputValueKind.Digital, LegacyBitMask: 0x10));
                actions.Add(new InputActionDescriptor("down", "Down", port.PortId, InputValueKind.Digital, LegacyBitMask: 0x20));
                actions.Add(new InputActionDescriptor("left", "Left", port.PortId, InputValueKind.Digital, LegacyBitMask: 0x40));
                actions.Add(new InputActionDescriptor("right", "Right", port.PortId, InputValueKind.Digital, LegacyBitMask: 0x80));
                actions.Add(new InputActionDescriptor("l1", "L1 (Reserved)", port.PortId, InputValueKind.Digital, IsBindable: false));
                actions.Add(new InputActionDescriptor("r1", "R1 (Reserved)", port.PortId, InputValueKind.Digital, IsBindable: false));
                actions.Add(new InputActionDescriptor("l2", "L2 (Reserved)", port.PortId, InputValueKind.Digital, IsBindable: false));
                actions.Add(new InputActionDescriptor("r2", "R2 (Reserved)", port.PortId, InputValueKind.Digital, IsBindable: false));
                actions.Add(new InputActionDescriptor("l3", "L3 (Reserved)", port.PortId, InputValueKind.Digital, IsBindable: false));
                actions.Add(new InputActionDescriptor("r3", "R3 (Reserved)", port.PortId, InputValueKind.Digital, IsBindable: false));
            }

            return actions;
        }
    }
}

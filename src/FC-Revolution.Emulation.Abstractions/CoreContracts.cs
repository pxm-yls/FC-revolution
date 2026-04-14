using System.Collections.ObjectModel;

namespace FCRevolution.Emulation.Abstractions;

public static class CoreBinaryKinds
{
    public const string ManagedDotNet = "managed-dotnet";
    public const string NativeCabi = "native-cabi";
}

public static class CoreCapabilityIds
{
    public const string VideoFrame = "video-frame";
    public const string AudioOutput = "audio-output";
    public const string SaveState = "save-state";
    public const string MediaLoad = "media-load";
    public const string InputSchema = "input-schema";
    public const string InputState = "input-state";
    public const string TimeTravel = "time-travel";
    public const string DebugMemory = "debug-memory";
    public const string DebugRegisters = "debug-registers";
    public const string Disassembly = "disassembly";
    public const string LayeredFrame = "layered-frame";
}

public sealed record CoreManifest(
    string CoreId,
    string DisplayName,
    string SystemId,
    string Version,
    string BinaryKind)
{
    public IReadOnlyList<string> SupportedMediaFilePatterns { get; init; } = [];
}

public sealed record CoreRuntimeInfo(
    string CoreId,
    string DisplayName,
    string SystemId,
    string Version,
    string BinaryKind);

public sealed record CoreSessionCreateOptions(string? PreferredCoreId = null);

public sealed record CoreMediaLoadRequest(string MediaPath);

public sealed record CoreLoadResult(bool Success, string? ErrorMessage = null)
{
    public static CoreLoadResult Ok() => new(true);

    public static CoreLoadResult Fail(string errorMessage) => new(false, errorMessage);
}

public sealed record CoreStepResult(bool Success, long PresentationIndex = -1, string? ErrorMessage = null)
{
    public static CoreStepResult Ok(long presentationIndex = -1) => new(true, presentationIndex);

    public static CoreStepResult Fail(string errorMessage) => new(false, -1, errorMessage);
}

public sealed class CoreStateBlob
{
    public required string Format { get; init; }

    public required byte[] Data { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));
}

public sealed class VideoFramePacket
{
    public required uint[] Pixels { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public required string PixelFormat { get; init; }

    public required long PresentationIndex { get; init; }

    public required double TimestampSeconds { get; init; }
}

public sealed class AudioPacket
{
    public required float[] Samples { get; init; }

    public required int SampleRate { get; init; }

    public required int Channels { get; init; }

    public required string SampleFormat { get; init; }

    public required int SampleCount { get; init; }

    public required double TimestampSeconds { get; init; }
}

public enum InputValueKind
{
    Digital,
    Analog,
    RelativeAxis
}

public sealed record InputPortDescriptor(string PortId, string DisplayName, int PlayerIndex);

public sealed record InputActionDescriptor(
    string ActionId,
    string DisplayName,
    string PortId,
    InputValueKind ValueKind,
    string? CanonicalActionId = null,
    bool IsBindable = true,
    byte? LegacyBitMask = null)
{
    public string? ResolvedCanonicalActionId =>
        string.IsNullOrWhiteSpace(CanonicalActionId)
            ? (IsBindable ? ActionId : null)
            : CanonicalActionId;
}

public interface IInputSchema
{
    IReadOnlyList<InputPortDescriptor> Ports { get; }

    IReadOnlyList<InputActionDescriptor> Actions { get; }
}

public sealed class CoreCapabilitySet
{
    private readonly HashSet<string> _capabilities;

    public CoreCapabilitySet(IEnumerable<string> capabilityIds)
    {
        _capabilities = new HashSet<string>(capabilityIds, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> Ids => _capabilities;

    public bool Supports(string capabilityId) => _capabilities.Contains(capabilityId);

    public static CoreCapabilitySet From(params string[] capabilityIds) => new(capabilityIds);
}

public sealed record CoreTimelineThumbnail(long Frame, uint[] Thumbnail);

public sealed record CoreTimeTravelCacheInfo(int HotCount, int WarmCount, long NewestFrame, int SnapshotInterval);

public sealed class CoreTimelineSnapshot
{
    public required long Frame { get; init; }

    public required double TimestampSeconds { get; init; }

    public required uint[] Thumbnail { get; init; }

    public required CoreStateBlob State { get; init; }
}

public sealed class CoreBranchPoint
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = "Branch";

    public string RomPath { get; set; } = string.Empty;

    public required long Frame { get; init; }

    public required double TimestampSeconds { get; init; }

    public required CoreTimelineSnapshot Snapshot { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public List<CoreBranchPoint> Children { get; } = [];
}

public interface ICoreDebugSurface
{
    CoreDebugState CaptureDebugState();

    byte ReadMemory(ushort address);

    void WriteMemory(ushort address, byte value);

    byte[] ReadMemoryBlock(ushort startAddress, int length);
}

public interface ICoreInputStateWriter
{
    void SetInputState(string portId, string actionId, float value);
}

public interface ITimeTravelService
{
    long CurrentFrame { get; }

    double CurrentTimestampSeconds { get; }

    int SnapshotInterval { get; set; }

    int HotCacheCount { get; }

    int WarmCacheCount { get; }

    long NewestFrame { get; }

    CoreTimeTravelCacheInfo GetCacheInfo();

    IReadOnlyList<CoreTimelineThumbnail> GetThumbnails();

    CoreBranchPoint CreateBranch(string name, uint[] frameBuffer);

    void RestoreSnapshot(CoreTimelineSnapshot snapshot);

    long SeekToFrame(long frame);

    long RewindFrames(int frames);

    CoreTimelineSnapshot? GetNearestSnapshot(long frame);

    CoreStateBlob? GetNearestState(long frame, bool includeThumbnail = false);

    void PauseRecording();

    void ResumeRecording();
}

public interface IManagedCoreModule
{
    CoreManifest Manifest { get; }

    IEmulatorCoreFactory CreateFactory();
}

public interface IEmulatorCoreFactory
{
    IEmulatorCoreSession CreateSession(CoreSessionCreateOptions options);
}

public interface IEmulatorCoreSession : IDisposable
{
    event Action<VideoFramePacket>? VideoFrameReady;

    event Action<AudioPacket>? AudioReady;

    CoreRuntimeInfo RuntimeInfo { get; }

    CoreCapabilitySet Capabilities { get; }

    IInputSchema InputSchema { get; }

    CoreLoadResult LoadMedia(CoreMediaLoadRequest request);

    void Reset();

    void Pause();

    void Resume();

    CoreStepResult RunFrame();

    CoreStepResult StepInstruction();

    CoreStateBlob CaptureState(bool includeThumbnail = false);

    void RestoreState(CoreStateBlob state);

    bool TryGetCapability<TCapability>(out TCapability capability)
        where TCapability : class;
}

using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.Emulation.Host;

internal sealed class UnavailableEmulatorCoreSession : IEmulatorCoreSession, ITimeTravelService, ICoreInputStateWriter
{
    private const string EmptyCoreId = "fc.none";
    private readonly string _errorMessage;

    public UnavailableEmulatorCoreSession(string? requestedCoreId = null, string? errorMessage = null)
    {
        _errorMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? "当前没有可用核心，请先安装、导入或启用模拟器核心。"
            : errorMessage!;
        RuntimeInfo = new CoreRuntimeInfo(
            string.IsNullOrWhiteSpace(requestedCoreId) ? EmptyCoreId : requestedCoreId,
            "No Core Installed",
            "none",
            "0.0.0",
            "unavailable");
        Capabilities = CoreCapabilitySet.From(
            CoreCapabilityIds.InputState,
            CoreCapabilityIds.TimeTravel,
            CoreCapabilityIds.MediaLoad,
            CoreCapabilityIds.SaveState);
    }

    public event Action<VideoFramePacket>? VideoFrameReady
    {
        add { }
        remove { }
    }

    public event Action<AudioPacket>? AudioReady
    {
        add { }
        remove { }
    }

    public CoreRuntimeInfo RuntimeInfo { get; }

    public CoreCapabilitySet Capabilities { get; }

    public IInputSchema InputSchema { get; } = new EmptyInputSchema();

    public int SnapshotInterval { get; set; } = 1;

    public long CurrentFrame => 0;

    public double CurrentTimestampSeconds => 0;

    public int HotCacheCount => 0;

    public int WarmCacheCount => 0;

    public long NewestFrame => 0;

    public CoreLoadResult LoadMedia(CoreMediaLoadRequest request) => CoreLoadResult.Fail(_errorMessage);

    public void Reset()
    {
    }

    public void Pause()
    {
    }

    public void Resume()
    {
    }

    public CoreStepResult RunFrame() => CoreStepResult.Fail(_errorMessage);

    public CoreStepResult StepInstruction() => CoreStepResult.Fail(_errorMessage);

    public CoreStateBlob CaptureState(bool includeThumbnail = false) => new()
    {
        Format = "unavailable-session",
        Data = []
    };

    public void RestoreState(CoreStateBlob state)
    {
    }

    public bool TryGetCapability<TCapability>(out TCapability capability)
        where TCapability : class
    {
        if (typeof(TCapability) == typeof(ITimeTravelService))
        {
            capability = (TCapability)(object)this;
            return true;
        }

        if (typeof(TCapability) == typeof(ICoreInputStateWriter))
        {
            capability = (TCapability)(object)this;
            return true;
        }

        capability = null!;
        return false;
    }

    public void Dispose()
    {
    }

    public CoreTimeTravelCacheInfo GetCacheInfo() => new(0, 0, 0, SnapshotInterval);

    public IReadOnlyList<CoreTimelineThumbnail> GetThumbnails() => [];

    public CoreBranchPoint CreateBranch(string name, uint[] frameBuffer) => new()
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Branch" : name,
        RomPath = string.Empty,
        Frame = 0,
        TimestampSeconds = 0,
        Snapshot = new CoreTimelineSnapshot
        {
            Frame = 0,
            TimestampSeconds = 0,
            Thumbnail = frameBuffer.Length == 0 ? [] : [.. frameBuffer],
            State = CaptureState(includeThumbnail: true)
        }
    };

    public void RestoreSnapshot(CoreTimelineSnapshot snapshot)
    {
    }

    public long SeekToFrame(long frame) => 0;

    public long RewindFrames(int frames) => 0;

    public CoreTimelineSnapshot? GetNearestSnapshot(long frame) => null;

    public CoreStateBlob? GetNearestState(long frame, bool includeThumbnail = false) => CaptureState(includeThumbnail);

    public void PauseRecording()
    {
    }

    public void ResumeRecording()
    {
    }

    public void SetInputState(string portId, string actionId, float value)
    {
    }

    private sealed class EmptyInputSchema : IInputSchema
    {
        public IReadOnlyList<InputPortDescriptor> Ports { get; } = [];

        public IReadOnlyList<InputActionDescriptor> Actions { get; } = [];
    }
}

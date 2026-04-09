using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.Tests;

public sealed class CoreSessionCapabilityResolverTests
{
    [Fact]
    public void ResolveTimeTravelService_PrefersCapability()
    {
        var session = new FakeEmulatorCoreSession("fake.core");
        var expected = new FakeTimeTravelService();
        session.AddCapability<ITimeTravelService>(expected);

        var resolved = CoreSessionCapabilityResolver.ResolveTimeTravelService(session);

        Assert.Same(expected, resolved);
    }

    [Fact]
    public void ResolveInputStateWriter_UsesLegacyFallbackWhenCapabilityMissing()
    {
        var session = new FakeEmulatorCoreSession("fake.core");

        var ex = Assert.Throws<InvalidOperationException>(
            () => CoreSessionCapabilityResolver.ResolveInputStateWriter(session));

        Assert.Contains("fake.core", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ICoreInputStateWriter), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveDebugSurface_WithoutCapabilityOrLegacy_ThrowsClearError()
    {
        var session = new FakeEmulatorCoreSession("missing.core");

        var ex = Assert.Throws<InvalidOperationException>(
            () => CoreSessionCapabilityResolver.ResolveDebugSurface(session));

        Assert.Contains("missing.core", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ICoreDebugSurface), ex.Message, StringComparison.Ordinal);
    }

    private sealed class FakeEmulatorCoreSession : IEmulatorCoreSession
    {
        private readonly Dictionary<Type, object> _capabilities = [];

        public FakeEmulatorCoreSession(string coreId)
        {
            RuntimeInfo = new CoreRuntimeInfo(
                coreId,
                "Fake Core",
                "fake",
                "0.0.1",
                CoreBinaryKinds.ManagedDotNet);
            Capabilities = CoreCapabilitySet.From();
            InputSchema = new FakeInputSchema();
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

        public IInputSchema InputSchema { get; }

        public void AddCapability<TCapability>(TCapability capability)
            where TCapability : class
        {
            _capabilities[typeof(TCapability)] = capability;
        }

        public CoreLoadResult LoadMedia(CoreMediaLoadRequest request) => CoreLoadResult.Ok();

        public void Reset()
        {
        }

        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public CoreStepResult RunFrame() => CoreStepResult.Ok();

        public CoreStepResult StepInstruction() => CoreStepResult.Ok();

        public CoreStateBlob CaptureState(bool includeThumbnail = false) => new()
        {
            Format = "fake/state",
            Data = []
        };

        public void RestoreState(CoreStateBlob state)
        {
        }

        public bool TryGetCapability<TCapability>(out TCapability capability)
            where TCapability : class
        {
            if (_capabilities.TryGetValue(typeof(TCapability), out var resolved) &&
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
    }

    private sealed class FakeInputSchema : IInputSchema
    {
        public IReadOnlyList<InputPortDescriptor> Ports { get; } = [];

        public IReadOnlyList<InputActionDescriptor> Actions { get; } = [];
    }

    private sealed class FakeTimeTravelService : ITimeTravelService
    {
        public long CurrentFrame => 0;

        public double CurrentTimestampSeconds => 0;

        public int SnapshotInterval { get; set; } = 5;

        public int HotCacheCount => 0;

        public int WarmCacheCount => 0;

        public long NewestFrame => 0;

        public CoreTimeTravelCacheInfo GetCacheInfo() => new(0, 0, 0, SnapshotInterval);

        public IReadOnlyList<CoreTimelineThumbnail> GetThumbnails() => [];

        public CoreBranchPoint CreateBranch(string name, uint[] frameBuffer) =>
            throw new NotSupportedException();

        public void RestoreSnapshot(CoreTimelineSnapshot snapshot) =>
            throw new NotSupportedException();

        public long SeekToFrame(long frame) => throw new NotSupportedException();

        public long RewindFrames(int frames) => throw new NotSupportedException();

        public CoreTimelineSnapshot? GetNearestSnapshot(long frame) => null;

        public CoreStateBlob? GetNearestState(long frame, bool includeThumbnail = false) => null;

        public void PauseRecording()
        {
        }

        public void ResumeRecording()
        {
        }
    }
}

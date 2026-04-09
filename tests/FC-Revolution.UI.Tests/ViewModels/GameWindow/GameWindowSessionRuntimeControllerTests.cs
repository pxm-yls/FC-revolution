using FCRevolution.Core.Debug;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowSessionRuntimeControllerTests
{
    [Fact]
    public void TogglePause_TransitionsBetweenPausedAndResumed()
    {
        var session = new FakeCoreSession();
        var controller = CreateController(session, new FakeDebugSurface());

        Assert.False(controller.IsPaused);

        var paused = controller.TogglePause();
        Assert.True(paused);
        Assert.True(controller.IsPaused);
        Assert.Equal(1, session.PauseCount);

        var resumed = controller.TogglePause();
        Assert.False(resumed);
        Assert.False(controller.IsPaused);
        Assert.Equal(1, session.ResumeCount);
    }

    [Fact]
    public void RunFrame_AppliesLockedMemoryBeforeAndAfterSuccessfulFrame()
    {
        var session = new FakeCoreSession();
        var debugSurface = new FakeDebugSurface();
        var controller = CreateController(session, debugSurface);
        var decision = GameWindowModifiedMemoryLockStateController.BuildReplaceDecision(
            [new ModifiedMemoryRuntimeEntry(0x4016, 0x7F, IsLocked: true)]);

        controller.ReplaceModifiedMemoryEntries(decision);
        debugSurface.ClearWrites();

        var result = controller.RunFrame();

        Assert.True(result.Success);
        Assert.Equal(1, session.RunFrameCount);
        Assert.Equal(2, debugSurface.Writes.Count);
        Assert.All(debugSurface.Writes, write =>
        {
            Assert.Equal((ushort)0x4016, write.Address);
            Assert.Equal((byte)0x7F, write.Value);
        });
    }

    [Fact]
    public void CaptureDebugRefreshSnapshot_ReadsRequestedSlicesFromDebugSurface()
    {
        var expectedMemoryStart = (ushort)(2 * DebugViewModel.MemoryPageSize);
        var expectedStackStart = (ushort)(0x0100 + DebugViewModel.StackPageSize);
        var expectedZeroPageStart = (ushort)DebugViewModel.ZeroPageSliceSize;

        var debugSurface = new FakeDebugSurface
        {
            State = new CoreDebugState { PC = 0x8000 }
        };
        debugSurface.SeedRange(expectedMemoryStart, DebugViewModel.MemoryPageSize, 0x10);
        debugSurface.SeedRange(expectedStackStart, DebugViewModel.StackPageSize, 0x20);
        debugSurface.SeedRange(expectedZeroPageStart, DebugViewModel.ZeroPageSliceSize, 0x30);
        debugSurface.SeedRange(0x8000, DebugViewModel.DisasmPageSize, 0x40);

        var controller = CreateController(new FakeCoreSession(), debugSurface);

        var snapshot = controller.CaptureDebugRefreshSnapshot(new DebugRefreshRequest
        {
            MemoryPageIndex = 2,
            StackPageIndex = 1,
            ZeroPageSliceIndex = 1,
            DisasmPageIndex = 0,
            CaptureMemoryPage = true,
            CaptureStack = true,
            CaptureZeroPage = true,
            CaptureDisasm = true
        });

        Assert.Equal(expectedMemoryStart, snapshot.MemoryPageStart);
        Assert.Equal(expectedStackStart, snapshot.StackPageStart);
        Assert.Equal(expectedZeroPageStart, snapshot.ZeroPageStart);
        Assert.Equal((ushort)0x8000, snapshot.DisasmStart);
        Assert.Equal((byte)0x10, snapshot.MemoryPage[0]);
        Assert.Equal((byte)0x20, snapshot.StackPage[0]);
        Assert.Equal((byte)0x30, snapshot.ZeroPage[0]);
        Assert.Equal((byte)0x40, snapshot.Disasm[0]);
        Assert.Equal((ushort)0x8000, snapshot.State.PC);
    }

    [Fact]
    public void TimelineAccess_DelegatesThroughController()
    {
        var timeTravel = new FakeTimeTravelService
        {
            CurrentFrame = 120,
            NewestFrame = 180,
            CurrentTimestampSeconds = 2.5,
            SnapshotInterval = 6,
            SeekResult = 42
        };
        var snapshot = new CoreTimelineSnapshot
        {
            Frame = 12,
            TimestampSeconds = 0.2,
            Thumbnail = [1u, 2u],
            State = new CoreStateBlob
            {
                Format = "test/state",
                Data = [1, 2, 3]
            }
        };
        timeTravel.Snapshots[12] = snapshot;
        var controller = CreateController(new FakeCoreSession(), new FakeDebugSurface(), timeTravel);

        var position = controller.CaptureTimelinePosition();
        controller.PauseRecording();
        controller.ResumeRecording();
        var nearest = controller.GetNearestSnapshot(12);
        var landed = controller.SeekToFrame(12);

        Assert.Equal(120, position.CurrentFrame);
        Assert.Equal(180, position.NewestFrame);
        Assert.Equal(2.5, position.TimestampSeconds);
        Assert.Equal(6, controller.SnapshotInterval);
        Assert.Same(snapshot, nearest);
        Assert.Equal(42, landed);
        Assert.Equal(1, timeTravel.PauseRecordingCount);
        Assert.Equal(1, timeTravel.ResumeRecordingCount);
        Assert.Equal(12, timeTravel.LastSeekFrame);
    }

    private static GameWindowSessionRuntimeController CreateController(
        FakeCoreSession session,
        FakeDebugSurface debugSurface,
        FakeTimeTravelService? timeTravel = null,
        FakeInputStateWriter? inputStateWriter = null) =>
        new(
            new object(),
            session,
            debugSurface,
            timeTravel ?? new FakeTimeTravelService(),
            inputStateWriter ?? new FakeInputStateWriter());

    private sealed class FakeDebugSurface : ICoreDebugSurface
    {
        private readonly Dictionary<ushort, byte> _memory = [];

        public CoreDebugState State { get; set; } = new();

        public List<(ushort Address, byte Value)> Writes { get; } = [];

        public CoreDebugState CaptureDebugState() => State;

        public byte ReadMemory(ushort address) =>
            _memory.TryGetValue(address, out var value) ? value : (byte)0;

        public void WriteMemory(ushort address, byte value)
        {
            _memory[address] = value;
            Writes.Add((address, value));
        }

        public byte[] ReadMemoryBlock(ushort startAddress, int length)
        {
            var values = new byte[length];
            for (var index = 0; index < length; index++)
                values[index] = ReadMemory(unchecked((ushort)(startAddress + index)));

            return values;
        }

        public void SeedRange(ushort startAddress, int length, byte startValue)
        {
            for (var index = 0; index < length; index++)
                _memory[unchecked((ushort)(startAddress + index))] = unchecked((byte)(startValue + index));
        }

        public void ClearWrites() => Writes.Clear();
    }

    private sealed class FakeCoreSession : IEmulatorCoreSession
    {
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

        public CoreRuntimeInfo RuntimeInfo { get; } =
            new("fake.core", "Fake Core", "nes", "1.0.0", CoreBinaryKinds.ManagedDotNet);

        public CoreCapabilitySet Capabilities { get; } = CoreCapabilitySet.From();

        public IInputSchema InputSchema { get; } = new FakeInputSchema();

        public CoreLoadResult NextLoadResult { get; set; } = CoreLoadResult.Ok();

        public CoreStepResult NextStepResult { get; set; } = CoreStepResult.Ok();

        public int PauseCount { get; private set; }

        public int ResumeCount { get; private set; }

        public int RunFrameCount { get; private set; }

        public CoreLoadResult LoadMedia(CoreMediaLoadRequest request) => NextLoadResult;

        public void Reset()
        {
        }

        public void Pause() => PauseCount++;

        public void Resume() => ResumeCount++;

        public CoreStepResult RunFrame()
        {
            RunFrameCount++;
            return NextStepResult;
        }

        public CoreStepResult StepInstruction() => CoreStepResult.Ok();

        public CoreStateBlob CaptureState(bool includeThumbnail = false) =>
            new()
            {
                Format = "test/state",
                Data = [1, 2, 3]
            };

        public void RestoreState(CoreStateBlob state)
        {
        }

        public bool TryGetCapability<TCapability>(out TCapability capability)
            where TCapability : class
        {
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
        public Dictionary<long, CoreTimelineSnapshot> Snapshots { get; } = [];

        public long SeekResult { get; set; }

        public int PauseRecordingCount { get; private set; }

        public int ResumeRecordingCount { get; private set; }

        public long LastSeekFrame { get; private set; } = -1;

        public long CurrentFrame { get; set; }

        public double CurrentTimestampSeconds { get; set; }

        public int SnapshotInterval { get; set; }

        public int HotCacheCount => 0;

        public int WarmCacheCount => 0;

        public long NewestFrame { get; set; }

        public CoreTimeTravelCacheInfo GetCacheInfo() => new(0, 0, NewestFrame, SnapshotInterval);

        public IReadOnlyList<CoreTimelineThumbnail> GetThumbnails() => [];

        public CoreBranchPoint CreateBranch(string name, uint[] frameBuffer) =>
            throw new NotSupportedException();

        public void RestoreSnapshot(CoreTimelineSnapshot snapshot) =>
            throw new NotSupportedException();

        public long SeekToFrame(long frame)
        {
            LastSeekFrame = frame;
            return SeekResult;
        }

        public long RewindFrames(int frames) => throw new NotSupportedException();

        public CoreTimelineSnapshot? GetNearestSnapshot(long frame) =>
            Snapshots.TryGetValue(frame, out var snapshot) ? snapshot : null;

        public CoreStateBlob? GetNearestState(long frame, bool includeThumbnail = false) => null;

        public void PauseRecording() => PauseRecordingCount++;

        public void ResumeRecording() => ResumeRecordingCount++;
    }

    private sealed class FakeInputStateWriter : ICoreInputStateWriter
    {
        public void SetInputState(string portId, string actionId, float value)
        {
        }
    }
}

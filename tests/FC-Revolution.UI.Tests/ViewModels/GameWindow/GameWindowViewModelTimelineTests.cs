using Avalonia.Input;
using FCRevolution.Core.Mappers;
using FCRevolution.Core.PPU;
using FCRevolution.Core.State;
using FCRevolution.Core.Timeline;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.ViewModels;
using System.Globalization;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class GameWindowViewModelTimelineTests
{
    [Fact]
    public void TimelineHotkey_ShowsBottomTimelineHint()
    {
        using var host = CreateHost();
        var vm = host.ViewModel;

        vm.OnKeyDown(Key.F4);

        Assert.True(vm.IsBranchGalleryVisible);
        Assert.True(vm.HasTransientMessage);
        Assert.Contains("时间线", vm.TransientMessage);
    }

    [Fact]
    public void BranchGalleryRewind_UpdatesTemporalResetReason_ToTimelineJump()
    {
        using var host = CreateHost();
        var vm = host.ViewModel;
        EnsureTimelineReady(host, vm);
        SetTemporalResetReasonToSaveStateLoaded(vm);
        vm.BranchGallery.RewindCommand.Execute("1");

        Assert.Equal(
            MacMetalTemporalResetReason.TimelineJump,
            vm.TemporalHistoryResetReason);
    }

    [Fact]
    public void BranchGalleryLoadBranch_UpdatesTemporalResetReason_ToTimelineJump()
    {
        using var host = CreateHost();
        var vm = host.ViewModel;
        EnsureTimelineReady(host, vm);
        vm.BranchGallery.SetLastFrame(GameWindowViewModelTestHost.CreateSolidFrame(0xFF336699u));
        vm.BranchGallery.CreateBranchCommand.Execute(null);
        var branchNode = vm.BranchGallery.CanvasNodes.FirstOrDefault(node => node.BranchPoint != null);
        Assert.NotNull(branchNode);
        SetTemporalResetReasonToSaveStateLoaded(vm);

        vm.BranchGallery.SelectNodeCommand.Execute(branchNode);
        vm.BranchGallery.LoadBranchCommand.Execute(null);

        Assert.Equal(MacMetalTemporalResetReason.TimelineJump, vm.TemporalHistoryResetReason);
    }

    [Fact]
    public void PauseResume_DoesNotChangeTemporalResetReason()
    {
        using var host = CreateHost();
        var vm = host.ViewModel;
        SetTemporalResetReasonToSaveStateLoaded(vm);

        vm.OnKeyDown(Key.F5);
        vm.OnKeyDown(Key.F5);

        Assert.Equal(MacMetalTemporalResetReason.SaveStateLoaded, vm.TemporalHistoryResetReason);
    }

    private static void SetTemporalResetReasonToSaveStateLoaded(GameWindowViewModel vm)
    {
        vm.OnKeyDown(Key.F2);
        vm.OnKeyDown(Key.F3);
        Assert.Equal(MacMetalTemporalResetReason.SaveStateLoaded, vm.TemporalHistoryResetReason);
    }

    private static GameWindowViewModelTestHost CreateHost()
    {
        var timeTravel = new FakeTimeTravelService();
        var coreSession = new FakeCoreSession(timeTravel);
        return new GameWindowViewModelTestHost(coreSession: coreSession);
    }

    private static void EnsureTimelineReady(GameWindowViewModelTestHost host, GameWindowViewModel vm)
    {
        host.InvokeOnUiTick();
        AvaloniaThreadingTestHelper.DrainJobs();
        Assert.True(TryParseCurrentFrame(vm.TimelinePositionText) > 0);
        Assert.NotEmpty(vm.BranchGallery.CanvasNodes);
    }

    private static long TryParseCurrentFrame(string timelinePositionText)
    {
        var marker = "帧 ";
        var start = timelinePositionText.LastIndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return -1;

        var value = timelinePositionText[(start + marker.Length)..].Trim();
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frame)
            ? frame
            : -1;
    }

    private sealed class FakeCoreSession : IEmulatorCoreSession
    {
        private readonly FakeTimeTravelService _timeTravelService;
        private readonly FakeDebugSurface _debugSurface = new();
        private readonly FakeInputStateWriter _inputStateWriter = new();
        private readonly FakeRenderStateProvider _renderStateProvider = new();

        public FakeCoreSession(FakeTimeTravelService timeTravelService)
        {
            _timeTravelService = timeTravelService;
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

        public CoreRuntimeInfo RuntimeInfo { get; } =
            new("fake.timeline.core", "Fake Timeline Core", "nes", "1.0.0", CoreBinaryKinds.ManagedDotNet);

        public CoreCapabilitySet Capabilities { get; } = CoreCapabilitySet.From(
            CoreCapabilityIds.MediaLoad,
            CoreCapabilityIds.SaveState,
            CoreCapabilityIds.TimeTravel,
            CoreCapabilityIds.DebugMemory,
            CoreCapabilityIds.InputState,
            CoreCapabilityIds.LayeredFrame);

        public IInputSchema InputSchema { get; } = new FakeInputSchema();

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
            object? resolved = typeof(TCapability) switch
            {
                var type when type == typeof(ITimeTravelService) => _timeTravelService,
                var type when type == typeof(ICoreDebugSurface) => _debugSurface,
                var type when type == typeof(ICoreInputStateWriter) => _inputStateWriter,
                var type when type == typeof(ILayeredFrameProvider) => _renderStateProvider,
                _ => null
            };

            if (resolved is TCapability typed)
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

    private sealed class FakeTimeTravelService : ITimeTravelService
    {
        private readonly List<CoreTimelineThumbnail> _thumbnails;

        public FakeTimeTravelService()
        {
            var thumbnailPixels = Enumerable.Repeat(0xFF336699u, 64 * 60).ToArray();
            _thumbnails =
            [
                new CoreTimelineThumbnail(60, thumbnailPixels),
                new CoreTimelineThumbnail(120, thumbnailPixels),
                new CoreTimelineThumbnail(180, thumbnailPixels)
            ];
            CurrentFrame = 180;
            NewestFrame = 180;
            CurrentTimestampSeconds = CurrentFrame / 60d;
        }

        public long CurrentFrame { get; private set; }

        public double CurrentTimestampSeconds { get; private set; }

        public int SnapshotInterval { get; set; } = 5;

        public int HotCacheCount => 0;

        public int WarmCacheCount => 0;

        public long NewestFrame { get; private set; }

        public CoreTimeTravelCacheInfo GetCacheInfo() => new(0, 0, NewestFrame, SnapshotInterval);

        public IReadOnlyList<CoreTimelineThumbnail> GetThumbnails() => _thumbnails;

        public CoreBranchPoint CreateBranch(string name, uint[] frameBuffer) =>
            new()
            {
                Id = Guid.NewGuid(),
                Name = name,
                RomPath = "/tmp/test.nes",
                Frame = CurrentFrame,
                TimestampSeconds = CurrentTimestampSeconds,
                Snapshot = new CoreTimelineSnapshot
                {
                    Frame = CurrentFrame,
                    TimestampSeconds = CurrentTimestampSeconds,
                    Thumbnail = Enumerable.Repeat(0xFF112233u, 64 * 60).ToArray(),
                    State = new CoreStateBlob
                    {
                        Format = "test/snapshot",
                        Data = StateSnapshotSerializer.Serialize(
                            new StateSnapshotData
                            {
                                Frame = CurrentFrame,
                                Timestamp = CurrentTimestampSeconds,
                                CpuState = [1],
                                PpuState = [2],
                                RamState = [3],
                                CartState = [4],
                                ApuState = [5],
                                Thumbnail = Enumerable.Repeat(0xFF112233u, 64 * 60).ToArray()
                            },
                            includeThumbnail: true)
                    }
                },
                CreatedAt = DateTime.UtcNow
            };

        public void RestoreSnapshot(CoreTimelineSnapshot snapshot)
        {
            CurrentFrame = snapshot.Frame;
            CurrentTimestampSeconds = snapshot.TimestampSeconds;
        }

        public long SeekToFrame(long frame)
        {
            CurrentFrame = frame;
            CurrentTimestampSeconds = frame / 60d;
            return frame;
        }

        public long RewindFrames(int frames)
        {
            CurrentFrame = Math.Max(0, CurrentFrame - frames);
            CurrentTimestampSeconds = CurrentFrame / 60d;
            return CurrentFrame;
        }

        public CoreTimelineSnapshot? GetNearestSnapshot(long frame) => null;

        public CoreStateBlob? GetNearestState(long frame, bool includeThumbnail = false) => null;

        public void PauseRecording()
        {
        }

        public void ResumeRecording()
        {
        }
    }

    private sealed class FakeDebugSurface : ICoreDebugSurface
    {
        public CoreDebugState CaptureDebugState() => new();

        public byte ReadMemory(ushort address) => 0;

        public void WriteMemory(ushort address, byte value)
        {
        }

        public byte[] ReadMemoryBlock(ushort startAddress, int length) => new byte[length];
    }

    private sealed class FakeInputStateWriter : ICoreInputStateWriter
    {
        public void SetInputState(string portId, string actionId, float value)
        {
        }
    }

    private sealed class FakeRenderStateProvider : ILayeredFrameProvider
    {
        public LayeredFrameData CaptureLayeredFrame() =>
            new(
                256,
                240,
                [],
                new uint[32],
                [],
                [],
                showBackground: true,
                showSprites: true,
                showBackgroundInFirstTileColumn: true,
                showSpritesInFirstTileColumn: true);

        public void ResetTemporalHistory()
        {
        }
    }

    private sealed class FakeInputSchema : IInputSchema
    {
        public IReadOnlyList<InputPortDescriptor> Ports { get; } = [];

        public IReadOnlyList<InputActionDescriptor> Actions { get; } = [];
    }
}

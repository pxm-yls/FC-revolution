using Avalonia.Input;
using FCRevolution.Core.Input;
using FCRevolution.Core.Mappers;
using FCRevolution.Core.PPU;
using FCRevolution.Core.State;
using FCRevolution.Core.Timeline;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class GameWindowViewModelInputTests
{
    [Fact]
    public void ShouldHandleKey_RecognizesGameWindowHotkeys()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;

        Assert.True(vm.ShouldHandleKey(Key.F2));
        Assert.True(vm.ShouldHandleKey(Key.F3));
        Assert.True(vm.ShouldHandleKey(Key.F4));
        Assert.True(vm.ShouldHandleKey(Key.F5));
        Assert.True(vm.ShouldHandleKey(Key.F7));
        Assert.True(vm.ShouldHandleKey(Key.F12));
    }

    [Fact]
    public void OverlayShortcut_CanRequireModifier()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;

        var shortcuts = ShortcutCatalog.BuildDefaultGestureMap();
        shortcuts[ShortcutCatalog.GameToggleInfoOverlay] = new ShortcutGesture(Key.F1, KeyModifiers.Control);
        vm.ApplyShortcutBindings(shortcuts);

        vm.OnKeyDown(Key.F1);
        Assert.False(vm.IsOverlayVisible);

        vm.OnKeyDown(Key.F1, KeyModifiers.Control);
        Assert.True(vm.IsOverlayVisible);
    }

    [Fact]
    public void OverlayShortcut_CanUseMetaNumberBinding()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;

        var shortcuts = ShortcutCatalog.BuildDefaultGestureMap();
        shortcuts[ShortcutCatalog.GameToggleInfoOverlay] = new ShortcutGesture(Key.D2, KeyModifiers.Meta);
        vm.ApplyShortcutBindings(shortcuts);

        Assert.True(vm.ShouldHandleKey(Key.D2, KeyModifiers.Meta));

        vm.OnKeyDown(Key.D2, KeyModifiers.Meta);
        Assert.True(vm.IsOverlayVisible);
    }

    [Fact]
    public void ApplyShortcutBindings_UpdatesShortcutHintText()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;

        var shortcuts = ShortcutCatalog.BuildDefaultGestureMap();
        var quickSaveGesture = new ShortcutGesture(Key.D1, KeyModifiers.Control);
        var infoGesture = new ShortcutGesture(Key.D9, KeyModifiers.Meta);
        shortcuts[ShortcutCatalog.GameQuickSave] = quickSaveGesture;
        shortcuts[ShortcutCatalog.GameToggleInfoOverlay] = infoGesture;
        vm.ApplyShortcutBindings(shortcuts);

        Assert.Contains($"存档 {quickSaveGesture.ToDisplayString()}", vm.ShortcutHintText);
        Assert.Contains($"信息 {infoGesture.ToDisplayString()}", vm.ShortcutHintText);
    }

    [Fact]
    public void ShouldHandleKey_RecognizesPlayer2Bindings()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;

        Assert.True(vm.ShouldHandleKey(Key.I));
        Assert.True(vm.ShouldHandleKey(Key.J));
        Assert.True(vm.ShouldHandleKey(Key.K));
        Assert.True(vm.ShouldHandleKey(Key.L));
        Assert.True(vm.ShouldHandleKey(Key.U));
        Assert.True(vm.ShouldHandleKey(Key.O));
    }

    [Fact]
    public void ComboExtraBinding_PressesBothMappedButtons()
    {
        using var host = new GameWindowViewModelTestHost(
            [
                new ExtraInputBindingProfile
                {
                    Player = 0,
                    Kind = ExtraInputBindingKind.Combo.ToString(),
                    Key = Key.Q.ToString(),
                    Buttons = [NesButton.A.ToString(), NesButton.B.ToString()]
                }
            ]);
        var vm = host.ViewModel;

        vm.OnKeyDown(Key.Q);
        Assert.Equal((byte)((byte)NesButton.A | (byte)NesButton.B), host.ReadCombinedInputMask(0));

        vm.OnKeyUp(Key.Q);
        Assert.Equal(0, host.ReadCombinedInputMask(0));
    }

    [Fact]
    public void TurboExtraBinding_StartsImmediately_AndTurnsOffAfterConfiguredWindow()
    {
        using var host = new GameWindowViewModelTestHost(
            [
                new ExtraInputBindingProfile
                {
                    Player = 0,
                    Kind = ExtraInputBindingKind.Turbo.ToString(),
                    Key = Key.Q.ToString(),
                    Buttons = [NesButton.A.ToString()]
                }
            ]);
        var vm = host.ViewModel;

        vm.OnKeyDown(Key.Q);
        Assert.Equal((byte)NesButton.A, host.ReadCombinedInputMask(0));

        for (var i = 0; i < 5; i++)
        {
            host.InvokeOnUiTick();
            Assert.Equal((byte)NesButton.A, host.ReadCombinedInputMask(0));
        }

        host.InvokeOnUiTick();
        Assert.Equal(0, host.ReadCombinedInputMask(0));
    }

    [Fact]
    public void RemoteControl_AcquireAndRelease_PreservesLocalStateAndAppliesRemoteMask()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;

        vm.OnKeyDown(Key.Z);
        Assert.Equal((byte)NesButton.A, host.ReadCombinedInputMask(0));

        var acquired = vm.AcquireRemoteControl(0, "127.0.0.1", "remote-client");
        Assert.True(acquired);
        Assert.Equal(0, host.ReadCombinedInputMask(0));
        Assert.Equal(GamePlayerControlSource.Remote, vm.Player1ControlSource);
        Assert.Equal(GamePlayerControlSource.Local, vm.Player2ControlSource);
        Assert.True(vm.HasRemoteControlStatus);
        Assert.Contains("remote-client (127.0.0.1)", vm.RemoteControlStatusText);
        Assert.Equal("1P 已切换为 127.0.0.1 网页控制", vm.TransientMessage);

        var remoteApplied = vm.SetRemoteButtonState(0, NesButton.B, true, "127.0.0.1", "remote-client");
        Assert.True(remoteApplied);
        Assert.Equal((byte)NesButton.B, host.ReadCombinedInputMask(0));

        vm.OnKeyDown(Key.X);
        Assert.Equal((byte)NesButton.B, host.ReadCombinedInputMask(0));

        var remoteReleasedButton = vm.SetRemoteButtonState(0, NesButton.B, false, "127.0.0.1", "remote-client");
        Assert.True(remoteReleasedButton);
        Assert.Equal(0, host.ReadCombinedInputMask(0));

        vm.ReleaseRemoteControl(0, "remote disconnected");
        Assert.Equal((byte)((byte)NesButton.A | (byte)NesButton.B), host.ReadCombinedInputMask(0));
        Assert.Equal(GamePlayerControlSource.Local, vm.Player1ControlSource);
        Assert.False(vm.HasRemoteControlStatus);
        Assert.Equal(string.Empty, vm.RemoteControlStatusText);
        Assert.Equal("remote disconnected", vm.TransientMessage);
    }

    [Fact]
    public void RemoteControl_NesGenericXAction_StillMapsToTurboABridge()
    {
        using var host = new GameWindowViewModelTestHost();
        var vm = host.ViewModel;

        Assert.True(vm.AcquireRemoteControl(0, "127.0.0.1", "remote-client"));

        var applied = vm.SetRemoteInputState("p1", "x", 1f, "127.0.0.1", "remote-client");

        Assert.True(applied);
        Assert.Equal((byte)NesButton.A, host.ReadCombinedInputMask(0));
    }

    [Fact]
    public void RemoteControl_GenericNonNesXAction_PassesThroughWithoutNesAliasing()
    {
        var coreSession = new FakeGenericCoreSession("arcade", [new InputActionDescriptor("x", "X", "p1", InputValueKind.Digital)]);
        using var host = new GameWindowViewModelTestHost(coreSession: coreSession);
        var vm = host.ViewModel;

        Assert.True(vm.AcquireRemoteControl(0, "127.0.0.1", "remote-client"));

        var applied = vm.SetRemoteInputState("p1", "x", 1f, "127.0.0.1", "remote-client");

        Assert.True(applied);
        Assert.Equal(0, host.ReadCombinedInputMask(0));
        var call = Assert.Single(coreSession.InputWriter.Calls);
        Assert.Equal("p1", call.PortId);
        Assert.Equal("x", call.ActionId);
        Assert.Equal(1f, call.Value);
    }

    [Fact]
    public void RemoteControl_GenericNonNesUnsupportedAction_ReturnsFalse()
    {
        var coreSession = new FakeGenericCoreSession("arcade", []);
        using var host = new GameWindowViewModelTestHost(coreSession: coreSession);
        var vm = host.ViewModel;

        Assert.True(vm.AcquireRemoteControl(0, "127.0.0.1", "remote-client"));

        var applied = vm.SetRemoteInputState("p1", "fire", 1f, "127.0.0.1", "remote-client");

        Assert.False(applied);
        Assert.Empty(coreSession.InputWriter.Calls);
    }

    private sealed class FakeGenericCoreSession : IEmulatorCoreSession
    {
        private readonly FakeTimeTravelService _timeTravelService = new();
        private readonly FakeDebugSurface _debugSurface = new();
        private readonly FakeRenderStateProvider _renderStateProvider = new();

        public FakeGenericCoreSession(string systemId, IReadOnlyList<InputActionDescriptor> actions)
        {
            RuntimeInfo = new CoreRuntimeInfo(
                "fake.generic.core",
                "Fake Generic Core",
                systemId,
                "1.0.0",
                CoreBinaryKinds.ManagedDotNet);
            InputSchema = new FakeInputSchema(actions);
        }

        public RecordingInputStateWriter InputWriter { get; } = new();

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

        public CoreCapabilitySet Capabilities { get; } = CoreCapabilitySet.From(
            CoreCapabilityIds.MediaLoad,
            CoreCapabilityIds.SaveState,
            CoreCapabilityIds.TimeTravel,
            CoreCapabilityIds.DebugMemory,
            CoreCapabilityIds.InputState,
            CoreCapabilityIds.SystemNesRenderState);

        public IInputSchema InputSchema { get; }

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
                var type when type == typeof(ICoreInputStateWriter) => InputWriter,
                var type when type == typeof(INesRenderStateProvider) => _renderStateProvider,
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
        public long CurrentFrame => 0;
        public double CurrentTimestampSeconds => 0;
        public int SnapshotInterval { get; set; } = 5;
        public int HotCacheCount => 0;
        public int WarmCacheCount => 0;
        public long NewestFrame => 0;
        public CoreTimeTravelCacheInfo GetCacheInfo() => new(0, 0, 0, SnapshotInterval);
        public IReadOnlyList<CoreTimelineThumbnail> GetThumbnails() => [];
        public CoreBranchPoint CreateBranch(string name, uint[] frameBuffer) =>
            new()
            {
                Id = Guid.NewGuid(),
                Name = name,
                Frame = 0,
                TimestampSeconds = 0,
                Snapshot = new CoreTimelineSnapshot
                {
                    Frame = 0,
                    TimestampSeconds = 0,
                    Thumbnail = [],
                    State = new CoreStateBlob
                    {
                        Format = "test/state",
                        Data = []
                    }
                }
            };
        public void RestoreSnapshot(CoreTimelineSnapshot snapshot)
        {
        }
        public long SeekToFrame(long frame) => frame;
        public long RewindFrames(int frames) => 0;
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

    private sealed class RecordingInputStateWriter : ICoreInputStateWriter
    {
        public List<InputWriteCall> Calls { get; } = [];

        public void SetInputState(string portId, string actionId, float value)
        {
            Calls.Add(new InputWriteCall(portId, actionId, value));
        }
    }

    private sealed class FakeRenderStateProvider : INesRenderStateProvider
    {
        public PpuRenderStateSnapshot CaptureRenderStateSnapshot() =>
            new()
            {
                NametableBytes = new byte[0x1000],
                PatternTableBytes = new byte[0x2000],
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
                BackgroundScanlineStates = []
            };
    }

    private sealed class FakeInputSchema(IReadOnlyList<InputActionDescriptor> actions) : IInputSchema
    {
        public IReadOnlyList<InputPortDescriptor> Ports { get; } = [new("p1", "P1", 0)];
        public IReadOnlyList<InputActionDescriptor> Actions { get; } = actions;
    }

    private readonly record struct InputWriteCall(string PortId, string ActionId, float Value);
}

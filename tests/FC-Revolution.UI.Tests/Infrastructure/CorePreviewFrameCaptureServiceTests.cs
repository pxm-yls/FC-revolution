using FCRevolution.Emulation.Abstractions;
using FCRevolution.Emulation.Host;

namespace FC_Revolution.UI.Tests;

public sealed class CorePreviewFrameCaptureServiceTests
{
    [Fact]
    public void Capture_CapturesFrames_OnStride_AndClonesVideoPackets()
    {
        var session = new FakePreviewCoreSession(width: 256, height: 240);
        var service = new CorePreviewFrameCaptureService(() => session);
        var captured = new List<CorePreviewCapturedFrame>();

        var result = service.Capture(
            new CorePreviewFrameCaptureRequest(
                MediaPath: "/roms/test.nes",
                TotalFrames: 5,
                CaptureStride: 2,
                MaxCapturedFrames: 2,
                ExpectedWidth: 256,
                ExpectedHeight: 240,
                TargetRunFps: 600),
            captured.Add);

        Assert.Equal(4, result.GeneratedFrames);
        Assert.Equal(2, result.CapturedFrames);
        Assert.Equal([2, 4], captured.Select(frame => frame.SourceFrameIndex));
        Assert.Equal(2u, captured[0].Frame.Pixels[0]);
        Assert.Equal(4u, captured[1].Frame.Pixels[0]);
        Assert.NotSame(session.EmittedPackets[1].Pixels, captured[0].Frame.Pixels);
        Assert.NotSame(session.EmittedPackets[3].Pixels, captured[1].Frame.Pixels);
    }

    [Fact]
    public void Capture_ThrowsFriendlyMessage_WhenNoCoreIsAvailable()
    {
        var service = new CorePreviewFrameCaptureService(() => ManagedCoreRuntime.CreateUnavailableSession());

        var ex = Assert.Throws<InvalidOperationException>(() => service.Capture(
            new CorePreviewFrameCaptureRequest(
                MediaPath: "/roms/test.nes",
                TotalFrames: 1,
                CaptureStride: 1,
                MaxCapturedFrames: 1,
                ExpectedWidth: 256,
                ExpectedHeight: 240,
                TargetRunFps: 60),
            _ => { }));

        Assert.Contains("当前没有可用核心", ex.Message, StringComparison.Ordinal);
    }

    private sealed class FakePreviewCoreSession : IEmulatorCoreSession
    {
        private readonly int _width;
        private readonly int _height;
        private int _frameNumber;

        public FakePreviewCoreSession(int width, int height)
        {
            _width = width;
            _height = height;
            RuntimeInfo = new CoreRuntimeInfo("fc.fake.preview", "Fake Preview", "test", "1.0.0", CoreBinaryKinds.ManagedDotNet);
        }

        public List<VideoFramePacket> EmittedPackets { get; } = [];

        public event Action<VideoFramePacket>? VideoFrameReady;

        public event Action<AudioPacket>? AudioReady
        {
            add { }
            remove { }
        }

        public CoreRuntimeInfo RuntimeInfo { get; }

        public CoreCapabilitySet Capabilities { get; } = CoreCapabilitySet.From(CoreCapabilityIds.VideoFrame, CoreCapabilityIds.MediaLoad);

        public IInputSchema InputSchema { get; } = new EmptyInputSchema();

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

        public CoreStepResult RunFrame()
        {
            _frameNumber++;
            var packet = new VideoFramePacket
            {
                Pixels = CreateFrame(_width, _height, (uint)_frameNumber),
                Width = _width,
                Height = _height,
                PixelFormat = "rgba32",
                PresentationIndex = _frameNumber,
                TimestampSeconds = _frameNumber / 60d
            };
            EmittedPackets.Add(packet);
            VideoFrameReady?.Invoke(packet);
            return CoreStepResult.Ok(_frameNumber);
        }

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
            capability = null!;
            return false;
        }

        public void Dispose()
        {
        }

        private static uint[] CreateFrame(int width, int height, uint value)
        {
            var frame = new uint[width * height];
            Array.Fill(frame, value);
            return frame;
        }
    }

    private sealed class EmptyInputSchema : IInputSchema
    {
        public IReadOnlyList<InputPortDescriptor> Ports { get; } = [];

        public IReadOnlyList<InputActionDescriptor> Actions { get; } = [];
    }
}

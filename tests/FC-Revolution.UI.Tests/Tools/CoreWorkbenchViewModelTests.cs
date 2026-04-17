using System.Runtime.InteropServices;
using FCRevolution.Core.Workbench.ViewModels;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Emulation.Host;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class CoreWorkbenchViewModelTests
{
    [Fact]
    public async Task CapturePreviewCommand_CapturesBitmap_AndUpdatesSummary()
    {
        var resourceRoot = Path.Combine(Path.GetTempPath(), $"fc-workbench-preview-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(resourceRoot);

        try
        {
            var viewModel = new CoreWorkbenchViewModel(
                createWorkspace: CoreRuntimeWorkspace.Create,
                createPreviewService: (_, _) => new CorePreviewFrameCaptureService(() => new FakePreviewCoreSession(width: 256, height: 240)))
            {
                ResourceRootPath = resourceRoot,
                SelectedCoreId = "fc.fake.preview",
                RomPath = "/roms/test.nes"
            };

            await viewModel.CapturePreviewCommand.ExecuteAsync(null);

            Assert.Equal("Preview captured.", viewModel.StatusText);
            Assert.Contains($"Resource Root: {resourceRoot}", viewModel.PreviewSummary, StringComparison.Ordinal);
            Assert.Contains("Generated Frames: 1", viewModel.PreviewSummary, StringComparison.Ordinal);
            Assert.Contains("Captured Frames: 1", viewModel.PreviewSummary, StringComparison.Ordinal);
            Assert.NotNull(viewModel.PreviewBitmap);
            Assert.Equal(256, viewModel.PreviewBitmap!.PixelSize.Width);
            Assert.Equal(240, viewModel.PreviewBitmap.PixelSize.Height);

            using var locked = viewModel.PreviewBitmap.Lock();
            var firstPixel = unchecked((uint)Marshal.ReadInt32(locked.Address));
            Assert.Equal(1u, firstPixel);
        }
        finally
        {
            if (Directory.Exists(resourceRoot))
                Directory.Delete(resourceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CapturePreviewCommand_WithoutRom_ClearsStaleBitmap_AndSkipsService()
    {
        var previewServiceCreated = false;
        var viewModel = new CoreWorkbenchViewModel(
            createWorkspace: CoreRuntimeWorkspace.Create,
            createPreviewService: (_, _) =>
            {
                previewServiceCreated = true;
                return new CorePreviewFrameCaptureService(() => new FakePreviewCoreSession(width: 256, height: 240));
            })
        {
            PreviewBitmap = CreateBitmap(0xFF102030u),
            RomPath = ""
        };

        await viewModel.CapturePreviewCommand.ExecuteAsync(null);

        Assert.False(previewServiceCreated);
        Assert.Equal("Preview capture requires a ROM / media path.", viewModel.StatusText);
        Assert.Null(viewModel.PreviewBitmap);
    }

    [Fact]
    public async Task CapturePreviewCommand_WhenPreviewServiceThrows_ClearsStaleBitmap_AndSurfacesError()
    {
        var resourceRoot = Path.Combine(Path.GetTempPath(), $"fc-workbench-preview-error-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(resourceRoot);

        try
        {
            var viewModel = new CoreWorkbenchViewModel(
                createWorkspace: CoreRuntimeWorkspace.Create,
                createPreviewService: (_, _) => throw new InvalidOperationException("boom"))
            {
                ResourceRootPath = resourceRoot,
                SelectedCoreId = "fc.fake.preview",
                RomPath = "/roms/test.nes",
                PreviewBitmap = CreateBitmap(0xFF556677u)
            };

            await viewModel.CapturePreviewCommand.ExecuteAsync(null);

            Assert.Equal("Preview capture failed: boom", viewModel.StatusText);
            Assert.Contains("boom", viewModel.PreviewSummary, StringComparison.Ordinal);
            Assert.Null(viewModel.PreviewBitmap);
        }
        finally
        {
            if (Directory.Exists(resourceRoot))
                Directory.Delete(resourceRoot, recursive: true);
        }
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

        public event Action<VideoFramePacket>? VideoFrameReady;

        public event Action<AudioPacket>? AudioReady
        {
            add { }
            remove { }
        }

        public CoreRuntimeInfo RuntimeInfo { get; }

        public CoreCapabilitySet Capabilities { get; } =
            CoreCapabilitySet.From(CoreCapabilityIds.VideoFrame, CoreCapabilityIds.MediaLoad);

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
            VideoFrameReady?.Invoke(new VideoFramePacket
            {
                Pixels = CreateFrame(_width, _height, (uint)_frameNumber),
                Width = _width,
                Height = _height,
                PixelFormat = "rgba32",
                PresentationIndex = _frameNumber,
                TimestampSeconds = _frameNumber / 60d
            });
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

    private static Avalonia.Media.Imaging.WriteableBitmap CreateBitmap(uint pixel)
    {
        var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(
            new Avalonia.PixelSize(1, 1),
            new Avalonia.Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888,
            Avalonia.Platform.AlphaFormat.Opaque);
        using var locked = bitmap.Lock();
        Marshal.WriteInt32(locked.Address, unchecked((int)pixel));
        return bitmap;
    }
}

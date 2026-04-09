using System;
using FCRevolution.Core.PPU;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Common;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.ViewModels;

internal sealed class GameWindowLayeredFrameBuilderController
{
    private readonly Func<PpuRenderStateSnapshot> _captureRenderState;
    private readonly Func<PpuRenderStateSnapshot, IFrameMetadata?, FrameMetadata> _extractFrameMetadata;
    private readonly Func<IFrameMetadata, LayeredFrameData> _buildLayeredFrame;
    private readonly Action<Exception> _logFailure;
    private IFrameMetadata? _previousRenderMetadata;
    private bool _didLogFailure;

    public GameWindowLayeredFrameBuilderController(INesRenderStateProvider renderStateProvider)
        : this(
            renderStateProvider.CaptureRenderStateSnapshot,
            ExtractFrameMetadata,
            BuildLayeredFrame,
            ex => StartupDiagnostics.WriteException("game-window", "failed to build layered frame data", ex))
    {
        ArgumentNullException.ThrowIfNull(renderStateProvider);
    }

    internal GameWindowLayeredFrameBuilderController(
        Func<PpuRenderStateSnapshot> captureRenderState,
        Func<PpuRenderStateSnapshot, IFrameMetadata?, FrameMetadata> extractFrameMetadata,
        Func<IFrameMetadata, LayeredFrameData> buildLayeredFrame,
        Action<Exception> logFailure)
    {
        _captureRenderState = captureRenderState ?? throw new ArgumentNullException(nameof(captureRenderState));
        _extractFrameMetadata = extractFrameMetadata ?? throw new ArgumentNullException(nameof(extractFrameMetadata));
        _buildLayeredFrame = buildLayeredFrame ?? throw new ArgumentNullException(nameof(buildLayeredFrame));
        _logFailure = logFailure ?? throw new ArgumentNullException(nameof(logFailure));
    }

    public LayeredFrameData? TryBuildLayeredFrame()
    {
        try
        {
            var renderState = _captureRenderState();
            var metadata = _extractFrameMetadata(renderState, _previousRenderMetadata);
            _previousRenderMetadata = metadata;
            return _buildLayeredFrame(metadata);
        }
        catch (Exception ex)
        {
            if (!_didLogFailure)
            {
                _didLogFailure = true;
                _logFailure(ex);
            }

            _previousRenderMetadata = null;
            return null;
        }
    }

    public void ResetTemporalHistory()
    {
        _previousRenderMetadata = null;
    }

    private static FrameMetadata ExtractFrameMetadata(
        PpuRenderStateSnapshot snapshot,
        IFrameMetadata? previousRenderMetadata) =>
        new RenderDataExtractor().Extract(snapshot, previousRenderMetadata);

    private static LayeredFrameData BuildLayeredFrame(IFrameMetadata metadata) =>
        LayeredFrameBuilder.Build(metadata);
}

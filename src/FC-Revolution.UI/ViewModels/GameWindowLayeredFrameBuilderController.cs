using System;
using FCRevolution.Rendering.Abstractions;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.ViewModels;

internal sealed class GameWindowLayeredFrameBuilderController
{
    private readonly Func<LayeredFrameData> _captureLayeredFrame;
    private readonly Action _resetTemporalHistory;
    private readonly Action<Exception> _logFailure;
    private bool _didLogFailure;

    public GameWindowLayeredFrameBuilderController(ILayeredFrameProvider layeredFrameProvider)
        : this(
            layeredFrameProvider.CaptureLayeredFrame,
            layeredFrameProvider.ResetTemporalHistory,
            ex => StartupDiagnostics.WriteException("game-window", "failed to build layered frame data", ex))
    {
        ArgumentNullException.ThrowIfNull(layeredFrameProvider);
    }

    internal GameWindowLayeredFrameBuilderController(
        Func<LayeredFrameData> captureLayeredFrame,
        Action resetTemporalHistory,
        Action<Exception> logFailure)
    {
        _captureLayeredFrame = captureLayeredFrame ?? throw new ArgumentNullException(nameof(captureLayeredFrame));
        _resetTemporalHistory = resetTemporalHistory ?? throw new ArgumentNullException(nameof(resetTemporalHistory));
        _logFailure = logFailure ?? throw new ArgumentNullException(nameof(logFailure));
    }

    public LayeredFrameData? TryBuildLayeredFrame()
    {
        try
        {
            return _captureLayeredFrame();
        }
        catch (Exception ex)
        {
            if (!_didLogFailure)
            {
                _didLogFailure = true;
                _logFailure(ex);
            }

            _resetTemporalHistory();
            return null;
        }
    }

    public void ResetTemporalHistory() => _resetTemporalHistory();
}

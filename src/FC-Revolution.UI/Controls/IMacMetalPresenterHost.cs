using System;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Metal;

namespace FC_Revolution.UI.Controls;

internal interface IMacMetalPresenterHost : IDisposable
{
    IntPtr ViewHandle { get; }

    MacMetalPresenterDiagnostics Diagnostics { get; }

    bool PresentFrame(ReadOnlySpan<uint> frameBuffer);

    bool PresentFrame(LayeredFrameData frameData);

    void SetDisplaySize(double width, double height);

    void SetCornerRadius(double radius);

    void SetUpscaleOutputResolution(MacUpscaleOutputResolution outputResolution);

    void SetUpscaleMode(MacUpscaleMode upscaleMode);

    void RequestTemporalHistoryReset(MacMetalTemporalResetReason reason);
}

internal sealed class MacMetalPresenterHost : IMacMetalPresenterHost
{
    private readonly MacMetalPresenter _presenter;

    public MacMetalPresenterHost(IntPtr parentViewHandle, MacUpscaleMode upscaleMode)
    {
        _presenter = new MacMetalPresenter(parentViewHandle, upscaleMode: upscaleMode);
    }

    public IntPtr ViewHandle => _presenter.ViewHandle;

    public MacMetalPresenterDiagnostics Diagnostics => _presenter.Diagnostics;

    public bool PresentFrame(ReadOnlySpan<uint> frameBuffer) => _presenter.PresentFrame(frameBuffer);

    public bool PresentFrame(LayeredFrameData frameData) => _presenter.PresentFrame(frameData);

    public void SetDisplaySize(double width, double height) => _presenter.SetDisplaySize(width, height);

    public void SetCornerRadius(double radius) => _presenter.SetCornerRadius(radius);

    public void SetUpscaleOutputResolution(MacUpscaleOutputResolution outputResolution) => _presenter.SetUpscaleOutputResolution(outputResolution);

    public void SetUpscaleMode(MacUpscaleMode upscaleMode) => _presenter.SetUpscaleMode(upscaleMode);

    public void RequestTemporalHistoryReset(MacMetalTemporalResetReason reason) => _presenter.RequestTemporalHistoryReset(reason);

    public void Dispose() => _presenter.Dispose();
}

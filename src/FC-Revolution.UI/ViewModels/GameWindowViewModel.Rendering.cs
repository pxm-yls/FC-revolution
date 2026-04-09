using System;
using Avalonia.Threading;
using FCRevolution.Backend.Hosting;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Rendering.Abstractions;
using FCRevolution.Rendering.Common;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.ViewModels;

public sealed partial class GameWindowViewModel
{
    private void OnUiTick(object? sender, EventArgs e)
    {
        UpdateTurboPulse();
        var cadenceDecision = GameWindowTimelineRefreshCadenceController.BuildUiTickDecision(TryPresentPendingFrame());
        if (cadenceDecision.ShouldSyncManifest)
            MaybeSyncTimelineState();
    }

    private void UpdateDisplay(uint[] frameBuffer)
    {
        if (_isDisposed)
            return;

        ScreenBitmap = _framePresenter.PresentFrame(frameBuffer, _enhancementMode);
    }

    public void ApplyUpscaleMode(MacUpscaleMode mode)
    {
        bool changed = _configuredUpscaleMode != mode;
        _configuredUpscaleMode = mode;
        bool shouldRequestTemporalReset = changed && _hasInitializedUpscaleMode;
        if (changed)
        {
            OnPropertyChanged(nameof(UpscaleMode));
            OnPropertyChanged(nameof(ConfiguredUpscaleModeLabel));
            if (shouldRequestTemporalReset)
                RequestTemporalHistoryReset(MacMetalTemporalResetReason.UpscaleModeChanged);
        }

        _hasInitializedUpscaleMode = true;

        UpdateSoftwareRendererStatus(OperatingSystem.IsMacOS()
            ? $"等待 presenter 应用 {GetUpscaleModeLabel(mode)}"
            : "当前平台不使用 macOS Metal presenter");
    }

    public void ApplyUpscaleOutputResolution(MacUpscaleOutputResolution outputResolution)
    {
        bool changed = _configuredUpscaleOutputResolution != outputResolution;
        _configuredUpscaleOutputResolution = outputResolution;
        if (changed)
        {
            OnPropertyChanged(nameof(UpscaleOutputResolution));
            OnPropertyChanged(nameof(ConfiguredUpscaleOutputResolutionLabel));
        }

        if (_configuredUpscaleMode != MacUpscaleMode.None)
            UpdateSoftwareRendererStatus($"已选择 {GetUpscaleOutputResolutionLabel(_configuredUpscaleOutputResolution)} 超分输出档位");
    }

    public void ApplyEnhancementMode(PixelEnhancementMode mode)
    {
        _enhancementMode = mode;
        OnPropertyChanged(nameof(EnhancementModeLabel));
    }

    public string EnhancementModeLabel => _enhancementMode switch
    {
        PixelEnhancementMode.SubtleSharpen => "轻微锐化",
        PixelEnhancementMode.CrtScanlines  => "CRT 扫描线",
        PixelEnhancementMode.SoftBlur      => "柔和模糊",
        PixelEnhancementMode.VividColor    => "鲜艳色彩",
        _                                  => "无增强",
    };

    private void UpdateFps()
    {
        _fpsCounter++;
        var elapsed = _fpsWatch.Elapsed.TotalSeconds;
        if (!GameWindowFpsStatusController.ShouldUpdate(elapsed))
            return;

        var uiFps = _fpsCounter / elapsed;
        _fpsCounter = 0;
        _fpsWatch.Restart();
        FpsText = GameWindowFpsStatusController.BuildStatusText(
            uiFps,
            _emuFpsRaw,
            _frameTimeMicros,
            _audio.IsAvailable,
            _audio.InitError);
    }

    private void OnFrameReady(VideoFramePacket framePacket)
    {
        if (_isDisposed)
            return;

        var snapshot = framePacket.Pixels;
        LayeredFrameData? layeredFrame = _layeredFrameBuilder.TryBuildLayeredFrame();
        _framePresenter.EnqueueCoreFrame(snapshot, layeredFrame);
        QueueFramePresent();
    }

    private void OnAudioReady(AudioPacket packet)
    {
        if (_isDisposed)
            return;

        _audio.PushChunk(packet.Samples);
    }

    private void QueueFramePresent()
    {
        if (!_framePresenter.TryAcquirePresentSlot())
            return;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (_isDisposed)
                    return;

                TryPresentPendingFrame();
            }
            finally
            {
                if (_framePresenter.ReleasePresentSlotAndCheckForPending())
                    QueueFramePresent();
            }
        }, DispatcherPriority.Render);
    }

    private bool TryPresentPendingFrame()
    {
        if (_isDisposed)
            return false;

        if (!_framePresenter.TryTakePendingPresentation(out var presentation))
            return false;

        UpdateDisplay(presentation.FrameBuffer);
        if (presentation.LayeredFrame != null)
            LayeredFramePresented?.Invoke(presentation.LayeredFrame);
        else
            RawFramePresented?.Invoke(presentation.FrameBuffer);
        UpdateFps();
        var cadenceDecision = GameWindowTimelineRefreshCadenceController.BuildPresentedFrameDecision(_branchGalleryRefreshTick);
        _branchGalleryRefreshTick = cadenceDecision.NextBranchGalleryRefreshTick;
        if (cadenceDecision.ShouldSyncManifest)
        {
            MaybeSyncTimelineState();
            if (cadenceDecision.ShouldRefreshGallery)
                RefreshBranchGallery();
        }

        return true;
    }

    public uint[]? LastPresentedFrame => _framePresenter.LastPresentedFrame;

    public LayeredFrameData? LastPresentedLayeredFrame => _framePresenter.LastPresentedLayeredFrame;

    internal void UpdateMetalPresenterDiagnostics(MacMetalPresenterDiagnostics diagnostics)
    {
        ApplyRenderDiagnostics(_renderDiagnostics.UpdateMetalPresenterDiagnostics(
            diagnostics,
            _configuredUpscaleOutputResolution));
    }

    internal void UpdateSoftwareRendererStatus(string reason)
    {
        ApplyRenderDiagnostics(_renderDiagnostics.UpdateSoftwareRendererStatus(
            _configuredUpscaleMode,
            _configuredUpscaleOutputResolution,
            ScreenWidth,
            ScreenHeight,
            reason));
    }

    internal void UpdateTemporalHistoryResetStatus(string status)
    {
        ApplyRenderDiagnostics(_renderDiagnostics.UpdateTemporalHistoryResetStatus(
            status,
            _configuredUpscaleMode,
            _configuredUpscaleOutputResolution,
            ScreenWidth,
            ScreenHeight));
    }

    private void RequestTemporalHistoryReset(MacMetalTemporalResetReason reason)
    {
        if (reason == MacMetalTemporalResetReason.None)
            return;

        _layeredFrameBuilder.ResetTemporalHistory();
        string label = GameWindowViewportDiagnosticsController.GetTemporalResetReasonLabel(reason);
        ApplyRenderDiagnostics(_renderDiagnostics.RequestTemporalReset(
            reason,
            _configuredUpscaleMode,
            _configuredUpscaleOutputResolution,
            ScreenWidth,
            ScreenHeight));
        RuntimeDiagnostics.Write("render", $"temporal reset requested | reason={reason} ({label})");
        OnPropertyChanged(nameof(TemporalHistoryResetReason));
        OnPropertyChanged(nameof(TemporalHistoryResetVersion));
    }

    private static string GetUpscaleModeLabel(MacUpscaleMode mode) =>
        GameWindowViewportDiagnosticsController.GetUpscaleModeLabel(mode);

    private static string GetUpscaleOutputResolutionLabel(MacUpscaleOutputResolution outputResolution) =>
        GameWindowViewportDiagnosticsController.GetUpscaleOutputResolutionLabel(outputResolution);

    private void ApplyRenderDiagnostics(GameWindowRenderDiagnosticsUpdate update)
    {
        ViewportRendererLabel = update.ViewportRendererLabel;
        ViewportRenderDiagnostics = update.ViewportRenderDiagnostics;
    }
}

using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using FCRevolution.Backend.Hosting;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed record LanServerStartResult(
    bool IsSuccess,
    bool IsAlreadyRunning,
    WriteableBitmap? QrCode,
    string StatusText,
    string? DiagnosticsText = null,
    string? LastTrafficText = null);

internal sealed record LanServerStopResult(
    bool Stopped,
    WriteableBitmap? QrCode,
    string StatusText,
    string DiagnosticsText,
    string LastTrafficText);

internal sealed record LanPortApplyDecision(
    bool IsValid,
    bool IsChanged,
    bool IsOccupied,
    int ResolvedPort,
    string StatusText);

internal sealed record LanTrafficUpdate(int NextCount, string NextText);

internal sealed class MainWindowLanArcadeController
{
    private readonly ILanArcadeService _lanArcadeService;
    private readonly ILanArcadeDiagnosticsService _lanArcadeDiagnosticsService;
    private readonly Func<string, WriteableBitmap?> _createQrCode;

    public MainWindowLanArcadeController(
        ILanArcadeService lanArcadeService,
        ILanArcadeDiagnosticsService lanArcadeDiagnosticsService,
        Func<string, WriteableBitmap?>? createQrCode = null)
    {
        _lanArcadeService = lanArcadeService;
        _lanArcadeDiagnosticsService = lanArcadeDiagnosticsService;
        _createQrCode = createQrCode ?? LanQrCodeBitmapFactory.Create;
    }

    public string BuildAccessSummary(bool isEnabled, string entryUrl) =>
        _lanArcadeService.IsRunning
            ? $"手机或平板可访问 {entryUrl}"
            : isEnabled ? "局域网点播服务未启动" : "局域网点播功能已关闭";

    public LanPortApplyDecision ValidatePortApply(string portInput, int currentPort, bool isEnabled)
    {
        if (!int.TryParse(portInput, out var port) || port is < 1024 or > 65535)
        {
            return new LanPortApplyDecision(
                IsValid: false,
                IsChanged: false,
                IsOccupied: false,
                ResolvedPort: currentPort,
                StatusText: "局域网端口无效，请输入 1024-65535");
        }

        if (port == currentPort)
        {
            return new LanPortApplyDecision(
                IsValid: true,
                IsChanged: false,
                IsOccupied: false,
                ResolvedPort: port,
                StatusText: $"局域网端口保持为 {port}");
        }

        if (isEnabled && !IsPortAvailable(port))
        {
            return new LanPortApplyDecision(
                IsValid: false,
                IsChanged: false,
                IsOccupied: true,
                ResolvedPort: currentPort,
                StatusText: $"端口 {port} 已被占用，无法启用");
        }

        return new LanPortApplyDecision(
            IsValid: true,
            IsChanged: true,
            IsOccupied: false,
            ResolvedPort: port,
            StatusText: $"已保存局域网端口: {port}");
    }

    public async Task<LanServerStartResult> StartServerAsync(
        int port,
        bool enableWebPad,
        bool enableDebugPages,
        int streamScaleMultiplier,
        int streamJpegQuality,
        LanStreamSharpenMode streamSharpenMode,
        string entryUrl)
    {
        if (_lanArcadeService.IsRunning)
        {
            if (_lanArcadeService.Port == port)
            {
                return new LanServerStartResult(
                    IsSuccess: true,
                    IsAlreadyRunning: true,
                    QrCode: null,
                    StatusText: $"局域网点播已启动: {entryUrl}");
            }

            await StopServerAsync();
        }

        if (!IsPortAvailable(port))
        {
            return new LanServerStartResult(
                IsSuccess: false,
                IsAlreadyRunning: false,
                QrCode: null,
                StatusText: $"局域网点播启动失败: 端口 {port} 已被占用");
        }

        await _lanArcadeService.StartAsync(new BackendHostOptions(
            port,
            EnableWebPad: enableWebPad,
            EnableDebugPages: enableDebugPages,
            StreamScaleMultiplier: streamScaleMultiplier,
            StreamJpegQuality: streamJpegQuality,
            StreamEnhancementMode: LanSharpenToPixelMode(streamSharpenMode)));

        return new LanServerStartResult(
            IsSuccess: true,
            IsAlreadyRunning: false,
            QrCode: _createQrCode(entryUrl),
            StatusText: $"局域网点播已启动: {entryUrl}",
            LastTrafficText: "尚未收到任何局域网请求。");
    }

    public async Task<LanServerStopResult> StopServerAsync()
    {
        var stopped = true;
        if (_lanArcadeService.IsRunning)
            stopped = await _lanArcadeService.StopAsync(TimeSpan.FromMilliseconds(800));

        return new LanServerStopResult(
            Stopped: stopped,
            QrCode: null,
            StatusText: stopped ? "局域网点播功能已关闭" : "局域网点播关闭超时，请稍后重试",
            DiagnosticsText: stopped ? "局域网监听已停止。" : "局域网监听停止超时，可能仍在等待底层套接字释放。",
            LastTrafficText: stopped ? "局域网监听已停止。" : "局域网监听关闭异常，最近命中记录已保留。");
    }

    public async Task<string> BuildDiagnosticsAsync(int port, string entryUrl)
    {
        return await _lanArcadeDiagnosticsService.BuildDiagnosticsAsync(
            port,
            entryUrl,
            _lanArcadeService,
            LanNetworkHelper.GetCandidateLanAddresses());
    }

    public LanFirewallStatus ProbeFirewall()
    {
        return LanFirewallProbe.Probe();
    }

    public LanTrafficUpdate AppendTraffic(string currentText, int currentCount, string message)
    {
        var nextCount = currentCount + 1;
        var merged = string.IsNullOrWhiteSpace(currentText) || currentText.StartsWith("尚未收到", StringComparison.Ordinal)
            ? message
            : $"{message}{Environment.NewLine}{currentText}";

        var lines = merged
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Take(10)
            .ToArray();
        return new LanTrafficUpdate(nextCount, string.Join(Environment.NewLine, lines));
    }

    public void ApplyStreamParameters(int scaleMultiplier, int jpegQuality, LanStreamSharpenMode sharpenMode)
    {
        if (_lanArcadeService.IsRunning)
            _lanArcadeService.UpdateStreamParameters(scaleMultiplier, jpegQuality, sharpenMode);
    }

    public static bool IsPortAvailable(int port)
    {
        var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        return listeners.All(endpoint => endpoint.Port != port);
    }

    private static PixelEnhancementMode LanSharpenToPixelMode(LanStreamSharpenMode mode) => mode switch
    {
        LanStreamSharpenMode.Subtle   => PixelEnhancementMode.SubtleSharpen,
        LanStreamSharpenMode.Standard => PixelEnhancementMode.CrtScanlines,
        LanStreamSharpenMode.Strong   => PixelEnhancementMode.VividColor,
        _                             => PixelEnhancementMode.None,
    };
}

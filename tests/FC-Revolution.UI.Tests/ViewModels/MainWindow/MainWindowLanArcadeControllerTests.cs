using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowLanArcadeControllerTests
{
    [Fact]
    public void BuildAccessSummary_ReflectsServiceAndSwitchState()
    {
        var service = new FakeLanArcadeService { IsRunningValue = true };
        var controller = new MainWindowLanArcadeController(service, new FakeLanArcadeDiagnosticsService("ok"), _ => null);

        Assert.Contains("http://1.2.3.4:9000/", controller.BuildAccessSummary(isEnabled: true, "http://1.2.3.4:9000/"));

        service.IsRunningValue = false;
        Assert.Equal("局域网点播服务未启动", controller.BuildAccessSummary(isEnabled: true, "http://1.2.3.4:9000/"));
        Assert.Equal("局域网点播功能已关闭", controller.BuildAccessSummary(isEnabled: false, "http://1.2.3.4:9000/"));
    }

    [Fact]
    public void ValidatePortApply_RejectsInvalidOrUnchangedPort()
    {
        var controller = new MainWindowLanArcadeController(new FakeLanArcadeService(), new FakeLanArcadeDiagnosticsService("ok"), _ => null);

        var invalid = controller.ValidatePortApply("abc", currentPort: 8888, isEnabled: true);
        Assert.False(invalid.IsValid);
        Assert.Equal(8888, invalid.ResolvedPort);

        var unchanged = controller.ValidatePortApply("8888", currentPort: 8888, isEnabled: true);
        Assert.True(unchanged.IsValid);
        Assert.False(unchanged.IsChanged);
        Assert.Equal("局域网端口保持为 8888", unchanged.StatusText);
    }

    [Fact]
    public async Task StartAndStopServer_ReturnExpectedResultPayload()
    {
        var service = new FakeLanArcadeService();
        var controller = new MainWindowLanArcadeController(service, new FakeLanArcadeDiagnosticsService("ok"), _ => null);

        var start = await controller.StartServerAsync(
            port: 18888,
            enableWebPad: true,
            enableDebugPages: false,
            streamScaleMultiplier: 2,
            streamJpegQuality: 85,
            streamSharpenMode: LanStreamSharpenMode.None,
            entryUrl: "http://127.0.0.1:18888/");
        Assert.True(start.IsSuccess);
        Assert.False(start.IsAlreadyRunning);
        Assert.Equal("尚未收到任何局域网请求。", start.LastTrafficText);
        Assert.Equal(18888, service.Port);

        var stop = await controller.StopServerAsync();
        Assert.True(stop.Stopped);
        Assert.Equal("局域网监听已停止。", stop.DiagnosticsText);
        Assert.Equal("局域网监听已停止。", stop.LastTrafficText);
    }

    [Fact]
    public void AppendTraffic_PrependsAndKeepsAtMostTenLines()
    {
        var controller = new MainWindowLanArcadeController(new FakeLanArcadeService(), new FakeLanArcadeDiagnosticsService("ok"), _ => null);
        var text = "尚未收到任何局域网请求。";
        var count = 0;

        for (var i = 1; i <= 12; i++)
        {
            var updated = controller.AppendTraffic(text, count, $"line-{i}");
            text = updated.NextText;
            count = updated.NextCount;
        }

        Assert.Equal(12, count);
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(10, lines.Length);
        Assert.Equal("line-12", lines[0]);
        Assert.Equal("line-3", lines[^1]);
    }

    private sealed class FakeLanArcadeDiagnosticsService : ILanArcadeDiagnosticsService
    {
        private readonly string _text;

        public FakeLanArcadeDiagnosticsService(string text)
        {
            _text = text;
        }

        public Task<string> BuildDiagnosticsAsync(int port, string entryUrl, ILanArcadeService lanArcadeService, IReadOnlyList<string>? lanCandidates = null)
        {
            return Task.FromResult(_text);
        }
    }

    private sealed class FakeLanArcadeService : ILanArcadeService
    {
        public bool IsRunningValue { get; set; }

        public bool IsRunning => IsRunningValue;

        public int Port { get; private set; } = 18888;

        public string LocalBaseUrl => $"http://127.0.0.1:{Port}/";

        public Task StartAsync(FCRevolution.Backend.Hosting.BackendHostOptions options)
        {
            Port = options.Port;
            IsRunningValue = true;
            return Task.CompletedTask;
        }

        public Task<bool> StopAsync(TimeSpan? timeout = null)
        {
            IsRunningValue = false;
            return Task.FromResult(true);
        }

        public Task<string> GetLocalHealthAsync(TimeSpan? timeout = null)
        {
            return Task.FromResult("ok");
        }

        public void UpdateStreamParameters(int scaleMultiplier, int jpegQuality, LanStreamSharpenMode sharpenMode)
        {
        }

        public void Dispose()
        {
        }
    }
}

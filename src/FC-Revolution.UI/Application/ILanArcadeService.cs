using System;
using System.Threading.Tasks;
using FCRevolution.Backend.Hosting;

namespace FC_Revolution.UI.AppServices;

public interface ILanArcadeService : IDisposable
{
    bool IsRunning { get; }
    int Port { get; }
    string LocalBaseUrl { get; }
    Task StartAsync(BackendHostOptions options);
    Task<bool> StopAsync(TimeSpan? timeout = null);
    Task<string> GetLocalHealthAsync(TimeSpan? timeout = null);
    /// <summary>热更新串流参数，无需重启服务器，立即对下一帧生效。</summary>
    void UpdateStreamParameters(int scaleMultiplier, int jpegQuality, LanStreamSharpenMode sharpenMode);
}

using System;

namespace FC_Revolution.UI.ViewModels;

internal sealed record SessionLaunchResult(
    bool IsSuccess,
    string StatusText,
    string RuntimeDiagnosticsMessage,
    string? RuntimeDiagnosticsExceptionText = null,
    Exception? FailureException = null,
    string? StartupDiagnosticsContext = null);

internal sealed class MainWindowSessionLaunchController
{
    public SessionLaunchResult Launch(string displayName, string mapperDescription, Action launchSession)
    {
        try
        {
            launchSession();
            return new SessionLaunchResult(
                IsSuccess: true,
                StatusText: $"当前正在运行 {displayName}，其核心为 {mapperDescription}",
                RuntimeDiagnosticsMessage: $"启动游戏 {displayName}，其核心为 {mapperDescription}");
        }
        catch (Exception ex)
        {
            return new SessionLaunchResult(
                IsSuccess: false,
                StatusText: $"启动失败: {displayName}，其核心为 {mapperDescription}，{ex.Message}",
                RuntimeDiagnosticsMessage: $"启动失败 {displayName}，其核心为 {mapperDescription}，{ex.Message}",
                RuntimeDiagnosticsExceptionText: ex.ToString(),
                FailureException: ex,
                StartupDiagnosticsContext: $"failed to start game session for {displayName}");
        }
    }
}

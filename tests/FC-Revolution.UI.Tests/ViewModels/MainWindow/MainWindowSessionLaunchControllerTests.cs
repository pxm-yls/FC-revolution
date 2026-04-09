using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowSessionLaunchControllerTests
{
    [Fact]
    public void Launch_OnSuccess_ReturnsSuccessStatusAndRuntimeMessage()
    {
        var controller = new MainWindowSessionLaunchController();
        var launchCalls = 0;

        var result = controller.Launch(
            displayName: "Contra",
            mapperDescription: "Mapper-2",
            launchSession: () => launchCalls++);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, launchCalls);
        Assert.Equal("当前正在运行 Contra，其核心为 Mapper-2", result.StatusText);
        Assert.Equal("启动游戏 Contra，其核心为 Mapper-2", result.RuntimeDiagnosticsMessage);
        Assert.Null(result.RuntimeDiagnosticsExceptionText);
        Assert.Null(result.FailureException);
        Assert.Null(result.StartupDiagnosticsContext);
    }

    [Fact]
    public void Launch_OnFailure_ReturnsFailureStatusAndDiagnosticsPayload()
    {
        var controller = new MainWindowSessionLaunchController();
        var exception = new InvalidOperationException("boom");

        var result = controller.Launch(
            displayName: "Contra",
            mapperDescription: "Mapper-2",
            launchSession: () => throw exception);

        Assert.False(result.IsSuccess);
        Assert.Equal("启动失败: Contra，其核心为 Mapper-2，boom", result.StatusText);
        Assert.Equal("启动失败 Contra，其核心为 Mapper-2，boom", result.RuntimeDiagnosticsMessage);
        Assert.NotNull(result.RuntimeDiagnosticsExceptionText);
        Assert.Contains("InvalidOperationException", result.RuntimeDiagnosticsExceptionText!, StringComparison.Ordinal);
        Assert.Same(exception, result.FailureException);
        Assert.Equal("failed to start game session for Contra", result.StartupDiagnosticsContext);
    }
}

using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowDisposeHandlerTests
{
    [Fact]
    public void Dispose_WhenActive_RunsCleanupSequenceOnce()
    {
        var disposed = false;
        List<string> calls = [];
        var handler = new GameWindowDisposeHandler(
            () => disposed,
            () =>
            {
                disposed = true;
                calls.Add("mark");
            },
            () => calls.Add("clear-frame"),
            () => calls.Add("unsubscribe"),
            () => calls.Add("stop-loop"),
            () => calls.Add("stop-ui"),
            () => calls.Add("stop-toast"),
            () => calls.Add("close-debug"),
            () => calls.Add("dispose-audio"),
            () => calls.Add("dispose-core"),
            () => calls.Add("dispose-presenter"));

        handler.Dispose();
        handler.Dispose();

        Assert.True(disposed);
        Assert.Equal(
        [
            "mark",
            "clear-frame",
            "unsubscribe",
            "stop-loop",
            "stop-ui",
            "stop-toast",
            "close-debug",
            "dispose-audio",
            "dispose-core",
            "dispose-presenter"
        ], calls);
    }
}

using Avalonia.Controls;
using FCRevolution.Storage;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class GameSessionRegistryTests
{
    [Fact]
    public void GetUsableOwner_ReturnsNull_WhenOwnerIsMissing()
    {
        Assert.Null(GameSessionRegistry.GetUsableOwner(null));
    }

    [Fact]
    public void GetUsableOwner_ReturnsOwner_WhenOwnerIsDifferentFromChild()
    {
        var result = AvaloniaThreadingTestHelper.RunOnUiThread(() =>
        {
            var owner = new Window { Title = "Main" };
            var childWindow = new Window { Title = "Child" };

            return (owner, resolved: GameSessionRegistry.GetUsableOwner(owner, childWindow));
        });

        Assert.Same(result.owner, result.resolved);
    }

    [Fact]
    public void GetUsableOwner_IgnoresChildWindowReference()
    {
        var owner = AvaloniaThreadingTestHelper.RunOnUiThread(() =>
        {
            var window = new Window { Title = "Main" };
            return GameSessionRegistry.GetUsableOwner(window, window);
        });

        Assert.Null(owner);
    }

    [Fact]
    public void StartSessionWithInputBindings_ThrowsFriendlyMessage_WhenNoCoreIsAvailable()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-game-session-zero-core-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot,
                DefaultCoreId = string.Empty
            });

            var registry = new GameSessionRegistry();
            var ex = Assert.Throws<InvalidOperationException>(() => registry.StartSessionWithInputBindings(
                "Zero Core",
                "/tmp/zero-core-test.nes",
                GameAspectRatioMode.Native,
                new Dictionary<string, Dictionary<string, Avalonia.Input.Key>>()));

            Assert.Contains("当前没有可用核心", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }
}

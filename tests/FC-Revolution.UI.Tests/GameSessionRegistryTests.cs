using Avalonia.Controls;
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
        var owner = new Window { Title = "Main" };
        var childWindow = new Window { Title = "Child" };

        var resolved = GameSessionRegistry.GetUsableOwner(owner, childWindow);

        Assert.Same(owner, resolved);
    }

    [Fact]
    public void GetUsableOwner_IgnoresChildWindowReference()
    {
        var window = new Window { Title = "Main" };

        var owner = GameSessionRegistry.GetUsableOwner(window, window);

        Assert.Null(owner);
    }
}

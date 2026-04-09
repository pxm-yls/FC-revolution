using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowStatusToastControllerTests
{
    [Fact]
    public void BuildStatusUpdate_AlwaysMapsStatusText()
    {
        var controller = new GameWindowStatusToastController();

        var update = controller.BuildStatusUpdate("running", toastMessage: null);

        Assert.Equal("running", update.StatusText);
        Assert.False(update.ShouldShowToast);
        Assert.Null(update.ToastMessage);
    }

    [Fact]
    public void BuildStatusUpdate_UsesWhitespaceRuleForToast()
    {
        var controller = new GameWindowStatusToastController();

        var whitespace = controller.BuildStatusUpdate("paused", "   ");
        var withToast = controller.BuildStatusUpdate("paused", "toast");

        Assert.False(whitespace.ShouldShowToast);
        Assert.True(withToast.ShouldShowToast);
        Assert.Equal("toast", withToast.ToastMessage);
    }

    [Fact]
    public void BuildToastState_AndClearedState_MapExpectedMessages()
    {
        var controller = new GameWindowStatusToastController();

        var shown = controller.BuildToastState("hello");
        var cleared = controller.BuildClearedToastState();

        Assert.Equal("hello", shown.TransientMessage);
        Assert.Equal(string.Empty, cleared.TransientMessage);
    }
}

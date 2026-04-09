using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class GameWindowAspectRatioProjectionControllerTests
{
    [Theory]
    [InlineData(GameAspectRatioMode.Native, 256d, 240d, "原始 256:240")]
    [InlineData(GameAspectRatioMode.Aspect8By7, 256d, 224d, "8:7")]
    [InlineData(GameAspectRatioMode.Aspect4By3, 320d, 240d, "4:3")]
    [InlineData(GameAspectRatioMode.Aspect16By9, 427d, 240d, "16:9")]
    public void Build_ReturnsExpectedProjection(
        GameAspectRatioMode mode,
        double expectedWidth,
        double expectedHeight,
        string expectedLabel)
    {
        var projection = GameWindowAspectRatioProjectionController.Build(mode);

        Assert.Equal(expectedWidth, projection.Width);
        Assert.Equal(expectedHeight, projection.Height);
        Assert.Equal(expectedLabel, projection.Label);
    }

    [Fact]
    public void Build_FallsBackToNativeProjection_ForUnknownMode()
    {
        var projection = GameWindowAspectRatioProjectionController.Build((GameAspectRatioMode)999);

        Assert.Equal(256d, projection.Width);
        Assert.Equal(240d, projection.Height);
        Assert.Equal("原始 256:240", projection.Label);
    }
}

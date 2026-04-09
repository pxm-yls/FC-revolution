using System;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowResourceLayoutSummaryControllerTests
{
    [Fact]
    public void Build_FormatsAllResourceDirectoriesInExpectedOrder()
    {
        var summary = MainWindowResourceLayoutSummaryController.Build(
            "/tmp/root",
            "/tmp/root/roms",
            "/tmp/root/previews",
            "/tmp/root/configs",
            "/tmp/root/saves",
            "/tmp/root/images");

        var expected =
            $"统一资源根目录: /tmp/root{Environment.NewLine}" +
            $"ROM: /tmp/root/roms{Environment.NewLine}" +
            $"预览: /tmp/root/previews{Environment.NewLine}" +
            $"配置: /tmp/root/configs{Environment.NewLine}" +
            $"存档: /tmp/root/saves{Environment.NewLine}" +
            "图片: /tmp/root/images";

        Assert.Equal(expected, summary);
    }

    [Fact]
    public void Build_PreservesProvidedPathsWithoutNormalization()
    {
        var summary = MainWindowResourceLayoutSummaryController.Build(
            "",
            "roms",
            "previews",
            "configs",
            "saves",
            "images");

        Assert.StartsWith($"统一资源根目录: {Environment.NewLine}", summary);
        Assert.Contains($"{Environment.NewLine}ROM: roms{Environment.NewLine}", summary);
        Assert.EndsWith("图片: images", summary);
    }
}

using System;

namespace FC_Revolution.UI.ViewModels;

internal static class MainWindowResourceLayoutSummaryController
{
    public static string Build(
        string resourceRootPath,
        string romsDirectory,
        string previewVideosDirectory,
        string configurationsDirectory,
        string savesDirectory,
        string imagesDirectory)
    {
        return
            $"统一资源根目录: {resourceRootPath}{Environment.NewLine}" +
            $"ROM: {romsDirectory}{Environment.NewLine}" +
            $"预览: {previewVideosDirectory}{Environment.NewLine}" +
            $"配置: {configurationsDirectory}{Environment.NewLine}" +
            $"存档: {savesDirectory}{Environment.NewLine}" +
            $"图片: {imagesDirectory}";
    }
}

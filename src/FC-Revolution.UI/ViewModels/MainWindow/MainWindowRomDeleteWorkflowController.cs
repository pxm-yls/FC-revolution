using System;
using System.IO;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct RomDeleteWorkflowResult(string StatusText);

internal sealed class MainWindowRomDeleteWorkflowController
{
    private readonly Func<string, string> _buildResourceSummary;
    private readonly Action<string> _deleteRomAssociatedResources;
    private readonly Func<string, bool> _fileExists;
    private readonly Action<string> _deleteFile;

    public MainWindowRomDeleteWorkflowController(MainWindowResourceManagementController resourceManagementController)
        : this(
            resourceManagementController.BuildRomAssociatedResourceSummary,
            resourceManagementController.DeleteRomAssociatedResources,
            File.Exists,
            File.Delete)
    {
    }

    internal MainWindowRomDeleteWorkflowController(
        Func<string, string> buildResourceSummary,
        Action<string> deleteRomAssociatedResources,
        Func<string, bool> fileExists,
        Action<string> deleteFile)
    {
        _buildResourceSummary = buildResourceSummary;
        _deleteRomAssociatedResources = deleteRomAssociatedResources;
        _fileExists = fileExists;
        _deleteFile = deleteFile;
    }

    public string BuildResourceSummary(string romPath) => _buildResourceSummary(romPath);

    public RomDeleteWorkflowResult ExecuteConfirmedDelete(string romPath, string displayName, bool deleteAssociatedResources)
    {
        if (_fileExists(romPath))
            _deleteFile(romPath);

        if (deleteAssociatedResources)
            _deleteRomAssociatedResources(romPath);

        return new(deleteAssociatedResources
            ? $"已删除游戏及关联资源: {displayName}"
            : $"已删除游戏: {displayName}");
    }
}

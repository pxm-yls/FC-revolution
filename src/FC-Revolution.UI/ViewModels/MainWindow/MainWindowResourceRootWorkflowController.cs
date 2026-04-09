using System;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct ResourceRootWorkflowResult(
    string ResourceRootPath,
    string StatusText);

internal sealed class MainWindowResourceRootWorkflowController
{
    private readonly Func<string?, string> _configureResourceRoot;

    public MainWindowResourceRootWorkflowController(MainWindowResourceManagementController resourceManagementController)
        : this(resourceManagementController.ConfigureResourceRoot)
    {
    }

    internal MainWindowResourceRootWorkflowController(Func<string?, string> configureResourceRoot)
    {
        _configureResourceRoot = configureResourceRoot;
    }

    public ResourceRootWorkflowResult Apply(
        string? input,
        Action saveSystemConfig,
        Action refreshRomLibrary,
        Action updateCurrentRomPresentation)
    {
        var resourceRootPath = _configureResourceRoot(input);
        saveSystemConfig();
        refreshRomLibrary();
        updateCurrentRomPresentation();
        return new(resourceRootPath, $"已更新资源根目录: {resourceRootPath}");
    }
}

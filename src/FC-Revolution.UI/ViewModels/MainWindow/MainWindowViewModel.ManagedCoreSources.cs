using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using FCRevolution.Storage;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

public partial class MainWindowViewModel
{
    public string CoreProbePathsInput
    {
        get => _managedCoreProbePathsInput;
        set => SetProperty(ref _managedCoreProbePathsInput, value);
    }

    public string CoreProbePathsHint =>
        "附加开发核心探测目录每行填写一个；兼容目录 cores/managed 与资源根目录下的开发核心目录会自动纳入。";

    public string ManagedCoreReloadHint =>
        "重新加载会刷新已安装核心包与开发探测目录，仅影响后续新建会话；已运行会话不会切换核心。";

    public string CoreSourceSummary =>
        $"当前已发现 {InstalledCoreManifests.Count} 个核心；附加开发目录 {_managedCoreProbePaths.Count} 个，有效探测目录 {EffectiveCoreProbeDirectories.Count} 个。";

    public IReadOnlyList<string> EffectiveCoreProbeDirectories =>
        SystemConfigProfile.ResolveEffectiveCoreProbeDirectories(ResourceRootPath, _managedCoreProbePaths);

    public string EffectiveCoreProbeDirectoriesSummary => EffectiveCoreProbeDirectories.Count == 0
        ? "暂无有效核心探测目录。"
        : string.Join(Environment.NewLine, EffectiveCoreProbeDirectories.Select(path => $"- {path}"));

    public bool CanOpenManagedCoreVersionManagement => false;

    public bool CanOpenManagedCoreRemoteCatalog => false;

    public string ManagedCoreVersionManagementHint =>
        "预留给后续同核心多版本切换、清理与本地版本拓扑管理；当前只保留界面入口，不接实际流程。";

    public string ManagedCoreRemoteCatalogHint =>
        "预留给后续远程核心目录、下载与更新；当前版本不接入联网能力，仅保留入口位置。";

    [RelayCommand]
    private async Task InstallManagedCorePackageAsync()
    {
        var sourcePath = await PickSingleFileAsync(
            "安装核心包",
            new FilePickerFileType("FC-Revolution Core Package") { Patterns = ["*.fcrcore.zip", "*.zip"] },
            new FilePickerFileType("Zip Archive") { Patterns = ["*.zip"] });
        if (string.IsNullOrWhiteSpace(sourcePath))
            return;

        try
        {
            InstallManagedCoreFromPath(sourcePath);
        }
        catch (Exception ex)
        {
            StatusText = $"安装核心包失败: {ex.Message}";
        }
    }

    public bool CanExportSelectedManagedCore =>
        GetSelectedManagedCoreCatalogEntry() is not null;

    [RelayCommand(CanExecute = nameof(CanExportSelectedManagedCore))]
    private async Task ExportSelectedManagedCorePackageAsync()
    {
        var selectedEntry = GetSelectedManagedCoreCatalogEntry();
        if (selectedEntry is null)
        {
            StatusText = "当前没有可导出的核心。";
            return;
        }

        var topLevel = GetDesktopMainWindow();
        if (topLevel == null)
            return;

        var suggestedFileName = $"{AppObjectStorage.SanitizeFileName(selectedEntry.Manifest.CoreId)}-{AppObjectStorage.SanitizeFileName(selectedEntry.Manifest.Version)}.fcrcore.zip";
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出核心包",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "zip",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType("FC-Revolution Core Package")
                {
                    Patterns = ["*.fcrcore.zip", "*.zip"]
                }
            ]
        });
        if (file == null)
            return;

        var destinationPath = file.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            StatusText = "当前导出位置不是本地文件系统路径，暂不支持直接导出。";
            return;
        }

        try
        {
            var exportResult = _managedCoreExportController.Export(selectedEntry, destinationPath);
            StatusText = $"已导出核心包：{Path.GetFileName(exportResult.PackagePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"导出核心包失败: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanUninstallSelectedManagedCore))]
    private void UninstallSelectedManagedCore()
    {
        var selectedEntry = GetSelectedManagedCoreCatalogEntry();
        if (selectedEntry is not { CanUninstall: true } entry)
        {
            StatusText = "当前所选核心不可卸载。";
            return;
        }

        try
        {
            UninstallManagedCore(entry);
        }
        catch (Exception ex)
        {
            StatusText = $"卸载核心失败: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenManagedCoreVersionManagement))]
    private void OpenManagedCoreVersionManagement()
    {
    }

    [RelayCommand(CanExecute = nameof(CanOpenManagedCoreRemoteCatalog))]
    private void OpenManagedCoreRemoteCatalog()
    {
    }

    [RelayCommand]
    private void ApplyCoreProbePaths()
    {
        try
        {
            _managedCoreProbePaths = ParseCoreProbePathsInput(CoreProbePathsInput);
            CoreProbePathsInput = FormatCoreProbePathsInput(_managedCoreProbePaths);
            RefreshManagedCoreCatalogState();
            SaveSystemConfig();
            StatusText = $"已更新开发核心来源，共 {EffectiveCoreProbeDirectories.Count} 个有效探测目录；仅影响后续新建会话。";
        }
        catch (Exception ex)
        {
            StatusText = $"更新开发核心来源失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ReloadCoreSources()
    {
        try
        {
            var profile = SystemConfigProfile.Load();
            _managedCoreProbePaths = [.. profile.CoreProbePaths];
            CoreProbePathsInput = FormatCoreProbePathsInput(_managedCoreProbePaths);
            RefreshManagedCoreCatalogState();
            StatusText = $"已重新加载核心来源，共 {EffectiveCoreProbeDirectories.Count} 个有效探测目录；运行中的会话保持不变。";
        }
        catch (Exception ex)
        {
            StatusText = $"重新加载核心来源失败: {ex.Message}";
        }
    }

    private void LoadManagedCoreSourceSettings(SystemConfigProfile profile)
    {
        _managedCoreProbePaths = [.. profile.CoreProbePaths];
        CoreProbePathsInput = FormatCoreProbePathsInput(_managedCoreProbePaths);
        RefreshManagedCoreCatalogState();
    }

    private void InstallManagedCoreFromPath(string sourcePath)
    {
        var installResult = _managedCoreInstallController.Install(sourcePath, ResourceRootPath);
        RefreshManagedCoreCatalogState();

        StatusText = installResult.ReplacedExistingCore
            ? $"已更新核心包：{installResult.Manifest.DisplayName}"
            : $"已安装核心包：{installResult.Manifest.DisplayName}";
    }

    private void UninstallManagedCore(MainWindowManagedCoreCatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var wasSelectedDefaultCore = string.Equals(DefaultCoreId, entry.Manifest.CoreId, StringComparison.OrdinalIgnoreCase);
        if (!_managedCoreInstallController.Uninstall(entry, ResourceRootPath))
        {
            RefreshManagedCoreCatalogState();
            StatusText = $"未找到可卸载的核心：{entry.Manifest.DisplayName}";
            return;
        }

        RefreshManagedCoreCatalogState();
        if (wasSelectedDefaultCore)
            SaveSystemConfig();

        StatusText = $"已卸载核心：{entry.Manifest.DisplayName}";
    }

    private void RefreshManagedCoreCatalogState()
    {
        var catalogState = _managedCoreCatalogController.LoadCatalog(ResourceRootPath, _managedCoreProbePaths);
        _managedCoreCatalogEntries = catalogState.Entries;
        _installedCoreManifests = catalogState.Manifests;

        var normalizedDefaultCoreId = NormalizeConfiguredCoreId(_defaultCoreId);
        if (!string.Equals(_defaultCoreId, normalizedDefaultCoreId, StringComparison.OrdinalIgnoreCase))
            _defaultCoreId = normalizedDefaultCoreId;

        OnPropertyChanged(nameof(CoreSourceSummary));
        OnPropertyChanged(nameof(EffectiveCoreProbeDirectories));
        OnPropertyChanged(nameof(EffectiveCoreProbeDirectoriesSummary));
        OnPropertyChanged(nameof(InstalledCoreManifests));
        OnPropertyChanged(nameof(DefaultCoreId));
        OnPropertyChanged(nameof(SelectedDefaultCoreManifest));
        OnPropertyChanged(nameof(DefaultCoreDisplayName));
        OnPropertyChanged(nameof(DefaultCoreSummary));
        OnPropertyChanged(nameof(InstalledCoreCatalogSummary));
        OnPropertyChanged(nameof(SelectedDefaultCoreSystemId));
        OnPropertyChanged(nameof(SelectedDefaultCoreVersion));
        OnPropertyChanged(nameof(SelectedDefaultCoreBinaryKind));
        OnPropertyChanged(nameof(SelectedDefaultCoreSourceLabel));
        OnPropertyChanged(nameof(SelectedDefaultCoreRemovabilityLabel));
        OnPropertyChanged(nameof(SelectedDefaultCoreAssemblyPathDisplay));
        OnPropertyChanged(nameof(SelectedDefaultCoreSourceSummary));
        OnPropertyChanged(nameof(CanUninstallSelectedManagedCore));
        OnPropertyChanged(nameof(CanExportSelectedManagedCore));
        UninstallSelectedManagedCoreCommand.NotifyCanExecuteChanged();
        ExportSelectedManagedCorePackageCommand.NotifyCanExecuteChanged();
    }

    private MainWindowManagedCoreCatalogEntry? GetSelectedManagedCoreCatalogEntry() =>
        _managedCoreCatalogEntries.FirstOrDefault(entry =>
            string.Equals(entry.Manifest.CoreId, DefaultCoreId, StringComparison.OrdinalIgnoreCase));

    private static string FormatCoreProbePathsInput(IEnumerable<string> probePaths) =>
        string.Join(Environment.NewLine, probePaths);

    private static List<string> ParseCoreProbePathsInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return [];

        var normalizedPaths = new List<string>();
        var seen = new HashSet<string>(GetCorePathComparer());
        foreach (var line in input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine))
                continue;

            var normalizedPath = Path.GetFullPath(trimmedLine);
            if (seen.Add(normalizedPath))
                normalizedPaths.Add(normalizedPath);
        }

        return normalizedPaths;
    }

    private static StringComparer GetCorePathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}

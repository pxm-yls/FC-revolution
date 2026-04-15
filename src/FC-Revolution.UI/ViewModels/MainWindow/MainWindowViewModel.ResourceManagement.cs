using System;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using FCRevolution.Storage;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

public partial class MainWindowViewModel
{
    public string ResourceCleanupSummary
    {
        get => _resourceCleanupSummary;
        private set => SetProperty(ref _resourceCleanupSummary, value);
    }

    public string ResourceCleanupResultText
    {
        get => _resourceCleanupResultText;
        private set => SetProperty(ref _resourceCleanupResultText, value);
    }

    public bool CleanupPreviewAnimationsSelected
    {
        get => _cleanupPreviewAnimationsSelected;
        set
        {
            if (SetProperty(ref _cleanupPreviewAnimationsSelected, value))
                OnPropertyChanged(nameof(HasSelectedCleanupTargets));
        }
    }

    public bool CleanupThumbnailsSelected
    {
        get => _cleanupThumbnailsSelected;
        set
        {
            if (SetProperty(ref _cleanupThumbnailsSelected, value))
                OnPropertyChanged(nameof(HasSelectedCleanupTargets));
        }
    }

    public bool CleanupTimelineSavesSelected
    {
        get => _cleanupTimelineSavesSelected;
        set
        {
            if (SetProperty(ref _cleanupTimelineSavesSelected, value))
                OnPropertyChanged(nameof(HasSelectedCleanupTargets));
        }
    }

    public bool CleanupExportVideosSelected
    {
        get => _cleanupExportVideosSelected;
        set
        {
            if (SetProperty(ref _cleanupExportVideosSelected, value))
                OnPropertyChanged(nameof(HasSelectedCleanupTargets));
        }
    }

    public bool HasSelectedCleanupTargets => BuildCleanupSelectionState().HasSelectedCleanupTargets;

    public string ResourceRootPath
    {
        get => _resourceRootPath;
        private set
        {
            if (SetProperty(ref _resourceRootPath, value))
            {
                ResourceRootPathInput = value;
                OnPropertyChanged(nameof(ResourceLayoutSummary));
                OnPropertyChanged(nameof(ManagedCoreInstallDirectory));
                OnPropertyChanged(nameof(ManagedCoreInstallHint));
                OnPropertyChanged(nameof(CoreSourceSummary));
                OnPropertyChanged(nameof(EffectiveCoreProbeDirectories));
                OnPropertyChanged(nameof(EffectiveCoreProbeDirectoriesSummary));
            }
        }
    }

    public string ResourceRootPathInput
    {
        get => _resourceRootPathInput;
        set => SetProperty(ref _resourceRootPathInput, value);
    }

    public string ResourceLayoutSummary =>
        MainWindowResourceLayoutSummaryController.Build(
            ResourceRootPath,
            AppObjectStorage.GetRomsDirectory(),
            AppObjectStorage.GetPreviewVideosDirectory(),
            AppObjectStorage.GetConfigurationsDirectory(),
            AppObjectStorage.GetSavesDirectory(),
            AppObjectStorage.GetImagesDirectory());

    public string ResourceCleanupHint =>
        "可按类别清理预览动画、缩略图/封面、时间线存档和导出视频。清理不会删除 ROM 本体，也不会删除系统配置。";

    [RelayCommand]
    private void ApplyResourceRoot()
    {
        try
        {
            var workflowResult = _resourceRootWorkflowController.Apply(
                ResourceRootPathInput,
                SaveSystemConfig,
                RefreshRomLibrary,
                UpdateCurrentRomPresentation);
            ResourceRootPath = workflowResult.ResourceRootPath;
            RefreshManagedCoreCatalogState();
            SaveSystemConfig();
            StatusText = workflowResult.StatusText;
        }
        catch (Exception ex)
        {
            StatusText = $"更新资源根目录失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportPreviewVideoForRomAsync(RomLibraryItem? rom)
    {
        if (rom == null)
            return;

        var sourcePath = await PickSingleFileAsync(
            "导入预览视频",
            new FilePickerFileType("视频文件") { Patterns = ["*.mp4", "*.mov", "*.m4v", "*.webm"] });
        if (string.IsNullOrWhiteSpace(sourcePath))
            return;

        try
        {
            var workflowResult = _romResourceImportWorkflowController.ImportPreviewVideo(
                rom,
                sourcePath,
                ReferenceEquals(CurrentRom, rom),
                TryLoadItemPreview,
                RefreshCurrentRomPreviewAssetState,
                SyncCurrentPreviewBitmapFromCurrentRom);

            PreviewStatusText = workflowResult.PreviewStatusText ?? PreviewStatusText;
            StatusText = workflowResult.StatusText;
        }
        catch (Exception ex)
        {
            StatusText = $"导入预览失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportCoverImageForRomAsync(RomLibraryItem? rom)
    {
        if (rom == null)
            return;

        var sourcePath = await PickSingleFileAsync(
            "导入封面图",
            new FilePickerFileType("图片文件") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"] });
        if (string.IsNullOrWhiteSpace(sourcePath))
            return;

        try
        {
            var workflowResult = _romResourceImportWorkflowController.ImportCoverImage(rom, sourcePath);
            StatusText = workflowResult.StatusText;
        }
        catch (Exception ex)
        {
            StatusText = $"导入封面失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportArtworkImageForRomAsync(RomLibraryItem? rom)
    {
        if (rom == null)
            return;

        var sourcePath = await PickSingleFileAsync(
            "导入附加图片",
            new FilePickerFileType("图片文件") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"] });
        if (string.IsNullOrWhiteSpace(sourcePath))
            return;

        try
        {
            var workflowResult = _romResourceImportWorkflowController.ImportArtworkImage(rom, sourcePath);
            StatusText = workflowResult.StatusText;
        }
        catch (Exception ex)
        {
            StatusText = $"导入图片失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SelectAllCleanupTargets()
    {
        ApplyCleanupSelectionState(MainWindowResourceCleanupSelectionController.SelectAll());
    }

    [RelayCommand]
    private void ClearCleanupTargetSelection()
    {
        ApplyCleanupSelectionState(MainWindowResourceCleanupSelectionController.ClearAll());
    }

    [RelayCommand]
    private void ExecuteResourceCleanup()
    {
        var selection = BuildCleanupSelectionState().ToSelection();
        try
        {
            var workflowResult = _resourceCleanupWorkflowController.ExecuteCleanup(selection, _romLibrary);

            ResourceCleanupSummary = workflowResult.SummaryText;
            ResourceCleanupResultText = workflowResult.ResultText;
            if (!workflowResult.ShouldRefreshLibrary)
                return;

            RefreshRomLibrary();
            StatusText = ResourceCleanupResultText;
        }
        catch (Exception ex)
        {
            ResourceCleanupResultText = $"清理失败: {ex.Message}";
            StatusText = ResourceCleanupResultText;
        }
    }

    private void DeleteRomAssociatedResources(string romPath)
    {
        _resourceManagementController.DeleteRomAssociatedResources(romPath);
    }

    private string BuildRomAssociatedResourceSummary(string romPath)
    {
        return _resourceManagementController.BuildRomAssociatedResourceSummary(romPath);
    }

    private MainWindowResourceCleanupSelectionState BuildCleanupSelectionState() =>
        MainWindowResourceCleanupSelectionController.Build(
            CleanupPreviewAnimationsSelected,
            CleanupThumbnailsSelected,
            CleanupTimelineSavesSelected,
            CleanupExportVideosSelected);

    private void ApplyCleanupSelectionState(MainWindowResourceCleanupSelectionState state)
    {
        CleanupPreviewAnimationsSelected = state.CleanupPreviewAnimationsSelected;
        CleanupThumbnailsSelected = state.CleanupThumbnailsSelected;
        CleanupTimelineSavesSelected = state.CleanupTimelineSavesSelected;
        CleanupExportVideosSelected = state.CleanupExportVideosSelected;
    }

    private void RefreshResourceCleanupSummary()
    {
        ResourceCleanupSummary = _resourceCleanupWorkflowController.BuildCleanupSummary();
    }
}

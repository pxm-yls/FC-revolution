using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

public partial class MainWindowViewModel
{
    private void UpdateCurrentRomPresentation()
    {
        if (CurrentRom == null)
        {
            CurrentRomName = "尚未选择游戏";
            CurrentRomPathText = $"工作目录: {GetRomDirectory()}";
            PreviewStatusText = IsGeneratingPreview ? PreviewStatusText : "尚未生成预览";
            return;
        }

        CurrentRomName = CurrentRom.DisplayName;
        CurrentRomPathText = CurrentRom.Path;
        if (!IsGeneratingPreview)
        {
            PreviewStatusText = CurrentRom.HasPreview
                ? $"已缓存 {PreviewDurationSeconds} 秒自动预览"
                : "当前没有预览，可在设置中一键生成";
        }
    }

    private void LoadPreviewForCurrentRom()
        => _previewLoadController.LoadPreviewForCurrentRom(
            CurrentRom,
            IsGeneratingPreview,
            ResolvePreviewPlaybackPath,
            File.Exists,
            () => CurrentPreviewBitmap = null,
            StopPreviewPlayback,
            status => PreviewStatusText = status,
            TryLoadItemPreview,
            item => CurrentPreviewBitmap = item.CurrentPreviewBitmap,
            () => _romLibrary.Any(item => item.IsPreviewAnimated),
            StartPreviewPlayback);

    private void StopPreviewPlayback()
        => _previewLoadController.StopPreviewPlayback(
            _romLibrary.Any(item => item.IsPreviewAnimated),
            () => _previewTickCounter = 0,
            () => _previewPlaybackWatch.Restart(),
            () => _previewTimer.Stop(),
            () => PreviewDebugText = "",
            () => _previewTimer.Start());

    private void StartPreviewPlayback()
        => _previewLoadController.StartPreviewPlayback(
            _previewTimer.IsEnabled,
            () => _previewTickCounter = 0,
            () => _previewPlaybackWatch.Restart(),
            () => _previewTimer.Stop(),
            () => PreviewDebugText = "",
            () => _previewTimer.Start());

    private void RestartPreviewPlayback()
        => _previewLoadController.RestartPreviewPlayback(
            _previewTimer.IsEnabled,
            () => _previewTickCounter = 0,
            () => _previewPlaybackWatch.Restart(),
            () => _previewTimer.Stop(),
            () => PreviewDebugText = "",
            () => _previewTimer.Start());

    private void ApplyLoadedPreviewPlaybackState(RomLibraryItem item, bool restartPlayback)
        => _previewLoadController.ApplyLoadedPreviewPlaybackState(
            item,
            ReferenceEquals(CurrentRom, item),
            restartPlayback,
            _previewTimer.IsEnabled,
            _previewPlaybackWatch.ElapsedMilliseconds,
            loaded => CurrentPreviewBitmap = loaded.CurrentPreviewBitmap,
            RestartPreviewPlayback);

    private void TryLoadItemPreview(RomLibraryItem item)
        => _previewLoadController.TryLoadItemPreview(
            item,
            rom => _previewStreamController.LoadPreviewStream(
                rom.Path,
                ResolvePreviewPlaybackPath,
                GetPreviewPath,
                (legacyPath, migratedPath) => _previewGenerationController.UpgradeLegacyPreview(legacyPath, migratedPath)),
            debugText => PreviewDebugText = debugText,
            interval => _previewTimer.Interval = interval,
            rom => ReferenceEquals(CurrentRom, rom),
            (loadedRom, shouldRestartPlayback) => ApplyLoadedPreviewPlaybackState(loadedRom, shouldRestartPlayback),
            status => StatusText = status,
            (romPath, isAnimated, intervalMs, frameCount) =>
                _ = Task.Run(() => RomConfigProfile.SavePreviewMetadata(romPath, isAnimated, intervalMs, frameCount)));

    private void RefreshCurrentRomPreviewAssetState()
    {
        LoadPreviewForCurrentRom();
        OnPropertyChanged(nameof(PreviewActionText));
        OnPropertyChanged(nameof(PreviewQuickActionText));
        OnPropertyChanged(nameof(CurrentRomActionSummary));
    }

    private void SyncCurrentPreviewBitmapFromCurrentRom()
    {
        CurrentPreviewBitmap = CurrentRom?.CurrentPreviewBitmap;
    }

    private void UpdateDiscDisplayBitmap()
    {
        DiscDisplayBitmap = ShouldShowLiveGameOnDisc()
            ? ScreenBitmap
            : CurrentPreviewBitmap;
    }

    private bool ShouldShowLiveGameOnDisc() =>
        IsRomLoaded &&
        _romPath != null &&
        CurrentRom != null &&
        PathsEqual(_romPath, CurrentRom.Path) &&
        ScreenBitmap != null;
}

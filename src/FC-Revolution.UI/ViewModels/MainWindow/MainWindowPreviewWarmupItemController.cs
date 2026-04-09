using System;
using System.Threading;
using System.Threading.Tasks;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowPreviewWarmupItemController
{
    private readonly MainWindowPreviewStreamController _previewStreamController;

    public MainWindowPreviewWarmupItemController(MainWindowPreviewStreamController previewStreamController)
    {
        _previewStreamController = previewStreamController;
    }

    public async Task WarmItemAsync(
        RomLibraryItem item,
        CancellationToken cancellationToken,
        Func<RomLibraryItem, PreviewStreamLoadResult> loadPreviewStream,
        Func<Action, Task> runOnUiThreadAsync,
        Func<RomLibraryItem, bool> isCurrentRomItem,
        Action<TimeSpan> setPreviewTimerInterval,
        Action<RomLibraryItem, bool> applyLoadedPreviewPlaybackState,
        Action<RomLibraryItem> clearPreviewFrames,
        Action<string, bool, int, int, CancellationToken> persistPreviewMetadata)
    {
        try
        {
            var loadResult = await Task.Run(() => loadPreviewStream(item), cancellationToken);

            if (!loadResult.FileExists || loadResult.Preview == null || loadResult.Metadata == null)
            {
                await runOnUiThreadAsync(() => clearPreviewFrames(item));
                return;
            }

            await runOnUiThreadAsync(() =>
            {
                item.UpdatePreviewFilePath(loadResult.PlaybackPath);
                var preview = loadResult.Preview!;
                item.SetPreviewStream(preview);
                if (preview.IntervalMs > 0)
                    setPreviewTimerInterval(TimeSpan.FromMilliseconds(preview.IntervalMs));

                applyLoadedPreviewPlaybackState(item, isCurrentRomItem(item));
            });

            if (_previewStreamController.ShouldPersistMetadata(
                    item.KnownPreviewIsAnimated,
                    item.KnownPreviewIntervalMs,
                    item.KnownPreviewFrameCount,
                    loadResult.Metadata))
            {
                persistPreviewMetadata(
                    item.Path,
                    loadResult.Metadata.IsAnimated,
                    loadResult.Metadata.IntervalMs,
                    loadResult.Metadata.FrameCount,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            await runOnUiThreadAsync(() => clearPreviewFrames(item));
        }
    }
}

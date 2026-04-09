using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

public partial class MainWindowViewModel
{
    private void ShowBranchGalleryPreview(RomLibraryItem rom)
    {
        _ = ShowBranchGalleryPreviewAsync(rom);
    }

    private async Task ShowBranchGalleryPreviewAsync(RomLibraryItem rom)
    {
        ResetBranchGalleryPreviewState();
        CurrentRom = rom;
        IsBranchGalleryPreviewLoading = true;

        var cts = new CancellationTokenSource();
        var previousCts = Interlocked.Exchange(ref _branchGalleryPreviewCts, cts);
        previousCts?.Cancel();
        previousCts?.Dispose();

        bool hasGallery;
        try
        {
            hasGallery = await Task.Run(() => HasBranchGalleryForRom(rom.Path), cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (!cts.IsCancellationRequested &&
                CurrentRom != null &&
                PathsEqual(CurrentRom.Path, rom.Path))
            {
                IsBranchGalleryPreviewLoading = false;
            }
        }

        if (cts.IsCancellationRequested || CurrentRom == null || !PathsEqual(CurrentRom.Path, rom.Path))
            return;

        if (!hasGallery)
            return;

        IsBranchGalleryPreviewOpen = true;
        OnPropertyChanged(nameof(BranchGalleryPreviewTitle));
        OnPropertyChanged(nameof(BranchGalleryPreviewHint));
        OnPropertyChanged(nameof(BranchGalleryPreviewBitmap));
    }

    private void ResetBranchGalleryPreviewState()
    {
        var cts = Interlocked.Exchange(ref _branchGalleryPreviewCts, null);
        cts?.Cancel();
        cts?.Dispose();
        IsBranchGalleryPreviewLoading = false;
        IsBranchGalleryPreviewOpen = false;
    }

    private bool HasBranchGalleryForCurrentRom() => CurrentRom != null && HasBranchGalleryForRom(CurrentRom.Path);

    private bool HasBranchGalleryForRom(string romPath) =>
        _branchTree.AllBranches().Any(branch => PathsEqual(branch.RomPath, romPath));

    public void SetBranchGalleryPreviewMargin(Thickness margin) => BranchGalleryPreviewMargin = margin;
}

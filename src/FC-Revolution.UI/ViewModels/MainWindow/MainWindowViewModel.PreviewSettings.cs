using System;
using CommunityToolkit.Mvvm.Input;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private void IncreasePreviewGenerationParallelism()
    {
        if (PreviewGenerationParallelism >= 8)
            return;

        PreviewGenerationParallelism++;
        OnPropertyChanged(nameof(PreviewGenerationParallelismSummary));
    }

    [RelayCommand]
    private void DecreasePreviewGenerationParallelism()
    {
        if (PreviewGenerationParallelism <= 1)
            return;

        PreviewGenerationParallelism--;
        OnPropertyChanged(nameof(PreviewGenerationParallelismSummary));
    }

    [RelayCommand]
    private void IncreasePreviewGenerationSpeed()
    {
        if (PreviewGenerationSpeedMultiplier >= 10)
            return;

        PreviewGenerationSpeedMultiplier++;
    }

    [RelayCommand]
    private void DecreasePreviewGenerationSpeed()
    {
        if (PreviewGenerationSpeedMultiplier <= 1)
            return;

        PreviewGenerationSpeedMultiplier--;
    }

    [RelayCommand]
    private void UsePreviewEncodingModeAuto() => SelectedPreviewEncodingMode = PreviewEncodingMode.Auto;

    [RelayCommand]
    private void UsePreviewEncodingModeSoftware() => SelectedPreviewEncodingMode = PreviewEncodingMode.Software;

    [RelayCommand]
    private void SetPreviewResolutionScale50() => PreviewResolutionScale = 0.5;

    [RelayCommand]
    private void SetPreviewResolutionScale75() => PreviewResolutionScale = 0.75;

    [RelayCommand]
    private void SetPreviewResolutionScale100() => PreviewResolutionScale = 1.0;

    [RelayCommand]
    private void IncreasePreviewResolutionScale()
    {
        if (PreviewResolutionScale >= 1.0)
            return;

        PreviewResolutionScale = Math.Min(1.0, PreviewResolutionScale + 0.05);
    }

    [RelayCommand]
    private void DecreasePreviewResolutionScale()
    {
        if (PreviewResolutionScale <= 0.5)
            return;

        PreviewResolutionScale = Math.Max(0.5, PreviewResolutionScale - 0.05);
    }

    [RelayCommand]
    private void SetPreviewPreloadWindow1s() => PreviewPreloadWindowSeconds = 1;

    [RelayCommand]
    private void SetPreviewPreloadWindow2s() => PreviewPreloadWindowSeconds = 2;

    [RelayCommand]
    private void SetPreviewPreloadWindow3s() => PreviewPreloadWindowSeconds = 3;
}

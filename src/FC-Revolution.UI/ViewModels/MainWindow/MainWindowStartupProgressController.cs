using System;

namespace FC_Revolution.UI.ViewModels;

internal sealed record MainWindowStartupProgressState(
    string CurrentStep,
    string GameListStatus,
    string PreviewStatus,
    string LanStatus,
    bool IsVisible);

internal sealed record MainWindowStartupProgressUpdateResult(
    MainWindowStartupProgressState State,
    bool Changed,
    bool CurrentStepChanged);

internal sealed class MainWindowStartupProgressController
{
    public string BuildProgressText(MainWindowStartupProgressState state)
    {
        return string.Join(
            Environment.NewLine,
            [
                $"当前阶段: {state.CurrentStep}",
                $"游戏列表: {state.GameListStatus}",
                $"预览动画: {state.PreviewStatus}",
                $"局域网后台: {state.LanStatus}"
            ]);
    }

    public MainWindowStartupProgressUpdateResult Update(
        MainWindowStartupProgressState currentState,
        string currentStep,
        string? gameListStatus = null,
        string? previewStatus = null,
        string? lanStatus = null,
        bool? isVisible = null)
    {
        var nextState = currentState;
        var changed = false;
        var currentStepChanged = false;

        if (isVisible.HasValue && currentState.IsVisible != isVisible.Value)
        {
            nextState = nextState with { IsVisible = isVisible.Value };
            changed = true;
        }

        if (!string.Equals(currentState.CurrentStep, currentStep, StringComparison.Ordinal))
        {
            nextState = nextState with { CurrentStep = currentStep };
            changed = true;
            currentStepChanged = true;
        }

        if (gameListStatus != null && !string.Equals(currentState.GameListStatus, gameListStatus, StringComparison.Ordinal))
        {
            nextState = nextState with { GameListStatus = gameListStatus };
            changed = true;
        }

        if (previewStatus != null && !string.Equals(currentState.PreviewStatus, previewStatus, StringComparison.Ordinal))
        {
            nextState = nextState with { PreviewStatus = previewStatus };
            changed = true;
        }

        if (lanStatus != null && !string.Equals(currentState.LanStatus, lanStatus, StringComparison.Ordinal))
        {
            nextState = nextState with { LanStatus = lanStatus };
            changed = true;
        }

        return new MainWindowStartupProgressUpdateResult(nextState, changed, currentStepChanged);
    }
}

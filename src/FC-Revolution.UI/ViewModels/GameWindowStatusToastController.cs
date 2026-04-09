namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowStatusUpdateDecision(
    string StatusText,
    string? ToastMessage)
{
    public bool ShouldShowToast => !string.IsNullOrWhiteSpace(ToastMessage);
}

internal readonly record struct GameWindowToastState(string TransientMessage);

internal sealed class GameWindowStatusToastController
{
    public GameWindowStatusUpdateDecision BuildStatusUpdate(string statusText, string? toastMessage) =>
        new(statusText, toastMessage);

    public GameWindowToastState BuildToastState(string transientMessage) =>
        new(transientMessage);

    public GameWindowToastState BuildClearedToastState() =>
        new(string.Empty);
}

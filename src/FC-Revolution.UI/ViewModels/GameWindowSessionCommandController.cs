using System;

namespace FC_Revolution.UI.ViewModels;

internal sealed class GameWindowSessionCommandController
{
    private readonly Func<GameWindowSaveStateWorkflowResult> _quickSave;
    private readonly Func<GameWindowSaveStateWorkflowResult> _quickLoad;
    private readonly Func<bool> _togglePause;
    private readonly Action<string, string?> _updateStatus;

    public GameWindowSessionCommandController(
        Func<GameWindowSaveStateWorkflowResult> quickSave,
        Func<GameWindowSaveStateWorkflowResult> quickLoad,
        Func<bool> togglePause,
        Action<string, string?> updateStatus)
    {
        ArgumentNullException.ThrowIfNull(quickSave);
        ArgumentNullException.ThrowIfNull(quickLoad);
        ArgumentNullException.ThrowIfNull(togglePause);
        ArgumentNullException.ThrowIfNull(updateStatus);

        _quickSave = quickSave;
        _quickLoad = quickLoad;
        _togglePause = togglePause;
        _updateStatus = updateStatus;
    }

    public void QuickSave()
    {
        var result = _quickSave();
        _updateStatus(result.StatusText, result.ToastText);
    }

    public void QuickLoad()
    {
        var result = _quickLoad();
        _updateStatus(result.StatusText, result.ToastText);
    }

    public void TogglePause()
    {
        if (_togglePause())
        {
            _updateStatus("游戏已暂停", "已暂停");
            return;
        }

        _updateStatus("游戏已继续", "已继续");
    }
}

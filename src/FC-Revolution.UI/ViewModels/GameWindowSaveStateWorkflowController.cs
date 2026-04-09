using System;
using System.IO;
using FCRevolution.Emulation.Abstractions;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowSaveStateWorkflowResult(
    string StatusText,
    string ToastText,
    bool StateRestored = false);

internal sealed class GameWindowSaveStateWorkflowController
{
    private const string LegacyQuickSaveStateFormat = "legacy/nes-state";

    private readonly string _quickSavePath;
    private readonly Func<CoreStateBlob> _captureState;
    private readonly Action<CoreStateBlob> _restoreState;
    private readonly Action _afterQuickLoadRestore;

    public GameWindowSaveStateWorkflowController(
        string quickSavePath,
        Func<CoreStateBlob> captureState,
        Action<CoreStateBlob> restoreState,
        Action? afterQuickLoadRestore = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quickSavePath);
        ArgumentNullException.ThrowIfNull(captureState);
        ArgumentNullException.ThrowIfNull(restoreState);

        _quickSavePath = quickSavePath;
        _captureState = captureState;
        _restoreState = restoreState;
        _afterQuickLoadRestore = afterQuickLoadRestore ?? (() => { });
    }

    public string QuickSavePath => _quickSavePath;

    public GameWindowSaveStateWorkflowResult QuickSave()
    {
        try
        {
            var state = _captureState();
            File.WriteAllBytes(_quickSavePath, CoreStateBlobFileCodec.Serialize(state));
            return new(
                $"快速存档成功: {Path.GetFileName(_quickSavePath)}",
                "已快速存档");
        }
        catch (Exception ex)
        {
            return new(
                $"快速存档失败: {ex.Message}",
                $"快速存档失败: {ex.Message}");
        }
    }

    public GameWindowSaveStateWorkflowResult QuickLoad()
    {
        if (!File.Exists(_quickSavePath))
        {
            return new(
                "当前游戏还没有快速存档",
                "没有可用快速存档");
        }

        try
        {
            var payload = File.ReadAllBytes(_quickSavePath);
            _restoreState(CoreStateBlobFileCodec.Deserialize(payload, LegacyQuickSaveStateFormat));
            _afterQuickLoadRestore();
            return new(
                $"快速读档成功: {Path.GetFileName(_quickSavePath)}",
                "已快速读档",
                StateRestored: true);
        }
        catch (Exception ex)
        {
            return new(
                $"快速读档失败: {ex.Message}",
                $"快速读档失败: {ex.Message}");
        }
    }
}

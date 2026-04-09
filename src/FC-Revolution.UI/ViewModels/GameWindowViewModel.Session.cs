using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using FC_Revolution.UI.Infrastructure;

namespace FC_Revolution.UI.ViewModels;

public sealed partial class GameWindowViewModel
{
    private void LoadRom(string romPath) => _romLoadHandler.Load(romPath);

    private string DescribeMapper()
    {
        return $"mapper {RomMapperInspector.Inspect(_romPath).DisplayLabel}";
    }

    public Task EnsureProfileTrustAsync(Window owner) => _profileTrustHandler.EnsureAsync(owner);

    public void Dispose() => _disposeHandler.Dispose();

    private void TryOpenDebugWindow() => _debugWindowOpenController.TryOpen();

    private void HandleSessionFailure(string message, Exception? ex = null) => _sessionFailureHandler.Handle(message, ex);

    private void QuickSave() => _sessionCommandController.QuickSave();

    private void QuickLoad() => _sessionCommandController.QuickLoad();

    private void TogglePause() => _sessionCommandController.TogglePause();
}

using Avalonia.Controls;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Views;

internal sealed class MainWindowMemoryDiagnosticsController
{
    private readonly Window _owner;
    private MemoryDiagnosticsWindow? _window;
    private MemoryDiagnosticsViewModel? _viewModel;

    public MainWindowMemoryDiagnosticsController(Window owner)
    {
        _owner = owner;
    }

    public void Toggle(MainWindowViewModel vm)
    {
        if (_window?.IsVisible == true)
        {
            _window.Close();
            return;
        }

        if (_window == null)
        {
            _viewModel = new MemoryDiagnosticsViewModel(vm);
            _window = new MemoryDiagnosticsWindow
            {
                DataContext = _viewModel
            };
            _window.Closed += (_, _) =>
            {
                _viewModel?.Dispose();
                _viewModel = null;
                _window = null;
            };
        }

        _window.Show(_owner);
        _window.Activate();
    }

    public void Close()
    {
        if (_window == null)
            return;

        _window.Close();
        _window = null;
        _viewModel?.Dispose();
        _viewModel = null;
    }
}

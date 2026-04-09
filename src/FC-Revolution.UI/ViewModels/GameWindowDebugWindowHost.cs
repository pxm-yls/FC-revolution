using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using FC_Revolution.UI.Views;

namespace FC_Revolution.UI.ViewModels;

internal sealed class GameWindowDebugWindowHost : IDisposable
{
    private readonly string _title;
    private readonly Func<DebugViewModel> _createViewModel;

    private DebugWindow? _window;

    public GameWindowDebugWindowHost(string title, Func<DebugViewModel> createViewModel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(createViewModel);

        _title = title;
        _createViewModel = createViewModel;
    }

    public void OpenOrActivate()
    {
        if (_window == null || !_window.IsVisible)
        {
            var viewModel = _createViewModel();
            var window = new DebugWindow
            {
                DataContext = viewModel,
                Title = _title
            };
            window.Closed += (_, _) =>
            {
                if (window.DataContext is IDisposable disposable)
                    disposable.Dispose();

                if (ReferenceEquals(_window, window))
                    _window = null;
            };

            ShowWindow(window);
            _window = window;
            Dispatcher.UIThread.Post(viewModel.Refresh, DispatcherPriority.Background);
            return;
        }

        if (_window.DataContext is DebugViewModel activeViewModel)
            activeViewModel.Refresh();

        _window.Activate();
    }

    public void Close() => _window?.Close();

    public void Dispose() => Close();

    private static void ShowWindow(Window window)
    {
        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (lifetime?.MainWindow != null)
            window.Show(lifetime.MainWindow);
        else
            window.Show();
    }
}

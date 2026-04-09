using System;
using Avalonia.Controls;
using Avalonia.Input;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;
using FC_Revolution.UI.Views.MainWindowParts;

namespace FC_Revolution.UI.Views;

internal sealed class MainWindowKeyRouter
{
    private readonly Window _owner;
    private readonly Func<MainWindowViewModel?> _getViewModel;
    private readonly MainWindowTaskMessagePanelView _taskMessagePanelView;
    private readonly MainWindowMemoryDiagnosticsController _memoryDiagnosticsController;

    public MainWindowKeyRouter(
        Window owner,
        Func<MainWindowViewModel?> getViewModel,
        MainWindowTaskMessagePanelView taskMessagePanelView,
        MainWindowMemoryDiagnosticsController memoryDiagnosticsController)
    {
        _owner = owner;
        _getViewModel = getViewModel;
        _taskMessagePanelView = taskMessagePanelView;
        _memoryDiagnosticsController = memoryDiagnosticsController;
    }

    public void HandleKeyDown(KeyEventArgs e)
    {
        if (!_owner.IsActive || ShouldIgnoreInputSource(e.Source))
            return;

        var vm = _getViewModel();
        if (vm == null)
            return;

        if (vm.IsShortcutMatch(ShortcutCatalog.MainToggleMemoryDiagnostics, e.Key, e.KeyModifiers))
        {
            _memoryDiagnosticsController.Toggle(vm);
            e.Handled = true;
            return;
        }

        if (vm.IsShortcutMatch(ShortcutCatalog.MainShowTaskMessages, e.Key, e.KeyModifiers))
        {
            _taskMessagePanelView.ShowPanel();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && TryCloseTransientPanels(vm))
        {
            e.Handled = true;
            return;
        }

        if (!vm.ShouldHandleKey(e.Key, e.KeyModifiers))
            return;

        vm.OnKeyDown(e.Key, e.KeyModifiers);
        e.Handled = true;
    }

    public void HandleKeyUp(KeyEventArgs e)
    {
        if (!_owner.IsActive || ShouldIgnoreInputSource(e.Source))
            return;

        var vm = _getViewModel();
        if (vm == null)
            return;

        if (vm.IsShortcutMatch(ShortcutCatalog.MainShowTaskMessages, e.Key, e.KeyModifiers))
        {
            _taskMessagePanelView.StartAutoHideCountdown();
            e.Handled = true;
            return;
        }

        if (!vm.ShouldHandleKey(e.Key, e.KeyModifiers))
            return;

        vm.OnKeyUp(e.Key, e.KeyModifiers);
        e.Handled = true;
    }

    private static bool ShouldIgnoreInputSource(object? source)
    {
        return source is Control { DataContext: IKeyCaptureBinding or ShortcutBindingEntry };
    }

    private static bool TryCloseTransientPanels(MainWindowViewModel vm)
    {
        if (vm.IsQuickRomInputEditorOpen)
        {
            vm.CloseQuickRomInputEditorCommand.Execute(null);
            return true;
        }

        if (vm.IsSettingsOpen)
        {
            vm.CloseSettingsCommand.Execute(null);
            return true;
        }

        return false;
    }
}

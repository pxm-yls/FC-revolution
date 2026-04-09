using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Views;

public partial class DebugWindow : Window
{
    public DebugWindow()
    {
        InitializeComponent();
    }

    private void OnMemoryCellGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is TextBox { DataContext: MemoryCellItem cell } textBox &&
            DataContext is DebugViewModel vm)
        {
            vm.SetMemoryCellEditing(true);
            vm.SelectMemoryCellCommand.Execute(cell);
            textBox.SelectAll();
        }
    }

    private void OnMemoryCellLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: MemoryCellItem cell } &&
            DataContext is DebugViewModel vm)
        {
            vm.CommitMemoryCellEdit(cell);
            vm.SetMemoryCellEditing(false);
        }
    }

    private void OnMemoryCellKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (sender is TextBox { DataContext: MemoryCellItem cell } &&
            DataContext is DebugViewModel vm)
        {
            vm.CommitMemoryCellEdit(cell);
            e.Handled = true;
        }
    }

    private async void OnImportProfileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DebugViewModel vm)
            await vm.ImportProfileAsync(this);
    }

    private void OnDisplaySettingsClick(object? sender, RoutedEventArgs e)
    {
        DisplaySettingsPopup.IsOpen = !DisplaySettingsPopup.IsOpen;
    }
}

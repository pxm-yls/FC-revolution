using Avalonia.Controls;
using FCRevolution.Core.Workbench.ViewModels;

namespace FCRevolution.Core.Workbench.Views;

public partial class CoreWorkbenchWindow : Window
{
    public CoreWorkbenchWindow()
    {
        InitializeComponent();
        Opened += HandleOpened;
    }

    private void HandleOpened(object? sender, EventArgs e)
    {
        if (DataContext is CoreWorkbenchViewModel viewModel &&
            viewModel.CatalogEntries.Count == 0)
        {
            viewModel.RefreshCatalogCommand.Execute(null);
        }
    }
}

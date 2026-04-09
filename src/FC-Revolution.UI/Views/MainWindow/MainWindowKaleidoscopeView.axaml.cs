using Avalonia.Controls;
using Avalonia.Input;

namespace FC_Revolution.UI.Views.MainWindowParts;

public partial class MainWindowKaleidoscopeView : MainWindowHostedControlBase
{
    public MainWindowKaleidoscopeView()
    {
        InitializeComponent();
    }

    private void OnShelfSlotTapped(object? sender, TappedEventArgs e)
    {
        if (TryGetRomFromControl(sender as Control, out var rom) && ViewModel != null)
        {
            ViewModel.PreviewRomFromShelf(rom);
            PositionBranchGalleryPreviewAbove(sender as Control, 220);
            e.Handled = true;
        }
    }

    private void OnShelfSlotDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (TryGetRomFromControl(sender as Control, out var rom) && ViewModel != null)
        {
            ViewModel.PlayRomFromShelf(rom);
            e.Handled = true;
        }
    }
}

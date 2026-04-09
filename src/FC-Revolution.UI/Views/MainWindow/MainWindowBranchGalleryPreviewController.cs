using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Views;

internal sealed class MainWindowBranchGalleryPreviewController
{
    private readonly Window _owner;
    private readonly Func<MainWindowViewModel?> _getViewModel;
    private readonly Func<Control?> _getCarouselAnchor;

    public MainWindowBranchGalleryPreviewController(
        Window owner,
        Func<MainWindowViewModel?> getViewModel,
        Func<Control?> getCarouselAnchor)
    {
        _owner = owner;
        _getViewModel = getViewModel;
        _getCarouselAnchor = getCarouselAnchor;
    }

    public void HandleViewModelPropertyChanged(string? propertyName)
    {
        if (propertyName is nameof(MainWindowViewModel.IsBranchGalleryPreviewOpen) or nameof(MainWindowViewModel.CurrentRom))
            UpdateAnchor();
    }

    public void HandlePreviewCardPressed(PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    public void UpdateAnchor()
    {
        if (_getViewModel() is not { IsBranchGalleryPreviewOpen: true, IsCarouselMode: true } vm)
            return;

        PositionAbove(_getCarouselAnchor(), 236d, vm);
    }

    private void PositionAbove(Control? anchor, double popupHeight, MainWindowViewModel vm)
    {
        if (anchor == null)
            return;

        var topLeft = anchor.TranslatePoint(new Point(0, 0), _owner);
        if (topLeft == null)
            return;

        const double popupWidth = 340d;
        var x = topLeft.Value.X + (anchor.Bounds.Width - popupWidth) / 2d;
        var y = topLeft.Value.Y - popupHeight - 18d;

        x = Math.Clamp(x, 16d, Math.Max(16d, _owner.Bounds.Width - popupWidth - 16d));
        y = Math.Max(16d, y);

        vm.SetBranchGalleryPreviewMargin(new Thickness(x, y, 0, 0));
    }
}

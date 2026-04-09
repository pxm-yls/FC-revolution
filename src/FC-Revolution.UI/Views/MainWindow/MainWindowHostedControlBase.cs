using System;
using Avalonia;
using Avalonia.Controls;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Views.MainWindowParts;

public abstract class MainWindowHostedControlBase : UserControl
{
    protected MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    protected static bool TryGetRomFromControl(Control? control, out RomLibraryItem rom)
    {
        switch (control?.DataContext)
        {
            case ShelfSlotItem { Rom: { } shelfRom }:
                rom = shelfRom;
                return true;
            case KaleidoscopeSlotItem { Rom: { } kaleidoscopeRom }:
                rom = kaleidoscopeRom;
                return true;
            default:
                rom = null!;
                return false;
        }
    }

    protected void PositionBranchGalleryPreviewAbove(Control? anchor, double popupHeight)
    {
        if (anchor == null || ViewModel == null)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        var topLeft = anchor.TranslatePoint(new Point(0, 0), topLevel);
        if (topLeft == null)
            return;

        const double popupWidth = 340d;
        var x = topLeft.Value.X + (anchor.Bounds.Width - popupWidth) / 2d;
        var y = topLeft.Value.Y - popupHeight - 18d;

        x = Math.Clamp(x, 16d, Math.Max(16d, topLevel.Bounds.Width - popupWidth - 16d));
        y = Math.Max(16d, y);

        ViewModel.SetBranchGalleryPreviewMargin(new Thickness(x, y, 0, 0));
    }
}

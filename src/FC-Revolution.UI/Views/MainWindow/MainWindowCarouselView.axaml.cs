using Avalonia.Controls;

namespace FC_Revolution.UI.Views.MainWindowParts;

public partial class MainWindowCarouselView : UserControl
{
    public MainWindowCarouselView()
    {
        InitializeComponent();
    }

    public Control PreviewAnchor => CarouselDiscAnchor;
}

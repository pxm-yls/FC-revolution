using CommunityToolkit.Mvvm.ComponentModel;

namespace FC_Revolution.UI.Models;

public sealed partial class KaleidoscopePageItem : ObservableObject
{
    public KaleidoscopePageItem(int index, string label)
    {
        Index = index;
        Label = label;
    }

    public int Index { get; }

    public string Label { get; }

    [ObservableProperty]
    private bool _isCurrent;
}

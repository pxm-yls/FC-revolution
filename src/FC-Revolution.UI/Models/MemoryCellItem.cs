using CommunityToolkit.Mvvm.ComponentModel;

namespace FC_Revolution.UI.Models;

public sealed partial class MemoryCellItem : ObservableObject
{
    public required ushort Address { get; init; }

    public required string DisplayAddress { get; init; }

    [ObservableProperty]
    private string _value = "00";

    [ObservableProperty]
    private bool _isHighlighted;

    [ObservableProperty]
    private bool _isRowLocatorHighlighted;

    [ObservableProperty]
    private bool _isColumnLocatorHighlighted;
}

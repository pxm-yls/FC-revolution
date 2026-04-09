using CommunityToolkit.Mvvm.ComponentModel;

namespace FC_Revolution.UI.Models;

public sealed partial class ModifiedMemoryEntry : ObservableObject
{
    public required ushort Address { get; init; }

    public required string DisplayAddress { get; init; }

    [ObservableProperty]
    private string _value = "00";

    [ObservableProperty]
    private bool _isLocked;

    public string LockButtonText => IsLocked ? "解锁" : "锁定";

    partial void OnIsLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(LockButtonText));
    }
}

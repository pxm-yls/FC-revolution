namespace FC_Revolution.UI.Models;

public sealed class ShelfSlotItem
{
    public ShelfSlotItem(int index, RomLibraryItem? rom)
    {
        Index = index;
        Rom = rom;
    }

    public int Index { get; }

    public RomLibraryItem? Rom { get; }

    public bool HasRom => Rom != null;

    public bool IsEmpty => Rom == null;
}

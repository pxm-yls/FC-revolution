namespace FC_Revolution.UI.Models;

public sealed class KaleidoscopeSlotItem
{
    public KaleidoscopeSlotItem(int index, RomLibraryItem? rom, double x, double y, double width, double height)
    {
        Index = index;
        Rom = rom;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int Index { get; }

    public RomLibraryItem? Rom { get; }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public bool HasRom => Rom != null;

    public bool IsEmpty => Rom == null;
}

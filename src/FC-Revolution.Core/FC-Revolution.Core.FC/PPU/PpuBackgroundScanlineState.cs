namespace FCRevolution.Core.PPU;

public readonly record struct PpuBackgroundScanlineState
{
    public int FineScrollX { get; init; }

    public int FineScrollY { get; init; }

    public int CoarseScrollX { get; init; }

    public int CoarseScrollY { get; init; }

    public int NametableSelect { get; init; }

    public bool UseBackgroundPatternTableHighBank { get; init; }

    public bool ShowBackground { get; init; }

    public bool ShowBackgroundLeft8 { get; init; }
}

using FCRevolution.Core.Mappers;

namespace FCRevolution.Rendering.Common;

public static class VisibleTileResolver
{
    private const int NametableWidthInTiles = 32;
    private const int NametableHeightInTiles = 30;
    private const int NametableByteLength = 1024;
    private const int AttributeTableOffset = 0x03C0;

    public static List<VisibleTile> Resolve(
        ReadOnlySpan<byte> nametable,
        ReadOnlySpan<byte> patternTable,
        int fineScrollX,
        int fineScrollY,
        int coarseScrollX,
        int coarseScrollY,
        int nametableSelect,
        MirroringMode mirrorMode,
        int screenWidth,
        int screenHeight,
        int screenOffsetY = 0,
        int clipTop = 0,
        int clipBottom = int.MaxValue,
        bool useBackgroundPatternTableHighBank = false)
    {
        _ = patternTable;

        ValidateArguments(
            nametable,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            nametableSelect,
            mirrorMode,
            screenWidth,
            screenHeight);

        int visibleTileCount = GetVisibleTileCountCore(fineScrollX, fineScrollY, screenWidth, screenHeight);
        if (visibleTileCount == 0)
            return [];

        var tiles = new List<VisibleTile>(visibleTileCount);
        ResolveIntoCore(
            tiles,
            nametable,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            nametableSelect,
            mirrorMode,
            screenWidth,
            screenHeight,
            screenOffsetY,
            clipTop,
            clipBottom,
            useBackgroundPatternTableHighBank);
        return tiles;
    }

    public static VisibleTile[] ResolveArray(
        ReadOnlySpan<byte> nametable,
        ReadOnlySpan<byte> patternTable,
        int fineScrollX,
        int fineScrollY,
        int coarseScrollX,
        int coarseScrollY,
        int nametableSelect,
        MirroringMode mirrorMode,
        int screenWidth,
        int screenHeight,
        int screenOffsetY = 0,
        int clipTop = 0,
        int clipBottom = int.MaxValue,
        bool useBackgroundPatternTableHighBank = false)
    {
        _ = patternTable;

        ValidateArguments(
            nametable,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            nametableSelect,
            mirrorMode,
            screenWidth,
            screenHeight);

        int visibleTileCount = GetVisibleTileCountCore(fineScrollX, fineScrollY, screenWidth, screenHeight);
        if (visibleTileCount == 0)
            return [];

        int visibleColumns = (screenWidth + fineScrollX + 7) / 8;
        int visibleRows = (screenHeight + fineScrollY + 7) / 8;
        int baseNametableX = nametableSelect & 0x01;
        int baseNametableY = (nametableSelect >> 1) & 0x01;
        var tiles = new VisibleTile[visibleTileCount];
        int index = 0;

        for (int row = 0; row < visibleRows; row++)
        {
            int absoluteTileY = coarseScrollY + row;
            int wrappedTileY = absoluteTileY % NametableHeightInTiles;
            int nametableOffsetY = absoluteTileY / NametableHeightInTiles;
            int logicalNametableY = (baseNametableY + nametableOffsetY) & 0x01;
            int screenY = (row * 8) - fineScrollY + screenOffsetY;

            for (int column = 0; column < visibleColumns; column++)
            {
                int absoluteTileX = coarseScrollX + column;
                int wrappedTileX = absoluteTileX % NametableWidthInTiles;
                int nametableOffsetX = absoluteTileX / NametableWidthInTiles;
                int logicalNametableX = (baseNametableX + nametableOffsetX) & 0x01;
                int logicalNametableIndex = (logicalNametableY << 1) | logicalNametableX;
                int physicalNametableIndex = MapPhysicalNametableIndex(logicalNametableIndex, mirrorMode);
                int nametableBaseOffset = physicalNametableIndex * NametableByteLength;
                int tileOffset = (wrappedTileY * NametableWidthInTiles) + wrappedTileX;
                int screenX = (column * 8) - fineScrollX;

                tiles[index++] = new VisibleTile
                {
                    ScreenX = screenX,
                    ScreenY = screenY,
                    TileId = nametable[nametableBaseOffset + tileOffset],
                    PaletteId = ReadBackgroundPaletteId(nametable, nametableBaseOffset, wrappedTileX, wrappedTileY),
                    LogicalNametableIndex = logicalNametableIndex,
                    PhysicalNametableIndex = physicalNametableIndex,
                    TileX = wrappedTileX,
                    TileY = wrappedTileY,
                    ClipTop = clipTop,
                    ClipBottom = clipBottom,
                    UseBackgroundPatternTableHighBank = useBackgroundPatternTableHighBank
                };
            }
        }

        return tiles;
    }

    public static void ResolveInto(
        List<VisibleTile> destination,
        ReadOnlySpan<byte> nametable,
        ReadOnlySpan<byte> patternTable,
        int fineScrollX,
        int fineScrollY,
        int coarseScrollX,
        int coarseScrollY,
        int nametableSelect,
        MirroringMode mirrorMode,
        int screenWidth,
        int screenHeight,
        int screenOffsetY = 0,
        int clipTop = 0,
        int clipBottom = int.MaxValue,
        bool useBackgroundPatternTableHighBank = false)
    {
        ArgumentNullException.ThrowIfNull(destination);
        _ = patternTable;

        ValidateArguments(
            nametable,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            nametableSelect,
            mirrorMode,
            screenWidth,
            screenHeight);

        if (screenWidth == 0 || screenHeight == 0)
            return;

        ResolveIntoCore(
            destination,
            nametable,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            nametableSelect,
            mirrorMode,
            screenWidth,
            screenHeight,
            screenOffsetY,
            clipTop,
            clipBottom,
            useBackgroundPatternTableHighBank);
    }

    public static int ResolveInto(
        VisibleTile[] destination,
        int startIndex,
        ReadOnlySpan<byte> nametable,
        ReadOnlySpan<byte> patternTable,
        int fineScrollX,
        int fineScrollY,
        int coarseScrollX,
        int coarseScrollY,
        int nametableSelect,
        MirroringMode mirrorMode,
        int screenWidth,
        int screenHeight,
        int screenOffsetY = 0,
        int clipTop = 0,
        int clipBottom = int.MaxValue,
        bool useBackgroundPatternTableHighBank = false)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (startIndex < 0 || startIndex > destination.Length)
            throw new ArgumentOutOfRangeException(nameof(startIndex));

        _ = patternTable;

        ValidateArguments(
            nametable,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            nametableSelect,
            mirrorMode,
            screenWidth,
            screenHeight);

        int visibleTileCount = GetVisibleTileCountCore(fineScrollX, fineScrollY, screenWidth, screenHeight);
        if (visibleTileCount == 0)
            return 0;

        if (destination.Length - startIndex < visibleTileCount)
            throw new ArgumentException("Destination does not have enough capacity.", nameof(destination));

        int endIndexExclusive = startIndex + visibleTileCount;
        int visibleColumns = (screenWidth + fineScrollX + 7) / 8;
        int visibleRows = (screenHeight + fineScrollY + 7) / 8;
        int baseNametableX = nametableSelect & 0x01;
        int baseNametableY = (nametableSelect >> 1) & 0x01;

        for (int row = 0; row < visibleRows; row++)
        {
            int absoluteTileY = coarseScrollY + row;
            int wrappedTileY = absoluteTileY % NametableHeightInTiles;
            int nametableOffsetY = absoluteTileY / NametableHeightInTiles;
            int logicalNametableY = (baseNametableY + nametableOffsetY) & 0x01;
            int screenY = (row * 8) - fineScrollY + screenOffsetY;

            for (int column = 0; column < visibleColumns; column++)
            {
                int absoluteTileX = coarseScrollX + column;
                int wrappedTileX = absoluteTileX % NametableWidthInTiles;
                int nametableOffsetX = absoluteTileX / NametableWidthInTiles;
                int logicalNametableX = (baseNametableX + nametableOffsetX) & 0x01;
                int logicalNametableIndex = (logicalNametableY << 1) | logicalNametableX;
                int physicalNametableIndex = MapPhysicalNametableIndex(logicalNametableIndex, mirrorMode);
                int nametableBaseOffset = physicalNametableIndex * NametableByteLength;
                int tileOffset = (wrappedTileY * NametableWidthInTiles) + wrappedTileX;
                int screenX = (column * 8) - fineScrollX;

                destination[startIndex++] = new VisibleTile
                {
                    ScreenX = screenX,
                    ScreenY = screenY,
                    TileId = nametable[nametableBaseOffset + tileOffset],
                    PaletteId = ReadBackgroundPaletteId(nametable, nametableBaseOffset, wrappedTileX, wrappedTileY),
                    LogicalNametableIndex = logicalNametableIndex,
                    PhysicalNametableIndex = physicalNametableIndex,
                    TileX = wrappedTileX,
                    TileY = wrappedTileY,
                    ClipTop = clipTop,
                    ClipBottom = clipBottom,
                    UseBackgroundPatternTableHighBank = useBackgroundPatternTableHighBank
                };
            }
        }

        return endIndexExclusive;
    }

    public static int GetVisibleTileCount(
        ReadOnlySpan<byte> nametable,
        ReadOnlySpan<byte> patternTable,
        int fineScrollX,
        int fineScrollY,
        int coarseScrollX,
        int coarseScrollY,
        int nametableSelect,
        MirroringMode mirrorMode,
        int screenWidth,
        int screenHeight)
    {
        _ = patternTable;
        ValidateArguments(
            nametable,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            nametableSelect,
            mirrorMode,
            screenWidth,
            screenHeight);

        return GetVisibleTileCountCore(fineScrollX, fineScrollY, screenWidth, screenHeight);
    }

    private static void ResolveIntoCore(
        List<VisibleTile> destination,
        ReadOnlySpan<byte> nametable,
        int fineScrollX,
        int fineScrollY,
        int coarseScrollX,
        int coarseScrollY,
        int nametableSelect,
        MirroringMode mirrorMode,
        int screenWidth,
        int screenHeight,
        int screenOffsetY,
        int clipTop,
        int clipBottom,
        bool useBackgroundPatternTableHighBank)
    {
        int visibleColumns = (screenWidth + fineScrollX + 7) / 8;
        int visibleRows = (screenHeight + fineScrollY + 7) / 8;
        int baseNametableX = nametableSelect & 0x01;
        int baseNametableY = (nametableSelect >> 1) & 0x01;

        for (int row = 0; row < visibleRows; row++)
        {
            int absoluteTileY = coarseScrollY + row;
            int wrappedTileY = absoluteTileY % NametableHeightInTiles;
            int nametableOffsetY = absoluteTileY / NametableHeightInTiles;
            int logicalNametableY = (baseNametableY + nametableOffsetY) & 0x01;
            int screenY = (row * 8) - fineScrollY + screenOffsetY;

            for (int column = 0; column < visibleColumns; column++)
            {
                int absoluteTileX = coarseScrollX + column;
                int wrappedTileX = absoluteTileX % NametableWidthInTiles;
                int nametableOffsetX = absoluteTileX / NametableWidthInTiles;
                int logicalNametableX = (baseNametableX + nametableOffsetX) & 0x01;
                int logicalNametableIndex = (logicalNametableY << 1) | logicalNametableX;
                int physicalNametableIndex = MapPhysicalNametableIndex(logicalNametableIndex, mirrorMode);
                int nametableBaseOffset = physicalNametableIndex * NametableByteLength;
                int tileOffset = (wrappedTileY * NametableWidthInTiles) + wrappedTileX;
                int screenX = (column * 8) - fineScrollX;

                destination.Add(new VisibleTile
                {
                    ScreenX = screenX,
                    ScreenY = screenY,
                    TileId = nametable[nametableBaseOffset + tileOffset],
                    PaletteId = ReadBackgroundPaletteId(nametable, nametableBaseOffset, wrappedTileX, wrappedTileY),
                    LogicalNametableIndex = logicalNametableIndex,
                    PhysicalNametableIndex = physicalNametableIndex,
                    TileX = wrappedTileX,
                    TileY = wrappedTileY,
                    ClipTop = clipTop,
                    ClipBottom = clipBottom,
                    UseBackgroundPatternTableHighBank = useBackgroundPatternTableHighBank
                });
            }
        }
    }

    private static int GetVisibleTileCountCore(
        int fineScrollX,
        int fineScrollY,
        int screenWidth,
        int screenHeight)
    {
        if (screenWidth == 0 || screenHeight == 0)
            return 0;

        int visibleColumns = (screenWidth + fineScrollX + 7) / 8;
        int visibleRows = (screenHeight + fineScrollY + 7) / 8;
        return visibleColumns * visibleRows;
    }

    private static void ValidateArguments(
        ReadOnlySpan<byte> nametable,
        int fineScrollX,
        int fineScrollY,
        int coarseScrollX,
        int coarseScrollY,
        int nametableSelect,
        MirroringMode mirrorMode,
        int screenWidth,
        int screenHeight)
    {
        if ((uint)fineScrollX > 7)
            throw new ArgumentOutOfRangeException(nameof(fineScrollX), "fineScrollX must be between 0 and 7.");

        if ((uint)fineScrollY > 7)
            throw new ArgumentOutOfRangeException(nameof(fineScrollY), "fineScrollY must be between 0 and 7.");

        if ((uint)coarseScrollX > 31)
            throw new ArgumentOutOfRangeException(nameof(coarseScrollX), "coarseScrollX must be between 0 and 31.");

        if ((uint)coarseScrollY > 29)
            throw new ArgumentOutOfRangeException(nameof(coarseScrollY), "coarseScrollY must be between 0 and 29.");

        if ((uint)nametableSelect > 3)
            throw new ArgumentOutOfRangeException(nameof(nametableSelect), "nametableSelect must be between 0 and 3.");

        if (screenWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(screenWidth), "screenWidth must be zero or greater.");

        if (screenHeight < 0)
            throw new ArgumentOutOfRangeException(nameof(screenHeight), "screenHeight must be zero or greater.");

        int requiredBytes = mirrorMode == MirroringMode.FourScreen ? NametableByteLength * 4 : NametableByteLength * 2;
        if (nametable.Length < requiredBytes)
            throw new ArgumentException($"Nametable span must contain at least {requiredBytes} bytes for {mirrorMode} mirroring.", nameof(nametable));
    }

    private static int MapPhysicalNametableIndex(int logicalNametableIndex, MirroringMode mirrorMode) =>
        mirrorMode switch
        {
            MirroringMode.Horizontal => logicalNametableIndex < 2 ? 0 : 1,
            MirroringMode.Vertical => logicalNametableIndex & 0x01,
            MirroringMode.SingleLower => 0,
            MirroringMode.SingleUpper => 1,
            MirroringMode.FourScreen => logicalNametableIndex,
            _ => throw new ArgumentOutOfRangeException(nameof(mirrorMode), mirrorMode, "Unsupported mirroring mode.")
        };

    private static byte ReadBackgroundPaletteId(
        ReadOnlySpan<byte> nametable,
        int nametableBaseOffset,
        int tileX,
        int tileY)
    {
        int attributeOffset = AttributeTableOffset + ((tileY / 4) * 8) + (tileX / 4);
        byte attributeByte = nametable[nametableBaseOffset + attributeOffset];
        int shift = ((tileY & 0x02) << 1) | (tileX & 0x02);
        return (byte)((attributeByte >> shift) & 0x03);
    }
}

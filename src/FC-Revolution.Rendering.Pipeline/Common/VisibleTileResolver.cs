using FCRevolution.Rendering.Abstractions;

namespace FCRevolution.Rendering.Common;

public static class VisibleTileResolver
{
    private const int BackgroundPlaneWidthInTiles = 32;
    private const int BackgroundPlaneHeightInTiles = 30;
    private const int BackgroundPlaneByteLength = 1024;
    private const int AttributeTableOffset = 0x03C0;

    public static List<VisibleTile> Resolve(
        ReadOnlySpan<byte> backgroundPlanes,
        int fineScrollX,
        int fineScrollY,
        int coarseScrollX,
        int coarseScrollY,
        int backgroundPlaneSelect,
        BackgroundPlaneLayoutMode backgroundPlaneLayout,
        int screenWidth,
        int screenHeight,
        int screenOffsetY = 0,
        int clipTop = 0,
        int clipBottom = int.MaxValue,
        bool useUpperBackgroundTileBank = false)
    {
        ValidateArguments(
            backgroundPlanes,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            backgroundPlaneSelect,
            backgroundPlaneLayout,
            screenWidth,
            screenHeight);

        int visibleTileCount = GetVisibleTileCountCore(fineScrollX, fineScrollY, screenWidth, screenHeight);
        if (visibleTileCount == 0)
            return [];

        var tiles = new List<VisibleTile>(visibleTileCount);
        ResolveIntoCore(
            tiles,
            backgroundPlanes,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            backgroundPlaneSelect,
            backgroundPlaneLayout,
            screenWidth,
            screenHeight,
            screenOffsetY,
            clipTop,
            clipBottom,
            useUpperBackgroundTileBank);
        return tiles;
    }

    public static VisibleTile[] ResolveArray(
        ReadOnlySpan<byte> backgroundPlanes,
        int fineScrollX,
        int fineScrollY,
        int coarseScrollX,
        int coarseScrollY,
        int backgroundPlaneSelect,
        BackgroundPlaneLayoutMode backgroundPlaneLayout,
        int screenWidth,
        int screenHeight,
        int screenOffsetY = 0,
        int clipTop = 0,
        int clipBottom = int.MaxValue,
        bool useUpperBackgroundTileBank = false)
    {
        ValidateArguments(
            backgroundPlanes,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            backgroundPlaneSelect,
            backgroundPlaneLayout,
            screenWidth,
            screenHeight);

        int visibleTileCount = GetVisibleTileCountCore(fineScrollX, fineScrollY, screenWidth, screenHeight);
        if (visibleTileCount == 0)
            return [];

        int visibleColumns = (screenWidth + fineScrollX + 7) / 8;
        int visibleRows = (screenHeight + fineScrollY + 7) / 8;
        int basePlaneX = backgroundPlaneSelect & 0x01;
        int basePlaneY = (backgroundPlaneSelect >> 1) & 0x01;
        var tiles = new VisibleTile[visibleTileCount];
        int index = 0;

        for (int row = 0; row < visibleRows; row++)
        {
            int absoluteTileY = coarseScrollY + row;
            int wrappedTileY = absoluteTileY % BackgroundPlaneHeightInTiles;
            int planeOffsetY = absoluteTileY / BackgroundPlaneHeightInTiles;
            int logicalPlaneY = (basePlaneY + planeOffsetY) & 0x01;
            int screenY = (row * 8) - fineScrollY + screenOffsetY;

            for (int column = 0; column < visibleColumns; column++)
            {
                int absoluteTileX = coarseScrollX + column;
                int wrappedTileX = absoluteTileX % BackgroundPlaneWidthInTiles;
                int planeOffsetX = absoluteTileX / BackgroundPlaneWidthInTiles;
                int logicalPlaneX = (basePlaneX + planeOffsetX) & 0x01;
                int logicalPlaneIndex = (logicalPlaneY << 1) | logicalPlaneX;
                int physicalPlaneIndex = MapPhysicalPlaneIndex(logicalPlaneIndex, backgroundPlaneLayout);
                int planeBaseOffset = physicalPlaneIndex * BackgroundPlaneByteLength;
                int tileOffset = (wrappedTileY * BackgroundPlaneWidthInTiles) + wrappedTileX;
                int screenX = (column * 8) - fineScrollX;

                tiles[index++] = new VisibleTile
                {
                    ScreenX = screenX,
                    ScreenY = screenY,
                    TileId = backgroundPlanes[planeBaseOffset + tileOffset],
                    PaletteId = ReadBackgroundPaletteId(backgroundPlanes, planeBaseOffset, wrappedTileX, wrappedTileY),
                    LogicalPlaneIndex = logicalPlaneIndex,
                    PhysicalPlaneIndex = physicalPlaneIndex,
                    TileX = wrappedTileX,
                    TileY = wrappedTileY,
                    ClipTop = clipTop,
                    ClipBottom = clipBottom,
                    UseUpperBackgroundTileBank = useUpperBackgroundTileBank
                };
            }
        }

        return tiles;
    }

    public static void ResolveInto(
        List<VisibleTile> destination,
        ReadOnlySpan<byte> backgroundPlanes,
        int fineScrollX,
        int fineScrollY,
        int coarseScrollX,
        int coarseScrollY,
        int backgroundPlaneSelect,
        BackgroundPlaneLayoutMode backgroundPlaneLayout,
        int screenWidth,
        int screenHeight,
        int screenOffsetY = 0,
        int clipTop = 0,
        int clipBottom = int.MaxValue,
        bool useUpperBackgroundTileBank = false)
    {
        ArgumentNullException.ThrowIfNull(destination);

        ValidateArguments(
            backgroundPlanes,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            backgroundPlaneSelect,
            backgroundPlaneLayout,
            screenWidth,
            screenHeight);

        if (screenWidth == 0 || screenHeight == 0)
            return;

        ResolveIntoCore(
            destination,
            backgroundPlanes,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            backgroundPlaneSelect,
            backgroundPlaneLayout,
            screenWidth,
            screenHeight,
            screenOffsetY,
            clipTop,
            clipBottom,
            useUpperBackgroundTileBank);
    }

    public static int ResolveInto(
        VisibleTile[] destination,
        int startIndex,
        ReadOnlySpan<byte> backgroundPlanes,
        int fineScrollX,
        int fineScrollY,
        int coarseScrollX,
        int coarseScrollY,
        int backgroundPlaneSelect,
        BackgroundPlaneLayoutMode backgroundPlaneLayout,
        int screenWidth,
        int screenHeight,
        int screenOffsetY = 0,
        int clipTop = 0,
        int clipBottom = int.MaxValue,
        bool useUpperBackgroundTileBank = false)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (startIndex < 0 || startIndex > destination.Length)
            throw new ArgumentOutOfRangeException(nameof(startIndex));

        ValidateArguments(
            backgroundPlanes,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            backgroundPlaneSelect,
            backgroundPlaneLayout,
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
        int basePlaneX = backgroundPlaneSelect & 0x01;
        int basePlaneY = (backgroundPlaneSelect >> 1) & 0x01;

        for (int row = 0; row < visibleRows; row++)
        {
            int absoluteTileY = coarseScrollY + row;
            int wrappedTileY = absoluteTileY % BackgroundPlaneHeightInTiles;
            int planeOffsetY = absoluteTileY / BackgroundPlaneHeightInTiles;
            int logicalPlaneY = (basePlaneY + planeOffsetY) & 0x01;
            int screenY = (row * 8) - fineScrollY + screenOffsetY;

            for (int column = 0; column < visibleColumns; column++)
            {
                int absoluteTileX = coarseScrollX + column;
                int wrappedTileX = absoluteTileX % BackgroundPlaneWidthInTiles;
                int planeOffsetX = absoluteTileX / BackgroundPlaneWidthInTiles;
                int logicalPlaneX = (basePlaneX + planeOffsetX) & 0x01;
                int logicalPlaneIndex = (logicalPlaneY << 1) | logicalPlaneX;
                int physicalPlaneIndex = MapPhysicalPlaneIndex(logicalPlaneIndex, backgroundPlaneLayout);
                int planeBaseOffset = physicalPlaneIndex * BackgroundPlaneByteLength;
                int tileOffset = (wrappedTileY * BackgroundPlaneWidthInTiles) + wrappedTileX;
                int screenX = (column * 8) - fineScrollX;

                destination[startIndex++] = new VisibleTile
                {
                    ScreenX = screenX,
                    ScreenY = screenY,
                    TileId = backgroundPlanes[planeBaseOffset + tileOffset],
                    PaletteId = ReadBackgroundPaletteId(backgroundPlanes, planeBaseOffset, wrappedTileX, wrappedTileY),
                    LogicalPlaneIndex = logicalPlaneIndex,
                    PhysicalPlaneIndex = physicalPlaneIndex,
                    TileX = wrappedTileX,
                    TileY = wrappedTileY,
                    ClipTop = clipTop,
                    ClipBottom = clipBottom,
                    UseUpperBackgroundTileBank = useUpperBackgroundTileBank
                };
            }
        }

        return endIndexExclusive;
    }

    public static int GetVisibleTileCount(
        ReadOnlySpan<byte> backgroundPlanes,
        int fineScrollX,
        int fineScrollY,
        int coarseScrollX,
        int coarseScrollY,
        int backgroundPlaneSelect,
        BackgroundPlaneLayoutMode backgroundPlaneLayout,
        int screenWidth,
        int screenHeight)
    {
        ValidateArguments(
            backgroundPlanes,
            fineScrollX,
            fineScrollY,
            coarseScrollX,
            coarseScrollY,
            backgroundPlaneSelect,
            backgroundPlaneLayout,
            screenWidth,
            screenHeight);

        return GetVisibleTileCountCore(fineScrollX, fineScrollY, screenWidth, screenHeight);
    }

    private static void ResolveIntoCore(
        List<VisibleTile> destination,
        ReadOnlySpan<byte> backgroundPlanes,
        int fineScrollX,
        int fineScrollY,
        int coarseScrollX,
        int coarseScrollY,
        int backgroundPlaneSelect,
        BackgroundPlaneLayoutMode backgroundPlaneLayout,
        int screenWidth,
        int screenHeight,
        int screenOffsetY,
        int clipTop,
        int clipBottom,
        bool useUpperBackgroundTileBank)
    {
        int visibleColumns = (screenWidth + fineScrollX + 7) / 8;
        int visibleRows = (screenHeight + fineScrollY + 7) / 8;
        int basePlaneX = backgroundPlaneSelect & 0x01;
        int basePlaneY = (backgroundPlaneSelect >> 1) & 0x01;

        for (int row = 0; row < visibleRows; row++)
        {
            int absoluteTileY = coarseScrollY + row;
            int wrappedTileY = absoluteTileY % BackgroundPlaneHeightInTiles;
            int planeOffsetY = absoluteTileY / BackgroundPlaneHeightInTiles;
            int logicalPlaneY = (basePlaneY + planeOffsetY) & 0x01;
            int screenY = (row * 8) - fineScrollY + screenOffsetY;

            for (int column = 0; column < visibleColumns; column++)
            {
                int absoluteTileX = coarseScrollX + column;
                int wrappedTileX = absoluteTileX % BackgroundPlaneWidthInTiles;
                int planeOffsetX = absoluteTileX / BackgroundPlaneWidthInTiles;
                int logicalPlaneX = (basePlaneX + planeOffsetX) & 0x01;
                int logicalPlaneIndex = (logicalPlaneY << 1) | logicalPlaneX;
                int physicalPlaneIndex = MapPhysicalPlaneIndex(logicalPlaneIndex, backgroundPlaneLayout);
                int planeBaseOffset = physicalPlaneIndex * BackgroundPlaneByteLength;
                int tileOffset = (wrappedTileY * BackgroundPlaneWidthInTiles) + wrappedTileX;
                int screenX = (column * 8) - fineScrollX;

                destination.Add(new VisibleTile
                {
                    ScreenX = screenX,
                    ScreenY = screenY,
                    TileId = backgroundPlanes[planeBaseOffset + tileOffset],
                    PaletteId = ReadBackgroundPaletteId(backgroundPlanes, planeBaseOffset, wrappedTileX, wrappedTileY),
                    LogicalPlaneIndex = logicalPlaneIndex,
                    PhysicalPlaneIndex = physicalPlaneIndex,
                    TileX = wrappedTileX,
                    TileY = wrappedTileY,
                    ClipTop = clipTop,
                    ClipBottom = clipBottom,
                    UseUpperBackgroundTileBank = useUpperBackgroundTileBank
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
        ReadOnlySpan<byte> backgroundPlanes,
        int fineScrollX,
        int fineScrollY,
        int coarseScrollX,
        int coarseScrollY,
        int backgroundPlaneSelect,
        BackgroundPlaneLayoutMode backgroundPlaneLayout,
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

        if ((uint)backgroundPlaneSelect > 3)
            throw new ArgumentOutOfRangeException(nameof(backgroundPlaneSelect), "backgroundPlaneSelect must be between 0 and 3.");

        if (screenWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(screenWidth), "screenWidth must be zero or greater.");

        if (screenHeight < 0)
            throw new ArgumentOutOfRangeException(nameof(screenHeight), "screenHeight must be zero or greater.");

        int requiredBytes = backgroundPlaneLayout == BackgroundPlaneLayoutMode.IndependentPlanes
            ? BackgroundPlaneByteLength * 4
            : BackgroundPlaneByteLength * 2;
        if (backgroundPlanes.Length < requiredBytes)
        {
            throw new ArgumentException(
                $"Background plane span must contain at least {requiredBytes} bytes for {backgroundPlaneLayout} layout.",
                nameof(backgroundPlanes));
        }
    }

    private static int MapPhysicalPlaneIndex(int logicalPlaneIndex, BackgroundPlaneLayoutMode backgroundPlaneLayout) =>
        backgroundPlaneLayout switch
        {
            BackgroundPlaneLayoutMode.SharedTopBottom => logicalPlaneIndex < 2 ? 0 : 1,
            BackgroundPlaneLayoutMode.SharedLeftRight => logicalPlaneIndex & 0x01,
            BackgroundPlaneLayoutMode.SinglePlane0 => 0,
            BackgroundPlaneLayoutMode.SinglePlane1 => 1,
            BackgroundPlaneLayoutMode.IndependentPlanes => logicalPlaneIndex,
            _ => throw new ArgumentOutOfRangeException(nameof(backgroundPlaneLayout), backgroundPlaneLayout, "Unsupported background plane layout.")
        };

    private static byte ReadBackgroundPaletteId(
        ReadOnlySpan<byte> backgroundPlanes,
        int planeBaseOffset,
        int tileX,
        int tileY)
    {
        int attributeOffset = AttributeTableOffset + ((tileY / 4) * 8) + (tileX / 4);
        byte attributeByte = backgroundPlanes[planeBaseOffset + attributeOffset];
        int shift = ((tileY & 0x02) << 1) | (tileX & 0x02);
        return (byte)((attributeByte >> shift) & 0x03);
    }
}

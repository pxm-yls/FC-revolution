namespace FCRevolution.Rendering.Abstractions;

public sealed class LayeredFrameData
{
    public LayeredFrameData(
        int frameWidth,
        int frameHeight,
        byte[] tileAtlas,
        uint[] palette,
        BackgroundTileRenderItem[] backgroundTiles,
        SpriteRenderItem[] sprites,
        bool showBackground,
        bool showSprites,
        bool showBackgroundInFirstTileColumn,
        bool showSpritesInFirstTileColumn,
        MotionTextureData? motionTexture = null)
    {
        if (frameWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameWidth));

        if (frameHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameHeight));

        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
        TileAtlas = tileAtlas ?? throw new ArgumentNullException(nameof(tileAtlas));
        Palette = palette ?? throw new ArgumentNullException(nameof(palette));
        BackgroundTiles = backgroundTiles ?? throw new ArgumentNullException(nameof(backgroundTiles));
        Sprites = sprites ?? throw new ArgumentNullException(nameof(sprites));
        ShowBackground = showBackground;
        ShowSprites = showSprites;
        ShowBackgroundInFirstTileColumn = showBackgroundInFirstTileColumn;
        ShowSpritesInFirstTileColumn = showSpritesInFirstTileColumn;
        if (motionTexture is not null && (motionTexture.Width != frameWidth || motionTexture.Height != frameHeight))
            throw new ArgumentException("Motion texture dimensions must match the frame size.", nameof(motionTexture));

        MotionTexture = motionTexture;
    }

    public int FrameWidth { get; }

    public int FrameHeight { get; }

    public byte[] TileAtlas { get; }

    public uint[] Palette { get; }

    public BackgroundTileRenderItem[] BackgroundTiles { get; }

    public SpriteRenderItem[] Sprites { get; }

    public bool ShowBackground { get; }

    public bool ShowSprites { get; }

    public bool ShowBackgroundInFirstTileColumn { get; }

    public bool ShowSpritesInFirstTileColumn { get; }

    public MotionTextureData? MotionTexture { get; }
}

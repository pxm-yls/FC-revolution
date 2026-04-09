namespace FCRevolution.Rendering.Abstractions;

public sealed class LayeredFrameData
{
    public LayeredFrameData(
        int frameWidth,
        int frameHeight,
        byte[] chrAtlas,
        uint[] palette,
        BackgroundTileRenderItem[] backgroundTiles,
        SpriteRenderItem[] sprites,
        bool showBackground,
        bool showSprites,
        bool showBackgroundLeft8,
        bool showSpritesLeft8,
        MotionTextureData? motionTexture = null)
    {
        if (frameWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameWidth));

        if (frameHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameHeight));

        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
        ChrAtlas = chrAtlas ?? throw new ArgumentNullException(nameof(chrAtlas));
        Palette = palette ?? throw new ArgumentNullException(nameof(palette));
        BackgroundTiles = backgroundTiles ?? throw new ArgumentNullException(nameof(backgroundTiles));
        Sprites = sprites ?? throw new ArgumentNullException(nameof(sprites));
        ShowBackground = showBackground;
        ShowSprites = showSprites;
        ShowBackgroundLeft8 = showBackgroundLeft8;
        ShowSpritesLeft8 = showSpritesLeft8;
        if (motionTexture is not null && (motionTexture.Width != frameWidth || motionTexture.Height != frameHeight))
            throw new ArgumentException("Motion texture dimensions must match the frame size.", nameof(motionTexture));

        MotionTexture = motionTexture;
    }

    public int FrameWidth { get; }

    public int FrameHeight { get; }

    public byte[] ChrAtlas { get; }

    public uint[] Palette { get; }

    public BackgroundTileRenderItem[] BackgroundTiles { get; }

    public SpriteRenderItem[] Sprites { get; }

    public bool ShowBackground { get; }

    public bool ShowSprites { get; }

    public bool ShowBackgroundLeft8 { get; }

    public bool ShowSpritesLeft8 { get; }

    public MotionTextureData? MotionTexture { get; }
}

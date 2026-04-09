namespace FCRevolution.Backend.Hosting;

public sealed record BackendHostOptions(
    int Port,
    bool EnableWebPad = true,
    bool EnableDebugPages = true,
    /// <summary>串流分辨率倍率，1=256×240，2=512×480，3=768×720</summary>
    int StreamScaleMultiplier = 2,
    /// <summary>JPEG 编码质量，范围 60–95</summary>
    int StreamJpegQuality = 85,
    /// <summary>画质增强模式（同时作用于游戏窗口与串流）</summary>
    PixelEnhancementMode StreamEnhancementMode = PixelEnhancementMode.None);

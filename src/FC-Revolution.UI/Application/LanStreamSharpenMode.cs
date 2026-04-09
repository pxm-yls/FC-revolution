namespace FC_Revolution.UI.AppServices;

/// <summary>
/// 局域网串流画面增强模式（UI 层枚举，映射到后端 <see cref="FCRevolution.Backend.Hosting.PixelEnhancementMode"/>）。
/// </summary>
public enum LanStreamSharpenMode
{
    /// <summary>无增强，原始像素直出</summary>
    None     = 0,
    /// <summary>轻微锐化——边缘微增强，适合大多数游戏</summary>
    Subtle   = 1,
    /// <summary>CRT 扫描线——奇数行降亮，还原复古 CRT 风格</summary>
    Standard = 2,
    /// <summary>鲜艳色彩——饱和度提升约 30%，画面更鲜艳</summary>
    Strong   = 3,
}

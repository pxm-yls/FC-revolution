namespace FCRevolution.Backend.Hosting;

/// <summary>
/// 画质增强模式，同时适用于游戏窗口显示和局域网串流。
/// </summary>
public enum PixelEnhancementMode
{
    /// <summary>无增强，忠实输出原始像素</summary>
    None = 0,
    /// <summary>轻微锐化——十字形卷积核，增强边缘而不产生过明显光晕</summary>
    SubtleSharpen = 1,
    /// <summary>CRT 扫描线——奇数行亮度降低 45%，模拟经典 CRT 电视效果</summary>
    CrtScanlines = 2,
    /// <summary>柔和模糊——3×3 盒式均值模糊，柔化硬边缘、现代电视感</summary>
    SoftBlur = 3,
    /// <summary>鲜艳色彩——色彩饱和度提升约 30%，画面更鲜艳</summary>
    VividColor = 4
}

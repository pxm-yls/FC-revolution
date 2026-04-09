using System.Runtime.CompilerServices;

namespace FCRevolution.Backend.Hosting;

/// <summary>
/// 纯 C# 像素增强处理器。
/// 操作 NES 原始帧（256×240，格式 0xAARRGGBB），无 SkiaSharp 依赖，60 FPS 无压力。
/// </summary>
public static class PixelEnhancer
{
    /// <summary>
    /// 将 <paramref name="src"/> 的增强结果写入 <paramref name="dst"/>。
    /// src 和 dst 可以是同一个数组（CrtScanlines / VividColor 安全原地写入）。
    /// SubtleSharpen 和 SoftBlur 需要读取邻域，因此要求 src != dst。
    /// 调用方应确保 dst 长度 >= width * height。
    /// </summary>
    public static void Apply(
        ReadOnlySpan<uint> src, Span<uint> dst,
        int width, int height,
        PixelEnhancementMode mode)
    {
        switch (mode)
        {
            case PixelEnhancementMode.SubtleSharpen:
                ApplySubtleSharpen(src, dst, width, height);
                return;
            case PixelEnhancementMode.CrtScanlines:
                ApplyCrtScanlines(src, dst, width, height);
                return;
            case PixelEnhancementMode.SoftBlur:
                ApplySoftBlur(src, dst, width, height);
                return;
            case PixelEnhancementMode.VividColor:
                ApplyVividColor(src, dst, width, height);
                return;
            default:
                src.CopyTo(dst);
                return;
        }
    }

    // ── SubtleSharpen ──────────────────────────────────────────────────────────
    // 十字形卷积核：中心 2.2，上下左右各 -0.3，角落 0，内核和 = 1（亮度不变）。
    // 只处理内部像素，边界行/列直接复制（NES 边界本来就是无效区域）。
    private static void ApplySubtleSharpen(ReadOnlySpan<uint> src, Span<uint> dst, int w, int h)
    {
        // 顶行 / 底行
        src[..w].CopyTo(dst);
        src[((h - 1) * w)..].CopyTo(dst[((h - 1) * w)..]);

        for (var y = 1; y < h - 1; y++)
        {
            // 左右边界列
            dst[y * w]         = src[y * w];
            dst[y * w + w - 1] = src[y * w + w - 1];

            for (var x = 1; x < w - 1; x++)
            {
                var i = y * w + x;
                var r = 2.2f * R(src[i])
                      - 0.3f * (R(src[i - w]) + R(src[i + w]) + R(src[i - 1]) + R(src[i + 1]));
                var g = 2.2f * G(src[i])
                      - 0.3f * (G(src[i - w]) + G(src[i + w]) + G(src[i - 1]) + G(src[i + 1]));
                var b = 2.2f * B(src[i])
                      - 0.3f * (B(src[i - w]) + B(src[i + w]) + B(src[i - 1]) + B(src[i + 1]));
                dst[i] = Pack(Sat(r), Sat(g), Sat(b));
            }
        }
    }

    // ── CrtScanlines ───────────────────────────────────────────────────────────
    // 奇数行亮度降至 55%，偶数行保持原样。
    // 安全原地写入（dst 可等于 src）。
    private static void ApplyCrtScanlines(ReadOnlySpan<uint> src, Span<uint> dst, int w, int h)
    {
        for (var y = 0; y < h; y++)
        {
            var rowSrc = src.Slice(y * w, w);
            var rowDst = dst.Slice(y * w, w);
            if ((y & 1) == 0)
            {
                rowSrc.CopyTo(rowDst);
            }
            else
            {
                for (var x = 0; x < w; x++)
                    rowDst[x] = Dim55(rowSrc[x]);
            }
        }
    }

    // ── SoftBlur ───────────────────────────────────────────────────────────────
    // 3×3 均值盒式模糊（9 像素取平均）。
    // 边界直接复制，内部像素全部处理。
    private static void ApplySoftBlur(ReadOnlySpan<uint> src, Span<uint> dst, int w, int h)
    {
        // 顶行 / 底行
        src[..w].CopyTo(dst);
        src[((h - 1) * w)..].CopyTo(dst[((h - 1) * w)..]);

        for (var y = 1; y < h - 1; y++)
        {
            dst[y * w]         = src[y * w];
            dst[y * w + w - 1] = src[y * w + w - 1];

            for (var x = 1; x < w - 1; x++)
            {
                var i  = y * w + x;
                var r  = 0;
                var g  = 0;
                var b  = 0;
                for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    var p = src[(y + dy) * w + (x + dx)];
                    r += R(p); g += G(p); b += B(p);
                }
                dst[i] = Pack((byte)(r / 9), (byte)(g / 9), (byte)(b / 9));
            }
        }
    }

    // ── VividColor ─────────────────────────────────────────────────────────────
    // 以灰度为锚点，向彩色方向拉伸 1.3 倍，相当于饱和度 +30%。
    // 安全原地写入。
    private static void ApplyVividColor(ReadOnlySpan<uint> src, Span<uint> dst, int w, int h)
    {
        for (var i = 0; i < src.Length; i++)
        {
            var p    = src[i];
            var rf   = (float)R(p);
            var gf   = (float)G(p);
            var bf   = (float)B(p);
            var gray = (rf + gf + bf) * (1f / 3f);
            const float k = 1.3f;
            dst[i] = Pack(
                Sat(gray + (rf - gray) * k),
                Sat(gray + (gf - gray) * k),
                Sat(gray + (bf - gray) * k));
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int R(uint p) => (int)((p >> 16) & 0xFF);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int G(uint p) => (int)((p >> 8) & 0xFF);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int B(uint p) => (int)(p & 0xFF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Sat(float v) =>
        v < 0f ? (byte)0 : v > 255f ? (byte)255 : (byte)(int)v;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Pack(byte r, byte g, byte b) =>
        0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Dim55(uint p) =>
        Pack((byte)(R(p) * 55 / 100), (byte)(G(p) * 55 / 100), (byte)(B(p) * 55 / 100));
}

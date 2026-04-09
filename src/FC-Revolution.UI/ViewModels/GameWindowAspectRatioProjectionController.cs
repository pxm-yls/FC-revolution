using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal readonly record struct GameWindowAspectRatioProjection(
    double Width,
    double Height,
    string Label);

internal static class GameWindowAspectRatioProjectionController
{
    public static GameWindowAspectRatioProjection Build(GameAspectRatioMode mode) => mode switch
    {
        GameAspectRatioMode.Aspect8By7 => new(256d, 224d, "8:7"),    // 256:224 = 8:7，NES CRT 标准
        GameAspectRatioMode.Aspect4By3 => new(320d, 240d, "4:3"),    // 320:240 = 4:3，NTSC 电视
        GameAspectRatioMode.Aspect16By9 => new(427d, 240d, "16:9"),  // 427:240 ≈ 16:9
        _ => new(256d, 240d, "原始 256:240"),
    };
}

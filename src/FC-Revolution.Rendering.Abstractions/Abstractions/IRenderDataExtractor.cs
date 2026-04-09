using FCRevolution.Core.PPU;

namespace FCRevolution.Rendering.Abstractions;

public interface IRenderDataExtractor
{
    FrameMetadata Extract(
        PpuRenderStateSnapshot snapshot,
        IFrameMetadata? previousFrame = null,
        int screenWidth = FCRevolution.Core.NesConstants.ScreenWidth,
        int screenHeight = FCRevolution.Core.NesConstants.ScreenHeight);
}

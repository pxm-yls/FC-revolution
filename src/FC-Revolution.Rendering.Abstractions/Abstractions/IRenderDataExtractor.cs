namespace FCRevolution.Rendering.Abstractions;

public interface IRenderDataExtractor
{
    FrameMetadata Extract(
        RenderStateSnapshot snapshot,
        IFrameMetadata? previousFrame = null,
        int screenWidth = 256,
        int screenHeight = 240);
}

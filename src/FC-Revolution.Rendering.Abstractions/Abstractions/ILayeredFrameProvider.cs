namespace FCRevolution.Rendering.Abstractions;

public interface ILayeredFrameProvider
{
    LayeredFrameData CaptureLayeredFrame();

    void ResetTemporalHistory();
}

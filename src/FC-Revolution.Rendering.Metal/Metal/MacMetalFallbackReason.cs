namespace FCRevolution.Rendering.Metal;

public enum MacMetalFallbackReason : uint
{
    None = 0,
    UnsupportedPlatform = 1,
    UnsupportedDevice = 2,
    OutputSmallerThanInput = 3,
    ScalerCreationFailed = 4,
    RuntimeCommandFailure = 5,
    RequestedPathUnavailable = 6
}

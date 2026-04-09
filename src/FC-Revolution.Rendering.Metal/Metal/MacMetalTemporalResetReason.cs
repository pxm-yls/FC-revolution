namespace FCRevolution.Rendering.Metal;

public enum MacMetalTemporalResetReason : uint
{
    None = 0,
    PresenterRecreated = 1,
    RomLoaded = 2,
    SaveStateLoaded = 3,
    UpscaleModeChanged = 4,
    RuntimeFallback = 5,
    TimelineJump = 6
}

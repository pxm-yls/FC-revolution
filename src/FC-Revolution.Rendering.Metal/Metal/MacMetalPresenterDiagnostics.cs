namespace FCRevolution.Rendering.Metal;

public readonly record struct MacMetalPresenterDiagnostics(
    MacUpscaleMode RequestedUpscaleMode,
    MacUpscaleMode EffectiveUpscaleMode,
    MacMetalFallbackReason FallbackReason,
    uint InternalWidth,
    uint InternalHeight,
    uint OutputWidth,
    uint OutputHeight,
    uint DrawableWidth,
    uint DrawableHeight,
    double TargetWidthPoints,
    double TargetHeightPoints,
    double DisplayScale,
    double HostWidthPoints,
    double HostHeightPoints,
    double LayerWidthPoints,
    double LayerHeightPoints,
    bool TemporalResetPending,
    bool TemporalResetApplied,
    uint TemporalResetCount,
    MacMetalTemporalResetReason TemporalResetReason)
{
    public static MacMetalPresenterDiagnostics Empty { get; } = new(
        RequestedUpscaleMode: MacUpscaleMode.None,
        EffectiveUpscaleMode: MacUpscaleMode.None,
        FallbackReason: MacMetalFallbackReason.None,
        InternalWidth: 0,
        InternalHeight: 0,
        OutputWidth: 0,
        OutputHeight: 0,
        DrawableWidth: 0,
        DrawableHeight: 0,
        TargetWidthPoints: 0,
        TargetHeightPoints: 0,
        DisplayScale: 1,
        HostWidthPoints: 0,
        HostHeightPoints: 0,
        LayerWidthPoints: 0,
        LayerHeightPoints: 0,
        TemporalResetPending: false,
        TemporalResetApplied: false,
        TemporalResetCount: 0,
        TemporalResetReason: MacMetalTemporalResetReason.None);

    public bool HasOutput => OutputWidth > 0 && OutputHeight > 0;
}

using FCRevolution.Emulation.Abstractions;

namespace FCRevolution.Emulation.Host;

public sealed record CoreSessionSmokeTestRequest(
    string? CoreId = null,
    string? MediaPath = null,
    int? FramesToRun = null,
    ManagedCoreRuntimeOptions? RuntimeOptions = null,
    IEnumerable<IManagedCoreModule>? AdditionalModules = null);

public sealed record CoreSessionSmokeTestResult(
    bool SessionCreated,
    string? RequestedCoreId,
    string? SelectedCoreId,
    CoreRuntimeInfo? RuntimeInfo,
    IReadOnlyCollection<string> CapabilityIds,
    int InputPortCount,
    int InputActionCount,
    CoreLoadResult? LoadResult,
    IReadOnlyList<CoreStepResult> StepResults,
    int VideoFrameCount,
    VideoFramePacket? LastVideoFrame,
    string? FailureMessage)
{
    public bool Succeeded =>
        SessionCreated &&
        (LoadResult is null || LoadResult.Success) &&
        StepResults.All(result => result.Success);
}

public static class CoreSessionSmokeTester
{
    public static CoreSessionSmokeTestResult Run(CoreSessionSmokeTestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.FramesToRun is < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "FramesToRun must be greater than or equal to zero.");

        if (!ManagedCoreRuntime.TryCreateSession(
                new CoreSessionLaunchRequest(request.CoreId),
                out var session,
                defaultCoreId: request.CoreId,
                options: request.RuntimeOptions,
                additionalModules: request.AdditionalModules))
        {
            return new CoreSessionSmokeTestResult(
                SessionCreated: false,
                RequestedCoreId: request.CoreId,
                SelectedCoreId: null,
                RuntimeInfo: null,
                CapabilityIds: [],
                InputPortCount: 0,
                InputActionCount: 0,
                LoadResult: null,
                StepResults: [],
                VideoFrameCount: 0,
                LastVideoFrame: null,
                FailureMessage: "No emulator core modules are currently available.");
        }

        var resolvedSession = session!;
        using (resolvedSession)
        {
            var videoFrameCount = 0;
            VideoFramePacket? lastVideoFrame = null;
            resolvedSession.VideoFrameReady += HandleVideoFrameReady;

            try
            {
                CoreLoadResult? loadResult = null;
                if (!string.IsNullOrWhiteSpace(request.MediaPath))
                {
                    loadResult = resolvedSession.LoadMedia(new CoreMediaLoadRequest(request.MediaPath));
                }

                var framesToRun = request.FramesToRun ?? (!string.IsNullOrWhiteSpace(request.MediaPath) ? 1 : 0);
                var stepResults = new List<CoreStepResult>(Math.Max(0, framesToRun));
                if (loadResult is null || loadResult.Success)
                {
                    for (var index = 0; index < framesToRun; index++)
                    {
                        var stepResult = resolvedSession.RunFrame();
                        stepResults.Add(stepResult);
                        if (!stepResult.Success)
                            break;
                    }
                }

                return new CoreSessionSmokeTestResult(
                    SessionCreated: true,
                    RequestedCoreId: request.CoreId,
                    SelectedCoreId: resolvedSession.RuntimeInfo.CoreId,
                    RuntimeInfo: resolvedSession.RuntimeInfo,
                    CapabilityIds: resolvedSession.Capabilities.Ids.ToArray(),
                    InputPortCount: resolvedSession.InputSchema.Ports.Count,
                    InputActionCount: resolvedSession.InputSchema.Actions.Count,
                    LoadResult: loadResult,
                    StepResults: stepResults,
                    VideoFrameCount: videoFrameCount,
                    LastVideoFrame: lastVideoFrame,
                    FailureMessage: ResolveFailureMessage(loadResult, stepResults));
            }
            finally
            {
                resolvedSession.VideoFrameReady -= HandleVideoFrameReady;
            }

            void HandleVideoFrameReady(VideoFramePacket packet)
            {
                videoFrameCount++;
                lastVideoFrame = packet;
            }
        }
    }

    private static string? ResolveFailureMessage(CoreLoadResult? loadResult, IReadOnlyList<CoreStepResult> stepResults)
    {
        if (loadResult is { Success: false })
            return loadResult.ErrorMessage ?? "Media load failed.";

        var failedStep = stepResults.FirstOrDefault(result => !result.Success);
        return failedStep?.ErrorMessage;
    }
}

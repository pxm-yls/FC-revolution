using FCRevolution.Emulation.Host;
using FCRevolution.Storage;

namespace FCRevolution.Core.Checker;

internal enum CoreCheckerCommandKind
{
    Check,
    List,
    Help
}

internal sealed record CoreCheckerOptions(
    CoreCheckerCommandKind Command,
    string? ResourceRootPath,
    IReadOnlyList<string> ProbeDirectories,
    string? PackagePath,
    string? CoreId,
    string? RomPath,
    int? FramesToRun);

public static class CoreCheckerCli
{
    private const int SuccessExitCode = 0;
    private const int UsageExitCode = 1;
    private const int ValidationFailureExitCode = 2;

    public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        if (!TryParseOptions(args, out var options, out var errorMessage))
        {
            await stderr.WriteLineAsync(errorMessage);
            await WriteUsageAsync(stderr);
            return UsageExitCode;
        }

        if (options.Command == CoreCheckerCommandKind.Help)
        {
            await WriteUsageAsync(stdout);
            return SuccessExitCode;
        }

        try
        {
            using var workspace = CoreCheckerWorkspace.Create(options);
            return options.Command switch
            {
                CoreCheckerCommandKind.List => await ExecuteListAsync(options, workspace, stdout),
                CoreCheckerCommandKind.Check => await ExecuteCheckAsync(options, workspace, stdout, stderr),
                _ => UsageExitCode
            };
        }
        catch (Exception ex)
        {
            await stderr.WriteLineAsync($"core-checker error: {ex.Message}");
            return ValidationFailureExitCode;
        }
    }

    private static async Task<int> ExecuteListAsync(
        CoreCheckerOptions options,
        CoreCheckerWorkspace workspace,
        TextWriter stdout)
    {
        var entries = ManagedCoreRuntime.LoadCatalogEntries(workspace.RuntimeOptions);

        await stdout.WriteLineAsync($"Catalog entries discovered: {entries.Count}");
        foreach (var entry in entries.OrderBy(entry => entry.Manifest.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            await stdout.WriteLineAsync(
                $"- {entry.Manifest.CoreId} | {entry.Manifest.DisplayName} | system={entry.Manifest.SystemId} | version={entry.Manifest.Version} | source={FormatSourceKind(entry.SourceKind)} | loader={(entry.IsLoadSupported ? "ready" : "missing")}");

            if (!string.IsNullOrWhiteSpace(entry.EntryPath))
                await stdout.WriteLineAsync($"  entry: {entry.EntryPath}");
            if (!string.IsNullOrWhiteSpace(entry.ManifestPath))
                await stdout.WriteLineAsync($"  manifest: {entry.ManifestPath}");
            if (!string.IsNullOrWhiteSpace(entry.InstallDirectory))
                await stdout.WriteLineAsync($"  install-dir: {entry.InstallDirectory}");
            if (!entry.IsLoadSupported && !string.IsNullOrWhiteSpace(entry.LoadSupportReason))
                await stdout.WriteLineAsync($"  loader-status: {entry.LoadSupportReason}");
        }

        if (entries.Count == 0)
            await stdout.WriteLineAsync("No cores discovered in the requested runtime context.");

        return SuccessExitCode;
    }

    private static async Task<int> ExecuteCheckAsync(
        CoreCheckerOptions options,
        CoreCheckerWorkspace workspace,
        TextWriter stdout,
        TextWriter stderr)
    {
        var entries = ManagedCoreRuntime.LoadCatalogEntries(workspace.RuntimeOptions);
        if (entries.Count == 0)
        {
            await stderr.WriteLineAsync("No cores discovered in the requested runtime context.");
            return ValidationFailureExitCode;
        }

        var requestedEntry = string.IsNullOrWhiteSpace(options.CoreId)
            ? null
            : entries.FirstOrDefault(entry => string.Equals(entry.Manifest.CoreId, options.CoreId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(options.CoreId) && requestedEntry is null)
        {
            await stderr.WriteLineAsync($"Requested core '{options.CoreId}' was not discovered.");
            return ValidationFailureExitCode;
        }

        if (requestedEntry is { IsLoadSupported: false })
        {
            await stderr.WriteLineAsync(requestedEntry.LoadSupportReason ??
                                        $"Requested core '{requestedEntry.Manifest.CoreId}' is installed but cannot be loaded by the current host.");
            return ValidationFailureExitCode;
        }

        if (string.IsNullOrWhiteSpace(options.CoreId) &&
            entries.Count > 0 &&
            entries.All(entry => !entry.IsLoadSupported))
        {
            await stderr.WriteLineAsync("Catalog entries were discovered, but the current host has no registered loader for any discovered binaryKind.");
            return ValidationFailureExitCode;
        }

        var smokeResult = CoreSessionSmokeTester.Run(new CoreSessionSmokeTestRequest(
            CoreId: options.CoreId,
            MediaPath: options.RomPath,
            FramesToRun: options.FramesToRun,
            RuntimeOptions: workspace.RuntimeOptions));

        await stdout.WriteLineAsync($"Catalog entries discovered: {entries.Count}");
        await stdout.WriteLineAsync($"Requested core: {options.CoreId ?? "(default)"}");
        await stdout.WriteLineAsync($"Selected core: {smokeResult.SelectedCoreId ?? "(none)"}");
        await stdout.WriteLineAsync($"Session created: {smokeResult.SessionCreated}");

        if (smokeResult.RuntimeInfo is not null)
        {
            await stdout.WriteLineAsync(
                $"Runtime: {smokeResult.RuntimeInfo.DisplayName} | system={smokeResult.RuntimeInfo.SystemId} | version={smokeResult.RuntimeInfo.Version} | binary={smokeResult.RuntimeInfo.BinaryKind}");
        }

        await stdout.WriteLineAsync(
            $"Input schema: ports={smokeResult.InputPortCount} | actions={smokeResult.InputActionCount}");
        await stdout.WriteLineAsync(
            $"Capabilities: {(smokeResult.CapabilityIds.Count == 0 ? "(none)" : string.Join(", ", smokeResult.CapabilityIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)))}");

        if (smokeResult.LoadResult is not null)
        {
            await stdout.WriteLineAsync(
                $"Media load: {(smokeResult.LoadResult.Success ? "ok" : "failed")} {FormatOptionalDetail(smokeResult.LoadResult.ErrorMessage)}");
        }

        if (smokeResult.StepResults.Count > 0)
        {
            await stdout.WriteLineAsync($"Frames executed: {smokeResult.StepResults.Count}");
            for (var index = 0; index < smokeResult.StepResults.Count; index++)
            {
                var stepResult = smokeResult.StepResults[index];
                await stdout.WriteLineAsync(
                    $"  frame[{index}]: {(stepResult.Success ? "ok" : "failed")} | presentation={stepResult.PresentationIndex} {FormatOptionalDetail(stepResult.ErrorMessage)}");
            }
        }

        await stdout.WriteLineAsync($"Video frames observed: {smokeResult.VideoFrameCount}");
        if (smokeResult.LastVideoFrame is not null)
        {
            await stdout.WriteLineAsync(
                $"Last video frame: {smokeResult.LastVideoFrame.Width}x{smokeResult.LastVideoFrame.Height} | presentation={smokeResult.LastVideoFrame.PresentationIndex} | pixel-format={smokeResult.LastVideoFrame.PixelFormat}");
        }

        if (!smokeResult.SessionCreated)
        {
            await stderr.WriteLineAsync(smokeResult.FailureMessage ?? "Session creation failed.");
            return ValidationFailureExitCode;
        }

        if (!string.IsNullOrWhiteSpace(options.CoreId) &&
            !string.Equals(smokeResult.SelectedCoreId, options.CoreId, StringComparison.OrdinalIgnoreCase))
        {
            await stderr.WriteLineAsync(
                $"Requested core '{options.CoreId}' was not selected. Actual session core: '{smokeResult.SelectedCoreId ?? "(none)"}'.");
            return ValidationFailureExitCode;
        }

        if (!smokeResult.Succeeded)
        {
            await stderr.WriteLineAsync(smokeResult.FailureMessage ?? "Smoke check failed.");
            return ValidationFailureExitCode;
        }

        await stdout.WriteLineAsync("Smoke check passed.");
        return SuccessExitCode;
    }

    private static bool TryParseOptions(string[] args, out CoreCheckerOptions options, out string errorMessage)
    {
        options = new CoreCheckerOptions(
            Command: CoreCheckerCommandKind.Check,
            ResourceRootPath: null,
            ProbeDirectories: [],
            PackagePath: null,
            CoreId: null,
            RomPath: null,
            FramesToRun: null);
        errorMessage = string.Empty;

        if (args.Length == 0)
        {
            options = options with { Command = CoreCheckerCommandKind.Help };
            return true;
        }

        var currentIndex = 0;
        if (!args[0].StartsWith("-", StringComparison.Ordinal))
        {
            if (!TryParseCommand(args[0], out var command))
            {
                errorMessage = $"Unknown command '{args[0]}'.";
                return false;
            }

            options = options with { Command = command };
            currentIndex = 1;
        }

        var probeDirectories = new List<string>();
        string? resourceRootPath = null;
        string? packagePath = null;
        string? coreId = null;
        string? romPath = null;
        int? framesToRun = null;

        while (currentIndex < args.Length)
        {
            var token = args[currentIndex++];
            switch (token)
            {
                case "--resource-root":
                    if (!TryReadValue(args, ref currentIndex, token, out resourceRootPath, out errorMessage))
                        return false;
                    break;
                case "--probe-dir":
                    if (!TryReadValue(args, ref currentIndex, token, out var probeDirectory, out errorMessage))
                        return false;
                    probeDirectories.Add(probeDirectory!);
                    break;
                case "--package":
                    if (!TryReadValue(args, ref currentIndex, token, out packagePath, out errorMessage))
                        return false;
                    break;
                case "--core-id":
                    if (!TryReadValue(args, ref currentIndex, token, out coreId, out errorMessage))
                        return false;
                    break;
                case "--rom":
                    if (!TryReadValue(args, ref currentIndex, token, out romPath, out errorMessage))
                        return false;
                    break;
                case "--frames":
                    if (!TryReadValue(args, ref currentIndex, token, out var rawFrames, out errorMessage))
                        return false;
                    if (!int.TryParse(rawFrames, out var parsedFrames) || parsedFrames < 0)
                    {
                        errorMessage = "--frames expects a non-negative integer value.";
                        return false;
                    }

                    framesToRun = parsedFrames;
                    break;
                case "--help":
                case "-h":
                    options = options with { Command = CoreCheckerCommandKind.Help };
                    return true;
                default:
                    errorMessage = $"Unknown option '{token}'.";
                    return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(resourceRootPath) && !string.IsNullOrWhiteSpace(packagePath))
        {
            errorMessage = "--resource-root cannot be combined with --package because package validation uses an isolated temporary resource root.";
            return false;
        }

        if (options.Command == CoreCheckerCommandKind.List &&
            (!string.IsNullOrWhiteSpace(coreId) || !string.IsNullOrWhiteSpace(romPath) || framesToRun is not null))
        {
            errorMessage = "The list command only supports --resource-root, --probe-dir, and --package.";
            return false;
        }

        options = new CoreCheckerOptions(
            options.Command,
            resourceRootPath,
            probeDirectories,
            packagePath,
            coreId,
            romPath,
            framesToRun);
        return true;
    }

    private static bool TryParseCommand(string rawValue, out CoreCheckerCommandKind command)
    {
        switch (rawValue.ToLowerInvariant())
        {
            case "check":
                command = CoreCheckerCommandKind.Check;
                return true;
            case "list":
                command = CoreCheckerCommandKind.List;
                return true;
            case "help":
            case "--help":
            case "-h":
                command = CoreCheckerCommandKind.Help;
                return true;
            default:
                command = CoreCheckerCommandKind.Check;
                return false;
        }
    }

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int currentIndex,
        string optionName,
        out string? value,
        out string errorMessage)
    {
        if (currentIndex >= args.Count)
        {
            value = null;
            errorMessage = $"Missing value for {optionName}.";
            return false;
        }

        value = args[currentIndex++];
        errorMessage = string.Empty;
        return true;
    }

    private static string FormatSourceKind(ManagedCoreCatalogSourceKind sourceKind) => sourceKind switch
    {
        ManagedCoreCatalogSourceKind.ProbeDirectory => "probe-directory",
        ManagedCoreCatalogSourceKind.InstalledPackage => "installed-package",
        ManagedCoreCatalogSourceKind.BundledPackage => "bundled-package",
        _ => sourceKind.ToString()
    };

    private static string FormatOptionalDetail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"| {value}";

    private static Task WriteUsageAsync(TextWriter writer)
    {
        return writer.WriteAsync(
            """
            FC-Revolution Core Checker

            Usage:
              fc-core-checker list [--resource-root <path>] [--probe-dir <path>]... [--package <path>]
              fc-core-checker check [--resource-root <path>] [--probe-dir <path>]... [--package <path>] [--core-id <id>] [--rom <path>] [--frames <count>]

            Notes:
              - check is the default command when only options are provided.
              - --package validates a core package inside an isolated temporary resource root.
              - --resource-root checks already-installed cores in an existing runtime root.
              - --probe-dir can be repeated to inspect loose core entry assemblies (currently managed-dotnet only).
            """);
    }

    internal sealed class CoreCheckerWorkspace : IDisposable
    {
        private readonly string _originalResourceRoot;
        private readonly string? _cleanupDirectory;

        private CoreCheckerWorkspace(
            string resourceRootPath,
            IReadOnlyList<string> probeDirectories,
            string originalResourceRoot,
            string? cleanupDirectory)
        {
            ResourceRootPath = resourceRootPath;
            RuntimeOptions = new ManagedCoreRuntimeOptions(
                ResourceRootPath: resourceRootPath,
                ProbeDirectories: probeDirectories);
            _originalResourceRoot = originalResourceRoot;
            _cleanupDirectory = cleanupDirectory;
        }

        public string ResourceRootPath { get; }

        public ManagedCoreRuntimeOptions RuntimeOptions { get; }

        public static CoreCheckerWorkspace Create(CoreCheckerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var originalResourceRoot = AppObjectStorage.GetResourceRoot();
            var probeDirectories = options.ProbeDirectories
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .Distinct(GetPathComparer())
                .ToList();

            if (!string.IsNullOrWhiteSpace(options.PackagePath))
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-core-checker-{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempRoot);
                new ManagedCorePackageService().InstallPackage(options.PackagePath, tempRoot);
                return new CoreCheckerWorkspace(tempRoot, probeDirectories, originalResourceRoot, cleanupDirectory: tempRoot);
            }

            var resourceRootPath = AppObjectStorage.NormalizeConfiguredResourceRoot(
                string.IsNullOrWhiteSpace(options.ResourceRootPath)
                    ? originalResourceRoot
                    : options.ResourceRootPath);
            return new CoreCheckerWorkspace(resourceRootPath, probeDirectories, originalResourceRoot, cleanupDirectory: null);
        }

        public void Dispose()
        {
            AppObjectStorage.ConfigureResourceRoot(_originalResourceRoot);

            if (string.IsNullOrWhiteSpace(_cleanupDirectory) || !Directory.Exists(_cleanupDirectory))
                return;

            try
            {
                Directory.Delete(_cleanupDirectory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for isolated checker workspaces.
            }
        }

        private static StringComparer GetPathComparer() => OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }
}

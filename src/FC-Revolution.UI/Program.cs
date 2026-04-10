using Avalonia;
using FCRevolution.Backend.Abstractions;
using FCRevolution.Backend.Hosting;
using FCRevolution.Contracts.RemoteControl;
using FCRevolution.Contracts.Sessions;
using FCRevolution.Emulation.Host;
using FCRevolution.Storage;
using FC_Revolution.UI.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FC_Revolution.UI;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupDiagnostics.InitializeForCurrentProcess(args);
        GeometryDiagnostics.InitializeForCurrentProcess(args);
        RegisterGlobalExceptionLogging();

        try
        {
            StartupDiagnostics.Write("program", "checking CLI host mode");
            if (TryRunLanProbeHost(args).GetAwaiter().GetResult())
            {
                StartupDiagnostics.Write("program", "CLI host mode completed, exiting process");
                return;
            }

            InitializeManagedCoreCatalog();
            StartupDiagnostics.Write("program", "building Avalonia app");
            var appBuilder = BuildAvaloniaApp();
            StartupDiagnostics.Write("program", "starting classic desktop lifetime");
            appBuilder.StartWithClassicDesktopLifetime(args);
            StartupDiagnostics.Write("program", "classic desktop lifetime returned");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.WriteException("program", "fatal startup exception", ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new AvaloniaNativePlatformOptions
            {
                RenderingMode =
                [
                    AvaloniaNativeRenderingMode.Metal,
                    AvaloniaNativeRenderingMode.Software
                ]
            })
            .WithInterFont()
            .LogToTrace();

    private static void InitializeManagedCoreCatalog()
    {
        var profile = Models.SystemConfigProfile.Load();
        AppObjectStorage.ConfigureResourceRoot(profile.ResourceRootPath);
        var managedCoreProbePaths = ResolveManagedCoreProbePaths(profile).ToArray();
        var runtimeOptions = new ManagedCoreRuntimeOptions(
            ResourceRootPath: profile.ResourceRootPath,
            ProbeDirectories: managedCoreProbePaths);

        var installedCoreIds = ManagedCoreRuntime.LoadCatalogEntries(runtimeOptions)
            .Select(entry => entry.Manifest)
            .Select(manifest => manifest.CoreId)
            .ToArray();
        StartupDiagnostics.Write(
            "program",
            $"managed core catalog initialized; sources=managed-core-package-registry,managed-core-probe-paths; probePaths={string.Join(", ", managedCoreProbePaths)}; installed={string.Join(", ", installedCoreIds)}");
    }

    private static IReadOnlyList<string> ResolveManagedCoreProbePaths(Models.SystemConfigProfile profile) =>
        profile.GetEffectiveManagedCoreProbeDirectories();

    private static async Task<bool> TryRunLanProbeHost(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "--lan-probe-host", StringComparison.OrdinalIgnoreCase))
            return false;

        var logPath = "/tmp/fc-lan-probe-cli.log";
        File.AppendAllText(logPath, $"[{DateTime.Now:O}] cli mode requested{Environment.NewLine}");

        var port = Models.SystemConfigProfile.DefaultLanArcadePort;
        if (args.Length > 1 && int.TryParse(args[1], out var parsedPort))
            port = parsedPort;

        try
        {
            StartupDiagnostics.Write("lan-probe", $"creating host for port {port}");
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] creating host port={port}{Environment.NewLine}");
            var runtimeBridge = new CliProbeRuntimeBridge();
            await using var host = new BackendHostService(
                new BackendHostOptions(port, EnableWebPad: true, EnableDebugPages: true),
                runtimeBridge,
                runtimeBridge,
                runtimeBridge,
                runtimeBridge,
                message =>
                {
                    Console.WriteLine($"[lan-probe] {message}");
                    File.AppendAllText(logPath, $"[{DateTime.Now:O}] traffic {message}{Environment.NewLine}");
                });

            StartupDiagnostics.Write("lan-probe", "starting host");
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] starting host{Environment.NewLine}");
            await host.StartAsync();
            StartupDiagnostics.Write("lan-probe", "host started");
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] host started{Environment.NewLine}");
            Console.WriteLine($"[lan-probe] listening on http://127.0.0.1:{port}/");

            using var quit = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                quit.Set();
            };

            quit.Wait();
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] stopping host{Environment.NewLine}");
            await host.StopAsync(TimeSpan.FromSeconds(2));
            return true;
        }
        catch (Exception ex)
        {
            StartupDiagnostics.WriteException("lan-probe", "CLI host failed", ex);
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] cli host failed: {ex}{Environment.NewLine}");
            throw;
        }
    }

    private static void RegisterGlobalExceptionLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception ex)
            {
                StartupDiagnostics.WriteException("exception", "AppDomain.CurrentDomain.UnhandledException", ex);
                GeometryDiagnostics.WriteException("exception", "AppDomain.CurrentDomain.UnhandledException", ex);
            }
            else
            {
                StartupDiagnostics.Write("exception", $"AppDomain.CurrentDomain.UnhandledException: {eventArgs.ExceptionObject}");
                GeometryDiagnostics.Write("exception", $"AppDomain.CurrentDomain.UnhandledException: {eventArgs.ExceptionObject}");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            StartupDiagnostics.WriteException("exception", "TaskScheduler.UnobservedTaskException", eventArgs.Exception);
            GeometryDiagnostics.WriteException("exception", "TaskScheduler.UnobservedTaskException", eventArgs.Exception);
            eventArgs.SetObserved();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            StartupDiagnostics.Write("program", "process exit");
            GeometryDiagnostics.Write("program", "process exit");
        };
    }

    private sealed class CliProbeRuntimeBridge : IBackendRuntimeBridge
    {
        public Task<StartSessionResponse?> StartSessionAsync(StartSessionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<StartSessionResponse?>(null);

        public Task<BackendMediaAsset?> GetRomPreviewAssetAsync(string romPath, CancellationToken cancellationToken = default)
            => Task.FromResult<BackendMediaAsset?>(null);

        public Task<bool> CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<byte[]?> GetSessionPreviewAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult<byte[]?>(null);

        public Task<bool> ClaimControlAsync(Guid sessionId, ClaimControlRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task ReleaseControlAsync(Guid sessionId, ReleaseControlRequest request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RefreshHeartbeatAsync(Guid sessionId, RefreshHeartbeatRequest request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> SetButtonStateAsync(Guid sessionId, ButtonStateRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> SetInputStateAsync(Guid sessionId, SetInputStateRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<BackendStreamSubscription?> SubscribeStreamAsync(Guid sessionId, int audioChunkSize = 882, CancellationToken cancellationToken = default)
            => Task.FromResult<BackendStreamSubscription?>(null);
    }
}

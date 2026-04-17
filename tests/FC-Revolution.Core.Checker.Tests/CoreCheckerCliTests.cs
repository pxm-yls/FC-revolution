using System.IO.Compression;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FCRevolution.Core.Checker;
using FCRevolution.Core.Sample.Managed;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Emulation.Host;

namespace FCRevolution.Core.Checker.Tests;

public sealed class CoreCheckerCliTests
{
    [Fact]
    public async Task RunAsync_List_ReportsDiscoveredProbeDirectoryCores()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var probeDirectory = Path.GetDirectoryName(typeof(SampleManagedCoreModule).Assembly.Location);
        Assert.False(string.IsNullOrWhiteSpace(probeDirectory));

        var exitCode = await CoreCheckerCli.RunAsync(
            ["list", "--probe-dir", probeDirectory!],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        Assert.Contains(SampleManagedCoreModule.CoreId, stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("probe-directory", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public async Task RunAsync_CheckPackage_CreatesSessionAndLoadsMedia()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"fc-core-checker-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var packagePath = BuildSamplePackage(tempDirectory);
            var romPath = Path.Combine(tempDirectory, "sample.rom");
            await File.WriteAllBytesAsync(romPath, "sample-core-checker"u8.ToArray());

            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exitCode = await CoreCheckerCli.RunAsync(
                ["check", "--package", packagePath, "--core-id", SampleManagedCoreModule.CoreId, "--rom", romPath, "--frames", "2"],
                stdout,
                stderr);

            Assert.Equal(0, exitCode);
            Assert.Contains("Smoke check passed.", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"Selected core: {SampleManagedCoreModule.CoreId}", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Media load: ok", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_Check_WhenRequestedCoreIsMissing_ReturnsValidationFailure()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var probeDirectory = Path.GetDirectoryName(typeof(SampleManagedCoreModule).Assembly.Location);
        Assert.False(string.IsNullOrWhiteSpace(probeDirectory));

        var exitCode = await CoreCheckerCli.RunAsync(
            ["check", "--probe-dir", probeDirectory!, "--core-id", "missing.core"],
            stdout,
            stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("missing.core", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_CheckPackage_WhenNativeCorePackageIsValid_ReturnsSuccess()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"fc-core-checker-native-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            if (!NativeCabiTestCoreBuilder.IsPlatformSupported)
                return;

            var nativeLibraryPath = NativeCabiTestCoreBuilder.BuildLibrary(
                tempDirectory,
                "fc.native.checker",
                exportCoreApi: true);
            var packagePath = BuildStandalonePackage(
                tempDirectory,
                new ManagedCorePackageManifestDocument
                {
                    Payload = new ManagedCorePackageManifestPayload
                    {
                        CoreId = "fc.native.checker",
                        DisplayName = "Native Checker Core",
                        Version = "1.0.0",
                        SystemIds = ["test"],
                        BinaryKind = CoreBinaryKinds.NativeCabi,
                        EntryPoint = new ManagedCorePackageEntryPoint
                        {
                            EntryPath = $"native/{Path.GetFileName(nativeLibraryPath)}"
                        },
                        Files = [CreateFileSpec($"native/{Path.GetFileName(nativeLibraryPath)}", File.ReadAllBytes(nativeLibraryPath)).Entry]
                    }
                },
                [CreateFileSpec($"native/{Path.GetFileName(nativeLibraryPath)}", File.ReadAllBytes(nativeLibraryPath))]);

            var romPath = Path.Combine(tempDirectory, "native-sample.rom");
            await File.WriteAllBytesAsync(romPath, "native-checker-smoke"u8.ToArray());
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exitCode = await CoreCheckerCli.RunAsync(
                ["check", "--package", packagePath, "--core-id", "fc.native.checker", "--rom", romPath, "--frames", "2"],
                stdout,
                stderr);

            Assert.Equal(0, exitCode);
            Assert.Contains("Selected core: fc.native.checker", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("binary=native-cabi", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Media load: ok", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Smoke check passed.", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_CheckPackage_WhenNativeCoreExportIsMissing_ReturnsValidationFailure()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"fc-core-checker-native-invalid-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            if (!NativeCabiTestCoreBuilder.IsPlatformSupported)
                return;

            var nativeLibraryPath = NativeCabiTestCoreBuilder.BuildLibrary(
                tempDirectory,
                "fc.native.invalid",
                exportCoreApi: false);
            var packagePath = BuildStandalonePackage(
                tempDirectory,
                new ManagedCorePackageManifestDocument
                {
                    Payload = new ManagedCorePackageManifestPayload
                    {
                        CoreId = "fc.native.invalid",
                        DisplayName = "Broken Native Core",
                        Version = "1.0.0",
                        SystemIds = ["test"],
                        BinaryKind = CoreBinaryKinds.NativeCabi,
                        EntryPoint = new ManagedCorePackageEntryPoint
                        {
                            EntryPath = $"native/{Path.GetFileName(nativeLibraryPath)}"
                        },
                        Files = [CreateFileSpec($"native/{Path.GetFileName(nativeLibraryPath)}", File.ReadAllBytes(nativeLibraryPath)).Entry]
                    }
                },
                [CreateFileSpec($"native/{Path.GetFileName(nativeLibraryPath)}", File.ReadAllBytes(nativeLibraryPath))]);

            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exitCode = await CoreCheckerCli.RunAsync(
                ["check", "--package", packagePath, "--core-id", "fc.native.invalid"],
                stdout,
                stderr);

            Assert.Equal(2, exitCode);
            Assert.Contains("FCR_GetCoreApi", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Smoke check passed.", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static string BuildSamplePackage(string tempDirectory)
    {
        var packageService = new ManagedCorePackageService();
        var module = new SampleManagedCoreModule();
        var packagePath = Path.Combine(
            tempDirectory,
            packageService.BuildSuggestedPackageFileName(module.Manifest));
        return packageService.ExportLooseManagedModule(
            module.Manifest,
            typeof(SampleManagedCoreModule).Assembly.Location,
            typeof(SampleManagedCoreModule).FullName ?? nameof(SampleManagedCoreModule),
            packagePath).PackagePath;
    }

    private static string BuildStandalonePackage(
        string rootDirectory,
        ManagedCorePackageManifestDocument manifestDocument,
        IReadOnlyList<PackageFileSpec> fileSpecs)
    {
        var packageDirectory = Path.Combine(rootDirectory, $"package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(packageDirectory);

        foreach (var fileSpec in fileSpecs)
        {
            var targetPath = Path.Combine(packageDirectory, fileSpec.Entry.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.WriteAllBytes(targetPath, fileSpec.Content);
        }

        File.WriteAllText(
            Path.Combine(packageDirectory, "core-manifest.fcr"),
            JsonSerializer.Serialize(manifestDocument, new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            }));

        var packagePath = Path.Combine(rootDirectory, $"{manifestDocument.Payload.CoreId}.fcrcore.zip");
        if (File.Exists(packagePath))
            File.Delete(packagePath);
        ZipFile.CreateFromDirectory(packageDirectory, packagePath);
        Directory.Delete(packageDirectory, recursive: true);
        return packagePath;
    }

    private static PackageFileSpec CreateFileSpec(string relativePath, byte[] content)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return new PackageFileSpec(
            new ManagedCorePackageFileEntry
            {
                Path = relativePath,
                Sha256 = hash
            },
            content);
    }

    private sealed record PackageFileSpec(ManagedCorePackageFileEntry Entry, byte[] Content);
}

internal static class NativeCabiTestCoreBuilder
{
    public static bool IsPlatformSupported => OperatingSystem.IsMacOS() || OperatingSystem.IsLinux();

    public static string BuildLibrary(string tempDirectory, string coreId, bool exportCoreApi)
    {
        if (!IsPlatformSupported)
            throw new PlatformNotSupportedException("Native C ABI test core builder currently supports macOS and Linux only.");

        var sourcePath = Path.Combine(tempDirectory, $"native-{Guid.NewGuid():N}.c");
        var outputPath = Path.Combine(tempDirectory, $"{coreId}{GetLibraryExtension()}");
        File.WriteAllText(sourcePath, BuildSource(coreId, exportCoreApi));

        var arguments = OperatingSystem.IsMacOS()
            ? $"-dynamiclib \"{sourcePath}\" -o \"{outputPath}\""
            : $"-shared -fPIC \"{sourcePath}\" -o \"{outputPath}\"";

        var startInfo = new ProcessStartInfo("cc", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start native test compiler process.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 || !File.Exists(outputPath))
        {
            throw new InvalidOperationException(
                $"Failed to build native test core.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}".Trim());
        }

        return outputPath;
    }

    private static string GetLibraryExtension() => OperatingSystem.IsMacOS() ? ".dylib" : ".so";

    private static string BuildSource(string coreId, bool exportCoreApi)
    {
        var manifestJson = JsonSerializer.Serialize(new
        {
            coreId,
            displayName = exportCoreApi ? "Native Checker Core" : "Broken Native Core",
            systemId = "test",
            version = "1.0.0",
            binaryKind = "native-cabi",
            supportedMediaFilePatterns = new[] { "*.rom" }
        });
        var manifestJsonLiteral = manifestJson.Replace("\\", "\\\\").Replace("\"", "\\\"");

        return $$"""
                 #include <stdint.h>
                 #include <stdlib.h>
                 #include <string.h>

                 #if defined(_WIN32)
                 #define FCR_EXPORT __declspec(dllexport)
                 #else
                 #define FCR_EXPORT __attribute__((visibility("default")))
                 #endif

                 typedef struct DemoSession
                 {
                     int loaded;
                     int frame;
                     uint8_t state[16];
                 } DemoSession;

                 typedef struct FcrCoreApi
                 {
                     uint32_t abi_version;
                     uint32_t struct_size;
                     const char* (*get_core_id)(void);
                     const char* (*get_manifest_json)(void);
                     void* (*create_session)(const void* host_api);
                     void  (*destroy_session)(void* session);
                     int   (*load_media)(void* session, const uint8_t* data, int32_t length, const char* file_name);
                     void  (*reset)(void* session);
                     void  (*pause)(void* session);
                     void  (*resume)(void* session);
                     int   (*run_frame)(void* session);
                     int   (*step_instruction)(void* session);
                     int   (*copy_video_frame)(void* session, void* dst, int32_t dst_length);
                     int   (*copy_audio_packet)(void* session, void* dst, int32_t dst_length);
                     int   (*capture_state)(void* session, uint8_t* dst, int32_t capacity);
                     int   (*restore_state)(void* session, const uint8_t* data, int32_t length);
                 } FcrCoreApi;

                 static const char* get_core_id(void)
                 {
                     return "{{coreId}}";
                 }

                 static const char* get_manifest_json(void)
                 {
                     return "{{manifestJsonLiteral}}";
                 }

                 static void* create_session(const void* host_api)
                 {
                     (void)host_api;
                     return calloc(1, sizeof(DemoSession));
                 }

                 static void destroy_session(void* session)
                 {
                     free(session);
                 }

                 static int load_media(void* session, const uint8_t* data, int32_t length, const char* file_name)
                 {
                     DemoSession* demo = (DemoSession*)session;
                     if (demo == NULL || data == NULL || length <= 0)
                         return 0;

                     demo->loaded = 1;
                     demo->state[0] = (uint8_t)length;
                     demo->state[1] = (uint8_t)((file_name != NULL && file_name[0] != '\0') ? file_name[0] : 0);
                     return 1;
                 }

                 static void reset(void* session)
                 {
                     DemoSession* demo = (DemoSession*)session;
                     if (demo != NULL)
                         demo->frame = 0;
                 }

                 static void pause(void* session)
                 {
                     (void)session;
                 }

                 static void resume(void* session)
                 {
                     (void)session;
                 }

                 static int run_frame(void* session)
                 {
                     DemoSession* demo = (DemoSession*)session;
                     if (demo == NULL || !demo->loaded)
                         return 0;

                     demo->frame += 1;
                     demo->state[2] = (uint8_t)demo->frame;
                     return 1;
                 }

                 static int step_instruction(void* session)
                 {
                     return run_frame(session);
                 }

                 static int copy_video_frame(void* session, void* dst, int32_t dst_length)
                 {
                     (void)session;
                     (void)dst;
                     (void)dst_length;
                     return 0;
                 }

                 static int copy_audio_packet(void* session, void* dst, int32_t dst_length)
                 {
                     (void)session;
                     (void)dst;
                     (void)dst_length;
                     return 0;
                 }

                 static int capture_state(void* session, uint8_t* dst, int32_t capacity)
                 {
                     DemoSession* demo = (DemoSession*)session;
                     if (demo == NULL)
                         return 0;

                     const int32_t required = (int32_t)sizeof(demo->state);
                     if (dst == NULL || capacity <= 0)
                         return required;
                     if (capacity < required)
                         return 0;

                     memcpy(dst, demo->state, required);
                     return required;
                 }

                 static int restore_state(void* session, const uint8_t* data, int32_t length)
                 {
                     DemoSession* demo = (DemoSession*)session;
                     if (demo == NULL || data == NULL || length <= 0)
                         return 0;

                     const int32_t copy_length = length < (int32_t)sizeof(demo->state)
                         ? length
                         : (int32_t)sizeof(demo->state);
                     memcpy(demo->state, data, copy_length);
                     return 1;
                 }

                 static const FcrCoreApi k_api =
                 {
                     1u,
                     (uint32_t)sizeof(FcrCoreApi),
                     get_core_id,
                     get_manifest_json,
                     create_session,
                     destroy_session,
                     load_media,
                     reset,
                     pause,
                     resume,
                     run_frame,
                     step_instruction,
                     copy_video_frame,
                     copy_audio_packet,
                     capture_state,
                     restore_state
                 };

                 {{(exportCoreApi
                     ? "FCR_EXPORT const FcrCoreApi* FCR_GetCoreApi(uint32_t host_abi_version)\n{\n    if (host_abi_version != 1u)\n        return NULL;\n\n    return &k_api;\n}"
                     : "FCR_EXPORT const char* FCR_NoApi(void)\n{\n    return \"missing\";\n}")}}
                 """;
    }
}

using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using FCRevolution.Emulation.Abstractions;
using FCRevolution.Storage;

namespace FCRevolution.Emulation.Host;

public static class ManagedCorePackageDocumentKinds
{
    public const string CoreManifest = "FC-Revolution-Core-Manifest";
    public const string CoreRegistry = "FC-Revolution-Core-Registry";
}

public sealed class ManagedCorePackageManifestDocument
{
    public int FcrFormatVersion { get; init; } = 1;

    public string DocumentKind { get; init; } = ManagedCorePackageDocumentKinds.CoreManifest;

    public int DocumentVersion { get; init; } = 1;

    public required ManagedCorePackageManifestPayload Payload { get; init; }
}

public sealed class ManagedCorePackageManifestPayload
{
    public required string CoreId { get; init; }

    public required string DisplayName { get; init; }

    public required string Version { get; init; }

    public IReadOnlyList<string> SystemIds { get; init; } = [];

    public string HostApiVersion { get; init; } = "1.0.0";

    public required string BinaryKind { get; init; }

    public string SourceLanguage { get; init; } = "csharp";

    public string ExecutionModel { get; init; } = "inproc";

    public required ManagedCorePackageEntryPoint EntryPoint { get; init; }

    public IReadOnlyList<string> Capabilities { get; init; } = [];

    public IReadOnlyList<ManagedCorePackageFileEntry> Files { get; init; } = [];
}

public sealed class ManagedCorePackageEntryPoint
{
    public required string AssemblyPath { get; init; }

    public required string FactoryType { get; init; }
}

public sealed class ManagedCorePackageFileEntry
{
    public required string Path { get; init; }

    public required string Sha256 { get; init; }
}

public sealed class ManagedCoreRegistryDocument
{
    public int FcrFormatVersion { get; init; } = 1;

    public string DocumentKind { get; init; } = ManagedCorePackageDocumentKinds.CoreRegistry;

    public int DocumentVersion { get; init; } = 1;

    public ManagedCoreRegistryPayload Payload { get; init; } = new();
}

public sealed class ManagedCoreRegistryPayload
{
    public IReadOnlyList<ManagedCoreRegistryEntry> Entries { get; init; } = [];
}

public sealed class ManagedCoreRegistryEntry
{
    public required string CoreId { get; init; }

    public required string DisplayName { get; init; }

    public required string Version { get; init; }

    public IReadOnlyList<string> SystemIds { get; init; } = [];

    public required string BinaryKind { get; init; }

    public required string InstallPath { get; init; }

    public required string ManifestPath { get; init; }

    public required string EntryAssemblyPath { get; init; }

    public required string FactoryType { get; init; }

    public bool IsBundled { get; init; }

    public DateTimeOffset InstalledAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record InstalledManagedCorePackage(
    CoreManifest Manifest,
    IReadOnlyList<string> SystemIds,
    string InstallDirectory,
    string ManifestPath,
    string EntryAssemblyPath,
    string FactoryType,
    bool IsBundled,
    DateTimeOffset InstalledAtUtc);

public sealed record ManagedCorePackageInstallResult(
    InstalledManagedCorePackage Package,
    bool ReplacedExistingCore);

public sealed record ManagedCorePackageExportResult(
    string PackagePath,
    ManagedCorePackageManifestDocument ManifestDocument);

public sealed class ManagedCorePackageService
{
    private const string ManifestFileName = "core-manifest.fcr";
    private const string PackageExtension = ".fcrcore.zip";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public IReadOnlyList<InstalledManagedCorePackage> GetInstalledPackages(string? resourceRootPath)
    {
        var normalizedResourceRoot = AppObjectStorage.NormalizeConfiguredResourceRoot(resourceRootPath);
        var registry = ReadRegistryDocument(normalizedResourceRoot);
        var coresRootDirectory = AppObjectStorage.GetCoresRootDirectory(normalizedResourceRoot);

        return registry.Payload.Entries
            .Select(entry => TryResolveInstalledPackage(coresRootDirectory, entry))
            .Where(package => package is not null)
            .Cast<InstalledManagedCorePackage>()
            .OrderBy(package => package.Manifest.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public ManagedCorePackageInstallResult InstallPackage(string packagePath, string? resourceRootPath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("Package path is required.", nameof(packagePath));

        var normalizedPackagePath = Path.GetFullPath(packagePath);
        if (!File.Exists(normalizedPackagePath))
            throw new FileNotFoundException("Core package was not found.", normalizedPackagePath);
        if (!normalizedPackagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("仅支持安装 .zip / .fcrcore.zip 核心包。");

        var normalizedResourceRoot = AppObjectStorage.NormalizeConfiguredResourceRoot(resourceRootPath);
        AppObjectStorage.ConfigureResourceRoot(normalizedResourceRoot);
        AppObjectStorage.EnsureDefaults();

        var tempRootDirectory = AppObjectStorage.GetCoreTempDirectory(normalizedResourceRoot);
        Directory.CreateDirectory(tempRootDirectory);

        var stagingDirectory = Path.Combine(tempRootDirectory, $"install-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            ExtractPackageToDirectory(normalizedPackagePath, stagingDirectory);
            return InstallStagedDirectory(stagingDirectory, normalizedResourceRoot);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
                Directory.Delete(stagingDirectory, recursive: true);
        }
    }

    public InstalledManagedCorePackage EnsureLooseManagedModuleInstalled(
        CoreManifest runtimeManifest,
        string assemblyPath,
        string moduleTypeName,
        string? resourceRootPath,
        bool isBundled = false)
    {
        ArgumentNullException.ThrowIfNull(runtimeManifest);
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Assembly path is required.", nameof(assemblyPath));
        if (string.IsNullOrWhiteSpace(moduleTypeName))
            throw new ArgumentException("Module type name is required.", nameof(moduleTypeName));

        var normalizedResourceRoot = AppObjectStorage.NormalizeConfiguredResourceRoot(resourceRootPath);
        var installed = GetInstalledPackages(normalizedResourceRoot).FirstOrDefault(package =>
            string.Equals(package.Manifest.CoreId, runtimeManifest.CoreId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(package.Manifest.Version, runtimeManifest.Version, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(package.EntryAssemblyPath) &&
            package.IsBundled == isBundled);
        if (installed is not null)
            return installed;

        var stagingDirectory = Path.Combine(
            AppObjectStorage.GetCoreTempDirectory(normalizedResourceRoot),
            $"bundle-{runtimeManifest.CoreId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            MaterializeLooseManagedModuleDirectory(runtimeManifest, assemblyPath, moduleTypeName, stagingDirectory);
            return InstallStagedDirectory(stagingDirectory, normalizedResourceRoot, isBundled).Package;
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
                Directory.Delete(stagingDirectory, recursive: true);
        }
    }

    public bool UninstallInstalledPackage(string? resourceRootPath, string coreId)
    {
        if (string.IsNullOrWhiteSpace(coreId))
            return false;

        var normalizedResourceRoot = AppObjectStorage.NormalizeConfiguredResourceRoot(resourceRootPath);
        var registry = ReadRegistryDocument(normalizedResourceRoot);
        var entry = registry.Payload.Entries.FirstOrDefault(candidate =>
            string.Equals(candidate.CoreId, coreId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            return false;
        if (entry.IsBundled)
            return false;

        var coresRootDirectory = AppObjectStorage.GetCoresRootDirectory(normalizedResourceRoot);
        var installDirectory = ResolveRegistryPath(coresRootDirectory, entry.InstallPath);
        if (Directory.Exists(installDirectory))
            Directory.Delete(installDirectory, recursive: true);

        var updatedEntries = registry.Payload.Entries
            .Where(candidate => !string.Equals(candidate.CoreId, coreId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        WriteRegistryDocument(normalizedResourceRoot, new ManagedCoreRegistryDocument
        {
            Payload = new ManagedCoreRegistryPayload
            {
                Entries = updatedEntries
            }
        });

        return true;
    }

    public ManagedCorePackageExportResult ExportLooseManagedModule(
        CoreManifest runtimeManifest,
        string assemblyPath,
        string moduleTypeName,
        string destinationPackagePath)
    {
        ArgumentNullException.ThrowIfNull(runtimeManifest);
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Assembly path is required.", nameof(assemblyPath));
        if (string.IsNullOrWhiteSpace(moduleTypeName))
            throw new ArgumentException("Module type name is required.", nameof(moduleTypeName));
        if (string.IsNullOrWhiteSpace(destinationPackagePath))
            throw new ArgumentException("Destination package path is required.", nameof(destinationPackagePath));

        var normalizedAssemblyPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(normalizedAssemblyPath))
            throw new FileNotFoundException("Managed core assembly was not found.", normalizedAssemblyPath);

        var normalizedDestinationPath = NormalizePackageOutputPath(destinationPackagePath, runtimeManifest);
        Directory.CreateDirectory(Path.GetDirectoryName(normalizedDestinationPath)!);

        var stagingDirectory = Path.Combine(Path.GetTempPath(), $"fc-core-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            var manifestDocument = MaterializeLooseManagedModuleDirectory(
                runtimeManifest,
                normalizedAssemblyPath,
                moduleTypeName,
                stagingDirectory);
            CreatePackageFromDirectory(stagingDirectory, normalizedDestinationPath);
            return new ManagedCorePackageExportResult(normalizedDestinationPath, manifestDocument);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
                Directory.Delete(stagingDirectory, recursive: true);
        }
    }

    public ManagedCorePackageExportResult ExportInstalledPackage(string installDirectory, string destinationPackagePath)
    {
        if (string.IsNullOrWhiteSpace(installDirectory))
            throw new ArgumentException("Install directory is required.", nameof(installDirectory));
        if (string.IsNullOrWhiteSpace(destinationPackagePath))
            throw new ArgumentException("Destination package path is required.", nameof(destinationPackagePath));

        var normalizedInstallDirectory = Path.GetFullPath(installDirectory);
        if (!Directory.Exists(normalizedInstallDirectory))
            throw new DirectoryNotFoundException($"Installed core directory was not found: {normalizedInstallDirectory}");

        var manifestPath = Path.Combine(normalizedInstallDirectory, ManifestFileName);
        var manifestDocument = ReadManifestDocument(manifestPath);
        ValidateManifestDocument(manifestDocument, normalizedInstallDirectory);

        var normalizedDestinationPath = NormalizePackageOutputPath(destinationPackagePath, manifestDocument.Payload);
        Directory.CreateDirectory(Path.GetDirectoryName(normalizedDestinationPath)!);
        CreatePackageFromDirectory(normalizedInstallDirectory, normalizedDestinationPath);
        return new ManagedCorePackageExportResult(normalizedDestinationPath, manifestDocument);
    }

    public string BuildSuggestedPackageFileName(CoreManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return $"{AppObjectStorage.SanitizeFileName(manifest.CoreId)}-{AppObjectStorage.SanitizeFileName(manifest.Version)}{PackageExtension}";
    }

    private static ManagedCorePackageManifestDocument CreateManifestDocument(
        CoreManifest runtimeManifest,
        string moduleTypeName,
        string entryAssemblyPath,
        IReadOnlyList<ManagedCorePackageFileEntry> fileEntries)
    {
        return new ManagedCorePackageManifestDocument
        {
            Payload = new ManagedCorePackageManifestPayload
            {
                CoreId = runtimeManifest.CoreId,
                DisplayName = runtimeManifest.DisplayName,
                Version = runtimeManifest.Version,
                SystemIds = string.IsNullOrWhiteSpace(runtimeManifest.SystemId)
                    ? []
                    : [runtimeManifest.SystemId],
                BinaryKind = runtimeManifest.BinaryKind,
                EntryPoint = new ManagedCorePackageEntryPoint
                {
                    AssemblyPath = entryAssemblyPath,
                    FactoryType = moduleTypeName
                },
                Files = fileEntries
            }
        };
    }

    private ManagedCorePackageInstallResult InstallStagedDirectory(
        string stagingDirectory,
        string normalizedResourceRoot,
        bool isBundled = false)
    {
        var manifestPath = Path.Combine(stagingDirectory, ManifestFileName);
        var manifestDocument = ReadManifestDocument(manifestPath);
        ValidateManifestDocument(manifestDocument, stagingDirectory);

        var payload = manifestDocument.Payload;
        var installDirectory = AppObjectStorage.GetInstalledCoreVersionDirectory(
            normalizedResourceRoot,
            payload.CoreId,
            payload.Version);
        var replacedExistingCore = GetInstalledPackages(normalizedResourceRoot)
            .Any(package => string.Equals(package.Manifest.CoreId, payload.CoreId, StringComparison.OrdinalIgnoreCase));

        if (Directory.Exists(installDirectory))
            Directory.Delete(installDirectory, recursive: true);

        CopyDirectory(stagingDirectory, installDirectory);

        var coresRootDirectory = AppObjectStorage.GetCoresRootDirectory(normalizedResourceRoot);
        var installRelativePath = Path.GetRelativePath(coresRootDirectory, installDirectory);
        var manifestRelativePath = Path.Combine(installRelativePath, ManifestFileName);
        var entryAssemblyRelativePath = Path.Combine(installRelativePath, payload.EntryPoint.AssemblyPath);
        var registryEntry = new ManagedCoreRegistryEntry
        {
            CoreId = payload.CoreId,
            DisplayName = payload.DisplayName,
            Version = payload.Version,
            SystemIds = payload.SystemIds.Count == 0 ? [payload.CoreId] : payload.SystemIds,
            BinaryKind = payload.BinaryKind,
            InstallPath = installRelativePath,
            ManifestPath = manifestRelativePath,
            EntryAssemblyPath = entryAssemblyRelativePath,
            FactoryType = payload.EntryPoint.FactoryType,
            IsBundled = isBundled,
            InstalledAtUtc = DateTimeOffset.UtcNow
        };

        var registry = ReadRegistryDocument(normalizedResourceRoot);
        var updatedEntries = registry.Payload.Entries
            .Where(entry => !string.Equals(entry.CoreId, registryEntry.CoreId, StringComparison.OrdinalIgnoreCase))
            .Append(registryEntry)
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        WriteRegistryDocument(normalizedResourceRoot, new ManagedCoreRegistryDocument
        {
            Payload = new ManagedCoreRegistryPayload
            {
                Entries = updatedEntries
            }
        });

        CleanupOtherInstalledVersions(normalizedResourceRoot, payload.CoreId, payload.Version);

        return new ManagedCorePackageInstallResult(
            BuildInstalledPackage(installDirectory, manifestDocument, registryEntry),
            replacedExistingCore);
    }

    private static ManagedCorePackageManifestDocument MaterializeLooseManagedModuleDirectory(
        CoreManifest runtimeManifest,
        string normalizedAssemblyPath,
        string moduleTypeName,
        string stagingDirectory)
    {
        var managedDirectory = Path.Combine(stagingDirectory, "managed");
        Directory.CreateDirectory(managedDirectory);

        var packageFileEntries = new List<ManagedCorePackageFileEntry>();
        var entryAssemblyRelativePath = Path.Combine("managed", Path.GetFileName(normalizedAssemblyPath));
        foreach (var sourceFile in EnumerateLoosePackageFiles(normalizedAssemblyPath))
        {
            var relativePath = Path.Combine("managed", Path.GetFileName(sourceFile));
            var destinationFile = Path.Combine(stagingDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(sourceFile, destinationFile, overwrite: true);

            packageFileEntries.Add(new ManagedCorePackageFileEntry
            {
                Path = NormalizeManifestPath(relativePath),
                Sha256 = ComputeSha256(destinationFile)
            });
        }

        var manifestDocument = CreateManifestDocument(
            runtimeManifest,
            moduleTypeName,
            NormalizeManifestPath(entryAssemblyRelativePath),
            packageFileEntries);
        WriteManifestDocument(Path.Combine(stagingDirectory, ManifestFileName), manifestDocument);
        return manifestDocument;
    }

    private static InstalledManagedCorePackage? TryResolveInstalledPackage(
        string coresRootDirectory,
        ManagedCoreRegistryEntry entry)
    {
        try
        {
            var manifestPath = ResolveRegistryPath(coresRootDirectory, entry.ManifestPath);
            if (!File.Exists(manifestPath))
                return null;

            var manifestDocument = ReadManifestDocument(manifestPath);
            return BuildInstalledPackage(
                ResolveRegistryPath(coresRootDirectory, entry.InstallPath),
                manifestDocument,
                entry);
        }
        catch
        {
            return null;
        }
    }

    private static InstalledManagedCorePackage BuildInstalledPackage(
        string installDirectory,
        ManagedCorePackageManifestDocument manifestDocument,
        ManagedCoreRegistryEntry entry)
    {
        var systemIds = manifestDocument.Payload.SystemIds.Count == 0
            ? entry.SystemIds
            : manifestDocument.Payload.SystemIds;
        var primarySystemId = systemIds.FirstOrDefault() ?? "unknown";
        return new InstalledManagedCorePackage(
            new CoreManifest(
                manifestDocument.Payload.CoreId,
                manifestDocument.Payload.DisplayName,
                primarySystemId,
                manifestDocument.Payload.Version,
                manifestDocument.Payload.BinaryKind),
            systemIds,
            installDirectory,
            Path.Combine(installDirectory, ManifestFileName),
            Path.Combine(installDirectory, manifestDocument.Payload.EntryPoint.AssemblyPath),
            manifestDocument.Payload.EntryPoint.FactoryType,
            entry.IsBundled,
            entry.InstalledAtUtc);
    }

    private static ManagedCoreRegistryDocument ReadRegistryDocument(string resourceRootPath)
    {
        var registryPath = AppObjectStorage.GetCoreRegistryPath(resourceRootPath);
        if (!File.Exists(registryPath))
            return new ManagedCoreRegistryDocument();

        var document = JsonSerializer.Deserialize<ManagedCoreRegistryDocument>(File.ReadAllText(registryPath), JsonOptions);
        if (document is null)
            throw new InvalidOperationException("core-registry.fcr 内容为空。");
        if (!string.Equals(document.DocumentKind, ManagedCorePackageDocumentKinds.CoreRegistry, StringComparison.Ordinal))
            throw new InvalidOperationException($"core-registry.fcr documentKind 非法：{document.DocumentKind}");
        return document;
    }

    private static void WriteRegistryDocument(string resourceRootPath, ManagedCoreRegistryDocument document)
    {
        var registryPath = AppObjectStorage.GetCoreRegistryPath(resourceRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(registryPath)!);
        File.WriteAllText(registryPath, JsonSerializer.Serialize(document, JsonOptions));
    }

    private static ManagedCorePackageManifestDocument ReadManifestDocument(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("core-manifest.fcr was not found.", manifestPath);

        var document = JsonSerializer.Deserialize<ManagedCorePackageManifestDocument>(File.ReadAllText(manifestPath), JsonOptions);
        if (document is null)
            throw new InvalidOperationException("core-manifest.fcr 内容为空。");
        return document;
    }

    private static void WriteManifestDocument(string manifestPath, ManagedCorePackageManifestDocument document)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(document, JsonOptions));
    }

    private static void ValidateManifestDocument(ManagedCorePackageManifestDocument document, string rootDirectory)
    {
        if (!string.Equals(document.DocumentKind, ManagedCorePackageDocumentKinds.CoreManifest, StringComparison.Ordinal))
            throw new InvalidOperationException($"core-manifest.fcr documentKind 非法：{document.DocumentKind}");

        var payload = document.Payload ?? throw new InvalidOperationException("core-manifest.fcr 缺少 payload。");
        if (string.IsNullOrWhiteSpace(payload.CoreId))
            throw new InvalidOperationException("core-manifest.fcr 缺少 coreId。");
        if (string.IsNullOrWhiteSpace(payload.Version))
            throw new InvalidOperationException("core-manifest.fcr 缺少 version。");
        if (!string.Equals(payload.BinaryKind, CoreBinaryKinds.ManagedDotNet, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"当前仅支持 managed-dotnet 核心包，收到 {payload.BinaryKind}。");
        if (string.IsNullOrWhiteSpace(payload.EntryPoint?.AssemblyPath))
            throw new InvalidOperationException("core-manifest.fcr 缺少 entryPoint.assemblyPath。");
        if (string.IsNullOrWhiteSpace(payload.EntryPoint.FactoryType))
            throw new InvalidOperationException("core-manifest.fcr 缺少 entryPoint.factoryType。");

        var entryAssemblyPath = ResolveManifestPath(rootDirectory, payload.EntryPoint.AssemblyPath);
        if (!File.Exists(entryAssemblyPath))
            throw new FileNotFoundException("core-manifest.fcr 指向的入口程序集不存在。", entryAssemblyPath);

        foreach (var fileEntry in payload.Files)
        {
            if (string.IsNullOrWhiteSpace(fileEntry.Path))
                throw new InvalidOperationException("core-manifest.fcr files[].path 不能为空。");

            var filePath = ResolveManifestPath(rootDirectory, fileEntry.Path);
            if (!File.Exists(filePath))
                throw new FileNotFoundException("core-manifest.fcr files[] 中声明的文件不存在。", filePath);

            if (!string.IsNullOrWhiteSpace(fileEntry.Sha256))
            {
                var actualHash = ComputeSha256(filePath);
                if (!string.Equals(actualHash, fileEntry.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"核心文件哈希不匹配：{fileEntry.Path}");
            }
        }
    }

    private static void ExtractPackageToDirectory(string packagePath, string destinationDirectory)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        foreach (var entry in archive.Entries)
        {
            var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
            if (!destinationPath.StartsWith(destinationDirectory, GetPathComparison()))
                throw new InvalidOperationException("核心包包含非法路径。");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private static void CreatePackageFromDirectory(string sourceDirectory, string destinationPackagePath)
    {
        if (File.Exists(destinationPackagePath))
            File.Delete(destinationPackagePath);

        using var archive = ZipFile.Open(destinationPackagePath, ZipArchiveMode.Create);
        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = NormalizeManifestPath(Path.GetRelativePath(sourceDirectory, filePath));
            archive.CreateEntryFromFile(filePath, relativePath, CompressionLevel.Optimal);
        }
    }

    private static IEnumerable<string> EnumerateLoosePackageFiles(string assemblyPath)
    {
        var normalizedAssemblyPath = Path.GetFullPath(assemblyPath);
        var assemblyDirectory = Path.GetDirectoryName(normalizedAssemblyPath)
            ?? throw new InvalidOperationException("程序集目录不可用。");

        var localAssemblyPaths = Directory.EnumerateFiles(assemblyDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .ToDictionary(
                path => Path.GetFileNameWithoutExtension(path),
                path => path,
                StringComparer.OrdinalIgnoreCase);

        var resolvedPackageAssemblies = new HashSet<string>(GetPathComparer())
        {
            normalizedAssemblyPath
        };
        var queuedAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFileNameWithoutExtension(normalizedAssemblyPath)
        };
        var pendingAssemblyPaths = new Queue<string>();
        pendingAssemblyPaths.Enqueue(normalizedAssemblyPath);

        while (pendingAssemblyPaths.Count > 0)
        {
            var currentAssemblyPath = pendingAssemblyPaths.Dequeue();
            var assembly = TryResolveLoadedAssembly(currentAssemblyPath) ?? TryLoadAssembly(currentAssemblyPath);
            if (assembly is null)
                continue;

            foreach (var referencedAssembly in assembly.GetReferencedAssemblies())
            {
                if (!localAssemblyPaths.TryGetValue(referencedAssembly.Name ?? string.Empty, out var referencedAssemblyPath))
                    continue;
                if (ShouldTreatAsHostSharedAssembly(referencedAssembly.Name))
                    continue;
                if (!queuedAssemblyNames.Add(referencedAssembly.Name!))
                    continue;

                resolvedPackageAssemblies.Add(referencedAssemblyPath);
                pendingAssemblyPaths.Enqueue(referencedAssemblyPath);
            }
        }

        var filePaths = new HashSet<string>(GetPathComparer());
        foreach (var resolvedAssemblyPath in resolvedPackageAssemblies)
        {
            filePaths.Add(resolvedAssemblyPath);

            var pdbPath = Path.ChangeExtension(resolvedAssemblyPath, ".pdb");
            if (File.Exists(pdbPath))
                filePaths.Add(pdbPath);

            var depsPath = Path.ChangeExtension(resolvedAssemblyPath, ".deps.json");
            if (File.Exists(depsPath))
                filePaths.Add(depsPath);
        }

        return filePaths
            .OrderBy(path => path, GetPathComparer())
            .ToList();
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, directory)));

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }

    private static void CleanupOtherInstalledVersions(string resourceRootPath, string coreId, string currentVersion)
    {
        var coreRootDirectory = Path.Combine(
            AppObjectStorage.GetInstalledCoreRootDirectory(resourceRootPath),
            AppObjectStorage.SanitizeFileName(coreId));
        if (!Directory.Exists(coreRootDirectory))
            return;

        foreach (var versionDirectory in Directory.EnumerateDirectories(coreRootDirectory))
        {
            var versionName = Path.GetFileName(versionDirectory);
            if (string.Equals(versionName, AppObjectStorage.SanitizeFileName(currentVersion), StringComparison.OrdinalIgnoreCase))
                continue;

            Directory.Delete(versionDirectory, recursive: true);
        }
    }

    private static string ResolveManifestPath(string rootDirectory, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(rootDirectory);
        var fullPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));
        if (!fullPath.StartsWith(normalizedRoot, GetPathComparison()))
            throw new InvalidOperationException("core-manifest.fcr 包含越界文件路径。");
        return fullPath;
    }

    private static string ResolveRegistryPath(string coresRootDirectory, string relativePath)
    {
        var normalizedCoresRoot = Path.GetFullPath(coresRootDirectory);
        var fullPath = Path.GetFullPath(Path.Combine(normalizedCoresRoot, relativePath));
        if (!fullPath.StartsWith(normalizedCoresRoot, GetPathComparison()))
            throw new InvalidOperationException("core-registry.fcr 包含越界安装路径。");
        return fullPath;
    }

    private static string NormalizePackageOutputPath(string destinationPackagePath, CoreManifest manifest)
    {
        var normalizedDestinationPath = Path.GetFullPath(destinationPackagePath);
        return normalizedDestinationPath.EndsWith(PackageExtension, StringComparison.OrdinalIgnoreCase)
            ? normalizedDestinationPath
            : Path.ChangeExtension(normalizedDestinationPath, null) + PackageExtension;
    }

    private static string NormalizePackageOutputPath(string destinationPackagePath, ManagedCorePackageManifestPayload payload)
    {
        var normalizedDestinationPath = Path.GetFullPath(destinationPackagePath);
        return normalizedDestinationPath.EndsWith(PackageExtension, StringComparison.OrdinalIgnoreCase)
            ? normalizedDestinationPath
            : Path.Combine(
                Path.GetDirectoryName(normalizedDestinationPath) ?? AppContext.BaseDirectory,
                $"{AppObjectStorage.SanitizeFileName(payload.CoreId)}-{AppObjectStorage.SanitizeFileName(payload.Version)}{PackageExtension}");
    }

    private static string NormalizeManifestPath(string path) =>
        path.Replace('\\', '/');

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static StringComparer GetPathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static StringComparison GetPathComparison() => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static bool ShouldTreatAsHostSharedAssembly(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
            return false;

        return assemblyName.StartsWith("FC-Revolution.Emulation.", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("FC-Revolution.Storage", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("FC-Revolution.Contracts", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("FC-Revolution.Rendering", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("FC-Revolution.Backend", StringComparison.OrdinalIgnoreCase) ||
               assemblyName.StartsWith("FC-Revolution.UI", StringComparison.OrdinalIgnoreCase);
    }

    private static Assembly? TryResolveLoadedAssembly(string assemblyPath)
    {
        var comparer = GetPathComparer();
        var normalizedPath = Path.GetFullPath(assemblyPath);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
                continue;

            try
            {
                if (comparer.Equals(Path.GetFullPath(assembly.Location), normalizedPath))
                    return assembly;
            }
            catch
            {
            }
        }

        return null;
    }

    private static Assembly? TryLoadAssembly(string assemblyPath)
    {
        try
        {
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
        }
        catch (FileLoadException)
        {
            return TryResolveLoadedAssembly(assemblyPath);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }
}

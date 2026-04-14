using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace FC_Revolution.UI.Tests;

internal sealed class MainWindowViewModelTestHost : IDisposable
{
    private readonly string _taskMessageStoragePath;
    private readonly List<string> _temporaryFiles = [];

    internal MainWindowViewModelTestHost(bool ensureBundledCorePackages = true)
    {
        _taskMessageStoragePath = Path.Combine(
            Path.GetTempPath(),
            $"fc-task-message-tests-{Guid.NewGuid():N}",
            "task-messages.json");
        TaskMessageHub.ResetForTests(_taskMessageStoragePath);
        var profile = SystemConfigProfile.Load();
        FCRevolution.Storage.AppObjectStorage.ConfigureResourceRoot(profile.ResourceRootPath);
        if (ensureBundledCorePackages)
            NesManagedCoreTestBootstrap.EnsureInstalled(profile.ResourceRootPath);
        ViewModel = new MainWindowViewModel();
    }

    internal MainWindowViewModel ViewModel { get; }

    internal string CreateAndLoadTestRom()
    {
        var romPath = Path.Combine(Path.GetTempPath(), $"main-window-test-rom-{Guid.NewGuid():N}.nes");
        File.WriteAllBytes(romPath, CreateMinimalTestRom());
        _temporaryFiles.Add(romPath);
        InvokeLoadRom(romPath);
        return romPath;
    }

    internal string InvokeResolvePreviewPlaybackPath(string romPath)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "ResolvePreviewPlaybackPath",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<string>(method!.Invoke(ViewModel, [romPath]));
    }

    internal void InvokeSaveSystemConfig()
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "SaveSystemConfig",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(ViewModel, null);
    }

    internal void InvokeLoadRom(string romPath)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "LoadRom",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(ViewModel, [romPath]);
    }

    internal byte ReadByteField(string fieldName)
    {
        var field = typeof(MainWindowViewModel).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (byte)(field!.GetValue(ViewModel) ?? (byte)0);
    }

    internal byte ReadInputMask(string portId)
    {
        var field = typeof(MainWindowViewModel).GetField(
            "_inputMasksByPort",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var masksByPort = Assert.IsType<Dictionary<string, byte>>(field!.GetValue(ViewModel));
        return masksByPort.TryGetValue(portId, out var mask) ? mask : (byte)0;
    }

    internal string InvokeGetQuickSavePath()
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "GetQuickSavePath",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<string>(method!.Invoke(ViewModel, null));
    }

    internal void InvokeInstallManagedCoreFromPath(string sourcePath)
    {
        var method = ResolveManagedCoreHelperMethod(
            new[]
            {
                "InstallManagedCoreFromPath",
                "InstallManagedCoreFromPathAsync",
                "ImportManagedCoreFromPath",
                "InstallManagedCoreAssembly"
            },
            new[] { typeof(string) });
        Assert.NotNull(method);
        var result = method!.Invoke(ViewModel, new object?[] { sourcePath });
        AwaitTaskIfNecessary(result);
    }

    internal void InvokeUninstallManagedCore(string assemblyPath)
    {
        var method = ResolveManagedCoreHelperMethod(
            new[]
            {
                "UninstallManagedCore",
                "UninstallManagedCoreFromAssemblyPath",
                "UninstallManagedCoreFromPath",
                "RemoveManagedCore",
                "RemoveManagedCoreFromAssemblyPath"
            },
            new[] { typeof(string) });
        Assert.NotNull(method);
        var result = method!.Invoke(ViewModel, new object?[] { assemblyPath });
        AwaitTaskIfNecessary(result);
    }

    public void Dispose()
    {
        ViewModel.ExitCommand.Execute(null);

        foreach (var filePath in _temporaryFiles)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        var directory = Path.GetDirectoryName(_taskMessageStoragePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private static byte[] CreateMinimalTestRom()
    {
        var rom = new byte[16 + 16384 + 8192];
        rom[0] = (byte)'N';
        rom[1] = (byte)'E';
        rom[2] = (byte)'S';
        rom[3] = 0x1A;
        rom[4] = 1;
        rom[5] = 1;

        var prgStart = 16;
        rom[prgStart + 0x0000] = 0xEA;
        rom[prgStart + 0x0001] = 0x4C;
        rom[prgStart + 0x0002] = 0x00;
        rom[prgStart + 0x0003] = 0x80;

        rom[prgStart + 0x3FFA] = 0x00;
        rom[prgStart + 0x3FFB] = 0x80;
        rom[prgStart + 0x3FFC] = 0x00;
        rom[prgStart + 0x3FFD] = 0x80;
        rom[prgStart + 0x3FFE] = 0x00;
        rom[prgStart + 0x3FFF] = 0x80;

        return rom;
    }

    private static MethodInfo? ResolveManagedCoreHelperMethod(string[] candidateNames, Type[] parameterTypes)
    {
        var type = typeof(MainWindowViewModel);
        foreach (var name in candidateNames)
        {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic, binder: null, types: parameterTypes, modifiers: null);
            if (method is not null)
                return method;
        }

        return null;
    }

    private static void AwaitTaskIfNecessary(object? possiblyTask)
    {
        if (possiblyTask is Task task)
            task.GetAwaiter().GetResult();
    }
}

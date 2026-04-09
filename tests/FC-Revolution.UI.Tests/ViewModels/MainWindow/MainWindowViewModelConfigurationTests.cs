using Avalonia.Input;
using FCRevolution.Core.Input;
using FCRevolution.Core.Sample.Managed;
using FCRevolution.Core.Timeline.Persistence;
using FCRevolution.Emulation.Host;
using FCRevolution.Storage;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class MainWindowViewModelConfigurationTests
{
    [Fact]
    public void SystemConfigProfile_SaveLoad_PersistsDefaultCoreId_WhenPropertyExposed()
    {
        var defaultCoreIdProperty = ResolveDefaultCoreIdProperty(typeof(SystemConfigProfile));
        if (defaultCoreIdProperty is null)
            return;

        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-default-core-config-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);

            var profile = new SystemConfigProfile
            {
                ResourceRootPath = tempRoot
            };
            const string expectedCoreId = "fc.gb.managed";
            defaultCoreIdProperty.SetValue(profile, expectedCoreId);

            SystemConfigProfile.Save(profile);
            var loaded = SystemConfigProfile.Load();

            Assert.Equal(expectedCoreId, Assert.IsType<string>(defaultCoreIdProperty.GetValue(loaded)));
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void MainWindowViewModel_LoadsDefaultCoreId_FromSystemConfig_WhenPublicPropertyExposed()
    {
        var profileDefaultCoreIdProperty = ResolveDefaultCoreIdProperty(typeof(SystemConfigProfile));
        if (profileDefaultCoreIdProperty is null)
            return;

        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-main-window-default-core-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            var profile = new SystemConfigProfile
            {
                ResourceRootPath = tempRoot
            };
            var expectedCoreId = ResolveInstalledTestCoreId();
            profileDefaultCoreIdProperty.SetValue(profile, expectedCoreId);
            SystemConfigProfile.Save(profile);

            using var host = new MainWindowViewModelTestHost();
            var vm = host.ViewModel;
            var vmDefaultCoreIdProperty = ResolveDefaultCoreIdProperty(vm.GetType());
            if (vmDefaultCoreIdProperty is null)
                return;

            Assert.Equal(expectedCoreId, Assert.IsType<string>(vmDefaultCoreIdProperty.GetValue(vm)));
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void SaveSystemConfig_PersistsDefaultCoreId_WhenPublicPropertyExposed()
    {
        var profileDefaultCoreIdProperty = ResolveDefaultCoreIdProperty(typeof(SystemConfigProfile));
        if (profileDefaultCoreIdProperty is null)
            return;

        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-save-default-core-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot
            });

            using var host = new MainWindowViewModelTestHost();
            var vm = host.ViewModel;
            var vmDefaultCoreIdProperty = ResolveDefaultCoreIdProperty(vm.GetType());
            if (vmDefaultCoreIdProperty is null)
                return;

            var expectedCoreId = ResolveInstalledTestCoreId();
            vmDefaultCoreIdProperty.SetValue(vm, expectedCoreId);
            host.InvokeSaveSystemConfig();

            var profile = SystemConfigProfile.Load();
            Assert.Equal(expectedCoreId, Assert.IsType<string>(profileDefaultCoreIdProperty.GetValue(profile)));
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void MainWindowViewModel_ExposesInstalledCoreManifests_ForDefaultCoreSelection()
    {
        using var host = new MainWindowViewModelTestHost();
        var vm = host.ViewModel;

        Assert.NotEmpty(vm.InstalledCoreManifests);
        Assert.Contains(vm.InstalledCoreManifests, manifest => manifest.CoreId == vm.DefaultCoreId);
        Assert.False(string.IsNullOrWhiteSpace(vm.DefaultCoreDisplayName));
    }

    [Fact]
    public void MainWindowViewModel_ExposesSelectedDefaultCoreManifest_ForSettingsBinding()
    {
        using var host = new MainWindowViewModelTestHost();
        var vm = host.ViewModel;

        var selectedManifest = Assert.IsAssignableFrom<FCRevolution.Emulation.Abstractions.CoreManifest>(vm.SelectedDefaultCoreManifest);
        Assert.Equal(vm.DefaultCoreId, selectedManifest.CoreId);

        var manifestToSelect = vm.InstalledCoreManifests.Last();
        vm.SelectedDefaultCoreManifest = manifestToSelect;

        Assert.Equal(manifestToSelect.CoreId, vm.DefaultCoreId);
        Assert.Same(manifestToSelect, vm.SelectedDefaultCoreManifest);
        Assert.Contains(manifestToSelect.DisplayName, vm.DefaultCoreSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowViewModel_LoadsUnknownDefaultCoreId_AsInstalledFallback()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-invalid-default-core-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot,
                DefaultCoreId = "unknown.core"
            });

            using var host = new MainWindowViewModelTestHost();

            Assert.Equal(DefaultEmulatorCoreHost.DefaultCoreId, host.ViewModel.DefaultCoreId);
            Assert.Contains(host.ViewModel.InstalledCoreManifests, manifest => manifest.CoreId == host.ViewModel.DefaultCoreId);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void SystemConfigProfile_SaveLoad_PersistsManagedCoreProbePaths()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-managed-core-probes-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            var expectedPath = Path.Combine(tempRoot, "cores", "managed");

            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot,
                ManagedCoreProbePaths = [" ", expectedPath, expectedPath]
            });

            var profile = SystemConfigProfile.Load();

            Assert.Equal([Path.GetFullPath(expectedPath)], profile.ManagedCoreProbePaths);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void InstallingManagedCoreFromDll_RefreshesInstalledCoreManifests()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-managed-core-install-test-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot
            });
            using var host = new MainWindowViewModelTestHost();
            var vm = host.ViewModel;
            var sourcePath = CreateSampleManagedCorePackage(tempRoot, "sample-managed-core-import");

            host.InvokeInstallManagedCoreFromPath(sourcePath);

            Assert.Contains(vm.InstalledCoreManifests, manifest => string.Equals(manifest.CoreId, SampleManagedCoreModule.CoreId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void UninstallingManagedCore_RevertsDefaultCoreAndRemovesEntry()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-managed-core-uninstall-test-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot
            });
            using var host = new MainWindowViewModelTestHost();
            var vm = host.ViewModel;
            var sourcePath = CreateSampleManagedCorePackage(tempRoot, "sample-managed-core-uninstall");

            host.InvokeInstallManagedCoreFromPath(sourcePath);
            var importedManifest = vm.InstalledCoreManifests.First(manifest => string.Equals(manifest.CoreId, SampleManagedCoreModule.CoreId, StringComparison.OrdinalIgnoreCase));
            vm.SelectedDefaultCoreManifest = importedManifest;

            var installedDirectory = AppObjectStorage.GetInstalledCoreVersionDirectory(
                tempRoot,
                importedManifest.CoreId,
                importedManifest.Version);
            Assert.True(Directory.Exists(installedDirectory));

            vm.UninstallSelectedManagedCoreCommand.Execute(null);

            Assert.DoesNotContain(vm.InstalledCoreManifests, manifest => string.Equals(manifest.CoreId, SampleManagedCoreModule.CoreId, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(DefaultEmulatorCoreHost.DefaultCoreId, vm.DefaultCoreId);
            Assert.False(Directory.Exists(installedDirectory));
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void MainWindowViewModel_ExposesManagedCoreInstallHint_FromResourceRoot()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-managed-core-install-dir-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot
            });

            using var host = new MainWindowViewModelTestHost();
            var expectedDirectory = AppObjectStorage.GetInstalledCoreRootDirectory(tempRoot);

            Assert.Equal(expectedDirectory, host.ViewModel.ManagedCoreInstallDirectory);
            Assert.Contains(expectedDirectory, host.ViewModel.ManagedCoreInstallHint, StringComparison.Ordinal);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CoreSettingsDetails_ReflectSelectedManifest()
    {
        using var host = new MainWindowViewModelTestHost();
        var vm = host.ViewModel;

        var manifest = vm.SelectedDefaultCoreManifest;
        Assert.NotNull(manifest);
        Assert.Equal(manifest.SystemId, vm.SelectedDefaultCoreSystemId);
        Assert.Equal(manifest.Version, vm.SelectedDefaultCoreVersion);
        Assert.Equal(manifest.BinaryKind, vm.SelectedDefaultCoreBinaryKind);
        Assert.Contains(vm.DefaultCoreDisplayName, vm.InstalledCoreCatalogSummary, StringComparison.Ordinal);

        var entry = ResolveSelectedCatalogEntry(vm);
        Assert.NotNull(entry);
        Assert.Equal(entry.SourceLabel, vm.SelectedDefaultCoreSourceLabel);
        var expectedRemovability = entry.CanUninstall ? "可卸载" : "不可卸载";
        Assert.Equal(expectedRemovability, vm.SelectedDefaultCoreRemovabilityLabel);
        if (!string.IsNullOrWhiteSpace(entry.AssemblyPath))
        {
            Assert.Contains(entry.AssemblyPath, vm.SelectedDefaultCoreAssemblyPathDisplay, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CoreSettingsLabels_UpdateAfterInstallingSampleCore()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-managed-core-core-settings-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot
            });

            using var host = new MainWindowViewModelTestHost();
            var vm = host.ViewModel;
            var sourcePath = CreateSampleManagedCorePackage(tempRoot, "sample-managed-core-core-settings");

            host.InvokeInstallManagedCoreFromPath(sourcePath);
            var importedManifest = Assert.Single(
                vm.InstalledCoreManifests,
                manifest => manifest.CoreId == SampleManagedCoreModule.CoreId);
            vm.SelectedDefaultCoreManifest = importedManifest;

            var entry = ResolveSelectedCatalogEntry(vm);
            Assert.NotNull(entry);
            var installPath = AppObjectStorage.GetInstalledCoreVersionDirectory(
                tempRoot,
                importedManifest.CoreId,
                importedManifest.Version);

            Assert.Equal("已安装核心包", vm.SelectedDefaultCoreSourceLabel);
            Assert.Equal("可卸载", vm.SelectedDefaultCoreRemovabilityLabel);
            Assert.Contains(installPath, vm.SelectedDefaultCoreAssemblyPathDisplay, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(importedManifest.DisplayName, vm.InstalledCoreCatalogSummary, StringComparison.Ordinal);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static MainWindowManagedCoreCatalogEntry? ResolveSelectedCatalogEntry(MainWindowViewModel vm)
    {
        var field = typeof(MainWindowViewModel).GetField(
            "_managedCoreCatalogEntries",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var entries = field?.GetValue(vm) as IReadOnlyList<MainWindowManagedCoreCatalogEntry>;
        return entries?.FirstOrDefault(entry => string.Equals(entry.Manifest.CoreId, vm.DefaultCoreId, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MainWindowViewModel_ExposesEffectiveManagedCoreProbeDirectories()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-managed-core-effective-dirs-{Guid.NewGuid():N}");
        var extraProbePath = Path.Combine(tempRoot, "external-managed-cores");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot,
                ManagedCoreProbePaths = [extraProbePath]
            });

            using var host = new MainWindowViewModelTestHost();
            var expectedDirectories = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "cores", "managed"),
                AppObjectStorage.GetManagedCoreModulesDirectory(tempRoot),
                Path.GetFullPath(extraProbePath)
            };

            Assert.Equal(expectedDirectories, host.ViewModel.EffectiveManagedCoreProbeDirectories);
            foreach (var expectedDirectory in expectedDirectories)
                Assert.Contains(expectedDirectory, host.ViewModel.EffectiveManagedCoreProbeDirectoriesSummary, StringComparison.Ordinal);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ApplyManagedCoreProbePathsCommand_PersistsNormalizedPaths_AndRefreshesInstalledCoreManifests()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-managed-core-apply-{Guid.NewGuid():N}");
        var sampleCoreProbePath = Path.GetDirectoryName(typeof(SampleManagedCoreModule).Assembly.Location);
        Assert.False(string.IsNullOrWhiteSpace(sampleCoreProbePath));

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot
            });

            using var host = new MainWindowViewModelTestHost();
            host.ViewModel.ManagedCoreProbePathsInput = $" {sampleCoreProbePath} {Environment.NewLine}{sampleCoreProbePath}";

            host.ViewModel.ApplyManagedCoreProbePathsCommand.Execute(null);

            var profile = SystemConfigProfile.Load();
            var normalizedProbePath = Path.GetFullPath(sampleCoreProbePath!);

            Assert.Equal([normalizedProbePath], profile.ManagedCoreProbePaths);
            Assert.Equal(normalizedProbePath, host.ViewModel.ManagedCoreProbePathsInput);
            Assert.Contains(host.ViewModel.EffectiveManagedCoreProbeDirectories, path => string.Equals(path, normalizedProbePath, StringComparison.Ordinal));
            Assert.Contains(host.ViewModel.InstalledCoreManifests, manifest => manifest.CoreId == SampleManagedCoreModule.CoreId);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ReloadManagedCoreSourcesCommand_ReloadsPersistedProbePaths()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-managed-core-reload-{Guid.NewGuid():N}");
        var initialProbePath = Path.Combine(tempRoot, "initial-managed-cores");
        var reloadedProbePath = Path.Combine(tempRoot, "reloaded-managed-cores");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot,
                ManagedCoreProbePaths = [initialProbePath]
            });

            using var host = new MainWindowViewModelTestHost();
            host.ViewModel.ManagedCoreProbePathsInput = string.Empty;

            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot,
                ManagedCoreProbePaths = [reloadedProbePath]
            });

            host.ViewModel.ReloadManagedCoreSourcesCommand.Execute(null);

            var normalizedProbePath = Path.GetFullPath(reloadedProbePath);
            Assert.Equal(normalizedProbePath, host.ViewModel.ManagedCoreProbePathsInput);
            Assert.Contains(host.ViewModel.EffectiveManagedCoreProbeDirectories, path => string.Equals(path, normalizedProbePath, StringComparison.Ordinal));
            Assert.DoesNotContain(host.ViewModel.EffectiveManagedCoreProbeDirectories, path => string.Equals(path, Path.GetFullPath(initialProbePath), StringComparison.Ordinal));
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void SaveSystemConfig_PreservesDebugWindowDisplaySettings()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-system-config-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot,
                DebugWindowDisplaySettings = new DebugWindowDisplaySettingsProfile
                {
                    ShowRegisters = false,
                    ShowPpu = true,
                    ShowDisasm = false,
                    ShowStack = true,
                    ShowZeroPage = false,
                    ShowMemoryEditor = true,
                    ShowMemoryPage = false,
                    ShowModifiedMemory = true
                }
            });

            using var host = new MainWindowViewModelTestHost();
            host.InvokeSaveSystemConfig();

            var profile = SystemConfigProfile.Load();
            Assert.False(profile.DebugWindowDisplaySettings.ShowRegisters);
            Assert.True(profile.DebugWindowDisplaySettings.ShowPpu);
            Assert.False(profile.DebugWindowDisplaySettings.ShowDisasm);
            Assert.True(profile.DebugWindowDisplaySettings.ShowStack);
            Assert.False(profile.DebugWindowDisplaySettings.ShowZeroPage);
            Assert.True(profile.DebugWindowDisplaySettings.ShowMemoryEditor);
            Assert.False(profile.DebugWindowDisplaySettings.ShowMemoryPage);
            Assert.True(profile.DebugWindowDisplaySettings.ShowModifiedMemory);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void SaveSystemConfig_PersistsGlobalInputBindingFields()
    {
        var originalRoot = AppObjectStorage.GetResourceRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"fc-system-input-config-{Guid.NewGuid():N}");

        try
        {
            AppObjectStorage.ConfigureResourceRoot(tempRoot);
            SystemConfigProfile.Save(new SystemConfigProfile
            {
                ResourceRootPath = tempRoot
            });

            using var host = new MainWindowViewModelTestHost();
            var vm = host.ViewModel;

            var actionIdA = NesInputTestAdapter.ActionId(NesButton.A);
            var player1A = Assert.Single(vm.GlobalInputBindingsPlayer1, entry => entry.ActionId == actionIdA);
            Assert.True(player1A.TrySetSelectedKey(Key.Q));

            var extraBinding = ExtraInputBindingEntry.CreateDefaultTurbo(
                player: 0,
                key: Key.A,
                availableKeys: player1A.AvailableKeys);
            extraBinding.SetTurboHz(12);
            vm.GlobalExtraInputBindings.Add(extraBinding);

            var quickLoad = Assert.Single(vm.SharedGameShortcutBindings, entry => entry.Id == ShortcutCatalog.GameQuickLoad);
            quickLoad.ApplyGesture(new ShortcutGesture(Key.F7, KeyModifiers.Control));

            vm.MoveInputBindingLayoutSlot(actionIdA, 8, -6);
            host.InvokeSaveSystemConfig();

            var profile = SystemConfigProfile.Load();
            Assert.Equal(nameof(Key.Q), profile.PlayerInputOverrides["Player1"][actionIdA]);

            var extra = Assert.Single(profile.ExtraInputBindings);
            Assert.Equal(0, extra.Player);
            Assert.Equal(nameof(ExtraInputBindingKind.Turbo), extra.Kind);
            Assert.Equal(nameof(Key.A), extra.Key);
            Assert.Equal(12, extra.TurboHz);
            Assert.Equal(actionIdA, Assert.Single(extra.Buttons));

            Assert.Equal(nameof(Key.F7), profile.ShortcutBindings[ShortcutCatalog.GameQuickLoad].Key);
            Assert.Equal(nameof(KeyModifiers.Control), profile.ShortcutBindings[ShortcutCatalog.GameQuickLoad].Modifiers);

            var defaultLayout = InputBindingLayoutProfile.CreateDefault();
            Assert.Equal(defaultLayout.GetSlot(actionIdA).CenterX + 8, profile.InputBindingLayout.GetSlot(actionIdA).CenterX, precision: 6);
            Assert.Equal(defaultLayout.GetSlot(actionIdA).CenterY - 6, profile.InputBindingLayout.GetSlot(actionIdA).CenterY, precision: 6);
        }
        finally
        {
            AppObjectStorage.ConfigureResourceRoot(originalRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void TimelineModeSummary_ReflectsSelectedMode()
    {
        using var host = new MainWindowViewModelTestHost();
        var vm = host.ViewModel;

        vm.TimelineMode = TimelineModeOption.Disabled;
        Assert.Contains("仅保留快速存档/读档", vm.TimelineModeSummary);

        vm.TimelineMode = TimelineModeOption.ShortRewindOnly;
        vm.ShortRewindSecondsInput = "5";
        Assert.Contains("最近 5 秒", vm.TimelineModeSummary);

        vm.TimelineMode = TimelineModeOption.FullTimeline;
        Assert.Contains("完整时间线", vm.TimelineModeSummary);
    }

    [Fact]
    public void OpenBranchGalleryCommand_IsBlockedOutsideFullTimelineMode()
    {
        using var host = new MainWindowViewModelTestHost();
        var vm = host.ViewModel;

        vm.TimelineMode = TimelineModeOption.ShortRewindOnly;
        vm.OpenBranchGalleryCommand.Execute(null);
        Assert.Equal("当前时间线模式未启用完整分支画廊", vm.StatusText);

        vm.TimelineMode = TimelineModeOption.Disabled;
        vm.OpenBranchGalleryCommand.Execute(null);
        Assert.Equal("当前时间线模式未启用完整分支画廊", vm.StatusText);
    }

    [Fact]
    public void ShortRewindSeconds_IsClamped()
    {
        using var host = new MainWindowViewModelTestHost();
        var vm = host.ViewModel;

        vm.ShortRewindSecondsInput = "0";
        Assert.Equal(1, vm.ShortRewindSeconds);

        vm.ShortRewindSecondsInput = "99";
        Assert.Equal(30, vm.ShortRewindSeconds);
    }

    [Fact]
    public void TimelineModeCommands_UpdateModeAndSummary()
    {
        using var host = new MainWindowViewModelTestHost();
        var vm = host.ViewModel;

        vm.SetTimelineModeShortRewindCommand.Execute(null);
        vm.SetShortRewindSeconds10Command.Execute(null);
        Assert.Equal(TimelineModeOption.ShortRewindOnly, vm.TimelineMode);
        Assert.Contains("最近 10 秒", vm.TimelineModeSummary);

        vm.SetTimelineModeDisabledCommand.Execute(null);
        Assert.Equal(TimelineModeOption.Disabled, vm.TimelineMode);
        Assert.Contains("仅保留快速存档/读档", vm.TimelineModeSummary);

        vm.SetTimelineModeFullCommand.Execute(null);
        Assert.Equal(TimelineModeOption.FullTimeline, vm.TimelineMode);
    }

    [Fact]
    public void PreviewGenerationSettings_AreClampedAndSummarized()
    {
        using var host = new MainWindowViewModelTestHost();
        var vm = host.ViewModel;

        vm.PreviewGenerationSpeedMultiplier = 99;
        Assert.Equal(10, vm.PreviewGenerationSpeedMultiplier);
        Assert.Contains("600 fps", vm.PreviewGenerationSpeedSummary);

        vm.PreviewResolutionScale = 0.1;
        Assert.Equal(0.5, vm.PreviewResolutionScale);
        Assert.Contains("128x120", vm.PreviewResolutionScaleSummary);

        vm.PreviewResolutionScale = 1.0;
        Assert.Contains("256x240", vm.PreviewResolutionScaleSummary);
    }

    private static PropertyInfo? ResolveDefaultCoreIdProperty(Type ownerType)
    {
        var candidates = new[]
        {
            "DefaultCoreId",
            "DefaultEmulatorCoreId",
            "PreferredCoreId",
            "SelectedCoreId"
        };

        foreach (var candidate in candidates)
        {
            var property = ownerType.GetProperty(candidate, BindingFlags.Instance | BindingFlags.Public);
            if (property is not null
                && property.PropertyType == typeof(string)
                && property.CanRead
                && property.CanWrite)
            {
                return property;
            }
        }

        return null;
    }

    private static string ResolveInstalledTestCoreId()
    {
        var installedCoreId = DefaultEmulatorCoreHost.Create()
            .GetInstalledCoreManifests()
            .Select(manifest => manifest.CoreId)
            .FirstOrDefault(coreId => !string.Equals(coreId, DefaultEmulatorCoreHost.DefaultCoreId, StringComparison.OrdinalIgnoreCase));

        return installedCoreId ?? DefaultEmulatorCoreHost.DefaultCoreId;
    }

    private static string CreateSampleManagedCorePackage(string baseRoot, string name)
    {
        var tempDir = Path.Combine(baseRoot, "managed-core-packages");
        Directory.CreateDirectory(tempDir);
        var destinationPath = Path.Combine(tempDir, $"{name}.fcrcore.zip");
        var controller = new MainWindowManagedCoreExportController();
        var entry = new MainWindowManagedCoreCatalogEntry(
            new SampleManagedCoreModule().Manifest,
            typeof(SampleManagedCoreModule).Assembly.Location,
            typeof(SampleManagedCoreModule).FullName,
            "探测目录",
            CanUninstall: false,
            InstallDirectory: null,
            ManifestPath: null);
        controller.Export(entry, destinationPath);
        return destinationPath;
    }
}

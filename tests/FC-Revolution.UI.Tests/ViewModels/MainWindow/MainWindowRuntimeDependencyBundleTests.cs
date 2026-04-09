using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Input;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;

namespace FC_Revolution.UI.Tests;

public sealed class MainWindowRuntimeDependencyBundleTests
{
    [Fact]
    public void Create_BuildsLanRuntimeDependencies()
    {
        var romLibrary = new ObservableCollection<RomLibraryItem>();
        var romInputOverrides = new Dictionary<string, Dictionary<int, Dictionary<string, Key>>>(StringComparer.OrdinalIgnoreCase);
        var globalInputBindings = new ObservableCollection<InputBindingEntry>();
        var gameSessionService = new GameSessionService();

        var bundle = MainWindowRuntimeDependencyBundle.Create(
            romLibrary,
            romInputOverrides,
            globalInputBindings,
            gameSessionService,
            () => GameAspectRatioMode.Native,
            () => MacUpscaleMode.None,
            () => MacUpscaleOutputResolution.Hd1080,
            () => new Dictionary<string, ShortcutGesture>(StringComparer.Ordinal),
            () => { },
            (left, right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            _ => { },
            () => null);

        Assert.IsType<ArcadeRuntimeContractAdapter>(bundle.ArcadeRuntimeAdapter);
        Assert.IsType<LanArcadeService>(bundle.LanArcadeService);
        Assert.IsType<BackendStateMirror>(bundle.BackendStateMirror);
        Assert.False(bundle.BackendStateMirror.IsEnabled);
        Assert.True(bundle.IsLanArcadeServerReady);

        bundle.BackendStateMirror.Dispose();
        bundle.LanArcadeService.Dispose();
    }
}

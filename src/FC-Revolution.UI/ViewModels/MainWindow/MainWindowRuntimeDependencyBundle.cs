using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Input;
using FCRevolution.Rendering.Metal;
using FC_Revolution.UI.AppServices;
using FC_Revolution.UI.Infrastructure;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class MainWindowRuntimeDependencyBundle
{
    private MainWindowRuntimeDependencyBundle(
        IArcadeRuntimeContractAdapter arcadeRuntimeAdapter,
        ILanArcadeService lanArcadeService,
        IBackendStateMirror backendStateMirror,
        bool isLanArcadeServerReady)
    {
        ArcadeRuntimeAdapter = arcadeRuntimeAdapter;
        LanArcadeService = lanArcadeService;
        BackendStateMirror = backendStateMirror;
        IsLanArcadeServerReady = isLanArcadeServerReady;
    }

    public IArcadeRuntimeContractAdapter ArcadeRuntimeAdapter { get; }

    public ILanArcadeService LanArcadeService { get; }

    public IBackendStateMirror BackendStateMirror { get; }

    public bool IsLanArcadeServerReady { get; }

    public static MainWindowRuntimeDependencyBundle Create(
        IReadOnlyList<RomLibraryItem> romLibrary,
        IReadOnlyDictionary<string, Dictionary<int, Dictionary<string, Key>>> romInputOverrides,
        ObservableCollection<InputBindingEntry> globalInputBindings,
        IGameSessionService gameSessionService,
        CoreInputBindingSchema inputBindingSchema,
        Func<GameAspectRatioMode> getGameAspectRatioMode,
        Func<MacUpscaleMode> getMacUpscaleMode,
        Func<MacUpscaleOutputResolution> getMacUpscaleOutputResolution,
        Func<IReadOnlyDictionary<string, ShortcutGesture>> buildGameWindowShortcutMap,
        Action syncLoadedFlags,
        Func<string, string, bool> pathsEqual,
        Action<string> reportStatus,
        Func<IBackendStateSyncClient?> createBackendStateSyncClient)
    {
        var arcadeRuntimeAdapter = new ArcadeRuntimeContractAdapter(
            romLibrary,
            InputBindingContractAdapter.BuildActionBindingsByRomPath(romInputOverrides, inputBindingSchema),
            gameSessionService,
            () => InputBindingContractAdapter.BuildActionBindingsFromEntries(globalInputBindings, inputBindingSchema),
            getGameAspectRatioMode,
            getMacUpscaleMode,
            getMacUpscaleOutputResolution,
            buildGameWindowShortcutMap,
            syncLoadedFlags,
            pathsEqual,
            reportStatus);
        var lanArcadeService = new LanArcadeService(arcadeRuntimeAdapter);
        var backendStateMirror = new BackendStateMirror(
            arcadeRuntimeAdapter,
            createBackendStateSyncClient());
        return new MainWindowRuntimeDependencyBundle(
            arcadeRuntimeAdapter,
            lanArcadeService,
            backendStateMirror,
            isLanArcadeServerReady: true);
    }
}

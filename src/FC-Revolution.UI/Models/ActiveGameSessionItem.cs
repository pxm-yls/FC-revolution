using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.ViewModels;
using FC_Revolution.UI.Views;

namespace FC_Revolution.UI.Models;

public sealed partial class ActiveGameSessionItem : ObservableObject
{
    private IEmulatorCoreSession? _coreSession;

    public ActiveGameSessionItem(Guid sessionId, string displayName, string romPath, GameWindow window, GameWindowViewModel viewModel)
        : this(sessionId, displayName, romPath, coreId: null, coreDisplayName: null, coreSession: null, window, viewModel)
    {
    }

    public ActiveGameSessionItem(
        Guid sessionId,
        string displayName,
        string romPath,
        string? coreId,
        string? coreDisplayName,
        IEmulatorCoreSession? coreSession,
        GameWindow window,
        GameWindowViewModel viewModel)
    {
        SessionId = sessionId;
        DisplayName = displayName;
        RomPath = romPath;
        CoreId = coreId;
        CoreDisplayName = coreDisplayName;
        _coreSession = coreSession;
        Window = window;
        ViewModel = viewModel;
    }

    public Guid SessionId { get; }

    public string DisplayName { get; }

    public string RomPath { get; }

    public string? CoreId { get; }

    public string? CoreDisplayName { get; }

    public GameWindow Window { get; }

    public GameWindowViewModel ViewModel { get; }

    [ObservableProperty]
    private WriteableBitmap? _snapshotBitmap;

    public bool HasSnapshot => SnapshotBitmap != null;

    public string RemoteControlSummary => string.IsNullOrWhiteSpace(ViewModel.RemoteControlStatusText)
        ? "当前由本地控制"
        : ViewModel.RemoteControlStatusText;

    partial void OnSnapshotBitmapChanged(WriteableBitmap? value)
    {
        OnPropertyChanged(nameof(HasSnapshot));
    }

    public void RefreshRemoteControlSummary() => OnPropertyChanged(nameof(RemoteControlSummary));

    public void DisposeCoreSession()
    {
        _coreSession?.Dispose();
        _coreSession = null;
    }
}

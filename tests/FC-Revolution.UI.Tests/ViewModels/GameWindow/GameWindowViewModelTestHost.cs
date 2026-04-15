using Avalonia.Input;
using FCRevolution.Emulation.Abstractions;
using FC_Revolution.UI.Models;
using FC_Revolution.UI.ViewModels;
using System.Reflection;

namespace FC_Revolution.UI.Tests;

internal sealed class GameWindowViewModelTestHost : IDisposable
{
    internal GameWindowViewModelTestHost(
        IReadOnlyList<ExtraInputBindingProfile>? extraInputBindings = null,
        IEmulatorCoreSession? coreSession = null)
    {
        RomPath = CreateTestRomFile();
        ViewModel = AvaloniaThreadingTestHelper.RunOnUiThread(() => CreateViewModel(RomPath, extraInputBindings, coreSession));
    }

    internal string RomPath { get; }

    internal GameWindowViewModel ViewModel { get; }

    internal void InvokeUpdateDisplay(uint[] frame)
    {
        AvaloniaThreadingTestHelper.RunOnUiThread(() =>
        {
            var method = typeof(GameWindowViewModel).GetMethod("UpdateDisplay", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method!.Invoke(ViewModel, [frame]);
        });
    }

    internal void InvokeOnUiTick()
    {
        AvaloniaThreadingTestHelper.RunOnUiThread(() =>
        {
            var method = typeof(GameWindowViewModel).GetMethod("OnUiTick", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method!.Invoke(ViewModel, [null, EventArgs.Empty]);
        });
    }

    internal void InvokeHandleSessionFailure(string message, Exception? ex = null)
    {
        AvaloniaThreadingTestHelper.RunOnUiThread(() =>
        {
            var method = typeof(GameWindowViewModel).GetMethod("HandleSessionFailure", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method!.Invoke(ViewModel, [message, ex]);
        });
    }

    internal byte ReadCombinedInputMask(string portId) => ViewModel.GetCombinedInputMask(portId);

    internal static uint[] CreateSolidFrame(uint pixel)
    {
        var frame = new uint[256 * 240];
        Array.Fill(frame, pixel);
        return frame;
    }

    public void Dispose()
    {
        AvaloniaThreadingTestHelper.RunOnUiThread(ViewModel.Dispose);

        var quickSavePath = Path.ChangeExtension(RomPath, ".fcs");
        if (File.Exists(quickSavePath))
            File.Delete(quickSavePath);

        if (File.Exists(RomPath))
            File.Delete(RomPath);
    }

    private static GameWindowViewModel CreateViewModel(
        string romPath,
        IReadOnlyList<ExtraInputBindingProfile>? extraInputBindings = null,
        IEmulatorCoreSession? coreSession = null)
    {
        var inputBindingsByPort = new Dictionary<string, Dictionary<string, Key>>(StringComparer.OrdinalIgnoreCase)
        {
            ["p1"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["a"] = Key.Z,
                ["b"] = Key.X,
                ["start"] = Key.Enter,
                ["select"] = Key.RightShift,
                ["up"] = Key.Up,
                ["down"] = Key.Down,
                ["left"] = Key.Left,
                ["right"] = Key.Right,
            },
            ["p2"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["a"] = Key.U,
                ["b"] = Key.O,
                ["start"] = Key.RightCtrl,
                ["select"] = Key.Space,
                ["up"] = Key.I,
                ["down"] = Key.K,
                ["left"] = Key.J,
                ["right"] = Key.L,
            }
        };

        return coreSession == null
            ? new GameWindowViewModel("Test Game", romPath, GameAspectRatioMode.Native, inputBindingsByPort, extraInputBindings)
            : new GameWindowViewModel("Test Game", romPath, GameAspectRatioMode.Native, inputBindingsByPort, coreSession, extraInputBindings);
    }

    private static string CreateTestRomFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"game-window-test-rom-{Guid.NewGuid():N}.nes");
        File.WriteAllBytes(path, CreateMinimalTestRom());
        return path;
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
}

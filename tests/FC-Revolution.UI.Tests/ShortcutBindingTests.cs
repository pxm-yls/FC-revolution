using Avalonia.Input;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.Tests;

[Collection("Avalonia")]
public sealed class ShortcutBindingTests
{
    [Fact]
    public void ShortcutBindingEntry_ModifierCapture_WaitsForPrimaryKey()
    {
        var descriptor = ShortcutCatalog.ById[ShortcutCatalog.MainShowTaskMessages];
        var entry = new ShortcutBindingEntry(descriptor, descriptor.DefaultGesture);
        entry.BeginCapture();

        Assert.False(entry.TryBuildGesture(Key.LeftCtrl, KeyModifiers.Control, out var gesture));
        Assert.True(entry.IsCapturing);
        Assert.Equal("Ctrl+...", entry.SelectedGestureDisplay);
        Assert.Contains("Ctrl", entry.CaptureHintText);

        Assert.True(entry.TryBuildGesture(Key.F1, KeyModifiers.Control, out gesture));
        Assert.Equal(new ShortcutGesture(Key.F1, KeyModifiers.Control), gesture);
    }

    [Fact]
    public void ShortcutCatalog_ResolveGestureMap_FallsBackWhenConfiguredShortcutsConflict()
    {
        var gestures = ShortcutCatalog.ResolveGestureMap(new Dictionary<string, ShortcutBindingProfile>
        {
            [ShortcutCatalog.MainShowTaskMessages] = new()
            {
                Key = nameof(Key.F10),
                Modifiers = nameof(KeyModifiers.None)
            },
            [ShortcutCatalog.GameQuickSave] = new()
            {
                Key = nameof(Key.F10),
                Modifiers = nameof(KeyModifiers.None)
            }
        });

        Assert.Equal(new ShortcutGesture(Key.F10, KeyModifiers.None), gestures[ShortcutCatalog.MainShowTaskMessages]);
        Assert.Equal(
            ShortcutCatalog.ById[ShortcutCatalog.GameQuickSave].DefaultGesture,
            gestures[ShortcutCatalog.GameQuickSave]);
    }
}

using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using FC_Revolution.UI.Models;

namespace FC_Revolution.UI.ViewModels;

internal sealed class GameWindowShortcutRouter
{
    private Dictionary<string, ShortcutGesture> _shortcutBindings = ShortcutCatalog.BuildDefaultGestureMap();

    public void ApplyShortcutBindings(IReadOnlyDictionary<string, ShortcutGesture> shortcutBindings)
    {
        _shortcutBindings = ShortcutCatalog.BuildDefaultGestureMap();
        foreach (var id in ShortcutCatalog.GameWindowShortcutIds)
        {
            if (shortcutBindings.TryGetValue(id, out var gesture))
                _shortcutBindings[id] = gesture;
        }
    }

    public bool ShouldHandleKey(Key key, KeyModifiers modifiers) =>
        ShortcutCatalog.GameWindowShortcutIds.Any(id => IsShortcutRoutingMatch(id, key, modifiers));

    public bool IsShortcutMatch(string shortcutId, Key key, KeyModifiers modifiers) =>
        _shortcutBindings.TryGetValue(shortcutId, out var gesture) && gesture.Matches(key, modifiers);

    public string BuildShortcutHintText() =>
        $"存档 {GetShortcutDisplay(ShortcutCatalog.GameQuickSave)} 读档 {GetShortcutDisplay(ShortcutCatalog.GameQuickLoad)} " +
        $"时间线 {GetShortcutDisplay(ShortcutCatalog.GameToggleBranchGallery)} 暂停 {GetShortcutDisplay(ShortcutCatalog.GameTogglePause)} " +
        $"回溯 {GetShortcutDisplay(ShortcutCatalog.GameRewind)} 信息 {GetShortcutDisplay(ShortcutCatalog.GameToggleInfoOverlay)}";

    private bool IsShortcutRoutingMatch(string shortcutId, Key key, KeyModifiers modifiers)
    {
        if (!_shortcutBindings.TryGetValue(shortcutId, out var gesture))
            return false;

        if (gesture.Key != key)
            return false;

        var normalizedModifiers = ShortcutGesture.NormalizeModifiers(modifiers);
        return gesture.Modifiers == KeyModifiers.None || gesture.Matches(key, normalizedModifiers);
    }

    private string GetShortcutDisplay(string shortcutId) =>
        _shortcutBindings.TryGetValue(shortcutId, out var gesture)
            ? gesture.ToDisplayString()
            : ShortcutCatalog.ById[shortcutId].DefaultGesture.ToDisplayString();
}

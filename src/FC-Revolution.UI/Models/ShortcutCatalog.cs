using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;

namespace FC_Revolution.UI.Models;

public sealed record ShortcutDescriptor(
    string Id,
    string ScopeLabel,
    string ActionName,
    string Description,
    ShortcutGesture DefaultGesture);

public static class ShortcutCatalog
{
    public const string MainShowTaskMessages = "Main.ShowTaskMessages";
    public const string MainToggleMemoryDiagnostics = "Main.ToggleMemoryDiagnostics";
    public const string GameToggleInfoOverlay = "Game.ToggleInfoOverlay";
    public const string GameQuickSave = "Game.QuickSave";
    public const string GameQuickLoad = "Game.QuickLoad";
    public const string GameToggleBranchGallery = "Game.ToggleBranchGallery";
    public const string GameTogglePause = "Game.TogglePause";
    public const string GameRewind = "Game.Rewind";
    public const string GameOpenDebugWindow = "Game.OpenDebugWindow";

    public static readonly IReadOnlyList<ShortcutDescriptor> MainWindowShortcuts =
    [
        new(
            MainShowTaskMessages,
            "主程序",
            "消息提醒面板",
            "打开主程序右侧的消息提醒面板。",
            new ShortcutGesture(Key.F1, KeyModifiers.Control)),
        new(
            MainToggleMemoryDiagnostics,
            "主程序",
            "内存诊断窗口",
            "打开或关闭主程序内存诊断窗口。",
            new ShortcutGesture(Key.F11, KeyModifiers.None))
    ];

    public static readonly IReadOnlyList<ShortcutDescriptor> SharedGameShortcuts =
    [
        new(
            GameQuickSave,
            "主程序 + 游戏窗口",
            "快速存档",
            "立即保存当前游戏进度。",
            new ShortcutGesture(Key.F2, KeyModifiers.None)),
        new(
            GameQuickLoad,
            "主程序 + 游戏窗口",
            "快速读档",
            "读取当前游戏的快速存档。",
            new ShortcutGesture(Key.F3, KeyModifiers.None)),
        new(
            GameToggleBranchGallery,
            "主程序 + 游戏窗口",
            "世界线视图",
            "打开或关闭时间线 / 分支视图。",
            new ShortcutGesture(Key.F4, KeyModifiers.None)),
        new(
            GameTogglePause,
            "主程序 + 游戏窗口",
            "暂停 / 继续",
            "暂停或恢复当前游戏。",
            new ShortcutGesture(Key.F5, KeyModifiers.None)),
        new(
            GameRewind,
            "主程序 + 游戏窗口",
            "短回溯",
            "按当前回溯时长设置回放最近片段。",
            new ShortcutGesture(Key.F7, KeyModifiers.None))
    ];

    public static readonly IReadOnlyList<ShortcutDescriptor> GameWindowOnlyShortcuts =
    [
        new(
            GameToggleInfoOverlay,
            "仅游戏窗口",
            "信息栏",
            "显示或隐藏游戏窗口信息栏。",
            new ShortcutGesture(Key.F1, KeyModifiers.None)),
        new(
            GameOpenDebugWindow,
            "仅游戏窗口",
            "调试窗口",
            "打开游戏调试窗口。",
            new ShortcutGesture(Key.F12, KeyModifiers.None))
    ];

    public static readonly IReadOnlyList<ShortcutDescriptor> AllShortcuts =
        MainWindowShortcuts
            .Concat(SharedGameShortcuts)
            .Concat(GameWindowOnlyShortcuts)
            .ToArray();

    public static readonly IReadOnlyList<string> SharedGameShortcutIds =
        SharedGameShortcuts.Select(descriptor => descriptor.Id).ToArray();

    public static readonly IReadOnlyList<string> GameWindowOnlyShortcutIds =
        GameWindowOnlyShortcuts.Select(descriptor => descriptor.Id).ToArray();

    public static readonly IReadOnlyList<string> GameWindowShortcutIds =
        SharedGameShortcutIds.Concat(GameWindowOnlyShortcutIds).ToArray();

    public static readonly IReadOnlyDictionary<string, ShortcutDescriptor> ById =
        new ReadOnlyDictionary<string, ShortcutDescriptor>(
            AllShortcuts.ToDictionary(descriptor => descriptor.Id, StringComparer.Ordinal));

    public static Dictionary<string, ShortcutGesture> BuildDefaultGestureMap() =>
        AllShortcuts.ToDictionary(
            descriptor => descriptor.Id,
            descriptor => descriptor.DefaultGesture,
            StringComparer.Ordinal);

    public static Dictionary<string, ShortcutGesture> ResolveGestureMap(
        IReadOnlyDictionary<string, ShortcutBindingProfile>? configuredProfiles)
    {
        var resolved = new Dictionary<string, ShortcutGesture>(StringComparer.Ordinal);
        var usedGestures = new HashSet<ShortcutGesture>();

        foreach (var descriptor in AllShortcuts)
        {
            var gesture = descriptor.DefaultGesture;
            if (configuredProfiles != null &&
                configuredProfiles.TryGetValue(descriptor.Id, out var profile) &&
                ShortcutGesture.TryParse(profile.Key, profile.Modifiers, out var configuredGesture) &&
                configuredGesture.IsComplete)
            {
                gesture = configuredGesture;
            }

            if (usedGestures.Contains(gesture))
                gesture = descriptor.DefaultGesture;

            usedGestures.Add(gesture);
            resolved[descriptor.Id] = gesture;
        }

        return resolved;
    }
}

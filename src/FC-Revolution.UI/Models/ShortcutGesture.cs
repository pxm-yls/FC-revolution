using System;
using System.Collections.Generic;
using Avalonia.Input;

namespace FC_Revolution.UI.Models;

public readonly record struct ShortcutGesture(Key Key, KeyModifiers Modifiers)
{
    private const KeyModifiers SupportedModifiers =
        KeyModifiers.Control |
        KeyModifiers.Shift |
        KeyModifiers.Alt |
        KeyModifiers.Meta;

    public static readonly ShortcutGesture Empty = new(Key.None, KeyModifiers.None);

    public bool IsEmpty => Key == Key.None && NormalizeModifiers(Modifiers) == KeyModifiers.None;

    public bool IsComplete => Key != Key.None && !IsModifierKey(Key);

    public bool Matches(Key key, KeyModifiers modifiers) =>
        IsComplete &&
        key == Key &&
        NormalizeModifiers(modifiers) == NormalizeModifiers(Modifiers);

    public ShortcutBindingProfile ToProfile() => new()
    {
        Key = Key.ToString(),
        Modifiers = NormalizeModifiers(Modifiers).ToString()
    };

    public string ToDisplayString()
    {
        List<string> parts = [];
        var normalizedModifiers = NormalizeModifiers(Modifiers);
        if (normalizedModifiers.HasFlag(KeyModifiers.Control))
            parts.Add("Ctrl");
        if (normalizedModifiers.HasFlag(KeyModifiers.Shift))
            parts.Add("Shift");
        if (normalizedModifiers.HasFlag(KeyModifiers.Alt))
            parts.Add("Alt");
        if (normalizedModifiers.HasFlag(KeyModifiers.Meta))
            parts.Add("Command");

        if (Key == Key.None)
            return parts.Count == 0 ? "未设置" : string.Join("+", parts) + "+...";

        parts.Add(FormatKey(Key));
        return string.Join("+", parts);
    }

    public static ShortcutGesture CreateCapturePreview(Key key, KeyModifiers modifiers)
    {
        var normalizedModifiers = NormalizeModifiers(modifiers) | GetModifierFlag(key);
        return IsModifierKey(key)
            ? new ShortcutGesture(Key.None, normalizedModifiers)
            : new ShortcutGesture(key, normalizedModifiers);
    }

    public static KeyModifiers NormalizeModifiers(KeyModifiers modifiers) => modifiers & SupportedModifiers;

    public static KeyModifiers GetModifierFlag(Key key) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl => KeyModifiers.Control,
        Key.LeftShift or Key.RightShift => KeyModifiers.Shift,
        Key.LeftAlt or Key.RightAlt => KeyModifiers.Alt,
        Key.LWin or Key.RWin => KeyModifiers.Meta,
        _ => KeyModifiers.None
    };

    public static bool IsModifierKey(Key key) => GetModifierFlag(key) != KeyModifiers.None;

    public static bool TryParse(string? keyText, string? modifiersText, out ShortcutGesture gesture)
    {
        gesture = Empty;
        if (!Enum.TryParse<Key>(keyText, out var key) || key == Key.None || IsModifierKey(key))
            return false;

        var modifiers = KeyModifiers.None;
        if (!string.IsNullOrWhiteSpace(modifiersText) &&
            !Enum.TryParse<KeyModifiers>(modifiersText, out modifiers))
        {
            modifiers = KeyModifiers.None;
        }

        gesture = new ShortcutGesture(key, NormalizeModifiers(modifiers));
        return true;
    }

    public static bool IsFunctionKey(Key key) => key >= Key.F1 && key <= Key.F24;

    internal static string FormatKey(Key key) => key switch
    {
        Key.LeftCtrl => "LCtrl",
        Key.RightCtrl => "RCtrl",
        Key.LeftShift => "LShift",
        Key.RightShift => "RShift",
        Key.LeftAlt => "LAlt",
        Key.RightAlt => "RAlt",
        Key.LWin => "LCmd",
        Key.RWin => "RCmd",
        Key.Return => "Enter",
        Key.Back => "Back",
        Key.PageUp => "PgUp",
        Key.PageDown => "PgDn",
        Key.CapsLock => "Caps",
        Key.NumPad0 => "N0",
        Key.NumPad1 => "N1",
        Key.NumPad2 => "N2",
        Key.NumPad3 => "N3",
        Key.NumPad4 => "N4",
        Key.NumPad5 => "N5",
        Key.NumPad6 => "N6",
        Key.NumPad7 => "N7",
        Key.NumPad8 => "N8",
        Key.NumPad9 => "N9",
        Key.D0 => "0",
        Key.D1 => "1",
        Key.D2 => "2",
        Key.D3 => "3",
        Key.D4 => "4",
        Key.D5 => "5",
        Key.D6 => "6",
        Key.D7 => "7",
        Key.D8 => "8",
        Key.D9 => "9",
        Key.OemPlus => "+",
        Key.OemMinus => "-",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.Oem2 => "/",
        Key.Oem3 => "`",
        Key.Oem4 => "[",
        Key.Oem5 => "\\",
        Key.Oem6 => "]",
        Key.Oem7 => "'",
        Key.Oem1 => ";",
        _ => key.ToString()
    };
}

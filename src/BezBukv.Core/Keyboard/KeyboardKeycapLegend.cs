using System.Globalization;
using BezBukv.Core.Platform;

namespace BezBukv.Core.Keyboard;

/// <summary>
/// Provides the platform-independent legend printed on PC US ANSI keycaps.
/// </summary>
public static class KeyboardKeycapLegend
{
    /// <summary>
    /// Gets a fixed label for a non-printable named key, or an empty string for a printable key.
    /// </summary>
    public static string GetNamedLabel(PhysicalKey key) =>
        IsNamedKey(key) ? GetLatinEngraving(key) : string.Empty;

    /// <summary>
    /// Gets the US ANSI engraving or a short keycap label for a physical key.
    /// </summary>
    public static string GetLatinEngraving(PhysicalKey key) =>
        key switch
        {
            PhysicalKey.Unknown => string.Empty,
            PhysicalKey.Escape => "Esc",
            >= PhysicalKey.F1 and <= PhysicalKey.F24 =>
                "F" + (key - PhysicalKey.F1 + 1).ToString(CultureInfo.InvariantCulture),
            PhysicalKey.PrintScreen => "Print Screen",
            PhysicalKey.ScrollLock => "Scroll Lock",
            PhysicalKey.Pause => "Pause",
            PhysicalKey.Backquote => "`",
            PhysicalKey.Digit1 => "1",
            PhysicalKey.Digit2 => "2",
            PhysicalKey.Digit3 => "3",
            PhysicalKey.Digit4 => "4",
            PhysicalKey.Digit5 => "5",
            PhysicalKey.Digit6 => "6",
            PhysicalKey.Digit7 => "7",
            PhysicalKey.Digit8 => "8",
            PhysicalKey.Digit9 => "9",
            PhysicalKey.Digit0 => "0",
            PhysicalKey.Minus => "-",
            PhysicalKey.Equal => "=",
            PhysicalKey.Backspace => "⌫",
            PhysicalKey.Tab => "Tab",
            PhysicalKey.KeyQ => "Q",
            PhysicalKey.KeyW => "W",
            PhysicalKey.KeyE => "E",
            PhysicalKey.KeyR => "R",
            PhysicalKey.KeyT => "T",
            PhysicalKey.KeyY => "Y",
            PhysicalKey.KeyU => "U",
            PhysicalKey.KeyI => "I",
            PhysicalKey.KeyO => "O",
            PhysicalKey.KeyP => "P",
            PhysicalKey.BracketLeft => "[",
            PhysicalKey.BracketRight => "]",
            PhysicalKey.Backslash => "\\",
            PhysicalKey.CapsLock => "Caps",
            PhysicalKey.KeyA => "A",
            PhysicalKey.KeyS => "S",
            PhysicalKey.KeyD => "D",
            PhysicalKey.KeyF => "F",
            PhysicalKey.KeyG => "G",
            PhysicalKey.KeyH => "H",
            PhysicalKey.KeyJ => "J",
            PhysicalKey.KeyK => "K",
            PhysicalKey.KeyL => "L",
            PhysicalKey.Semicolon => ";",
            PhysicalKey.Quote => "'",
            PhysicalKey.Enter => "Enter",
            PhysicalKey.ShiftLeft or PhysicalKey.ShiftRight => "Shift",
            PhysicalKey.KeyZ => "Z",
            PhysicalKey.KeyX => "X",
            PhysicalKey.KeyC => "C",
            PhysicalKey.KeyV => "V",
            PhysicalKey.KeyB => "B",
            PhysicalKey.KeyN => "N",
            PhysicalKey.KeyM => "M",
            PhysicalKey.Comma => ",",
            PhysicalKey.Period => ".",
            PhysicalKey.Slash => "/",
            PhysicalKey.ControlLeft or PhysicalKey.ControlRight => "Ctrl",
            PhysicalKey.MetaLeft or PhysicalKey.MetaRight => "Win",
            PhysicalKey.AltLeft or PhysicalKey.AltRight => "Alt",
            PhysicalKey.Space => "Space",
            PhysicalKey.ContextMenu => "Menu",
            PhysicalKey.Insert => "Insert",
            PhysicalKey.Home => "Home",
            PhysicalKey.PageUp => "Page Up",
            PhysicalKey.Delete => "Delete",
            PhysicalKey.End => "End",
            PhysicalKey.PageDown => "Page Down",
            PhysicalKey.ArrowRight => "Right",
            PhysicalKey.ArrowLeft => "Left",
            PhysicalKey.ArrowDown => "Down",
            PhysicalKey.ArrowUp => "Up",
            PhysicalKey.NumLock => "Num Lock",
            PhysicalKey.NumpadDivide => "/",
            PhysicalKey.NumpadMultiply => "*",
            PhysicalKey.NumpadSubtract => "-",
            PhysicalKey.Numpad7 => "7",
            PhysicalKey.Numpad8 => "8",
            PhysicalKey.Numpad9 => "9",
            PhysicalKey.NumpadAdd => "+",
            PhysicalKey.Numpad4 => "4",
            PhysicalKey.Numpad5 => "5",
            PhysicalKey.Numpad6 => "6",
            PhysicalKey.Numpad1 => "1",
            PhysicalKey.Numpad2 => "2",
            PhysicalKey.Numpad3 => "3",
            PhysicalKey.NumpadEnter => "Enter",
            PhysicalKey.Numpad0 => "0",
            PhysicalKey.NumpadDecimal => ".",
            _ => string.Empty,
        };

    /// <summary>
    /// Gets the US ANSI engraving produced while Shift is held, or an empty string when the
    /// physical key has no shifted symbol.
    /// </summary>
    public static string GetLatinShiftEngraving(PhysicalKey key) =>
        key switch
        {
            PhysicalKey.Backquote => "~",
            PhysicalKey.Digit1 => "!",
            PhysicalKey.Digit2 => "@",
            PhysicalKey.Digit3 => "#",
            PhysicalKey.Digit4 => "$",
            PhysicalKey.Digit5 => "%",
            PhysicalKey.Digit6 => "^",
            PhysicalKey.Digit7 => "&",
            PhysicalKey.Digit8 => "*",
            PhysicalKey.Digit9 => "(",
            PhysicalKey.Digit0 => ")",
            PhysicalKey.Minus => "_",
            PhysicalKey.Equal => "+",
            >= PhysicalKey.KeyQ and <= PhysicalKey.KeyP => GetLatinEngraving(key),
            PhysicalKey.BracketLeft => "{",
            PhysicalKey.BracketRight => "}",
            PhysicalKey.Backslash => "|",
            >= PhysicalKey.KeyA and <= PhysicalKey.KeyL => GetLatinEngraving(key),
            PhysicalKey.Semicolon => ":",
            PhysicalKey.Quote => "\"",
            >= PhysicalKey.KeyZ and <= PhysicalKey.KeyM => GetLatinEngraving(key),
            PhysicalKey.Comma => "<",
            PhysicalKey.Period => ">",
            PhysicalKey.Slash => "?",
            _ => string.Empty,
        };

    private static bool IsNamedKey(PhysicalKey key) =>
        key is PhysicalKey.Escape or
            >= PhysicalKey.F1 and <= PhysicalKey.F24 or
            >= PhysicalKey.PrintScreen and <= PhysicalKey.Pause or
            PhysicalKey.Backspace or
            PhysicalKey.Tab or
            PhysicalKey.CapsLock or
            PhysicalKey.Enter or
            PhysicalKey.ShiftLeft or
            PhysicalKey.ShiftRight or
            >= PhysicalKey.ControlLeft and <= PhysicalKey.ControlRight or
            >= PhysicalKey.Insert and <= PhysicalKey.NumpadDecimal;
}

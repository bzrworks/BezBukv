using BezBukv.Core.Keyboard;
using BezBukv.Core.Platform;
using BezBukv.Core.Settings;

namespace BezBukv.Core.Scenarios;

public static class KeyboardPeekBlockingPolicy
{
    public static bool ShouldShowOverlay(
        KeyboardScanCodeIdentity centerIdentity,
        CustomKeyboardLayout? customKeyboardLayout)
    {
        if (customKeyboardLayout is null)
        {
            return false;
        }

        foreach (var row in customKeyboardLayout.Rows)
        {
            foreach (var cell in row.Cells)
            {
                if (cell.Key is { } key
                    && key.ScanCode == centerIdentity.ScanCode
                    && key.Extended == centerIdentity.Extended)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool ShouldShowOverlay(
        PhysicalKey key,
        bool blockNumberRow,
        bool showServiceKeys) =>
        key != PhysicalKey.Escape &&
        (IsLetterOrSymbol(key) ||
            (blockNumberRow && IsNumberRow(key)) ||
            (showServiceKeys && IsServiceKey(key)));

    public static bool ShouldBlock(
        PhysicalKey key,
        bool blockNumberRow,
        bool showServiceKeys) =>
        key != PhysicalKey.Escape;

    public static bool ShouldBlock(
        PhysicalKey key,
        KeyboardKeyState state,
        IReadOnlySet<PhysicalKey> activationChordKeys,
        bool blockNumberRow,
        bool showServiceKeys) =>
        !(state == KeyboardKeyState.Up &&
            IsModifier(key) &&
            activationChordKeys.Contains(key)) &&
        ShouldBlock(key, blockNumberRow, showServiceKeys);

    private static bool IsModifier(PhysicalKey key) =>
        key is PhysicalKey.ControlLeft or
            PhysicalKey.ControlRight or
            PhysicalKey.AltLeft or
            PhysicalKey.AltRight or
            PhysicalKey.ShiftLeft or
            PhysicalKey.ShiftRight or
            PhysicalKey.MetaLeft or
            PhysicalKey.MetaRight;

    private static bool IsLetterOrSymbol(PhysicalKey key) =>
        key is PhysicalKey.KeyA or
            PhysicalKey.KeyB or
            PhysicalKey.KeyC or
            PhysicalKey.KeyD or
            PhysicalKey.KeyE or
            PhysicalKey.KeyF or
            PhysicalKey.KeyG or
            PhysicalKey.KeyH or
            PhysicalKey.KeyI or
            PhysicalKey.KeyJ or
            PhysicalKey.KeyK or
            PhysicalKey.KeyL or
            PhysicalKey.KeyM or
            PhysicalKey.KeyN or
            PhysicalKey.KeyO or
            PhysicalKey.KeyP or
            PhysicalKey.KeyQ or
            PhysicalKey.KeyR or
            PhysicalKey.KeyS or
            PhysicalKey.KeyT or
            PhysicalKey.KeyU or
            PhysicalKey.KeyV or
            PhysicalKey.KeyW or
            PhysicalKey.KeyX or
            PhysicalKey.KeyY or
            PhysicalKey.KeyZ or
            PhysicalKey.BracketLeft or
            PhysicalKey.BracketRight or
            PhysicalKey.Backslash or
            PhysicalKey.Semicolon or
            PhysicalKey.Quote or
            PhysicalKey.Comma or
            PhysicalKey.Period or
            PhysicalKey.Slash or
            PhysicalKey.Backquote;

    private static bool IsNumberRow(PhysicalKey key) =>
        key is PhysicalKey.Digit0 or
            PhysicalKey.Digit1 or
            PhysicalKey.Digit2 or
            PhysicalKey.Digit3 or
            PhysicalKey.Digit4 or
            PhysicalKey.Digit5 or
            PhysicalKey.Digit6 or
            PhysicalKey.Digit7 or
            PhysicalKey.Digit8 or
            PhysicalKey.Digit9 or
            PhysicalKey.Minus or
            PhysicalKey.Equal;

    private static bool IsServiceKey(PhysicalKey key) =>
        key is PhysicalKey.F1 or
            PhysicalKey.F2 or
            PhysicalKey.F3 or
            PhysicalKey.F4 or
            PhysicalKey.F5 or
            PhysicalKey.F6 or
            PhysicalKey.F7 or
            PhysicalKey.F8 or
            PhysicalKey.F9 or
            PhysicalKey.F10 or
            PhysicalKey.F11 or
            PhysicalKey.F12 or
            PhysicalKey.F13 or
            PhysicalKey.F14 or
            PhysicalKey.F15 or
            PhysicalKey.F16 or
            PhysicalKey.F17 or
            PhysicalKey.F18 or
            PhysicalKey.F19 or
            PhysicalKey.F20 or
            PhysicalKey.F21 or
            PhysicalKey.F22 or
            PhysicalKey.F23 or
            PhysicalKey.F24 or
            PhysicalKey.PrintScreen or
            PhysicalKey.ScrollLock or
            PhysicalKey.Pause or
            PhysicalKey.Backspace or
            PhysicalKey.Tab or
            PhysicalKey.CapsLock or
            PhysicalKey.Enter or
            PhysicalKey.Space or
            PhysicalKey.ShiftLeft or
            PhysicalKey.ShiftRight or
            PhysicalKey.ControlLeft or
            PhysicalKey.ControlRight or
            PhysicalKey.MetaLeft or
            PhysicalKey.MetaRight or
            PhysicalKey.AltLeft or
            PhysicalKey.AltRight or
            PhysicalKey.ContextMenu or
            PhysicalKey.Insert or
            PhysicalKey.Home or
            PhysicalKey.PageUp or
            PhysicalKey.Delete or
            PhysicalKey.End or
            PhysicalKey.PageDown or
            PhysicalKey.ArrowRight or
            PhysicalKey.ArrowLeft or
            PhysicalKey.ArrowDown or
            PhysicalKey.ArrowUp or
            PhysicalKey.NumLock or
            PhysicalKey.NumpadEnter;
}

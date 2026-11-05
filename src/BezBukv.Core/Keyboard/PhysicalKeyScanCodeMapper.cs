using BezBukv.Core.Platform;

namespace BezBukv.Core.Keyboard;

/// <summary>
/// Maps between known physical keys and standard PC scan-code identities.
/// </summary>
public static class PhysicalKeyScanCodeMapper
{
    private const int VirtualKeyF1 = 0x70;
    private const int VirtualKeyF24 = 0x87;

    public static bool TryGetScanCode(
        PhysicalKey key,
        out KeyboardScanCodeIdentity physicalIdentity)
    {
        KeyboardScanCodeIdentity? mappedIdentity = key switch
        {
            PhysicalKey.Backquote => Key(0x29),
            PhysicalKey.Digit1 => Key(0x02),
            PhysicalKey.Digit2 => Key(0x03),
            PhysicalKey.Digit3 => Key(0x04),
            PhysicalKey.Digit4 => Key(0x05),
            PhysicalKey.Digit5 => Key(0x06),
            PhysicalKey.Digit6 => Key(0x07),
            PhysicalKey.Digit7 => Key(0x08),
            PhysicalKey.Digit8 => Key(0x09),
            PhysicalKey.Digit9 => Key(0x0A),
            PhysicalKey.Digit0 => Key(0x0B),
            PhysicalKey.Minus => Key(0x0C),
            PhysicalKey.Equal => Key(0x0D),
            PhysicalKey.Backspace => Key(0x0E),
            PhysicalKey.Tab => Key(0x0F),
            PhysicalKey.KeyQ => Key(0x10),
            PhysicalKey.KeyW => Key(0x11),
            PhysicalKey.KeyE => Key(0x12),
            PhysicalKey.KeyR => Key(0x13),
            PhysicalKey.KeyT => Key(0x14),
            PhysicalKey.KeyY => Key(0x15),
            PhysicalKey.KeyU => Key(0x16),
            PhysicalKey.KeyI => Key(0x17),
            PhysicalKey.KeyO => Key(0x18),
            PhysicalKey.KeyP => Key(0x19),
            PhysicalKey.BracketLeft => Key(0x1A),
            PhysicalKey.BracketRight => Key(0x1B),
            PhysicalKey.Backslash => Key(0x2B),
            PhysicalKey.CapsLock => Key(0x3A),
            PhysicalKey.KeyA => Key(0x1E),
            PhysicalKey.KeyS => Key(0x1F),
            PhysicalKey.KeyD => Key(0x20),
            PhysicalKey.KeyF => Key(0x21),
            PhysicalKey.KeyG => Key(0x22),
            PhysicalKey.KeyH => Key(0x23),
            PhysicalKey.KeyJ => Key(0x24),
            PhysicalKey.KeyK => Key(0x25),
            PhysicalKey.KeyL => Key(0x26),
            PhysicalKey.Semicolon => Key(0x27),
            PhysicalKey.Quote => Key(0x28),
            PhysicalKey.Enter => Key(0x1C),
            PhysicalKey.ShiftLeft => Key(0x2A),
            PhysicalKey.KeyZ => Key(0x2C),
            PhysicalKey.KeyX => Key(0x2D),
            PhysicalKey.KeyC => Key(0x2E),
            PhysicalKey.KeyV => Key(0x2F),
            PhysicalKey.KeyB => Key(0x30),
            PhysicalKey.KeyN => Key(0x31),
            PhysicalKey.KeyM => Key(0x32),
            PhysicalKey.Comma => Key(0x33),
            PhysicalKey.Period => Key(0x34),
            PhysicalKey.Slash => Key(0x35),
            PhysicalKey.ShiftRight => Key(0x36),
            PhysicalKey.ControlLeft => Key(0x1D),
            PhysicalKey.MetaLeft => Key(0x5B, extended: true),
            PhysicalKey.AltLeft => Key(0x38),
            PhysicalKey.Space => Key(0x39),
            PhysicalKey.AltRight => Key(0x38, extended: true),
            PhysicalKey.MetaRight => Key(0x5C, extended: true),
            PhysicalKey.ContextMenu => Key(0x5D, extended: true),
            PhysicalKey.ControlRight => Key(0x1D, extended: true),
            _ => null,
        };

        physicalIdentity = mappedIdentity.GetValueOrDefault();
        return mappedIdentity.HasValue;
    }

    /// <summary>
    /// Maps a saved scan-code identity, with an optional virtual-key hint, to a known named key.
    /// Printable and unknown keys intentionally return no translation.
    /// </summary>
    public static bool TryGetNamedPhysicalKey(
        KeyboardScanCodeIdentity physicalIdentity,
        int? virtualKey,
        out PhysicalKey key)
    {
        if (virtualKey is >= VirtualKeyF1 and <= VirtualKeyF24)
        {
            key = PhysicalKey.F1 + (virtualKey.Value - VirtualKeyF1);
            return true;
        }

        if (virtualKey == 0x13)
        {
            key = PhysicalKey.Pause;
            return true;
        }

        if (virtualKey == 0x2C)
        {
            key = PhysicalKey.PrintScreen;
            return true;
        }

        key = (physicalIdentity.ScanCode, physicalIdentity.Extended) switch
        {
            (0x1C, true) => PhysicalKey.NumpadEnter,
            (0x1D, true) => PhysicalKey.ControlRight,
            (0x35, true) => PhysicalKey.NumpadDivide,
            (0x37, true) => PhysicalKey.PrintScreen,
            (0x38, true) => PhysicalKey.AltRight,
            (0x47, true) => PhysicalKey.Home,
            (0x48, true) => PhysicalKey.ArrowUp,
            (0x49, true) => PhysicalKey.PageUp,
            (0x4B, true) => PhysicalKey.ArrowLeft,
            (0x4D, true) => PhysicalKey.ArrowRight,
            (0x4F, true) => PhysicalKey.End,
            (0x50, true) => PhysicalKey.ArrowDown,
            (0x51, true) => PhysicalKey.PageDown,
            (0x52, true) => PhysicalKey.Insert,
            (0x53, true) => PhysicalKey.Delete,
            (0x5B, true) => PhysicalKey.MetaLeft,
            (0x5C, true) => PhysicalKey.MetaRight,
            (0x5D, true) => PhysicalKey.ContextMenu,
            (0x01, false) => PhysicalKey.Escape,
            (0x0E, false) => PhysicalKey.Backspace,
            (0x0F, false) => PhysicalKey.Tab,
            (0x1C, false) => PhysicalKey.Enter,
            (0x1D, false) => PhysicalKey.ControlLeft,
            (0x2A, false) => PhysicalKey.ShiftLeft,
            (0x36, false) => PhysicalKey.ShiftRight,
            (0x37, false) => PhysicalKey.NumpadMultiply,
            (0x38, false) => PhysicalKey.AltLeft,
            (0x39, false) => PhysicalKey.Space,
            (0x3A, false) => PhysicalKey.CapsLock,
            ( >= 0x3B and <= 0x44, false) =>
                PhysicalKey.F1 + (physicalIdentity.ScanCode - 0x3B),
            (0x45, _) => PhysicalKey.NumLock,
            (0x46, false) => PhysicalKey.ScrollLock,
            (0x47, false) => PhysicalKey.Numpad7,
            (0x48, false) => PhysicalKey.Numpad8,
            (0x49, false) => PhysicalKey.Numpad9,
            (0x4A, false) => PhysicalKey.NumpadSubtract,
            (0x4B, false) => PhysicalKey.Numpad4,
            (0x4C, false) => PhysicalKey.Numpad5,
            (0x4D, false) => PhysicalKey.Numpad6,
            (0x4E, false) => PhysicalKey.NumpadAdd,
            (0x4F, false) => PhysicalKey.Numpad1,
            (0x50, false) => PhysicalKey.Numpad2,
            (0x51, false) => PhysicalKey.Numpad3,
            (0x52, false) => PhysicalKey.Numpad0,
            (0x53, false) => PhysicalKey.NumpadDecimal,
            (0x57, false) => PhysicalKey.F11,
            (0x58, false) => PhysicalKey.F12,
            _ => PhysicalKey.Unknown,
        };

        if (key != PhysicalKey.Unknown)
        {
            return true;
        }

        key = virtualKey switch
        {
            0x08 => PhysicalKey.Backspace,
            0x09 => PhysicalKey.Tab,
            0x0D => PhysicalKey.Enter,
            0x10 or 0xA0 => PhysicalKey.ShiftLeft,
            0xA1 => PhysicalKey.ShiftRight,
            0x11 or 0xA2 => PhysicalKey.ControlLeft,
            0xA3 => PhysicalKey.ControlRight,
            0x12 or 0xA4 => PhysicalKey.AltLeft,
            0xA5 => PhysicalKey.AltRight,
            0x14 => PhysicalKey.CapsLock,
            0x1B => PhysicalKey.Escape,
            0x20 => PhysicalKey.Space,
            0x21 => PhysicalKey.PageUp,
            0x22 => PhysicalKey.PageDown,
            0x23 => PhysicalKey.End,
            0x24 => PhysicalKey.Home,
            0x25 => PhysicalKey.ArrowLeft,
            0x26 => PhysicalKey.ArrowUp,
            0x27 => PhysicalKey.ArrowRight,
            0x28 => PhysicalKey.ArrowDown,
            0x2D => PhysicalKey.Insert,
            0x2E => PhysicalKey.Delete,
            0x5B => PhysicalKey.MetaLeft,
            0x5C => PhysicalKey.MetaRight,
            0x5D => PhysicalKey.ContextMenu,
            0x60 => PhysicalKey.Numpad0,
            0x61 => PhysicalKey.Numpad1,
            0x62 => PhysicalKey.Numpad2,
            0x63 => PhysicalKey.Numpad3,
            0x64 => PhysicalKey.Numpad4,
            0x65 => PhysicalKey.Numpad5,
            0x66 => PhysicalKey.Numpad6,
            0x67 => PhysicalKey.Numpad7,
            0x68 => PhysicalKey.Numpad8,
            0x69 => PhysicalKey.Numpad9,
            0x6A => PhysicalKey.NumpadMultiply,
            0x6B => PhysicalKey.NumpadAdd,
            0x6D => PhysicalKey.NumpadSubtract,
            0x6E => PhysicalKey.NumpadDecimal,
            0x6F => PhysicalKey.NumpadDivide,
            0x90 => PhysicalKey.NumLock,
            0x91 => PhysicalKey.ScrollLock,
            _ => PhysicalKey.Unknown,
        };

        return key != PhysicalKey.Unknown;
    }

    private static KeyboardScanCodeIdentity Key(int scanCode, bool extended = false) =>
        new(scanCode, extended);
}

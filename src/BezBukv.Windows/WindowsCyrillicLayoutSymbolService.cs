using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using BezBukv.Core.Platform;

namespace BezBukv.Windows;

public sealed class WindowsCyrillicLayoutSymbolService : ICyrillicLayoutSymbolService
{
    private const uint MapVkVirtualScanCodeToVirtualKey = 3;
    private const uint ToUnicodeDoNotChangeKeyboardState = 0x0004;
    private const int VirtualKeyShift = 0x10;
    private const byte KeyPressed = 0x80;
    private const int KeyboardStateLength = 256;
    private const int SymbolBufferCapacity = 8;
    private const ushort RussianLanguageId = 0x0419;
    private const ushort RussianPrimaryLanguageId = 0x19;

    private readonly IntPtr _cyrillicKeyboardLayout;

    public WindowsCyrillicLayoutSymbolService()
    {
        try
        {
            _cyrillicKeyboardLayout = FindInstalledCyrillicLayout();
        }
        catch (Exception)
        {
            _cyrillicKeyboardLayout = IntPtr.Zero;
        }
    }

    public string GetCyrillicLayoutSymbol(PhysicalKey key)
    {
        try
        {
            if (_cyrillicKeyboardLayout == IntPtr.Zero ||
                !TryTranslateKey(key, _cyrillicKeyboardLayout, shiftPressed: false, out var symbol))
            {
                return string.Empty;
            }

            return symbol.ToUpperInvariant();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public string GetCyrillicLayoutShiftSymbol(PhysicalKey key)
    {
        try
        {
            if (_cyrillicKeyboardLayout == IntPtr.Zero ||
                !TryTranslateKey(key, _cyrillicKeyboardLayout, shiftPressed: true, out var symbol))
            {
                return string.Empty;
            }

            return symbol.ToUpperInvariant();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static IntPtr FindInstalledCyrillicLayout()
    {
        var layoutCount = GetKeyboardLayoutList(0, null);
        if (layoutCount <= 0)
        {
            return IntPtr.Zero;
        }

        var layouts = new IntPtr[layoutCount];
        var copiedLayoutCount = GetKeyboardLayoutList(layouts.Length, layouts);
        if (copiedLayoutCount <= 0)
        {
            return IntPtr.Zero;
        }

        var installedLayouts = layouts.Take(Math.Min(copiedLayoutCount, layouts.Length));
        var russianLayout = installedLayouts.FirstOrDefault(
            layout => IsRussianLayout(layout) && ProducesCyrillic(layout));

        return russianLayout != IntPtr.Zero
            ? russianLayout
            : installedLayouts.FirstOrDefault(ProducesCyrillic);
    }

    private static bool IsRussianLayout(IntPtr layout)
    {
        var languageId = GetLanguageId(layout);
        return languageId == RussianLanguageId ||
            (languageId & 0x03FF) == RussianPrimaryLanguageId;
    }

    private static ushort GetLanguageId(IntPtr layout) =>
        unchecked((ushort)((nuint)layout & 0xFFFF));

    private static bool ProducesCyrillic(IntPtr layout)
    {
        foreach (var key in Enum.GetValues<PhysicalKey>())
        {
            if (TryTranslateKey(key, layout, shiftPressed: false, out var symbol) &&
                symbol.EnumerateRunes().Any(IsCyrillic))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryTranslateKey(
        PhysicalKey key,
        IntPtr keyboardLayout,
        bool shiftPressed,
        out string symbol)
    {
        symbol = string.Empty;

        if (!TryGetScanCode(key, out var scanCode))
        {
            return false;
        }

        var virtualKey = MapVirtualKeyExW(
            scanCode,
            MapVkVirtualScanCodeToVirtualKey,
            keyboardLayout);
        if (virtualKey == 0)
        {
            return false;
        }

        var keyboardState = new byte[KeyboardStateLength];
        if (shiftPressed)
        {
            keyboardState[VirtualKeyShift] = KeyPressed;
        }

        var buffer = new StringBuilder(SymbolBufferCapacity);
        var characterCount = ToUnicodeEx(
            virtualKey,
            scanCode,
            keyboardState,
            buffer,
            buffer.Capacity,
            ToUnicodeDoNotChangeKeyboardState,
            keyboardLayout);

        // Zero means no translation; a negative result is a dead key. The flag above
        // keeps this probe from changing the thread's pending dead-key state.
        if (characterCount <= 0)
        {
            return false;
        }

        symbol = buffer.ToString(0, Math.Min(characterCount, buffer.Length));
        return IsPrintable(symbol);
    }

    private static bool IsCyrillic(Rune rune) =>
        rune.Value is >= 0x0400 and <= 0x052F or
            >= 0x1C80 and <= 0x1C8F or
            >= 0x2DE0 and <= 0x2DFF or
            >= 0xA640 and <= 0xA69F or
            >= 0xFE2E and <= 0xFE2F;

    private static bool IsPrintable(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return false;
        }

        return symbol.EnumerateRunes().All(
            rune => Rune.GetUnicodeCategory(rune) is not UnicodeCategory.Control and
                not UnicodeCategory.Format and
                not UnicodeCategory.LineSeparator and
                not UnicodeCategory.ParagraphSeparator and
                not UnicodeCategory.SpaceSeparator and
                not UnicodeCategory.OtherNotAssigned);
    }

    private static bool TryGetScanCode(PhysicalKey key, out uint scanCode)
    {
        scanCode = key switch
        {
            PhysicalKey.Backquote => 0x29,
            PhysicalKey.Digit1 => 0x02,
            PhysicalKey.Digit2 => 0x03,
            PhysicalKey.Digit3 => 0x04,
            PhysicalKey.Digit4 => 0x05,
            PhysicalKey.Digit5 => 0x06,
            PhysicalKey.Digit6 => 0x07,
            PhysicalKey.Digit7 => 0x08,
            PhysicalKey.Digit8 => 0x09,
            PhysicalKey.Digit9 => 0x0A,
            PhysicalKey.Digit0 => 0x0B,
            PhysicalKey.Minus => 0x0C,
            PhysicalKey.Equal => 0x0D,
            PhysicalKey.KeyQ => 0x10,
            PhysicalKey.KeyW => 0x11,
            PhysicalKey.KeyE => 0x12,
            PhysicalKey.KeyR => 0x13,
            PhysicalKey.KeyT => 0x14,
            PhysicalKey.KeyY => 0x15,
            PhysicalKey.KeyU => 0x16,
            PhysicalKey.KeyI => 0x17,
            PhysicalKey.KeyO => 0x18,
            PhysicalKey.KeyP => 0x19,
            PhysicalKey.BracketLeft => 0x1A,
            PhysicalKey.BracketRight => 0x1B,
            PhysicalKey.KeyA => 0x1E,
            PhysicalKey.KeyS => 0x1F,
            PhysicalKey.KeyD => 0x20,
            PhysicalKey.KeyF => 0x21,
            PhysicalKey.KeyG => 0x22,
            PhysicalKey.KeyH => 0x23,
            PhysicalKey.KeyJ => 0x24,
            PhysicalKey.KeyK => 0x25,
            PhysicalKey.KeyL => 0x26,
            PhysicalKey.Semicolon => 0x27,
            PhysicalKey.Quote => 0x28,
            PhysicalKey.Backslash => 0x2B,
            PhysicalKey.KeyZ => 0x2C,
            PhysicalKey.KeyX => 0x2D,
            PhysicalKey.KeyC => 0x2E,
            PhysicalKey.KeyV => 0x2F,
            PhysicalKey.KeyB => 0x30,
            PhysicalKey.KeyN => 0x31,
            PhysicalKey.KeyM => 0x32,
            PhysicalKey.Comma => 0x33,
            PhysicalKey.Period => 0x34,
            PhysicalKey.Slash => 0x35,
            PhysicalKey.Space => 0x39,
            _ => 0,
        };

        return scanCode != 0;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetKeyboardLayoutList(
        int count,
        [Out] IntPtr[]? layouts);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyExW(
        uint code,
        uint mapType,
        IntPtr keyboardLayout);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(
        uint virtualKey,
        uint scanCode,
        byte[] keyboardState,
        [Out] StringBuilder buffer,
        int bufferCapacity,
        uint flags,
        IntPtr keyboardLayout);
}

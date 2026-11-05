using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using BezBukv.Core.Platform;

namespace BezBukv.Windows;

public sealed class WindowsKeyboardLayoutSymbolService : IKeyboardLayoutSymbolService
{
    private const uint MapVkVirtualScanCodeToVirtualKeyEx = 3;
    private const uint ToUnicodeDoNotChangeKeyboardState = 0x0004;
    private const uint ExtendedScanCodePrefix = 0xE000;
    private const int MaxScanCode = 0xFF;
    private const int VirtualKeyShift = 0x10;
    private const byte KeyPressed = 0x80;
    private const int KeyboardStateLength = 256;
    private const int SymbolBufferCapacity = 8;

    public string GetKeyboardLayoutSymbol(
        int scanCode,
        bool extended,
        string klid,
        bool shiftPressed)
    {
        try
        {
            if (scanCode is <= 0 or > MaxScanCode ||
                !WindowsInstalledKeyboardLayoutService.TryGetInstalledKeyboardLayout(
                    klid,
                    out var keyboardLayout) ||
                !TryTranslateKey(
                    (uint)scanCode,
                    extended,
                    keyboardLayout,
                    shiftPressed,
                    out var symbol))
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

    private static bool TryTranslateKey(
        uint scanCode,
        bool extended,
        IntPtr keyboardLayout,
        bool shiftPressed,
        out string symbol)
    {
        symbol = string.Empty;

        var mapScanCode = extended
            ? scanCode | ExtendedScanCodePrefix
            : scanCode;
        var virtualKey = MapVirtualKeyExW(
            mapScanCode,
            MapVkVirtualScanCodeToVirtualKeyEx,
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

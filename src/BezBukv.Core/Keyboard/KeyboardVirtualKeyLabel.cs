using System.Globalization;

namespace BezBukv.Core.Keyboard;

/// <summary>
/// Provides keycap labels for non-printable keys identified by their virtual-key code.
/// </summary>
public static class KeyboardVirtualKeyLabel
{
    private const int VirtualKeyF1 = 0x70;
    private const int VirtualKeyF24 = 0x87;

    /// <summary>
    /// Returns a label for a known non-printable key, or an empty string for printable or
    /// unknown virtual-key codes.
    /// </summary>
    public static string GetLabel(int virtualKey) =>
        GetLabel(virtualKey, isAltGr: false);

    /// <summary>
    /// Returns an AltGr label for a right-Alt event explicitly marked as AltGr;
    /// otherwise returns the standard virtual-key label.
    /// </summary>
    public static string GetLabel(int virtualKey, bool isAltGr)
    {
        if (virtualKey == 0xA5 && isAltGr)
        {
            return "AltGr";
        }

        if (virtualKey is >= VirtualKeyF1 and <= VirtualKeyF24)
        {
            return "F" + (virtualKey - VirtualKeyF1 + 1)
                .ToString(CultureInfo.InvariantCulture);
        }

        return virtualKey switch
        {
            0x08 => "⌫",
            0x09 => "Tab",
            0x0D => "Enter",
            0x10 or 0xA0 or 0xA1 => "Shift",
            0x11 or 0xA2 or 0xA3 => "Ctrl",
            0x12 or 0xA4 or 0xA5 => "Alt",
            0x13 => "Pause",
            0x14 => "Caps",
            0x1B => "Esc",
            0x20 => "Space",
            0x21 => "Page Up",
            0x22 => "Page Down",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2C => "Print Screen",
            0x2D => "Insert",
            0x2E => "Delete",
            0x5B or 0x5C => "Win",
            0x5D => "Menu",
            0x90 => "Num Lock",
            0x91 => "Scroll Lock",
            _ => string.Empty,
        };
    }
}

namespace BezBukv.Core.Platform;

/// <summary>
/// Resolves symbols produced by a physical key in an installed Cyrillic keyboard layout.
/// </summary>
public interface ICyrillicLayoutSymbolService
{
    /// <summary>
    /// Returns an uppercase Cyrillic-layout keycap symbol, or an empty string when no printable
    /// symbol or installed Cyrillic layout is available.
    /// </summary>
    string GetCyrillicLayoutSymbol(PhysicalKey key);

    /// <summary>
    /// Returns the uppercase Cyrillic-layout keycap symbol produced while Shift is held, or an
    /// empty string when no printable symbol or installed Cyrillic layout is available.
    /// </summary>
    string GetCyrillicLayoutShiftSymbol(PhysicalKey key);
}

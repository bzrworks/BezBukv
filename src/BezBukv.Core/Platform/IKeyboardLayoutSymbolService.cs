namespace BezBukv.Core.Platform;

/// <summary>
/// Resolves a symbol produced by a raw physical key in a specific installed keyboard layout.
/// </summary>
public interface IKeyboardLayoutSymbolService
{
    /// <summary>
    /// Returns the uppercase printable keycap symbol produced by the raw key in the layout
    /// identified by <paramref name="klid"/>, or an empty string when it cannot be resolved.
    /// </summary>
    string GetKeyboardLayoutSymbol(
        int scanCode,
        bool extended,
        string klid,
        bool shiftPressed);
}

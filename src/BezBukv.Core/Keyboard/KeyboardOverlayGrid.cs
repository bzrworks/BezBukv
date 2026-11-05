namespace BezBukv.Core.Keyboard;

/// <summary>
/// Platform-independent identity of a physical key in the standard PC scan-code set.
/// </summary>
public readonly record struct KeyboardScanCodeIdentity(int ScanCode, bool Extended);

/// <summary>
/// Geometry, optional physical identity, and optional fixed label of one unresolved overlay cell.
/// </summary>
public readonly record struct KeyboardOverlayCellDefinition(
    KeyboardScanCodeIdentity? PhysicalIdentity,
    KeyboardGridOffset Offset,
    bool IsCenter,
    string? DisplayLabel = null,
    bool IsSpacer = false);

/// <summary>
/// Base and Shift symbols resolved for one selected keyboard-layout slot.
/// </summary>
public sealed record KeyboardOverlaySymbolSlot(
    int Slot,
    string Klid,
    string BaseSymbol,
    string ShiftSymbol);

/// <summary>
/// One resolved overlay cell with geometry, optional physical identity, fixed label, and layout slots.
/// </summary>
public sealed record KeyboardOverlayGridCell(
    KeyboardScanCodeIdentity? PhysicalIdentity,
    KeyboardGridOffset Offset,
    bool IsCenter,
    IReadOnlyList<KeyboardOverlaySymbolSlot> SymbolSlots,
    string? DisplayLabel,
    bool IsSpacer);

/// <summary>
/// Headless model consumed by a future overlay renderer.
/// </summary>
public sealed class KeyboardOverlayGrid
{
    public KeyboardOverlayGrid(IReadOnlyList<KeyboardOverlayGridCell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        Cells = Array.AsReadOnly(cells.ToArray());
    }

    public IReadOnlyList<KeyboardOverlayGridCell> Cells { get; }
}

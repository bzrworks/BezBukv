using BezBukv.Core.Platform;
using BezBukv.Core.Settings;

namespace BezBukv.Core.Keyboard;

/// <summary>
/// Builds headless overlay grids and resolves their symbols by selected KLID slots.
/// </summary>
public sealed class KeyboardOverlayGridBuilder
{
    private const int MaximumLayoutSlotCount = 2;

    private readonly IKeyboardLayoutSymbolService _symbolService;

    public KeyboardOverlayGridBuilder(IKeyboardLayoutSymbolService symbolService)
    {
        ArgumentNullException.ThrowIfNull(symbolService);
        _symbolService = symbolService;
    }

    /// <summary>
    /// Builds the center cell and its ANSI/ISO neighbors using an existing geometry profile.
    /// </summary>
    public KeyboardOverlayGrid BuildPreset(
        KeyboardGeometryProfile geometryProfile,
        PhysicalKey centerKey,
        IReadOnlyList<string> layoutKlids)
    {
        ArgumentNullException.ThrowIfNull(geometryProfile);

        var definitions = new List<KeyboardOverlayCellDefinition>
        {
            CreatePresetDefinition(centerKey, new KeyboardGridOffset(0, 0), isCenter: true),
        };

        foreach (var neighbor in geometryProfile.GetNeighbors(centerKey))
        {
            definitions.Add(CreatePresetDefinition(neighbor.Key, neighbor.Offset, isCenter: false));
        }

        return Build(definitions, layoutKlids);
    }

    /// <summary>
    /// Builds the center cell and its neighbors from a user-calibrated keyboard layout.
    /// </summary>
    public KeyboardOverlayGrid BuildCustom(
        CustomKeyboardLayout layout,
        KeyboardScanCodeIdentity centerIdentity,
        IReadOnlyList<string> layoutKlids)
    {
        var definitions = CustomKeyboardOverlayCellDefinitionMapper.CreateDefinitions(
            layout,
            centerIdentity);
        return Build(definitions, layoutKlids);
    }

    /// <summary>
    /// Builds a preview around the user-calibrated key with the most real neighbors.
    /// </summary>
    public KeyboardOverlayGrid BuildCustomPreview(
        CustomKeyboardLayout layout,
        IReadOnlyList<string> layoutKlids)
    {
        var definitions =
            CustomKeyboardOverlayCellDefinitionMapper.CreatePreviewDefinitions(layout);
        return Build(definitions, layoutKlids);
    }

    /// <summary>
    /// Resolves an arbitrary set of physical cells without changing their geometry.
    /// </summary>
    public KeyboardOverlayGrid Build(
        IReadOnlyList<KeyboardOverlayCellDefinition> definitions,
        IReadOnlyList<string> layoutKlids)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(layoutKlids);

        if (layoutKlids.Count > MaximumLayoutSlotCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(layoutKlids),
                layoutKlids.Count,
                $"At most {MaximumLayoutSlotCount} keyboard layout slots are supported.");
        }

        var cells = new KeyboardOverlayGridCell[definitions.Count];
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            cells[index] = new KeyboardOverlayGridCell(
                definition.PhysicalIdentity,
                definition.Offset,
                definition.IsCenter,
                definition.DisplayLabel is null && !definition.IsSpacer
                    ? ResolveSymbolSlots(definition.PhysicalIdentity, layoutKlids)
                    : [],
                definition.DisplayLabel,
                definition.IsSpacer);
        }

        return new KeyboardOverlayGrid(cells);
    }

    private static KeyboardOverlayCellDefinition CreatePresetDefinition(
        PhysicalKey key,
        KeyboardGridOffset offset,
        bool isCenter)
    {
        KeyboardScanCodeIdentity? physicalIdentity =
            PhysicalKeyScanCodeMapper.TryGetScanCode(key, out var mappedIdentity)
                ? mappedIdentity
                : null;

        var displayLabel = KeyboardKeycapLegend.GetNamedLabel(key);

        return new KeyboardOverlayCellDefinition(
            physicalIdentity,
            offset,
            isCenter,
            string.IsNullOrEmpty(displayLabel) ? null : displayLabel);
    }

    private IReadOnlyList<KeyboardOverlaySymbolSlot> ResolveSymbolSlots(
        KeyboardScanCodeIdentity? physicalIdentity,
        IReadOnlyList<string> layoutKlids)
    {
        var slots = new KeyboardOverlaySymbolSlot[layoutKlids.Count];

        for (var index = 0; index < layoutKlids.Count; index++)
        {
            var klid = layoutKlids[index];
            var baseSymbol = string.Empty;
            var shiftSymbol = string.Empty;

            if (physicalIdentity is { } identity)
            {
                baseSymbol = _symbolService.GetKeyboardLayoutSymbol(
                    identity.ScanCode,
                    identity.Extended,
                    klid,
                    shiftPressed: false) ?? string.Empty;
                shiftSymbol = _symbolService.GetKeyboardLayoutSymbol(
                    identity.ScanCode,
                    identity.Extended,
                    klid,
                    shiftPressed: true) ?? string.Empty;
            }

            slots[index] = new KeyboardOverlaySymbolSlot(
                index + 1,
                klid,
                baseSymbol,
                shiftSymbol);
        }

        return Array.AsReadOnly(slots);
    }
}

using BezBukv.Core.Settings;

namespace BezBukv.Core.Keyboard;

internal static class CustomKeyboardOverlayCellDefinitionMapper
{
    public static IReadOnlyList<KeyboardOverlayCellDefinition> CreatePreviewDefinitions(
        CustomKeyboardLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var rows = layout.Rows;
        ArgumentNullException.ThrowIfNull(rows);

        var rowWidths = new IReadOnlyList<int>[rows.Count];
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var cells = rows[rowIndex].Cells;
            ArgumentNullException.ThrowIfNull(cells);

            var widths = new int[cells.Count];
            for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                widths[cellIndex] = cells[cellIndex].WidthUnits;
            }

            rowWidths[rowIndex] = widths;
        }

        var neighborGeometry = new KeyboardNeighborGeometry(rowWidths);
        KeyboardCellPosition? densestPosition = null;
        CustomKeyboardKey? densestKey = null;
        var densestNeighborCount = -1;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var cells = rows[rowIndex].Cells;
            for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                var key = cells[cellIndex].Key;
                if (key is null)
                {
                    continue;
                }

                var position = new KeyboardCellPosition(rowIndex, cellIndex);
                var realNeighborCount = neighborGeometry
                    .GetNeighbors(position)
                    .Count(neighbor =>
                        rows[neighbor.Position.Row]
                            .Cells[neighbor.Position.Index]
                            .Key is not null);

                if (realNeighborCount <= densestNeighborCount)
                {
                    continue;
                }

                densestPosition = position;
                densestKey = key;
                densestNeighborCount = realNeighborCount;
            }
        }

        return densestPosition is { } center && densestKey is not null
            ? CreateDefinitions(rows, neighborGeometry, center, densestKey)
            : [];
    }

    public static IReadOnlyList<KeyboardOverlayCellDefinition> CreateDefinitions(
        CustomKeyboardLayout layout,
        KeyboardScanCodeIdentity centerIdentity)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var rows = layout.Rows;
        ArgumentNullException.ThrowIfNull(rows);

        var rowWidths = new IReadOnlyList<int>[rows.Count];
        KeyboardCellPosition? centerPosition = null;
        CustomKeyboardKey? centerKey = null;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var cells = rows[rowIndex].Cells;
            ArgumentNullException.ThrowIfNull(cells);

            var widths = new int[cells.Count];
            for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                var cell = cells[cellIndex];
                widths[cellIndex] = cell.WidthUnits;

                if (centerPosition is null && Matches(cell.Key, centerIdentity))
                {
                    centerPosition = new KeyboardCellPosition(rowIndex, cellIndex);
                    centerKey = cell.Key;
                }
            }

            rowWidths[rowIndex] = widths;
        }

        if (centerPosition is not { } center || centerKey is null)
        {
            return [];
        }

        return CreateDefinitions(
            rows,
            new KeyboardNeighborGeometry(rowWidths),
            center,
            centerKey);
    }

    private static IReadOnlyList<KeyboardOverlayCellDefinition> CreateDefinitions(
        IReadOnlyList<CustomKeyboardRow> rows,
        KeyboardNeighborGeometry neighborGeometry,
        KeyboardCellPosition center,
        CustomKeyboardKey centerKey)
    {
        var definitions = new List<KeyboardOverlayCellDefinition>(capacity: 9)
        {
            CreateKeyDefinition(
                centerKey,
                new KeyboardGridOffset(0, 0),
                isCenter: true),
        };

        foreach (var neighbor in neighborGeometry.GetNeighbors(center))
        {
            var key = rows[neighbor.Position.Row].Cells[neighbor.Position.Index].Key;
            definitions.Add(key is null
                ? new KeyboardOverlayCellDefinition(
                    PhysicalIdentity: null,
                    neighbor.Offset,
                    IsCenter: false,
                    IsSpacer: true)
                : CreateKeyDefinition(key, neighbor.Offset, isCenter: false));
        }

        return Array.AsReadOnly(definitions.ToArray());
    }

    private static KeyboardOverlayCellDefinition CreateKeyDefinition(
        CustomKeyboardKey key,
        KeyboardGridOffset offset,
        bool isCenter)
    {
        var physicalIdentity = new KeyboardScanCodeIdentity(key.ScanCode, key.Extended);
        string? displayLabel = null;

        if (PhysicalKeyScanCodeMapper.TryGetNamedPhysicalKey(
                physicalIdentity,
                key.VirtualKey,
                out var physicalKey))
        {
            displayLabel = KeyboardKeycapLegend.GetNamedLabel(physicalKey);
        }

        return new KeyboardOverlayCellDefinition(
            physicalIdentity,
            offset,
            isCenter,
            displayLabel);
    }

    private static bool Matches(
        CustomKeyboardKey? key,
        KeyboardScanCodeIdentity identity) =>
        key is not null &&
        key.ScanCode == identity.ScanCode &&
        key.Extended == identity.Extended;
}

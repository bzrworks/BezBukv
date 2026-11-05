namespace BezBukv.Core.Keyboard;

internal readonly record struct KeyboardCellPosition(int Row, int Index);

internal readonly record struct KeyboardPositionNeighbor(
    KeyboardCellPosition Position,
    KeyboardGridOffset Offset);

/// <summary>
/// Identity-agnostic neighbor geometry based only on ordered rows of cell widths.
/// </summary>
internal sealed class KeyboardNeighborGeometry
{
    private readonly IReadOnlyList<IReadOnlyList<CellPlacement>> _rows;

    public KeyboardNeighborGeometry(IReadOnlyList<IReadOnlyList<int>> rowWidths)
    {
        ArgumentNullException.ThrowIfNull(rowWidths);

        var rows = new IReadOnlyList<CellPlacement>[rowWidths.Count];
        for (var rowIndex = 0; rowIndex < rowWidths.Count; rowIndex++)
        {
            var widths = rowWidths[rowIndex];
            ArgumentNullException.ThrowIfNull(widths);

            var placements = new CellPlacement[widths.Count];
            long left = 0;

            for (var cellIndex = 0; cellIndex < widths.Count; cellIndex++)
            {
                var width = widths[cellIndex];
                if (width < 1)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(rowWidths),
                        width,
                        "Cell widths must be positive.");
                }

                placements[cellIndex] = new CellPlacement(
                    cellIndex,
                    checked((left * 2) + width));
                left = checked(left + width);
            }

            rows[rowIndex] = Array.AsReadOnly(placements);
        }

        _rows = Array.AsReadOnly(rows);
    }

    public IReadOnlyList<KeyboardPositionNeighbor> GetNeighbors(KeyboardCellPosition center)
    {
        if (center.Row < 0 || center.Row >= _rows.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(center));
        }

        var centerRow = _rows[center.Row];
        if (center.Index < 0 || center.Index >= centerRow.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(center));
        }

        var neighbors = new List<KeyboardPositionNeighbor>(capacity: 8);
        var centerPlacement = centerRow[center.Index];

        AddAdjacentRowNeighbors(center, centerPlacement, rowOffset: -1, neighbors);
        AddSameRowNeighbors(center, centerRow, neighbors);
        AddAdjacentRowNeighbors(center, centerPlacement, rowOffset: 1, neighbors);

        return Array.AsReadOnly(neighbors.ToArray());
    }

    private static void AddSameRowNeighbors(
        KeyboardCellPosition center,
        IReadOnlyList<CellPlacement> centerRow,
        ICollection<KeyboardPositionNeighbor> neighbors)
    {
        if (center.Index > 0)
        {
            neighbors.Add(new KeyboardPositionNeighbor(
                new KeyboardCellPosition(center.Row, center.Index - 1),
                new KeyboardGridOffset(0, -1)));
        }

        if (center.Index + 1 < centerRow.Count)
        {
            neighbors.Add(new KeyboardPositionNeighbor(
                new KeyboardCellPosition(center.Row, center.Index + 1),
                new KeyboardGridOffset(0, 1)));
        }
    }

    private void AddAdjacentRowNeighbors(
        KeyboardCellPosition center,
        CellPlacement centerPlacement,
        int rowOffset,
        ICollection<KeyboardPositionNeighbor> neighbors)
    {
        var adjacentRowIndex = center.Row + rowOffset;
        if (adjacentRowIndex < 0 || adjacentRowIndex >= _rows.Count)
        {
            return;
        }

        var adjacentRow = _rows[adjacentRowIndex];
        if (adjacentRow.Count == 0)
        {
            return;
        }

        var columnZero = adjacentRow[0];
        var nearestDistance = Math.Abs(columnZero.CenterXTwice - centerPlacement.CenterXTwice);

        for (var index = 1; index < adjacentRow.Count; index++)
        {
            var candidate = adjacentRow[index];
            var distance = Math.Abs(candidate.CenterXTwice - centerPlacement.CenterXTwice);
            if (distance < nearestDistance ||
                (distance == nearestDistance && candidate.CenterXTwice > columnZero.CenterXTwice))
            {
                columnZero = candidate;
                nearestDistance = distance;
            }
        }

        for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
        {
            var candidateIndex = columnZero.Index + columnOffset;
            if (candidateIndex < 0 || candidateIndex >= adjacentRow.Count)
            {
                continue;
            }

            neighbors.Add(new KeyboardPositionNeighbor(
                new KeyboardCellPosition(adjacentRowIndex, candidateIndex),
                new KeyboardGridOffset(rowOffset, columnOffset)));
        }
    }

    private readonly record struct CellPlacement(int Index, long CenterXTwice);
}

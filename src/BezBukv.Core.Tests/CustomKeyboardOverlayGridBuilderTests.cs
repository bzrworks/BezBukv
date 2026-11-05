using BezBukv.Core.Keyboard;
using BezBukv.Core.Platform;
using BezBukv.Core.Settings;

namespace BezBukv.Core.Tests;

public sealed class CustomKeyboardOverlayGridBuilderTests
{
    [Fact]
    public void MiddleCenterBuildsEightNeighborsInGridOrder()
    {
        var builder = CreateBuilder();
        var layout = Layout(
            [Key(0x10), Key(0x11, extended: true, virtualKey: 0xFF, isAltGr: true), Key(0x12)],
            [Key(0x20), Key(0x21), Key(0x22)],
            [Key(0x30), Key(0x31), Key(0x32)]);

        var grid = builder.BuildCustom(
            layout,
            new KeyboardScanCodeIdentity(0x21, false),
            []);

        AssertCells(
            grid.Cells,
            Expected(0x21, 0, 0, isCenter: true),
            Expected(0x10, -1, -1),
            Expected(0x11, -1, 0, extended: true),
            Expected(0x12, -1, 1),
            Expected(0x20, 0, -1),
            Expected(0x22, 0, 1),
            Expected(0x30, 1, -1),
            Expected(0x31, 1, 0),
            Expected(0x32, 1, 1));
    }

    [Fact]
    public void EmptyCellCanOccupyAdjacentRowColumnZero()
    {
        var symbolService = new FakeKeyboardLayoutSymbolService(FormatSymbol);
        var builder = new KeyboardOverlayGridBuilder(symbolService);
        var layout = Layout(
            [Key(0x10), Empty(width: 2), Key(0x12)],
            [Key(0x20), Key(0x21), Key(0x22)]);

        var grid = builder.BuildCustom(
            layout,
            new KeyboardScanCodeIdentity(0x21, false),
            ["00000409"]);

        AssertCells(
            grid.Cells,
            Expected(0x21, 0, 0, isCenter: true),
            Expected(0x10, -1, -1),
            Expected(null, -1, 0),
            Expected(0x12, -1, 1),
            Expected(0x20, 0, -1),
            Expected(0x22, 0, 1));

        var emptyCell = Assert.Single(
            grid.Cells,
            cell => cell.Offset == new KeyboardGridOffset(-1, 0));
        Assert.Null(emptyCell.PhysicalIdentity);
        Assert.Null(emptyCell.DisplayLabel);
        Assert.True(emptyCell.IsSpacer);
        Assert.Empty(emptyCell.SymbolSlots);
        Assert.Equal(10, symbolService.Requests.Count);
    }

    [Fact]
    public void WideCenterUsesItsFullWidthForCenterX()
    {
        var builder = CreateBuilder();
        var layout = Layout(
            [Key(0x10), Key(0x11), Key(0x12), Key(0x13)],
            [Key(0x20), Key(0x21, width: 3), Key(0x22)]);

        var grid = builder.BuildCustom(
            layout,
            new KeyboardScanCodeIdentity(0x21, false),
            []);

        AssertCells(
            grid.Cells,
            Expected(0x21, 0, 0, isCenter: true),
            Expected(0x11, -1, -1),
            Expected(0x12, -1, 0),
            Expected(0x13, -1, 1),
            Expected(0x20, 0, -1),
            Expected(0x22, 0, 1));
    }

    [Fact]
    public void CornerCenterTruncatesNeighborsAtLayoutEdges()
    {
        var builder = CreateBuilder();
        var layout = Layout(
            [Key(0x21), Key(0x22)],
            [Key(0x30), Key(0x31)]);

        var grid = builder.BuildCustom(
            layout,
            new KeyboardScanCodeIdentity(0x21, false),
            []);

        AssertCells(
            grid.Cells,
            Expected(0x21, 0, 0, isCenter: true),
            Expected(0x22, 0, 1),
            Expected(0x30, 1, 0),
            Expected(0x31, 1, 1));
    }

    [Fact]
    public void MissingCenterBuildsEmptyGridWithoutResolvingSymbols()
    {
        var symbolService = new FakeKeyboardLayoutSymbolService(FormatSymbol);
        var builder = new KeyboardOverlayGridBuilder(symbolService);
        var layout = Layout(
            [Key(0x10), Key(0x11)],
            [Key(0x20), Empty()]);

        var grid = builder.BuildCustom(
            layout,
            new KeyboardScanCodeIdentity(0x7F, true),
            ["00000409"]);

        Assert.Empty(grid.Cells);
        Assert.Empty(symbolService.Requests);
    }

    [Fact]
    public void ExistingCenterWithoutNeighborsBuildsSingleRealCell()
    {
        var builder = CreateBuilder();
        var layout = Layout([Key(0x21, extended: true)]);

        var grid = builder.BuildCustom(
            layout,
            new KeyboardScanCodeIdentity(0x21, true),
            []);

        AssertCells(
            grid.Cells,
            Expected(0x21, 0, 0, isCenter: true, extended: true));
    }

    [Fact]
    public void DifferentOutsideEnumIdentitiesBuildAsDifferentExactCenters()
    {
        var builder = CreateBuilder();
        var firstIdentity = new KeyboardScanCodeIdentity(0x56, false);
        var secondIdentity = new KeyboardScanCodeIdentity(0x7F, true);
        var layout = Layout(
            [Key(firstIdentity.ScanCode), Key(secondIdentity.ScanCode, extended: true)]);

        var firstGrid = builder.BuildCustom(layout, firstIdentity, []);
        var secondGrid = builder.BuildCustom(layout, secondIdentity, []);

        var firstCenter = Assert.Single(firstGrid.Cells, cell => cell.IsCenter);
        var secondCenter = Assert.Single(secondGrid.Cells, cell => cell.IsCenter);
        Assert.Equal(firstIdentity, firstCenter.PhysicalIdentity);
        Assert.Equal(secondIdentity, secondCenter.PhysicalIdentity);
        Assert.NotEqual(firstCenter.PhysicalIdentity, secondCenter.PhysicalIdentity);
    }

    [Fact]
    public void DuplicateCenterIdentityUsesFirstTopLeftOccurrence()
    {
        var builder = CreateBuilder();
        var layout = Layout(
            [Key(0x21), Key(0x22)],
            [Key(0x30), Key(0x31), Key(0x21)]);

        var grid = builder.BuildCustom(
            layout,
            new KeyboardScanCodeIdentity(0x21, false),
            []);

        AssertCells(
            grid.Cells,
            Expected(0x21, 0, 0, isCenter: true),
            Expected(0x22, 0, 1),
            Expected(0x30, 1, 0),
            Expected(0x31, 1, 1));
    }

    [Fact]
    public void CustomBuildPreservesOneAndTwoLayoutSlots()
    {
        var builder = CreateBuilder();
        var layout = Layout([Key(0x21, extended: true)]);
        var centerIdentity = new KeyboardScanCodeIdentity(0x21, true);

        var oneLayoutGrid = builder.BuildCustom(layout, centerIdentity, ["00000409"]);
        var twoLayoutGrid = builder.BuildCustom(
            layout,
            centerIdentity,
            ["00000409", "00000419"]);

        Assert.Equal(
            new KeyboardOverlaySymbolSlot[]
            {
                Slot(1, "00000409", centerIdentity),
            },
            Assert.Single(oneLayoutGrid.Cells).SymbolSlots);
        Assert.Equal(
            new KeyboardOverlaySymbolSlot[]
            {
                Slot(1, "00000409", centerIdentity),
                Slot(2, "00000419", centerIdentity),
            },
            Assert.Single(twoLayoutGrid.Cells).SymbolSlots);
    }

    [Fact]
    public void CustomNamedKeyUsesScanCodeLabelWithoutResolvingSymbols()
    {
        var symbolService = new FakeKeyboardLayoutSymbolService(
            (_, _, _, _) => "invisible-control-character");
        var builder = new KeyboardOverlayGridBuilder(symbolService);
        var layout = Layout([Key(0x0F)]);

        var grid = builder.BuildCustom(
            layout,
            new KeyboardScanCodeIdentity(0x0F, false),
            ["00000409"]);

        var cell = Assert.Single(grid.Cells);
        Assert.Equal("Tab", cell.DisplayLabel);
        Assert.False(cell.IsSpacer);
        Assert.Empty(cell.SymbolSlots);
        Assert.Empty(symbolService.Requests);
    }

    [Fact]
    public void CustomNamedKeyCanUseVirtualKeyWhenScanCodeIsUnknown()
    {
        var symbolService = new FakeKeyboardLayoutSymbolService(
            (_, _, _, _) => "must-not-be-resolved");
        var builder = new KeyboardOverlayGridBuilder(symbolService);
        var layout = Layout([Key(0x7F, virtualKey: 0x7C)]);

        var grid = builder.BuildCustom(
            layout,
            new KeyboardScanCodeIdentity(0x7F, false),
            ["00000409"]);

        var cell = Assert.Single(grid.Cells);
        Assert.Equal("F13", cell.DisplayLabel);
        Assert.False(cell.IsSpacer);
        Assert.Empty(cell.SymbolSlots);
        Assert.Empty(symbolService.Requests);
    }

    [Fact]
    public void CustomPreviewUsesKeyWithMostRealNeighbors()
    {
        var builder = CreateBuilder();
        var layout = Layout(
            [Key(0x10), Key(0x11), Key(0x12)],
            [Key(0x20), Key(0x21), Key(0x22)],
            [Key(0x30), Key(0x31), Key(0x32)]);

        var grid = builder.BuildCustomPreview(layout, []);

        var center = Assert.Single(grid.Cells, cell => cell.IsCenter);
        Assert.Equal(new KeyboardScanCodeIdentity(0x21, false), center.PhysicalIdentity);
        Assert.Equal(9, grid.Cells.Count);
    }

    [Fact]
    public void CustomPreviewTieUsesFirstTopLeftKey()
    {
        var builder = CreateBuilder();
        var layout = Layout([Key(0x10), Key(0x11)]);

        var grid = builder.BuildCustomPreview(layout, []);

        var center = Assert.Single(grid.Cells, cell => cell.IsCenter);
        Assert.Equal(new KeyboardScanCodeIdentity(0x10, false), center.PhysicalIdentity);
    }

    [Fact]
    public void CustomPreviewWithOneRealKeyBuildsSingleCell()
    {
        var builder = CreateBuilder();
        var layout = Layout([Key(0x21, extended: true)]);

        var grid = builder.BuildCustomPreview(layout, []);

        AssertCells(
            grid.Cells,
            Expected(0x21, 0, 0, isCenter: true, extended: true));
    }

    [Fact]
    public void CustomPreviewWithoutRealKeysBuildsEmptyGrid()
    {
        var symbolService = new FakeKeyboardLayoutSymbolService(FormatSymbol);
        var builder = new KeyboardOverlayGridBuilder(symbolService);
        var layout = Layout(
            [Empty(), Empty(width: 2)],
            []);

        var grid = builder.BuildCustomPreview(layout, ["00000409"]);

        Assert.Empty(grid.Cells);
        Assert.Empty(symbolService.Requests);
    }

    [Fact]
    public void CustomPreviewDoesNotCountSpacerNeighbors()
    {
        var builder = CreateBuilder();
        var layout = Layout(
            [Empty(), Empty(), Empty()],
            [Empty(), Key(0x10), Empty()],
            [Empty(), Empty(), Empty()],
            [Key(0x20), Key(0x21), Key(0x22)],
            [Key(0x30), Key(0x31), Key(0x32)]);

        var grid = builder.BuildCustomPreview(layout, []);

        var center = Assert.Single(grid.Cells, cell => cell.IsCenter);
        Assert.Equal(new KeyboardScanCodeIdentity(0x21, false), center.PhysicalIdentity);
    }

    private static KeyboardOverlayGridBuilder CreateBuilder() =>
        new(new FakeKeyboardLayoutSymbolService(FormatSymbol));

    private static CustomKeyboardLayout Layout(params CustomKeyboardCell[][] rows) =>
        new()
        {
            Rows = rows
                .Select(cells => new CustomKeyboardRow { Cells = cells })
                .ToArray(),
        };

    private static CustomKeyboardCell Key(
        int scanCode,
        int width = 1,
        bool extended = false,
        int? virtualKey = null,
        bool isAltGr = false) =>
        new()
        {
            WidthUnits = width,
            Key = new CustomKeyboardKey
            {
                ScanCode = scanCode,
                Extended = extended,
                VirtualKey = virtualKey,
                IsAltGr = isAltGr,
            },
        };

    private static CustomKeyboardCell Empty(int width = 1) =>
        new() { WidthUnits = width };

    private static void AssertCells(
        IReadOnlyList<KeyboardOverlayGridCell> actualCells,
        params ExpectedCell[] expectedCells)
    {
        Assert.Equal(expectedCells.Length, actualCells.Count);

        for (var index = 0; index < expectedCells.Length; index++)
        {
            var expected = expectedCells[index];
            var actual = actualCells[index];

            KeyboardScanCodeIdentity? expectedIdentity = expected.ScanCode is { } scanCode
                ? new KeyboardScanCodeIdentity(scanCode, expected.Extended)
                : null;

            Assert.Equal(expectedIdentity, actual.PhysicalIdentity);
            Assert.Equal(new KeyboardGridOffset(expected.Row, expected.Column), actual.Offset);
            Assert.Equal(expected.IsCenter, actual.IsCenter);
        }
    }

    private static ExpectedCell Expected(
        int? scanCode,
        int row,
        int column,
        bool isCenter = false,
        bool extended = false) =>
        new(scanCode, extended, row, column, isCenter);

    private static KeyboardOverlaySymbolSlot Slot(
        int slot,
        string klid,
        KeyboardScanCodeIdentity identity) =>
        new(
            slot,
            klid,
            FormatSymbol(identity.ScanCode, identity.Extended, klid, shiftPressed: false),
            FormatSymbol(identity.ScanCode, identity.Extended, klid, shiftPressed: true));

    private static string FormatSymbol(
        int scanCode,
        bool extended,
        string klid,
        bool shiftPressed) =>
        $"{klid}:{scanCode:X2}:{(extended ? "E" : "N")}:{(shiftPressed ? "S" : "B")}";

    private readonly record struct ExpectedCell(
        int? ScanCode,
        bool Extended,
        int Row,
        int Column,
        bool IsCenter);

    private readonly record struct SymbolRequest(
        int ScanCode,
        bool Extended,
        string Klid,
        bool ShiftPressed);

    private sealed class FakeKeyboardLayoutSymbolService(
        Func<int, bool, string, bool, string> resolve) : IKeyboardLayoutSymbolService
    {
        public List<SymbolRequest> Requests { get; } = [];

        public string GetKeyboardLayoutSymbol(
            int scanCode,
            bool extended,
            string klid,
            bool shiftPressed)
        {
            Requests.Add(new SymbolRequest(scanCode, extended, klid, shiftPressed));
            return resolve(scanCode, extended, klid, shiftPressed);
        }
    }
}

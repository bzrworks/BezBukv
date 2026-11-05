using BezBukv.Core.Keyboard;
using BezBukv.Core.Platform;

namespace BezBukv.Core.Tests;

public sealed class KeyboardOverlayGridBuilderTests
{
    [Theory]
    [InlineData(PhysicalKey.KeyF, 0x21, false)]
    [InlineData(PhysicalKey.AltRight, 0x38, true)]
    [InlineData(PhysicalKey.ControlRight, 0x1D, true)]
    public void PresetPhysicalKeyMapsToExpectedScanCode(
        PhysicalKey key,
        int expectedScanCode,
        bool expectedExtended)
    {
        var mapped = PhysicalKeyScanCodeMapper.TryGetScanCode(key, out var physicalIdentity);

        Assert.True(mapped);
        Assert.Equal(
            new KeyboardScanCodeIdentity(expectedScanCode, expectedExtended),
            physicalIdentity);
    }

    [Fact]
    public void EveryAnsiAndIsoPresetKeyHasScanCodeMapping()
    {
        var presetKeys = Enum.GetValues<PhysicalKey>()
            .Where(key =>
                KeyboardGeometryProfile.PcAnsi.GetNeighbors(key).Count > 0 ||
                KeyboardGeometryProfile.PcIso.GetNeighbors(key).Count > 0);

        Assert.All(
            presetKeys,
            key => Assert.True(
                PhysicalKeyScanCodeMapper.TryGetScanCode(key, out _),
                $"The preset key {key} has no scan-code mapping."));
    }

    [Fact]
    public void PhysicalKeyOutsidePresetMappingReturnsNoTranslation()
    {
        var mapped = PhysicalKeyScanCodeMapper.TryGetScanCode(
            PhysicalKey.F1,
            out var physicalIdentity);

        Assert.False(mapped);
        Assert.Equal(default, physicalIdentity);
    }

    [Theory]
    [InlineData(0x01, false, null, PhysicalKey.Escape, "Esc")]
    [InlineData(0x0E, false, null, PhysicalKey.Backspace, "⌫")]
    [InlineData(0x0F, false, null, PhysicalKey.Tab, "Tab")]
    [InlineData(0x1C, false, null, PhysicalKey.Enter, "Enter")]
    [InlineData(0x1D, true, null, PhysicalKey.ControlRight, "Ctrl")]
    [InlineData(0x2A, false, null, PhysicalKey.ShiftLeft, "Shift")]
    [InlineData(0x38, false, null, PhysicalKey.AltLeft, "Alt")]
    [InlineData(0x39, false, null, PhysicalKey.Space, "Space")]
    [InlineData(0x3A, false, null, PhysicalKey.CapsLock, "Caps")]
    [InlineData(0x3B, false, null, PhysicalKey.F1, "F1")]
    [InlineData(0x48, true, null, PhysicalKey.ArrowUp, "Up")]
    [InlineData(0x49, true, null, PhysicalKey.PageUp, "Page Up")]
    [InlineData(0x51, false, null, PhysicalKey.Numpad3, "3")]
    [InlineData(0x5B, true, null, PhysicalKey.MetaLeft, "Win")]
    [InlineData(0x5D, true, null, PhysicalKey.ContextMenu, "Menu")]
    [InlineData(0x7F, false, 0x87, PhysicalKey.F24, "F24")]
    public void SavedNamedKeyMapsToExpectedFixedLabel(
        int scanCode,
        bool extended,
        int? virtualKey,
        PhysicalKey expectedKey,
        string expectedLabel)
    {
        var mapped = PhysicalKeyScanCodeMapper.TryGetNamedPhysicalKey(
            new KeyboardScanCodeIdentity(scanCode, extended),
            virtualKey,
            out var key);

        Assert.True(mapped);
        Assert.Equal(expectedKey, key);
        Assert.Equal(expectedLabel, KeyboardKeycapLegend.GetNamedLabel(key));
    }

    [Fact]
    public void SavedPrintableKeyDoesNotMapToNamedKey()
    {
        var mapped = PhysicalKeyScanCodeMapper.TryGetNamedPhysicalKey(
            new KeyboardScanCodeIdentity(0x21, false),
            virtualKey: 0x46,
            out var key);

        Assert.False(mapped);
        Assert.Equal(PhysicalKey.Unknown, key);
    }

    [Fact]
    public void AnsiCenterBuildsCenterAndNeighborsWithBothLayoutSlots()
    {
        var symbolService = new FakeKeyboardLayoutSymbolService(FormatSymbol);
        var builder = new KeyboardOverlayGridBuilder(symbolService);

        var grid = builder.BuildPreset(
            KeyboardGeometryProfile.PcAnsi,
            PhysicalKey.KeyF,
            ["00000409", "00000419"]);

        var expectedCells = new ExpectedCell[]
        {
            Cell(0x21, 0, 0, isCenter: true),
            Cell(0x12, -1, -1),
            Cell(0x13, -1, 0),
            Cell(0x14, -1, 1),
            Cell(0x20, 0, -1),
            Cell(0x22, 0, 1),
            Cell(0x2E, 1, -1),
            Cell(0x2F, 1, 0),
            Cell(0x30, 1, 1),
        };

        Assert.Equal(expectedCells.Length, grid.Cells.Count);
        for (var index = 0; index < expectedCells.Length; index++)
        {
            var expected = expectedCells[index];
            var actual = grid.Cells[index];

            Assert.Equal(new KeyboardScanCodeIdentity(expected.ScanCode, false), actual.PhysicalIdentity);
            Assert.Equal(new KeyboardGridOffset(expected.Row, expected.Column), actual.Offset);
            Assert.Equal(expected.IsCenter, actual.IsCenter);
            Assert.Null(actual.DisplayLabel);
            Assert.False(actual.IsSpacer);
            Assert.Equal(
                new KeyboardOverlaySymbolSlot[]
                {
                    new(
                        1,
                        "00000409",
                        FormatSymbol(expected.ScanCode, false, "00000409", false),
                        FormatSymbol(expected.ScanCode, false, "00000409", true)),
                    new(
                        2,
                        "00000419",
                        FormatSymbol(expected.ScanCode, false, "00000419", false),
                        FormatSymbol(expected.ScanCode, false, "00000419", true)),
                },
                actual.SymbolSlots);
        }

        Assert.Equal(expectedCells.Length * 2 * 2, symbolService.Requests.Count);
    }

    [Fact]
    public void OneLayoutCreatesOnlySlotOneForSymbolicCells()
    {
        var builder = new KeyboardOverlayGridBuilder(
            new FakeKeyboardLayoutSymbolService(FormatSymbol));

        var grid = builder.BuildPreset(
            KeyboardGeometryProfile.PcIso,
            PhysicalKey.Backslash,
            ["00000409"]);

        Assert.All(
            grid.Cells,
            cell =>
            {
                if (cell.DisplayLabel is not null)
                {
                    Assert.Empty(cell.SymbolSlots);
                    return;
                }

                var slot = Assert.Single(cell.SymbolSlots);
                Assert.Equal(1, slot.Slot);
                Assert.Equal("00000409", slot.Klid);
            });
    }

    [Fact]
    public void NoLayoutsCreateNoSymbolSlots()
    {
        var symbolService = new FakeKeyboardLayoutSymbolService(FormatSymbol);
        var builder = new KeyboardOverlayGridBuilder(symbolService);

        var grid = builder.BuildPreset(
            KeyboardGeometryProfile.PcAnsi,
            PhysicalKey.KeyF,
            []);

        Assert.All(grid.Cells, cell => Assert.Empty(cell.SymbolSlots));
        Assert.Empty(symbolService.Requests);
    }

    [Fact]
    public void EmptyResolverResultsRemainEmptyWithoutFailure()
    {
        var builder = new KeyboardOverlayGridBuilder(
            new FakeKeyboardLayoutSymbolService((_, _, _, _) => string.Empty));

        var grid = builder.Build(
            [
                new KeyboardOverlayCellDefinition(
                    new KeyboardScanCodeIdentity(0x21, false),
                    new KeyboardGridOffset(0, 0),
                    IsCenter: true),
            ],
            ["00000409"]);

        var cell = Assert.Single(grid.Cells);
        Assert.Null(cell.DisplayLabel);
        Assert.False(cell.IsSpacer);
        var slot = Assert.Single(cell.SymbolSlots);
        Assert.Empty(slot.BaseSymbol);
        Assert.Empty(slot.ShiftSymbol);
    }

    [Fact]
    public void FunctionKeyBuildsSingleLabeledCenterWithoutResolvingSymbols()
    {
        var symbolService = new FakeKeyboardLayoutSymbolService(
            (_, _, _, _) => throw new InvalidOperationException("Unmapped keys must not be resolved."));
        var builder = new KeyboardOverlayGridBuilder(symbolService);

        var grid = builder.BuildPreset(
            KeyboardGeometryProfile.PcAnsi,
            PhysicalKey.F1,
            ["00000409", "00000419"]);

        var cell = Assert.Single(grid.Cells);
        Assert.Null(cell.PhysicalIdentity);
        Assert.Equal(new KeyboardGridOffset(0, 0), cell.Offset);
        Assert.True(cell.IsCenter);
        Assert.Equal("F1", cell.DisplayLabel);
        Assert.False(cell.IsSpacer);
        Assert.Empty(cell.SymbolSlots);
        Assert.Empty(symbolService.Requests);
    }

    [Fact]
    public void NamedPresetKeyUsesFixedLabelBeforeLayoutSymbols()
    {
        var symbolService = new FakeKeyboardLayoutSymbolService(
            (_, _, _, _) => "invisible-control-character");
        var builder = new KeyboardOverlayGridBuilder(symbolService);

        var grid = builder.BuildPreset(
            KeyboardGeometryProfile.PcAnsi,
            PhysicalKey.Tab,
            ["00000409"]);

        var center = Assert.Single(grid.Cells, cell => cell.IsCenter);
        Assert.Equal("Tab", center.DisplayLabel);
        Assert.False(center.IsSpacer);
        Assert.Empty(center.SymbolSlots);
        Assert.DoesNotContain(
            symbolService.Requests,
            request => request.ScanCode == 0x0F && !request.Extended);
    }

    private static string FormatSymbol(
        int scanCode,
        bool extended,
        string klid,
        bool shiftPressed) =>
        $"{klid}:{scanCode:X2}:{(extended ? "E" : "N")}:{(shiftPressed ? "S" : "B")}";

    private static ExpectedCell Cell(
        int scanCode,
        int row,
        int column,
        bool isCenter = false) =>
        new(scanCode, row, column, isCenter);

    private readonly record struct ExpectedCell(
        int ScanCode,
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

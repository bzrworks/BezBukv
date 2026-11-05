using BezBukv.Core.Platform;

namespace BezBukv.Core.Tests;

public sealed class InstalledKeyboardLayoutCatalogTests
{
    [Fact]
    public void NormalizesKlidToLowercaseEightDigitHexadecimal()
    {
        var layouts = InstalledKeyboardLayoutCatalog.Normalize(
            [new InstalledKeyboardLayout(" 419 ", "Russian")]);

        var layout = Assert.Single(layouts);
        Assert.Equal("00000419", layout.Klid);
        Assert.Equal("Russian", layout.DisplayName);
    }

    [Fact]
    public void DeduplicatesEquivalentKlids()
    {
        var layouts = InstalledKeyboardLayoutCatalog.Normalize(
        [
            new InstalledKeyboardLayout("00000409", string.Empty),
            new InstalledKeyboardLayout("00000409", "US"),
        ]);

        var layout = Assert.Single(layouts);
        Assert.Equal("00000409", layout.Klid);
        Assert.Equal("US", layout.DisplayName);
    }

    [Fact]
    public void OrdersLayoutsByNormalizedKlid()
    {
        var layouts = InstalledKeyboardLayoutCatalog.Normalize(
        [
            new InstalledKeyboardLayout("00000419", "Russian"),
            new InstalledKeyboardLayout("00000409", "US"),
            new InstalledKeyboardLayout("00010409", "United States-Dvorak"),
        ]);

        Assert.Equal(
            ["00000409", "00000419", "00010409"],
            layouts.Select(layout => layout.Klid));
    }

    [Fact]
    public void FallsBackToKlidWhenDisplayNameIsBlank()
    {
        var layouts = InstalledKeyboardLayoutCatalog.Normalize(
            [new InstalledKeyboardLayout("00000409", "  ")]);

        var layout = Assert.Single(layouts);
        Assert.Equal("00000409", layout.DisplayName);
    }

    [Fact]
    public void EmptyInputProducesEmptyCatalog()
    {
        var layouts = InstalledKeyboardLayoutCatalog.Normalize([]);

        Assert.Empty(layouts);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-klid")]
    [InlineData("000000000")]
    public void OmitsInvalidKlids(string invalidKlid)
    {
        var layouts = InstalledKeyboardLayoutCatalog.Normalize(
            [new InstalledKeyboardLayout(invalidKlid, "Invalid")]);

        Assert.Empty(layouts);
    }
}

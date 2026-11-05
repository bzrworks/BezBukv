using BezBukv.Core.Keyboard;

namespace BezBukv.Core.Tests;

public sealed class KeyboardKeySymbolsTests
{
    [Fact]
    public void SlashKeepsFourDistinctSymbolsInLayoutAndShiftOrder()
    {
        var symbols = new KeyboardKeySymbols("/", "?", ".", ",");

        Assert.Equal(
            new[] { "/", "?", ".", "," },
            symbols.GetDistinctSymbols());
    }

    [Fact]
    public void Digit3CollapsesSharedBaseAndKeepsThreeDistinctSymbols()
    {
        var symbols = new KeyboardKeySymbols("3", "#", "3", "№");

        Assert.Equal(
            new[] { "3", "#", "№" },
            symbols.GetDistinctSymbols());
    }

    [Fact]
    public void LetterCollapsesShiftVariantsWithoutCaseSensitivity()
    {
        var symbols = new KeyboardKeySymbols("A", "a", "Ф", "ф");

        Assert.Equal(
            new[] { "A", "Ф" },
            symbols.GetDistinctSymbols());
    }

    [Fact]
    public void EmptySymbolsAreOmitted()
    {
        var symbols = new KeyboardKeySymbols("/", string.Empty, ".", string.Empty);

        Assert.Equal(
            new[] { "/", "." },
            symbols.GetDistinctSymbols());
    }
}

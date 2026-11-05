using BezBukv.Core.Keyboard;
using BezBukv.Core.Platform;

namespace BezBukv.Core.Tests;

public sealed class KeyboardKeycapLegendTests
{
    [Theory]
    [InlineData(PhysicalKey.KeyA, "A")]
    [InlineData(PhysicalKey.KeyF, "F")]
    [InlineData(PhysicalKey.KeyZ, "Z")]
    public void LettersUseUppercaseLatinEngravings(PhysicalKey key, string expected)
    {
        Assert.Equal(expected, KeyboardKeycapLegend.GetLatinEngraving(key));
    }

    [Theory]
    [InlineData(PhysicalKey.Digit1, "1")]
    [InlineData(PhysicalKey.Digit5, "5")]
    [InlineData(PhysicalKey.Digit0, "0")]
    public void DigitRowUsesUnshiftedEngravings(PhysicalKey key, string expected)
    {
        Assert.Equal(expected, KeyboardKeycapLegend.GetLatinEngraving(key));
    }

    [Theory]
    [InlineData(PhysicalKey.Backquote, "`")]
    [InlineData(PhysicalKey.Minus, "-")]
    [InlineData(PhysicalKey.Equal, "=")]
    [InlineData(PhysicalKey.BracketLeft, "[")]
    [InlineData(PhysicalKey.BracketRight, "]")]
    [InlineData(PhysicalKey.Backslash, "\\")]
    [InlineData(PhysicalKey.Semicolon, ";")]
    [InlineData(PhysicalKey.Quote, "'")]
    [InlineData(PhysicalKey.Comma, ",")]
    [InlineData(PhysicalKey.Period, ".")]
    [InlineData(PhysicalKey.Slash, "/")]
    public void PunctuationUsesUnshiftedUsAnsiEngravings(
        PhysicalKey key,
        string expected)
    {
        Assert.Equal(expected, KeyboardKeycapLegend.GetLatinEngraving(key));
    }

    [Theory]
    [InlineData(PhysicalKey.Digit1, "!")]
    [InlineData(PhysicalKey.Digit2, "@")]
    [InlineData(PhysicalKey.Digit3, "#")]
    [InlineData(PhysicalKey.Digit4, "$")]
    [InlineData(PhysicalKey.Digit5, "%")]
    [InlineData(PhysicalKey.Digit6, "^")]
    [InlineData(PhysicalKey.Digit7, "&")]
    [InlineData(PhysicalKey.Digit8, "*")]
    [InlineData(PhysicalKey.Digit9, "(")]
    [InlineData(PhysicalKey.Digit0, ")")]
    public void DigitRowUsesShiftedUsAnsiEngravings(PhysicalKey key, string expected)
    {
        Assert.Equal(expected, KeyboardKeycapLegend.GetLatinShiftEngraving(key));
    }

    [Theory]
    [InlineData(PhysicalKey.Backquote, "~")]
    [InlineData(PhysicalKey.Minus, "_")]
    [InlineData(PhysicalKey.Equal, "+")]
    [InlineData(PhysicalKey.BracketLeft, "{")]
    [InlineData(PhysicalKey.BracketRight, "}")]
    [InlineData(PhysicalKey.Backslash, "|")]
    [InlineData(PhysicalKey.Semicolon, ":")]
    [InlineData(PhysicalKey.Quote, "\"")]
    [InlineData(PhysicalKey.Comma, "<")]
    [InlineData(PhysicalKey.Period, ">")]
    [InlineData(PhysicalKey.Slash, "?")]
    public void PunctuationUsesShiftedUsAnsiEngravings(
        PhysicalKey key,
        string expected)
    {
        Assert.Equal(expected, KeyboardKeycapLegend.GetLatinShiftEngraving(key));
    }

    [Theory]
    [InlineData(PhysicalKey.KeyA, "A")]
    [InlineData(PhysicalKey.KeyF, "F")]
    [InlineData(PhysicalKey.KeyZ, "Z")]
    public void LettersUseSameUppercaseEngravingWithShift(PhysicalKey key, string expected)
    {
        Assert.Equal(expected, KeyboardKeycapLegend.GetLatinShiftEngraving(key));
    }

    [Theory]
    [InlineData(PhysicalKey.Tab)]
    [InlineData(PhysicalKey.F1)]
    [InlineData(PhysicalKey.Numpad3)]
    public void KeysWithoutShiftSymbolsReturnEmpty(PhysicalKey key)
    {
        Assert.Empty(KeyboardKeycapLegend.GetLatinShiftEngraving(key));
    }

    [Theory]
    [InlineData(PhysicalKey.Tab, "Tab")]
    [InlineData(PhysicalKey.CapsLock, "Caps")]
    [InlineData(PhysicalKey.ShiftLeft, "Shift")]
    [InlineData(PhysicalKey.ShiftRight, "Shift")]
    public void EdgeKeysUseShortLabels(PhysicalKey key, string expected)
    {
        Assert.Equal(expected, KeyboardKeycapLegend.GetLatinEngraving(key));
    }

    [Fact]
    public void UnknownKeyHasNoEngraving()
    {
        Assert.Equal(
            string.Empty,
            KeyboardKeycapLegend.GetLatinEngraving(PhysicalKey.Unknown));
    }

    [Theory]
    [InlineData(PhysicalKey.Tab, "Tab")]
    [InlineData(PhysicalKey.F24, "F24")]
    [InlineData(PhysicalKey.PrintScreen, "Print Screen")]
    [InlineData(PhysicalKey.ScrollLock, "Scroll Lock")]
    [InlineData(PhysicalKey.Backspace, "⌫")]
    [InlineData(PhysicalKey.Insert, "Insert")]
    [InlineData(PhysicalKey.PageUp, "Page Up")]
    [InlineData(PhysicalKey.Delete, "Delete")]
    [InlineData(PhysicalKey.PageDown, "Page Down")]
    [InlineData(PhysicalKey.NumLock, "Num Lock")]
    [InlineData(PhysicalKey.Numpad3, "3")]
    public void NamedKeysExposeFixedLabels(PhysicalKey key, string expected)
    {
        Assert.Equal(expected, KeyboardKeycapLegend.GetNamedLabel(key));
    }

    [Theory]
    [InlineData(PhysicalKey.KeyA)]
    [InlineData(PhysicalKey.Digit1)]
    [InlineData(PhysicalKey.Semicolon)]
    public void PrintableKeysDoNotExposeNamedLabels(PhysicalKey key)
    {
        Assert.Empty(KeyboardKeycapLegend.GetNamedLabel(key));
    }

    [Fact]
    public void EveryKnownPhysicalKeyHasAnEngravingOrLabel()
    {
        var knownKeys = Enum.GetValues<PhysicalKey>()
            .Where(key => key != PhysicalKey.Unknown);

        Assert.All(
            knownKeys,
            key => Assert.NotEmpty(KeyboardKeycapLegend.GetLatinEngraving(key)));
    }
}

using BezBukv.Core.Keyboard;

namespace BezBukv.Core.Tests;

public sealed class KeyboardVirtualKeyLabelTests
{
    [Theory]
    [InlineData(0x70, "F1")]
    [InlineData(0x78, "F9")]
    [InlineData(0x87, "F24")]
    [InlineData(0x08, "⌫")]
    [InlineData(0x14, "Caps")]
    [InlineData(0x25, "Left")]
    [InlineData(0x26, "Up")]
    [InlineData(0x27, "Right")]
    [InlineData(0x28, "Down")]
    [InlineData(0x2C, "Print Screen")]
    [InlineData(0x21, "Page Up")]
    [InlineData(0x22, "Page Down")]
    [InlineData(0x91, "Scroll Lock")]
    [InlineData(0x90, "Num Lock")]
    [InlineData(0x24, "Home")]
    [InlineData(0x23, "End")]
    public void KnownNonPrintableKeysReturnExpectedLabels(
        int virtualKey,
        string expectedLabel)
    {
        Assert.Equal(expectedLabel, KeyboardVirtualKeyLabel.GetLabel(virtualKey));
    }

    [Theory]
    [InlineData(0xA5, true, "AltGr")]
    [InlineData(0xA5, false, "Alt")]
    [InlineData(0xA4, true, "Alt")]
    [InlineData(0xA2, true, "Ctrl")]
    public void AltGrLabelRequiresMarkedRightAlt(
        int virtualKey,
        bool isAltGr,
        string expectedLabel)
    {
        Assert.Equal(
            expectedLabel,
            KeyboardVirtualKeyLabel.GetLabel(virtualKey, isAltGr));
    }

    [Theory]
    [InlineData(0x30)]
    [InlineData(0x39)]
    [InlineData(0x41)]
    [InlineData(0x5A)]
    public void AlphanumericKeysReturnEmpty(int virtualKey)
    {
        Assert.Empty(KeyboardVirtualKeyLabel.GetLabel(virtualKey));
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0xFF)]
    [InlineData(-1)]
    public void UnknownVirtualKeysReturnEmptyWithoutThrowing(int virtualKey)
    {
        Assert.Empty(KeyboardVirtualKeyLabel.GetLabel(virtualKey));
    }
}

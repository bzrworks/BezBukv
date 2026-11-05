using BezBukv.Core.Settings;

namespace BezBukv.Core.Tests;

public sealed class AppSettingsNormalizerTests
{
    [Fact]
    public void CustomProfileWithoutValidLayoutNormalizesToUnknown()
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            General = defaults.General with
            {
                Autostart = true,
            },
            Peek = defaults.Peek with
            {
                KeyboardProfile = KeyboardProfile.Custom,
                CustomKeyboardLayout = null,
                AutoPeekEnabled = true,
                AutoPeekLingerMs = 875,
            },
        };

        var normalized = AppSettingsNormalizer.NormalizeCustomProfile(settings);

        Assert.Equal(KeyboardProfile.Unknown, normalized.Peek.KeyboardProfile);
        Assert.Null(normalized.Peek.CustomKeyboardLayout);
        Assert.True(normalized.Peek.AutoPeekEnabled);
        Assert.Equal(875, normalized.Peek.AutoPeekLingerMs);
        Assert.Same(settings.General, normalized.General);
        Assert.Same(settings.Correction, normalized.Correction);
    }

    [Fact]
    public void CustomProfileWithValidLayoutIsNotChanged()
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            Peek = defaults.Peek with
            {
                KeyboardProfile = KeyboardProfile.Custom,
                CustomKeyboardLayout = new CustomKeyboardLayout
                {
                    Rows =
                    [
                        new CustomKeyboardRow
                        {
                            Cells =
                            [
                                new CustomKeyboardCell
                                {
                                    Key = new CustomKeyboardKey { ScanCode = 0x1E },
                                },
                            ],
                        },
                    ],
                },
            },
        };

        var normalized = AppSettingsNormalizer.NormalizeCustomProfile(settings);

        Assert.Same(settings, normalized);
    }
}

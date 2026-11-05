using BezBukv.Core.Platform;

namespace BezBukv.Core.Settings;

public static class AppSettingsDefaults
{
    public static AppSettings Create() => new();

    internal static GlobalHotkey CreateCorrectionHotkey() =>
        new(HotkeyKey.F7, new[] { HotkeyModifier.Primary });

    internal static GlobalHotkey CreatePeekHotkey() =>
        new(HotkeyKey.F8, new[] { HotkeyModifier.Primary });
}

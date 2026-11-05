namespace BezBukv.Core.Settings;

public static class AppSettingsNormalizer
{
    public static AppSettings NormalizeCustomProfile(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var peek = settings.Peek;

        if (peek is null
            || peek.KeyboardProfile != KeyboardProfile.Custom
            || IsValidCustomLayout(peek.CustomKeyboardLayout))
        {
            return settings;
        }

        return settings with
        {
            Peek = peek with
            {
                KeyboardProfile = KeyboardProfile.Unknown,
                CustomKeyboardLayout = null,
            },
        };
    }

    internal static bool IsValidCustomLayout(CustomKeyboardLayout? layout)
    {
        if (layout?.Rows is not { Count: > 0 } rows)
        {
            return false;
        }

        var hasKey = false;

        foreach (var row in rows)
        {
            if (row?.Cells is not { } cells)
            {
                return false;
            }

            foreach (var cell in cells)
            {
                if (cell is null || cell.WidthUnits < 1)
                {
                    return false;
                }

                if (cell.Key is not null)
                {
                    hasKey = true;
                }
            }
        }

        return hasKey;
    }
}

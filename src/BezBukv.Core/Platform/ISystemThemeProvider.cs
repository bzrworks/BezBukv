using BezBukv.Core.Settings;

namespace BezBukv.Core.Platform;

public interface ISystemThemeProvider : IDisposable
{
    event EventHandler? ThemeChanged;

    bool IsDarkTheme { get; }

    void SetTheme(AppTheme theme);
}

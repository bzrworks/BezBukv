using BezBukv.Core.Platform;
using BezBukv.Core.Settings;
using Microsoft.Win32;

namespace BezBukv.Windows;

public sealed class WindowsSystemThemeProvider : ISystemThemeProvider
{
    private const string WindowsThemeRegistryPath =
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string WindowsAppsUseLightThemeValue = "AppsUseLightTheme";

    private AppTheme _selectedTheme = AppTheme.System;
    private bool _disposed;

    public WindowsSystemThemeProvider()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public event EventHandler? ThemeChanged;

    public bool IsDarkTheme
    {
        get
        {
            if (_selectedTheme != AppTheme.System)
            {
                return _selectedTheme == AppTheme.Dark;
            }

            try
            {
                return Registry.GetValue(
                    WindowsThemeRegistryPath,
                    WindowsAppsUseLightThemeValue,
                    1) is int value
                    && value == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public void SetTheme(AppTheme theme)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Enum.IsDefined(theme))
        {
            throw new ArgumentOutOfRangeException(nameof(theme));
        }

        if (_selectedTheme == theme)
        {
            return;
        }

        _selectedTheme = theme;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        ThemeChanged = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_selectedTheme == AppTheme.System
            && e.Category is UserPreferenceCategory.Color
            or UserPreferenceCategory.General
            or UserPreferenceCategory.VisualStyle)
        {
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

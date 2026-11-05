using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using BezBukv.App.Resources;
using BezBukv.Core.Localization;
using BezBukv.Core.Platform;

namespace BezBukv.App;

public partial class GuideWindow : Window, INotifyPropertyChanged
{
    private const int DwmWindowCornerPreferenceAttribute = 33;
    private const int DwmWindowCornerPreferenceRound = 2;
    private const string BrandMutedBrushKey = "SettingsBrandMutedBrush";
    private const string BrandMutedLightColorKey = "SettingsBrandMutedLightColor";
    private const string BrandMutedDarkColorKey = "SettingsBrandMutedDarkColor";

    private readonly ILanguageService _languageService;
    private readonly ISystemThemeProvider _systemThemeProvider;
    private bool _isListeningForThemeChanges;

    public GuideWindow(
        ILanguageService languageService,
        ISystemThemeProvider systemThemeProvider)
    {
        ArgumentNullException.ThrowIfNull(languageService);
        ArgumentNullException.ThrowIfNull(systemThemeProvider);

        _languageService = languageService;
        _systemThemeProvider = systemThemeProvider;

        InitializeComponent();
        ApplyBrandPalette();
        _languageService.LanguageChanged += OnLanguageChanged;
        _systemThemeProvider.ThemeChanged += OnSystemThemeChanged;
        _isListeningForThemeChanges = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string resourceKey] =>
        AppStrings.ResourceManager.GetString(
            resourceKey,
            CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException(
            $"Missing localized resource '{resourceKey}'.");

    internal void ShowAndActivate()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Topmost = true;
        Activate();
        Topmost = false;
        Focus();
    }

    protected override void OnClosed(EventArgs e)
    {
        _languageService.LanguageChanged -= OnLanguageChanged;

        if (_isListeningForThemeChanges)
        {
            _systemThemeProvider.ThemeChanged -= OnSystemThemeChanged;
            _isListeningForThemeChanges = false;
        }

        base.OnClosed(e);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var windowHandle = new WindowInteropHelper(this).Handle;
        var cornerPreference = DwmWindowCornerPreferenceRound;
        _ = DwmSetWindowAttribute(
            windowHandle,
            DwmWindowCornerPreferenceAttribute,
            ref cornerPreference,
            sizeof(int));
    }

    private void OnHeaderMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && !CloseButton.IsMouseOver)
        {
            DragMove();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Close();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(RefreshLocalizedStrings);
            return;
        }

        RefreshLocalizedStrings();
    }

    private void RefreshLocalizedStrings()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    private void OnSystemThemeChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(ApplyBrandPalette);
            return;
        }

        ApplyBrandPalette();
    }

    private void ApplyBrandPalette()
    {
        var colorKey = _systemThemeProvider.IsDarkTheme
            ? BrandMutedDarkColorKey
            : BrandMutedLightColorKey;

        Resources[BrandMutedBrushKey] = CreateFrozenBrush(
            (Color)FindResource(colorKey));
    }

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}

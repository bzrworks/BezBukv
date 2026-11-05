using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using BezBukv.App.Resources;
using BezBukv.Core.Localization;
using BezBukv.Core.Platform;
using BezBukv.Core.Settings;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DrawingRectangle = System.Drawing.Rectangle;

namespace BezBukv.App;

public readonly record struct CorrectionHotkeyRegistrationResult(
    GlobalHotkeyRegistrationStatus RegistrationStatus,
    bool HadPreviousRegistration,
    bool PreviousRegistrationRestored)
{
    internal bool Succeeded =>
        RegistrationStatus == GlobalHotkeyRegistrationStatus.Success;
}

public enum CorrectionHotkeyInternalConflict
{
    None,
    Peek,
    Diagnostics,
}

public readonly record struct PeekHotkeyRegistrationResult(
    GlobalHotkeyRegistrationStatus RegistrationStatus,
    bool HadPreviousRegistration,
    bool PreviousRegistrationRestored)
{
    internal bool Succeeded =>
        RegistrationStatus == GlobalHotkeyRegistrationStatus.Success;
}

public enum PeekHotkeyInternalConflict
{
    None,
    Correction,
    Diagnostics,
}

public static class GuideInlineText
{
    private const string BoldMarker = "**";
    private const string CodeMarker = "`";
    private static readonly FontFamily CodeFontFamily = new("Consolas");

    public static readonly DependencyProperty MarkupProperty =
        DependencyProperty.RegisterAttached(
            "Markup",
            typeof(string),
            typeof(GuideInlineText),
            new PropertyMetadata(string.Empty, OnMarkupChanged));

    public static string GetMarkup(DependencyObject element) =>
        (string)element.GetValue(MarkupProperty);

    public static void SetMarkup(DependencyObject element, string value) =>
        element.SetValue(MarkupProperty, value);

    private static void OnMarkupChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is TextBlock textBlock)
        {
            Render(textBlock, eventArgs.NewValue as string ?? string.Empty);
        }
    }

    private static void Render(TextBlock textBlock, string markup)
    {
        textBlock.Inlines.Clear();
        var plainText = new StringBuilder();

        for (var index = 0; index < markup.Length;)
        {
            if (markup[index] == '\\' &&
                index + 1 < markup.Length &&
                IsEscapable(markup[index + 1]))
            {
                plainText.Append(markup[index + 1]);
                index += 2;
                continue;
            }

            if (IsMarkerAt(markup, index, BoldMarker) &&
                TryReadMarkedText(
                    markup,
                    index,
                    BoldMarker,
                    out _,
                    out var indexAfterBold))
            {
                FlushPlainText(textBlock, plainText);
                var boldContentIndex = index + BoldMarker.Length;
                var boldMarkup = markup[
                    boldContentIndex..(indexAfterBold - BoldMarker.Length)];
                RenderBold(textBlock, boldMarkup);
                index = indexAfterBold;
                continue;
            }

            if (IsMarkerAt(markup, index, CodeMarker) &&
                TryReadMarkedText(
                    markup,
                    index,
                    CodeMarker,
                    out var codeText,
                    out var indexAfterCode))
            {
                FlushPlainText(textBlock, plainText);
                AddCodeRun(textBlock, codeText, isBold: false);
                index = indexAfterCode;
                continue;
            }

            plainText.Append(markup[index]);
            index++;
        }

        FlushPlainText(textBlock, plainText);
    }

    private static void RenderBold(TextBlock textBlock, string markup)
    {
        var boldText = new StringBuilder();

        for (var index = 0; index < markup.Length;)
        {
            if (markup[index] == '\\' &&
                index + 1 < markup.Length &&
                IsEscapable(markup[index + 1]))
            {
                boldText.Append(markup[index + 1]);
                index += 2;
                continue;
            }

            if (IsMarkerAt(markup, index, CodeMarker) &&
                TryReadMarkedText(
                    markup,
                    index,
                    CodeMarker,
                    out var codeText,
                    out var indexAfterCode))
            {
                FlushBoldText(textBlock, boldText);
                AddCodeRun(textBlock, codeText, isBold: true);
                index = indexAfterCode;
                continue;
            }

            boldText.Append(markup[index]);
            index++;
        }

        FlushBoldText(textBlock, boldText);
    }

    private static bool TryReadMarkedText(
        string markup,
        int markerIndex,
        string marker,
        out string text,
        out int indexAfterText)
    {
        var contentIndex = markerIndex + marker.Length;

        for (var index = contentIndex;
             index <= markup.Length - marker.Length;
             index++)
        {
            if (!IsMarkerAt(markup, index, marker) || IsEscaped(markup, index))
            {
                continue;
            }

            text = Unescape(markup[contentIndex..index]);
            indexAfterText = index + marker.Length;
            return true;
        }

        text = string.Empty;
        indexAfterText = markerIndex;
        return false;
    }

    private static bool IsMarkerAt(string markup, int index, string marker) =>
        index >= 0 &&
        index <= markup.Length - marker.Length &&
        string.CompareOrdinal(markup, index, marker, 0, marker.Length) == 0;

    private static bool IsEscaped(string markup, int index)
    {
        var slashCount = 0;

        for (var current = index - 1;
             current >= 0 && markup[current] == '\\';
             current--)
        {
            slashCount++;
        }

        return slashCount % 2 != 0;
    }

    private static bool IsEscapable(char character) =>
        character is '\\' or '*' or '`';

    private static string Unescape(string text)
    {
        var result = new StringBuilder(text.Length);

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\\' &&
                index + 1 < text.Length &&
                IsEscapable(text[index + 1]))
            {
                index++;
            }

            result.Append(text[index]);
        }

        return result.ToString();
    }

    private static void AddCodeRun(
        TextBlock textBlock,
        string text,
        bool isBold)
    {
        var codeRun = new Run(text)
        {
            FontFamily = CodeFontFamily,
        };

        if (isBold)
        {
            codeRun.FontWeight = FontWeights.Bold;
        }

        codeRun.SetResourceReference(
            TextElement.BackgroundProperty,
            "ControlFillColorSecondaryBrush");
        codeRun.SetResourceReference(
            TextElement.ForegroundProperty,
            "TextFillColorPrimaryBrush");
        textBlock.Inlines.Add(codeRun);
    }

    private static void FlushBoldText(
        TextBlock textBlock,
        StringBuilder boldText)
    {
        if (boldText.Length == 0)
        {
            return;
        }

        var boldRun = new Run(boldText.ToString())
        {
            FontWeight = FontWeights.Bold,
        };
        boldRun.SetResourceReference(
            TextElement.ForegroundProperty,
            "TextFillColorPrimaryBrush");
        textBlock.Inlines.Add(boldRun);
        boldText.Clear();
    }

    private static void FlushPlainText(
        TextBlock textBlock,
        StringBuilder plainText)
    {
        if (plainText.Length == 0)
        {
            return;
        }

        textBlock.Inlines.Add(new Run(plainText.ToString()));
        plainText.Clear();
    }
}

public partial class SettingsWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? GuideWindowRequested;

    public string this[string resourceKey] =>
        AppStrings.ResourceManager.GetString(
            resourceKey,
            CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException(
            $"Missing localized resource '{resourceKey}'.");

    private static readonly Duration PanelTransitionDuration =
        new(TimeSpan.FromMilliseconds(180));
    private static readonly string ApplicationVersion = GetApplicationVersion();

    private const int TextLimitBytesPerThousandCharacters =
        1_000 * ClipboardTextLimit.Utf16BytesPerCodeUnit;
    private const int AdvancedValueErrorFlashDurationMilliseconds = 1300;
    private const double PercentageScale = 100;
    private const double AppearancePreviewMargin = 16;
    private const double PanelScrollPixelsPerWheelDelta = 48d / 120d;
    private const int DwmWindowCornerPreferenceAttribute = 33;
    private const int DwmWindowCornerPreferenceRound = 2;
    private const string BrandAccentBrushKey = "SettingsBrandAccentBrush";
    private const string BrandAccentSoftBrushKey = "SettingsBrandAccentSoftBrush";
    private const string BrandAccentInkBrushKey = "SettingsBrandAccentInkBrush";
    private const string BrandMutedBrushKey = "SettingsBrandMutedBrush";
    private const string DangerBrushKey = "SettingsDangerBrush";
    private const string KeycapShadowColorKey = "SettingsKeycapShadowColor";
    private const string BrandAccentLightColorKey = "SettingsBrandAccentLightColor";
    private const string BrandAccentDarkColorKey = "SettingsBrandAccentDarkColor";
    private const string BrandAccentSoftLightColorKey = "SettingsBrandAccentSoftLightColor";
    private const string BrandAccentSoftDarkColorKey = "SettingsBrandAccentSoftDarkColor";
    private const string BrandAccentInkLightColorKey = "SettingsBrandAccentInkLightColor";
    private const string BrandAccentInkDarkColorKey = "SettingsBrandAccentInkDarkColor";
    private const string BrandMutedLightColorKey = "SettingsBrandMutedLightColor";
    private const string BrandMutedDarkColorKey = "SettingsBrandMutedDarkColor";
    private const string KeycapShadowLightColorKey = "SettingsKeycapShadowLightColor";
    private const string KeycapShadowDarkColorKey = "SettingsKeycapShadowDarkColor";

    private readonly JsonSettingsStore _settingsStore;
    private readonly IAutostartService _autostartService;
    private readonly IOverlayService _overlayService;
    private readonly ISystemThemeProvider _systemThemeProvider;
    private readonly ILanguageService _languageService;
    private readonly Func<GlobalHotkey, CorrectionHotkeyRegistrationResult>
        _tryRegisterCorrectionHotkey;
    private readonly Func<
        EventHandler<KeyboardCaptureEventArgs>,
        KeyboardCaptureStartStatus> _startCorrectionHotkeyRecordingCapture;
    private readonly Action<EventHandler<KeyboardCaptureEventArgs>>
        _stopCorrectionHotkeyRecordingCapture;
    private readonly Func<GlobalHotkey, CorrectionHotkeyInternalConflict>
        _findCorrectionHotkeyInternalConflict;
    private readonly Func<GlobalHotkey, PeekHotkeyRegistrationResult>
        _tryRegisterPeekHotkey;
    private readonly Func<
        EventHandler<KeyboardCaptureEventArgs>,
        KeyboardCaptureStartStatus> _startPeekHotkeyRecordingCapture;
    private readonly Action<EventHandler<KeyboardCaptureEventArgs>>
        _stopPeekHotkeyRecordingCapture;
    private readonly Func<GlobalHotkey, PeekHotkeyInternalConflict>
        _findPeekHotkeyInternalConflict;
    private readonly Action<string, bool> _showNotification;
    private readonly Action _keyboardProfileSelectionRequested;
    private readonly Action _keyboardProfileChangeRequested;
    private readonly Action _retryCorrectionHotkeyRegistration;
    private readonly Action _retryPeekHotkeyRegistration;
    private readonly Action<AppSettings> _settingsChanged;
    private readonly Action<AppSettings> _settingsImported;
    private readonly Action _mainFeatureEnableRequestedWhilePaused;
    private readonly DispatcherTimer _advancedValueErrorTimer;
    private readonly Dictionary<ScrollViewer, double> _panelScrollRemainders = [];

    private AppSettings _settings;
    private AppSettings? _appearancePersistBaseline;
    private TextBox? _advancedValueErrorTextBox;
    private bool _isPaused;
    private bool _isCorrectionHotkeyRegistered;
    private bool _isPeekHotkeyRegistered;
    private bool _isListeningForThemeChanges;
    private bool _isUpdatingControls;
    private bool _isAppearancePreviewActive;
    private bool _isRecordingCorrectionHotkey;
    private bool _isCorrectionHotkeyRecordingCaptureActive;
    private readonly HashSet<PhysicalKey> _pressedCorrectionHotkeyModifierKeys = [];
    private bool _isRecordingPeekHotkey;
    private bool _isPeekHotkeyRecordingCaptureActive;
    private readonly HashSet<PhysicalKey> _pressedPeekHotkeyModifierKeys = [];

    public SettingsWindow(
        JsonSettingsStore settingsStore,
        AppSettings settings,
        bool isPaused,
        bool isCorrectionHotkeyRegistered,
        bool isPeekHotkeyRegistered,
        IAutostartService autostartService,
        IOverlayService overlayService,
        ISystemThemeProvider systemThemeProvider,
        ILanguageService languageService,
        Func<GlobalHotkey, CorrectionHotkeyRegistrationResult>
            tryRegisterCorrectionHotkey,
        Func<EventHandler<KeyboardCaptureEventArgs>, KeyboardCaptureStartStatus>
            startCorrectionHotkeyRecordingCapture,
        Action<EventHandler<KeyboardCaptureEventArgs>>
            stopCorrectionHotkeyRecordingCapture,
        Func<GlobalHotkey, CorrectionHotkeyInternalConflict>
            findCorrectionHotkeyInternalConflict,
        Func<GlobalHotkey, PeekHotkeyRegistrationResult>
            tryRegisterPeekHotkey,
        Func<EventHandler<KeyboardCaptureEventArgs>, KeyboardCaptureStartStatus>
            startPeekHotkeyRecordingCapture,
        Action<EventHandler<KeyboardCaptureEventArgs>>
            stopPeekHotkeyRecordingCapture,
        Func<GlobalHotkey, PeekHotkeyInternalConflict>
            findPeekHotkeyInternalConflict,
        Action<string, bool> showNotification,
        Action keyboardProfileSelectionRequested,
        Action keyboardProfileChangeRequested,
        Action retryCorrectionHotkeyRegistration,
        Action retryPeekHotkeyRegistration,
        Action<AppSettings> settingsChanged,
        Action<AppSettings> settingsImported,
        Action mainFeatureEnableRequestedWhilePaused)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(autostartService);
        ArgumentNullException.ThrowIfNull(overlayService);
        ArgumentNullException.ThrowIfNull(systemThemeProvider);
        ArgumentNullException.ThrowIfNull(languageService);
        ArgumentNullException.ThrowIfNull(tryRegisterCorrectionHotkey);
        ArgumentNullException.ThrowIfNull(startCorrectionHotkeyRecordingCapture);
        ArgumentNullException.ThrowIfNull(stopCorrectionHotkeyRecordingCapture);
        ArgumentNullException.ThrowIfNull(findCorrectionHotkeyInternalConflict);
        ArgumentNullException.ThrowIfNull(tryRegisterPeekHotkey);
        ArgumentNullException.ThrowIfNull(startPeekHotkeyRecordingCapture);
        ArgumentNullException.ThrowIfNull(stopPeekHotkeyRecordingCapture);
        ArgumentNullException.ThrowIfNull(findPeekHotkeyInternalConflict);
        ArgumentNullException.ThrowIfNull(showNotification);
        ArgumentNullException.ThrowIfNull(keyboardProfileSelectionRequested);
        ArgumentNullException.ThrowIfNull(keyboardProfileChangeRequested);
        ArgumentNullException.ThrowIfNull(retryCorrectionHotkeyRegistration);
        ArgumentNullException.ThrowIfNull(retryPeekHotkeyRegistration);
        ArgumentNullException.ThrowIfNull(settingsChanged);
        ArgumentNullException.ThrowIfNull(settingsImported);
        ArgumentNullException.ThrowIfNull(mainFeatureEnableRequestedWhilePaused);

        _settingsStore = settingsStore;
        _settings = settings;
        _isPaused = isPaused;
        _isCorrectionHotkeyRegistered = isCorrectionHotkeyRegistered;
        _isPeekHotkeyRegistered = isPeekHotkeyRegistered;
        _autostartService = autostartService;
        _overlayService = overlayService;
        _systemThemeProvider = systemThemeProvider;
        _languageService = languageService;
        _tryRegisterCorrectionHotkey = tryRegisterCorrectionHotkey;
        _startCorrectionHotkeyRecordingCapture =
            startCorrectionHotkeyRecordingCapture;
        _stopCorrectionHotkeyRecordingCapture =
            stopCorrectionHotkeyRecordingCapture;
        _findCorrectionHotkeyInternalConflict =
            findCorrectionHotkeyInternalConflict;
        _tryRegisterPeekHotkey = tryRegisterPeekHotkey;
        _startPeekHotkeyRecordingCapture = startPeekHotkeyRecordingCapture;
        _stopPeekHotkeyRecordingCapture = stopPeekHotkeyRecordingCapture;
        _findPeekHotkeyInternalConflict = findPeekHotkeyInternalConflict;
        _showNotification = showNotification;
        _keyboardProfileSelectionRequested = keyboardProfileSelectionRequested;
        _keyboardProfileChangeRequested = keyboardProfileChangeRequested;
        _retryCorrectionHotkeyRegistration = retryCorrectionHotkeyRegistration;
        _retryPeekHotkeyRegistration = retryPeekHotkeyRegistration;
        _settingsChanged = settingsChanged;
        _settingsImported = settingsImported;
        _mainFeatureEnableRequestedWhilePaused =
            mainFeatureEnableRequestedWhilePaused;
        _advancedValueErrorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(
                AdvancedValueErrorFlashDurationMilliseconds),
        };
        _advancedValueErrorTimer.Tick += OnAdvancedValueErrorTimerTick;

        InitializeComponent();

        UpdateAboutVersionText();
        ApplyBrandPalette();
        ApplySettingsToControls();
        ConfigureBrandLinks();
        ConfigurePanelScrolling();
        SubscribeToSettingsChanges();
        _languageService.LanguageChanged += OnLanguageChanged;
        _systemThemeProvider.ThemeChanged += OnSystemThemeChanged;
        _isListeningForThemeChanges = true;
    }

    internal void UpdateSettings(
        AppSettings settings,
        bool isPaused,
        bool isCorrectionHotkeyRegistered,
        bool isPeekHotkeyRegistered)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        _isPaused = isPaused;
        _isCorrectionHotkeyRegistered = isCorrectionHotkeyRegistered;
        _isPeekHotkeyRegistered = isPeekHotkeyRegistered;
        ApplySettingsToControls();
    }

    internal void UpdateRuntimeState(
        bool isPaused,
        bool isCorrectionHotkeyRegistered,
        bool isPeekHotkeyRegistered)
    {
        if (_isPaused == isPaused
            && _isCorrectionHotkeyRegistered == isCorrectionHotkeyRegistered
            && _isPeekHotkeyRegistered == isPeekHotkeyRegistered)
        {
            return;
        }

        _isPaused = isPaused;
        _isCorrectionHotkeyRegistered = isCorrectionHotkeyRegistered;
        _isPeekHotkeyRegistered = isPeekHotkeyRegistered;
        _isUpdatingControls = true;

        try
        {
            ApplyMainFeatureStateToControls();
        }
        finally
        {
            _isUpdatingControls = false;
        }
    }

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

    private void OnNavigationChecked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        var selectedPanel = ReferenceEquals(sender, GuideNavigationButton)
            ? GuidePanel
            : ReferenceEquals(sender, GeneralNavigationButton)
                ? GeneralPanel
                : ReferenceEquals(sender, CorrectionNavigationButton)
                    ? CorrectionPanel
                    : ReferenceEquals(sender, PeekNavigationButton)
                        ? PeekPanel
                        : AdvancedPanel;

        foreach (var panel in GetPanels())
        {
            panel.Visibility = ReferenceEquals(panel, selectedPanel)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        selectedPanel.ScrollToTop();
        AnimatePanelEntry(selectedPanel);
    }

    protected override void OnClosed(EventArgs e)
    {
        ClearCurrentAdvancedValueError();
        _advancedValueErrorTimer.Tick -= OnAdvancedValueErrorTimerTick;
        _languageService.LanguageChanged -= OnLanguageChanged;

        if (_isListeningForThemeChanges)
        {
            _systemThemeProvider.ThemeChanged -= OnSystemThemeChanged;
            _isListeningForThemeChanges = false;
        }

        base.OnClosed(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        CancelCorrectionHotkeyRecording();
        CancelPeekHotkeyRecording();
        HideAppearancePreview();
        CommitAppearanceSliders();
        CommitAdvancedValue(CopyTimeoutTextBox);
        CommitAdvancedValue(TextLimitKilobytesTextBox);
        CommitAdvancedValue(ClipboardRestoreDelayTextBox);
        CommitAdvancedValue(AutoPeekLingerTextBox);
        CommitAdvancedValue(PeekAutoExitTextBox);

        base.OnClosing(e);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        CancelCorrectionHotkeyRecording();
        CancelPeekHotkeyRecording();
        base.OnDeactivated(e);
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

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

    private void SubscribeToSettingsChanges()
    {
        LanguageComboBox.SelectionChanged += OnLanguageSelectionChanged;
        ThemeComboBox.SelectionChanged += OnThemeSelectionChanged;
        StartWithWindowsToggle.Checked += OnAutostartToggleChanged;
        StartWithWindowsToggle.Unchecked += OnAutostartToggleChanged;

        foreach (var toggle in GetPersistedToggles())
        {
            toggle.Checked += OnPersistedToggleChanged;
            toggle.Unchecked += OnPersistedToggleChanged;
        }

        foreach (var slider in GetAppearanceSliders())
        {
            slider.ValueChanged += OnAppearanceSliderValueChanged;
            slider.PreviewMouseLeftButtonDown +=
                OnAppearanceSliderMouseLeftButtonDown;
            slider.PreviewMouseLeftButtonUp += OnAppearanceSliderMouseLeftButtonUp;
            slider.LostMouseCapture += OnAppearanceSliderLostMouseCapture;
            slider.PreviewKeyUp += OnAppearanceSliderKeyUp;
            slider.LostKeyboardFocus += OnAppearanceSliderLostKeyboardFocus;
        }
    }

    private IEnumerable<ToggleButton> GetPersistedToggles()
    {
        yield return CorrectionEnabledToggle;
        yield return LayoutAutoSwitchToggle;
        yield return PasteIntoUnknownToggle;
        yield return PeekEnabledToggle;
        yield return AutoPeekEnabledToggle;
        yield return PeekAnimationToggle;
        yield return PeekBlockNumberRowToggle;
        yield return PeekShowServiceKeysToggle;
    }

    private IEnumerable<Slider> GetAppearanceSliders()
    {
        yield return PeekOpacitySlider;
        yield return PeekScaleSlider;
    }

    private void ApplySettingsToControls()
    {
        _isUpdatingControls = true;

        try
        {
            LanguageComboBox.SelectedIndex = _settings.General.Language switch
            {
                AppLanguage.English => 1,
                AppLanguage.Russian => 2,
                _ => 0,
            };
            ThemeComboBox.SelectedIndex = _settings.General.Theme switch
            {
                AppTheme.Light => 1,
                AppTheme.Dark => 2,
                _ => 0,
            };
            ApplyAutostartStateToControl();

            ApplyMainFeatureStateToControls();
            LayoutAutoSwitchToggle.IsChecked = _settings.General.LayoutAutoSwitchEnabled;
            PasteIntoUnknownToggle.IsChecked = _settings.Correction.PasteIntoUnknown;

            PeekAnimationToggle.IsChecked = _settings.Peek.AnimationEnabled;
            PeekOpacitySlider.Value = _settings.Peek.Opacity * PercentageScale;
            PeekScaleSlider.Value = _settings.Peek.Scale * PercentageScale;
            UpdateAppearanceSliderReadouts();
            PeekBlockNumberRowToggle.IsChecked = _settings.Peek.BlockNumberRow;
            PeekShowServiceKeysToggle.IsChecked = _settings.Peek.ShowServiceKeys;
            KeyboardProfileValueText.Text = FormatKeyboardProfile(
                _settings.Peek.KeyboardProfile);

            ApplyCorrectionHotkeyToControls();
            ApplyPeekHotkeyToControls();

            RestoreAdvancedValue(CopyTimeoutTextBox);
            RestoreAdvancedValue(TextLimitKilobytesTextBox);
            RestoreAdvancedValue(ClipboardRestoreDelayTextBox);
            RestoreAdvancedValue(AutoPeekLingerTextBox);
            RestoreAdvancedValue(PeekAutoExitTextBox);
        }
        finally
        {
            _isUpdatingControls = false;
        }
    }

    private void ApplyMainFeatureStateToControls()
    {
        CorrectionEnabledToggle.IsChecked =
            _settings.Correction.CorrectionEnabled
            && (_settings.Correction.CorrectionHotkey is null
                || _isCorrectionHotkeyRegistered)
            && !_isPaused;
        PeekEnabledToggle.IsChecked =
            _settings.Peek.PeekEnabled
            && !_isPaused;
        AutoPeekEnabledToggle.IsChecked =
            _settings.Peek.AutoPeekEnabled
            && !_isPaused;
        CorrectionHotkeyConflictIcon.Visibility =
            _settings.Correction.CorrectionEnabled
            && _settings.Correction.CorrectionHotkey is not null
            && !_isCorrectionHotkeyRegistered
            && !_isPaused
                ? Visibility.Visible
                : Visibility.Collapsed;
        PeekHotkeyConflictIcon.Visibility =
            _settings.Peek.PeekEnabled
            && _settings.Peek.ActivationHotkey is not null
            && !_isPeekHotkeyRegistered
            && !_isPaused
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void ApplyAutostartStateToControl()
    {
        var state = _autostartService.IsSupported
            ? _autostartService.GetState()
            : AutostartState.NotSupported;

        switch (state)
        {
            case AutostartState.Enabled:
                StartWithWindowsToggle.IsChecked = true;
                StartWithWindowsToggle.IsEnabled = true;
                break;
            case AutostartState.Disabled:
                StartWithWindowsToggle.IsChecked = false;
                StartWithWindowsToggle.IsEnabled = true;
                break;
            case AutostartState.NotSupported:
            case AutostartState.PlatformError:
                StartWithWindowsToggle.IsChecked = false;
                StartWithWindowsToggle.IsEnabled = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state));
        }
    }

    private static void ApplyHotkeyToControls(
        GlobalHotkey? hotkey,
        FrameworkElement modifiersKeycap,
        TextBlock modifiersText,
        FrameworkElement separator,
        TextBlock keyText)
    {
        if (hotkey is null)
        {
            modifiersKeycap.Visibility = Visibility.Collapsed;
            separator.Visibility = Visibility.Collapsed;
            modifiersText.Text = string.Empty;
            keyText.Text = AppStrings.Settings_Hotkey_NotAssigned;
            return;
        }

        var formattedModifiers = string.Join(
            $" {AppStrings.Settings_Hotkey_Separator} ",
            hotkey.Modifiers.Select(FormatHotkeyModifier));
        var hasModifiers = formattedModifiers.Length > 0;

        modifiersKeycap.Visibility = hasModifiers
            ? Visibility.Visible
            : Visibility.Collapsed;
        separator.Visibility = hasModifiers
            ? Visibility.Visible
            : Visibility.Collapsed;
        modifiersText.Text = formattedModifiers;
        keyText.Text = FormatHotkeyKey(hotkey.Key);
    }

    private static string FormatHotkeyModifier(HotkeyModifier modifier) =>
        modifier switch
        {
            HotkeyModifier.Primary => AppStrings.Settings_Hotkey_Control,
            HotkeyModifier.Control => AppStrings.Settings_Hotkey_Control,
            HotkeyModifier.Alt => AppStrings.Settings_Hotkey_Alt,
            HotkeyModifier.Shift => AppStrings.Settings_Hotkey_Shift,
            HotkeyModifier.Meta => AppStrings.Settings_Hotkey_Meta,
            _ => throw new ArgumentOutOfRangeException(nameof(modifier)),
        };

    private static string FormatHotkeyKey(HotkeyKey key)
    {
        if (key is >= HotkeyKey.Digit0 and <= HotkeyKey.Digit9)
        {
            return ((int)key - (int)HotkeyKey.Digit0)
                .ToString(CultureInfo.InvariantCulture);
        }

        return key.ToString();
    }

    private void OnCorrectionHotkeyChangeClick(object sender, RoutedEventArgs e)
    {
        if (_isRecordingCorrectionHotkey)
        {
            CancelCorrectionHotkeyRecording();
            return;
        }

        var startStatus = _startCorrectionHotkeyRecordingCapture(
            OnCorrectionHotkeyCaptureKeyChanged);
        if (startStatus == KeyboardCaptureStartStatus.PlatformError)
        {
            ShowWarning(AppStrings.Settings_Hotkey_CaptureFailed);
            return;
        }

        _isCorrectionHotkeyRecordingCaptureActive = true;
        _isRecordingCorrectionHotkey = true;
        _pressedCorrectionHotkeyModifierKeys.Clear();
        CorrectionHotkeyModifiersKeycap.Visibility = Visibility.Collapsed;
        CorrectionHotkeySeparator.Visibility = Visibility.Collapsed;
        CorrectionHotkeyKeyText.Text = AppStrings.Settings_Hotkey_RecordingPrompt;
        CorrectionHotkeyChangeButton.Content = AppStrings.Settings_Hotkey_CancelRecording;
        CorrectionHotkeyClearButton.Visibility = Visibility.Visible;
        _ = CorrectionHotkeyChangeButton.Focus();
    }

    private void OnCorrectionHotkeyChangeLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (_isRecordingCorrectionHotkey
            && !ReferenceEquals(e.NewFocus, CorrectionHotkeyChangeButton)
            && !ReferenceEquals(e.NewFocus, CorrectionHotkeyClearButton))
        {
            CancelCorrectionHotkeyRecording();
        }
    }

    private void OnCorrectionHotkeyClearClick(object sender, RoutedEventArgs e)
    {
        if (!_isRecordingCorrectionHotkey)
        {
            return;
        }

        var updatedSettings = _settings with
        {
            Correction = _settings.Correction with
            {
                CorrectionHotkey = null,
            },
        };

        PersistSettings(updatedSettings);
        CancelCorrectionHotkeyRecording();
    }

    private void OnCorrectionHotkeyCaptureKeyChanged(
        object? sender,
        KeyboardCaptureEventArgs eventArgs)
    {
        if (!_isRecordingCorrectionHotkey)
        {
            return;
        }

        if (IsModifierKey(eventArgs.Key))
        {
            if (eventArgs.State == KeyboardKeyState.Down)
            {
                _pressedCorrectionHotkeyModifierKeys.Add(eventArgs.Key);
            }
            else if (_pressedCorrectionHotkeyModifierKeys.Remove(eventArgs.Key)
                && _pressedCorrectionHotkeyModifierKeys.Count == 0)
            {
                CancelCorrectionHotkeyRecording();
                ShowWarning(AppStrings.Settings_Hotkey_InvalidCombination);
            }

            return;
        }

        if (eventArgs.State != KeyboardKeyState.Down)
        {
            return;
        }

        if (eventArgs.Key == PhysicalKey.Escape)
        {
            CancelCorrectionHotkeyRecording();
            return;
        }

        var modifiers = GetHotkeyModifiers(
            _pressedCorrectionHotkeyModifierKeys);
        if (modifiers.Count != 1
            || !modifiers.Contains(HotkeyModifier.Primary)
            || !TryMapHotkeyKey(eventArgs.Key, out var hotkeyKey))
        {
            CancelCorrectionHotkeyRecording();
            ShowWarning(AppStrings.Settings_Hotkey_InvalidCombination);
            return;
        }

        ApplyRecordedCorrectionHotkey(new GlobalHotkey(hotkeyKey, modifiers));
    }

    private void ApplyRecordedCorrectionHotkey(GlobalHotkey hotkey)
    {
        var previousHotkey = _settings.Correction.CorrectionHotkey;
        if (hotkey.Equals(previousHotkey))
        {
            CancelCorrectionHotkeyRecording();
            return;
        }

        var internalConflict = _findCorrectionHotkeyInternalConflict(hotkey);
        if (internalConflict != CorrectionHotkeyInternalConflict.None)
        {
            CancelCorrectionHotkeyRecording();
            ShowWarning(internalConflict switch
            {
                CorrectionHotkeyInternalConflict.Peek =>
                    AppStrings.Settings_Hotkey_InternalConflict,
                CorrectionHotkeyInternalConflict.Diagnostics =>
                    AppStrings.Settings_Hotkey_DiagnosticsConflict,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(internalConflict)),
            });
            return;
        }

        var shouldRegister = _settings.Correction.CorrectionEnabled && !_isPaused;
        if (shouldRegister)
        {
            var registrationResult = _tryRegisterCorrectionHotkey(hotkey);
            if (!registrationResult.Succeeded)
            {
                CancelCorrectionHotkeyRecording();
                ShowWarning(FormatCorrectionHotkeyRegistrationFailure(registrationResult));
                return;
            }
        }

        var updatedSettings = _settings with
        {
            Correction = _settings.Correction with
            {
                CorrectionHotkey = hotkey,
            },
        };
        var saved = PersistSettings(updatedSettings);

        if (!saved && shouldRegister && previousHotkey is not null)
        {
            var restoreResult = _tryRegisterCorrectionHotkey(previousHotkey);
            if (!restoreResult.Succeeded)
            {
                ShowWarning(AppStrings.Settings_Hotkey_RestoreAfterSaveFailed);
            }
        }

        CancelCorrectionHotkeyRecording();
    }

    private void CancelCorrectionHotkeyRecording()
    {
        var wasRecording = _isRecordingCorrectionHotkey;
        _isRecordingCorrectionHotkey = false;
        _pressedCorrectionHotkeyModifierKeys.Clear();

        if (_isCorrectionHotkeyRecordingCaptureActive)
        {
            _isCorrectionHotkeyRecordingCaptureActive = false;
            _stopCorrectionHotkeyRecordingCapture(
                OnCorrectionHotkeyCaptureKeyChanged);
        }

        if (!wasRecording)
        {
            return;
        }

        CorrectionHotkeyChangeButton.Content = AppStrings.Settings_ChangeShortcut;
        CorrectionHotkeyClearButton.Visibility = Visibility.Collapsed;
        ApplyCorrectionHotkeyToControls();
    }

    private void ApplyCorrectionHotkeyToControls() =>
        ApplyHotkeyToControls(
            _settings.Correction.CorrectionHotkey,
            CorrectionHotkeyModifiersKeycap,
            CorrectionHotkeyModifiersText,
            CorrectionHotkeySeparator,
            CorrectionHotkeyKeyText);

    private static string FormatKeyboardProfile(KeyboardProfile keyboardProfile) =>
        keyboardProfile switch
        {
            KeyboardProfile.Ansi => AppStrings.Settings_Peek_KeyboardProfileAnsi,
            KeyboardProfile.Iso => AppStrings.Settings_Peek_KeyboardProfileIso,
            KeyboardProfile.Custom => AppStrings.Settings_Peek_KeyboardProfileCustom,
            _ => AppStrings.Settings_Peek_KeyboardProfileNotSet,
        };

    private void OnKeyboardProfileChangeClick(object sender, RoutedEventArgs e) =>
        _keyboardProfileChangeRequested();

    private void OnOpenGuideWindowClick(object sender, RoutedEventArgs e) =>
        GuideWindowRequested?.Invoke(this, EventArgs.Empty);

    private void OnPeekHotkeyChangeClick(object sender, RoutedEventArgs e)
    {
        if (_isRecordingPeekHotkey)
        {
            CancelPeekHotkeyRecording();
            return;
        }

        var startStatus = _startPeekHotkeyRecordingCapture(
            OnPeekHotkeyCaptureKeyChanged);
        if (startStatus == KeyboardCaptureStartStatus.PlatformError)
        {
            ShowWarning(AppStrings.Settings_Hotkey_CaptureFailed);
            return;
        }

        _isPeekHotkeyRecordingCaptureActive = true;
        _isRecordingPeekHotkey = true;
        _pressedPeekHotkeyModifierKeys.Clear();
        PeekHotkeyModifiersKeycap.Visibility = Visibility.Collapsed;
        PeekHotkeySeparator.Visibility = Visibility.Collapsed;
        PeekHotkeyKeyText.Text = AppStrings.Settings_Hotkey_RecordingPrompt;
        PeekHotkeyChangeButton.Content = AppStrings.Settings_Hotkey_CancelRecording;
        PeekHotkeyClearButton.Visibility = Visibility.Visible;
        _ = PeekHotkeyChangeButton.Focus();
    }

    private void OnPeekHotkeyChangeLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (_isRecordingPeekHotkey
            && !ReferenceEquals(e.NewFocus, PeekHotkeyChangeButton)
            && !ReferenceEquals(e.NewFocus, PeekHotkeyClearButton))
        {
            CancelPeekHotkeyRecording();
        }
    }

    private void OnPeekHotkeyClearClick(object sender, RoutedEventArgs e)
    {
        if (!_isRecordingPeekHotkey)
        {
            return;
        }

        var updatedSettings = _settings with
        {
            Peek = _settings.Peek with
            {
                ActivationHotkey = null,
            },
        };

        PersistSettings(updatedSettings);
        CancelPeekHotkeyRecording();
    }

    private void OnPeekHotkeyCaptureKeyChanged(
        object? sender,
        KeyboardCaptureEventArgs eventArgs)
    {
        if (!_isRecordingPeekHotkey)
        {
            return;
        }

        if (IsModifierKey(eventArgs.Key))
        {
            if (eventArgs.State == KeyboardKeyState.Down)
            {
                _pressedPeekHotkeyModifierKeys.Add(eventArgs.Key);
            }
            else if (_pressedPeekHotkeyModifierKeys.Remove(eventArgs.Key)
                && _pressedPeekHotkeyModifierKeys.Count == 0)
            {
                CancelPeekHotkeyRecording();
                ShowWarning(AppStrings.Settings_Hotkey_InvalidCombination);
            }

            return;
        }

        if (eventArgs.State != KeyboardKeyState.Down)
        {
            return;
        }

        if (eventArgs.Key == PhysicalKey.Escape)
        {
            CancelPeekHotkeyRecording();
            return;
        }

        var modifiers = GetHotkeyModifiers(_pressedPeekHotkeyModifierKeys);
        if (modifiers.Count != 1
            || !modifiers.Contains(HotkeyModifier.Primary)
            || !TryMapHotkeyKey(eventArgs.Key, out var hotkeyKey))
        {
            CancelPeekHotkeyRecording();
            ShowWarning(AppStrings.Settings_Hotkey_InvalidCombination);
            return;
        }

        ApplyRecordedPeekHotkey(new GlobalHotkey(hotkeyKey, modifiers));
    }

    private void ApplyRecordedPeekHotkey(GlobalHotkey hotkey)
    {
        var previousHotkey = _settings.Peek.ActivationHotkey;
        if (hotkey.Equals(previousHotkey))
        {
            CancelPeekHotkeyRecording();
            return;
        }

        var internalConflict = _findPeekHotkeyInternalConflict(hotkey);
        if (internalConflict != PeekHotkeyInternalConflict.None)
        {
            CancelPeekHotkeyRecording();
            ShowWarning(internalConflict switch
            {
                PeekHotkeyInternalConflict.Correction =>
                    AppStrings.Settings_Hotkey_CorrectionConflict,
                PeekHotkeyInternalConflict.Diagnostics =>
                    AppStrings.Settings_Hotkey_DiagnosticsConflict,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(internalConflict)),
            });
            return;
        }

        var shouldRegister = _settings.Peek.PeekEnabled && !_isPaused;
        if (shouldRegister)
        {
            var registrationResult = _tryRegisterPeekHotkey(hotkey);
            if (!registrationResult.Succeeded)
            {
                CancelPeekHotkeyRecording();
                ShowWarning(FormatPeekHotkeyRegistrationFailure(registrationResult));
                return;
            }
        }

        var updatedSettings = _settings with
        {
            Peek = _settings.Peek with
            {
                ActivationHotkey = hotkey,
            },
        };
        var saved = PersistSettings(updatedSettings);

        if (!saved && shouldRegister && previousHotkey is not null)
        {
            var restoreResult = _tryRegisterPeekHotkey(previousHotkey);
            if (!restoreResult.Succeeded)
            {
                ShowWarning(AppStrings.Settings_Hotkey_RestoreAfterSaveFailed);
            }
        }

        CancelPeekHotkeyRecording();
    }

    private void CancelPeekHotkeyRecording()
    {
        var wasRecording = _isRecordingPeekHotkey;
        _isRecordingPeekHotkey = false;
        _pressedPeekHotkeyModifierKeys.Clear();

        if (_isPeekHotkeyRecordingCaptureActive)
        {
            _isPeekHotkeyRecordingCaptureActive = false;
            _stopPeekHotkeyRecordingCapture(OnPeekHotkeyCaptureKeyChanged);
        }

        if (!wasRecording)
        {
            return;
        }

        PeekHotkeyChangeButton.Content = AppStrings.Settings_ChangeShortcut;
        PeekHotkeyClearButton.Visibility = Visibility.Collapsed;
        ApplyPeekHotkeyToControls();
    }

    private void ApplyPeekHotkeyToControls() =>
        ApplyHotkeyToControls(
            _settings.Peek.ActivationHotkey,
            PeekHotkeyModifiersKeycap,
            PeekHotkeyModifiersText,
            PeekHotkeySeparator,
            PeekHotkeyKeyText);

    private static bool IsModifierKey(PhysicalKey key) =>
        key is PhysicalKey.ControlLeft or PhysicalKey.ControlRight
            or PhysicalKey.AltLeft or PhysicalKey.AltRight
            or PhysicalKey.ShiftLeft or PhysicalKey.ShiftRight
            or PhysicalKey.MetaLeft or PhysicalKey.MetaRight;

    private static IReadOnlyCollection<HotkeyModifier> GetHotkeyModifiers(
        IReadOnlySet<PhysicalKey> modifierKeys)
    {
        var modifiers = new List<HotkeyModifier>(4);

        if (modifierKeys.Contains(PhysicalKey.ControlLeft)
            || modifierKeys.Contains(PhysicalKey.ControlRight))
        {
            modifiers.Add(HotkeyModifier.Primary);
        }

        if (modifierKeys.Contains(PhysicalKey.AltLeft)
            || modifierKeys.Contains(PhysicalKey.AltRight))
        {
            modifiers.Add(HotkeyModifier.Alt);
        }

        if (modifierKeys.Contains(PhysicalKey.ShiftLeft)
            || modifierKeys.Contains(PhysicalKey.ShiftRight))
        {
            modifiers.Add(HotkeyModifier.Shift);
        }

        if (modifierKeys.Contains(PhysicalKey.MetaLeft)
            || modifierKeys.Contains(PhysicalKey.MetaRight))
        {
            modifiers.Add(HotkeyModifier.Meta);
        }

        return modifiers;
    }

    private static bool TryMapHotkeyKey(
        PhysicalKey key,
        out HotkeyKey hotkeyKey)
    {
        var mappedKey = key switch
        {
            PhysicalKey.KeyA => (HotkeyKey?)HotkeyKey.A,
            PhysicalKey.KeyB => HotkeyKey.B,
            PhysicalKey.KeyC => HotkeyKey.C,
            PhysicalKey.KeyD => HotkeyKey.D,
            PhysicalKey.KeyE => HotkeyKey.E,
            PhysicalKey.KeyF => HotkeyKey.F,
            PhysicalKey.KeyG => HotkeyKey.G,
            PhysicalKey.KeyH => HotkeyKey.H,
            PhysicalKey.KeyI => HotkeyKey.I,
            PhysicalKey.KeyJ => HotkeyKey.J,
            PhysicalKey.KeyK => HotkeyKey.K,
            PhysicalKey.KeyL => HotkeyKey.L,
            PhysicalKey.KeyM => HotkeyKey.M,
            PhysicalKey.KeyN => HotkeyKey.N,
            PhysicalKey.KeyO => HotkeyKey.O,
            PhysicalKey.KeyP => HotkeyKey.P,
            PhysicalKey.KeyQ => HotkeyKey.Q,
            PhysicalKey.KeyR => HotkeyKey.R,
            PhysicalKey.KeyS => HotkeyKey.S,
            PhysicalKey.KeyT => HotkeyKey.T,
            PhysicalKey.KeyU => HotkeyKey.U,
            PhysicalKey.KeyV => HotkeyKey.V,
            PhysicalKey.KeyW => HotkeyKey.W,
            PhysicalKey.KeyX => HotkeyKey.X,
            PhysicalKey.KeyY => HotkeyKey.Y,
            PhysicalKey.KeyZ => HotkeyKey.Z,
            PhysicalKey.Digit0 => HotkeyKey.Digit0,
            PhysicalKey.Digit1 => HotkeyKey.Digit1,
            PhysicalKey.Digit2 => HotkeyKey.Digit2,
            PhysicalKey.Digit3 => HotkeyKey.Digit3,
            PhysicalKey.Digit4 => HotkeyKey.Digit4,
            PhysicalKey.Digit5 => HotkeyKey.Digit5,
            PhysicalKey.Digit6 => HotkeyKey.Digit6,
            PhysicalKey.Digit7 => HotkeyKey.Digit7,
            PhysicalKey.Digit8 => HotkeyKey.Digit8,
            PhysicalKey.Digit9 => HotkeyKey.Digit9,
            PhysicalKey.F1 => HotkeyKey.F1,
            PhysicalKey.F2 => HotkeyKey.F2,
            PhysicalKey.F3 => HotkeyKey.F3,
            PhysicalKey.F4 => HotkeyKey.F4,
            PhysicalKey.F5 => HotkeyKey.F5,
            PhysicalKey.F6 => HotkeyKey.F6,
            PhysicalKey.F7 => HotkeyKey.F7,
            PhysicalKey.F8 => HotkeyKey.F8,
            PhysicalKey.F9 => HotkeyKey.F9,
            PhysicalKey.F10 => HotkeyKey.F10,
            PhysicalKey.F11 => HotkeyKey.F11,
            PhysicalKey.F12 => HotkeyKey.F12,
            PhysicalKey.F13 => HotkeyKey.F13,
            PhysicalKey.F14 => HotkeyKey.F14,
            PhysicalKey.F15 => HotkeyKey.F15,
            PhysicalKey.F16 => HotkeyKey.F16,
            PhysicalKey.F17 => HotkeyKey.F17,
            PhysicalKey.F18 => HotkeyKey.F18,
            PhysicalKey.F19 => HotkeyKey.F19,
            PhysicalKey.F20 => HotkeyKey.F20,
            PhysicalKey.F21 => HotkeyKey.F21,
            PhysicalKey.F22 => HotkeyKey.F22,
            PhysicalKey.F23 => HotkeyKey.F23,
            PhysicalKey.F24 => HotkeyKey.F24,
            _ => null,
        };

        hotkeyKey = mappedKey.GetValueOrDefault();
        return mappedKey.HasValue;
    }

    private static string FormatCorrectionHotkeyRegistrationFailure(
        CorrectionHotkeyRegistrationResult result)
    {
        if (result.HadPreviousRegistration && !result.PreviousRegistrationRestored)
        {
            return AppStrings.Settings_Hotkey_RestoreFailed;
        }

        return result.RegistrationStatus switch
        {
            GlobalHotkeyRegistrationStatus.HotkeyUnavailable =>
                AppStrings.Settings_Hotkey_Unavailable,
            GlobalHotkeyRegistrationStatus.UnsupportedHotkey or
                GlobalHotkeyRegistrationStatus.PlatformError =>
                AppStrings.Settings_Hotkey_RegistrationFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
    }

    private static string FormatPeekHotkeyRegistrationFailure(
        PeekHotkeyRegistrationResult result)
    {
        if (result.HadPreviousRegistration && !result.PreviousRegistrationRestored)
        {
            return AppStrings.Settings_Hotkey_RestoreFailed;
        }

        return result.RegistrationStatus switch
        {
            GlobalHotkeyRegistrationStatus.HotkeyUnavailable =>
                AppStrings.Settings_Hotkey_Unavailable,
            GlobalHotkeyRegistrationStatus.UnsupportedHotkey or
                GlobalHotkeyRegistrationStatus.PlatformError =>
                AppStrings.Settings_Hotkey_RegistrationFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
    }

    private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingControls)
        {
            return;
        }

        var language = LanguageComboBox.SelectedIndex switch
        {
            1 => AppLanguage.English,
            2 => AppLanguage.Russian,
            _ => AppLanguage.System,
        };
        var updatedSettings = _settings with
        {
            General = _settings.General with
            {
                Language = language,
            },
        };

        PersistSettings(updatedSettings);
    }

    private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingControls)
        {
            return;
        }

        var theme = ThemeComboBox.SelectedIndex switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.System,
        };
        var updatedSettings = _settings with
        {
            General = _settings.General with
            {
                Theme = theme,
            },
        };

        PersistSettings(updatedSettings);
    }

    private void OnAutostartToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingControls || !ReferenceEquals(sender, StartWithWindowsToggle))
        {
            return;
        }

        var enableAutostart = StartWithWindowsToggle.IsChecked == true;
        var operationStatus = enableAutostart
            ? _autostartService.Enable()
            : _autostartService.Disable();

        if (operationStatus == AutostartOperationStatus.Success)
        {
            var updatedSettings = _settings with
            {
                General = _settings.General with
                {
                    Autostart = enableAutostart,
                },
            };

            PersistSettings(updatedSettings);
            return;
        }

        _isUpdatingControls = true;

        try
        {
            StartWithWindowsToggle.IsChecked = !enableAutostart;

            if (operationStatus == AutostartOperationStatus.NotSupported)
            {
                StartWithWindowsToggle.IsEnabled = false;
            }
        }
        finally
        {
            _isUpdatingControls = false;
        }

        ShowWarning(AppStrings.Settings_Autostart_ChangeFailed);
    }

    private void OnPersistedToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingControls || sender is not ToggleButton toggle)
        {
            return;
        }

        var isChecked = toggle.IsChecked == true;
        var shouldRequestResume = _isPaused
            && isChecked
            && (ReferenceEquals(toggle, CorrectionEnabledToggle)
                || ReferenceEquals(toggle, PeekEnabledToggle));
        var shouldRetryRegistration = !_isPaused
            && isChecked
            && ((ReferenceEquals(toggle, CorrectionEnabledToggle)
                    && _settings.Correction.CorrectionEnabled
                    && !_isCorrectionHotkeyRegistered)
                || (ReferenceEquals(toggle, PeekEnabledToggle)
                    && _settings.Peek.PeekEnabled
                    && !_isPeekHotkeyRegistered));
        AppSettings updatedSettings;

        if (ReferenceEquals(toggle, CorrectionEnabledToggle))
        {
            updatedSettings = _settings with
            {
                Correction = _settings.Correction with
                {
                    CorrectionEnabled = isChecked,
                },
            };
        }
        else if (ReferenceEquals(toggle, LayoutAutoSwitchToggle))
        {
            updatedSettings = _settings with
            {
                General = _settings.General with
                {
                    LayoutAutoSwitchEnabled = isChecked,
                },
            };
        }
        else if (ReferenceEquals(toggle, PasteIntoUnknownToggle))
        {
            updatedSettings = _settings with
            {
                Correction = _settings.Correction with
                {
                    PasteIntoUnknown = isChecked,
                },
            };
        }
        else if (ReferenceEquals(toggle, PeekEnabledToggle))
        {
            updatedSettings = _settings with
            {
                Peek = _settings.Peek with
                {
                    PeekEnabled = isChecked,
                },
            };
        }
        else if (ReferenceEquals(toggle, AutoPeekEnabledToggle))
        {
            if (isChecked
                && _settings.Peek.KeyboardProfile == KeyboardProfile.Unknown)
            {
                _isUpdatingControls = true;

                try
                {
                    AutoPeekEnabledToggle.IsChecked = false;
                }
                finally
                {
                    _isUpdatingControls = false;
                }

                _keyboardProfileSelectionRequested();
                return;
            }

            updatedSettings = _settings with
            {
                Peek = _settings.Peek with
                {
                    AutoPeekEnabled = isChecked,
                },
            };
        }
        else if (ReferenceEquals(toggle, PeekAnimationToggle))
        {
            updatedSettings = _settings with
            {
                Peek = _settings.Peek with
                {
                    AnimationEnabled = isChecked,
                },
            };
        }
        else if (ReferenceEquals(toggle, PeekBlockNumberRowToggle))
        {
            updatedSettings = _settings with
            {
                Peek = _settings.Peek with
                {
                    BlockNumberRow = isChecked,
                },
            };
        }
        else if (ReferenceEquals(toggle, PeekShowServiceKeysToggle))
        {
            updatedSettings = _settings with
            {
                Peek = _settings.Peek with
                {
                    ShowServiceKeys = isChecked,
                },
            };
        }
        else
        {
            return;
        }

        if (!PersistSettings(updatedSettings))
        {
            return;
        }

        if (shouldRequestResume)
        {
            _mainFeatureEnableRequestedWhilePaused();
            return;
        }

        if (shouldRetryRegistration)
        {
            if (ReferenceEquals(toggle, CorrectionEnabledToggle))
            {
                _retryCorrectionHotkeyRegistration();
            }
            else
            {
                _retryPeekHotkeyRegistration();
            }
        }
    }

    private void OnAppearanceSliderValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateAppearanceSliderReadouts();

        if (_isUpdatingControls)
        {
            return;
        }

        _appearancePersistBaseline ??= _settings;
        var updatedSettings = CreateAppearanceSettingsFromControls();
        _settingsChanged(updatedSettings);

        if (_isAppearancePreviewActive)
        {
            _overlayService.UpdatePreviewAppearance(
                CreatePreviewAppearanceOptions(updatedSettings));
        }
    }

    private void OnAppearanceSliderMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (_isUpdatingControls
            || _isAppearancePreviewActive
            || sender is not Slider { IsEnabled: true }
            || PeekEnabledToggle.IsChecked != true)
        {
            return;
        }

        var appearanceSettings = CreateAppearanceSettingsFromControls();
        _overlayService.ShowPreview(
            CalculateAppearancePreviewArea(),
            CreatePreviewAppearanceOptions(appearanceSettings));
        _isAppearancePreviewActive = true;
    }

    private void OnAppearanceSliderMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        HideAppearancePreview();
        CommitAppearanceSliders();
    }

    private void OnAppearanceSliderLostMouseCapture(
        object sender,
        MouseEventArgs e) =>
        HideAppearancePreview();

    private void OnAppearanceSliderKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down
            or Key.PageUp or Key.PageDown or Key.Home or Key.End)
        {
            CommitAppearanceSliders();
        }
    }

    private void OnAppearanceSliderLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e) =>
        CommitAppearanceSliders();

    private void CommitAppearanceSliders()
    {
        if (_isUpdatingControls)
        {
            return;
        }

        try
        {
            if (!PersistSettings(
                    CreateAppearanceSettingsFromControls(),
                    _appearancePersistBaseline))
            {
                _settingsChanged(_settings);
            }
        }
        finally
        {
            _appearancePersistBaseline = null;
        }
    }

    private AppSettings CreateAppearanceSettingsFromControls() =>
        _settings with
        {
            Peek = _settings.Peek with
            {
                Opacity = PeekOpacitySlider.Value / PercentageScale,
                Scale = PeekScaleSlider.Value / PercentageScale,
            },
        };

    private static OverlayAppearanceOptions CreatePreviewAppearanceOptions(
        AppSettings settings) =>
        new(
            opacity: settings.Peek.Opacity,
            scale: settings.Peek.Scale,
            animationEnabled: false,
            animationDurationMs: settings.Peek.AnimationDurationMs);

    private DrawingRectangle CalculateAppearancePreviewArea()
    {
        var windowTopLeft = PointToScreen(new Point(0, 0));
        var windowBottomRight = PointToScreen(new Point(ActualWidth, ActualHeight));
        var opacitySliderLeft = PeekOpacitySlider.PointToScreen(new Point(0, 0)).X;
        var scaleSliderLeft = PeekScaleSlider.PointToScreen(new Point(0, 0)).X;

        var left = windowTopLeft.X + AppearancePreviewMargin;
        var top = windowTopLeft.Y + AppearancePreviewMargin;
        var right = Math.Max(
            left + 1,
            Math.Min(opacitySliderLeft, scaleSliderLeft) - AppearancePreviewMargin);
        var bottom = Math.Max(
            top + 1,
            windowBottomRight.Y - AppearancePreviewMargin);

        return DrawingRectangle.FromLTRB(
            (int)Math.Floor(left),
            (int)Math.Floor(top),
            (int)Math.Ceiling(right),
            (int)Math.Ceiling(bottom));
    }

    private void HideAppearancePreview()
    {
        if (!_isAppearancePreviewActive)
        {
            return;
        }

        _isAppearancePreviewActive = false;
        _overlayService.HidePreview();
    }

    private void UpdateAppearanceSliderReadouts()
    {
        PeekOpacityValueText.Text = (PeekOpacitySlider.Value / PercentageScale)
            .ToString("P0", CultureInfo.CurrentCulture);
        PeekScaleValueText.Text = (PeekScaleSlider.Value / PercentageScale)
            .ToString("P0", CultureInfo.CurrentCulture);
    }

    private void OnAdvancedValueLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            CommitAdvancedValue(textBox);
        }
    }

    private void OnAdvancedValueGotKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            ClearAdvancedValueError(textBox);
        }
    }

    private void OnAdvancedValueTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            ClearAdvancedValueError(textBox);
        }
    }

    private void OnAdvancedValueKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (e.Key is Key.Enter or Key.Return)
        {
            CommitAdvancedValue(textBox);
            textBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            RestoreAdvancedValue(textBox);
            ClearAdvancedValueError(textBox);
            textBox.SelectAll();
            e.Handled = true;
        }
    }

    private void CommitAdvancedValue(TextBox textBox)
    {
        if (_isUpdatingControls)
        {
            RestoreAdvancedValue(textBox);
            return;
        }

        if (!int.TryParse(
                textBox.Text,
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out var value))
        {
            RestoreAdvancedValue(textBox);
            ShowAdvancedValueError(textBox);
            return;
        }

        AppSettings? updatedSettings = null;

        if (ReferenceEquals(textBox, CopyTimeoutTextBox) && value > 0)
        {
            updatedSettings = _settings with
            {
                Correction = _settings.Correction with
                {
                    CopyTimeoutMs = value,
                },
            };
        }
        else if (ReferenceEquals(textBox, TextLimitKilobytesTextBox)
            && value > 0
            && value <= int.MaxValue / TextLimitBytesPerThousandCharacters)
        {
            updatedSettings = _settings with
            {
                Correction = _settings.Correction with
                {
                    TextLimitBytes = value * TextLimitBytesPerThousandCharacters,
                },
            };
        }
        else if (ReferenceEquals(textBox, ClipboardRestoreDelayTextBox) && value > 0)
        {
            updatedSettings = _settings with
            {
                Correction = _settings.Correction with
                {
                    ClipboardRestoreDelayMs = value,
                },
            };
        }
        else if (ReferenceEquals(textBox, AutoPeekLingerTextBox) && value > 0)
        {
            updatedSettings = _settings with
            {
                Peek = _settings.Peek with
                {
                    AutoPeekLingerMs = value,
                },
            };
        }
        else if (ReferenceEquals(textBox, PeekAutoExitTextBox)
            && value >= PeekSettings.MinimumAutoExitTimeoutSeconds)
        {
            updatedSettings = _settings with
            {
                Peek = _settings.Peek with
                {
                    AutoExitTimeoutSeconds = value,
                },
            };
        }

        if (updatedSettings is null)
        {
            RestoreAdvancedValue(textBox);
            ShowAdvancedValueError(textBox);
            return;
        }

        ClearAdvancedValueError(textBox);
        PersistSettings(updatedSettings);
        RestoreAdvancedValue(textBox);
    }

    private void ShowAdvancedValueError(TextBox textBox)
    {
        if (_isUpdatingControls || GetAdvancedValueField(textBox) is not Border field)
        {
            return;
        }

        ClearCurrentAdvancedValueError();
        field.SetResourceReference(Border.BorderBrushProperty, DangerBrushKey);
        field.BorderThickness = new Thickness(2);
        _advancedValueErrorTextBox = textBox;
        _advancedValueErrorTimer.Start();
    }

    private void ClearAdvancedValueError(TextBox textBox)
    {
        if (GetAdvancedValueField(textBox) is null)
        {
            return;
        }

        ClearCurrentAdvancedValueError();
    }

    private void OnAdvancedValueErrorTimerTick(object? sender, EventArgs e)
    {
        ClearCurrentAdvancedValueError();
    }

    private void ClearCurrentAdvancedValueError()
    {
        _advancedValueErrorTimer.Stop();

        var textBox = _advancedValueErrorTextBox;
        _advancedValueErrorTextBox = null;

        if (textBox is null || GetAdvancedValueField(textBox) is not Border field)
        {
            return;
        }

        field.ClearValue(Border.BorderBrushProperty);
        field.ClearValue(Border.BorderThicknessProperty);
    }

    private Border? GetAdvancedValueField(TextBox textBox)
    {
        if (ReferenceEquals(textBox, CopyTimeoutTextBox))
        {
            return CopyTimeoutFieldBorder;
        }

        if (ReferenceEquals(textBox, TextLimitKilobytesTextBox))
        {
            return TextLimitKilobytesFieldBorder;
        }

        if (ReferenceEquals(textBox, ClipboardRestoreDelayTextBox))
        {
            return ClipboardRestoreDelayFieldBorder;
        }

        if (ReferenceEquals(textBox, AutoPeekLingerTextBox))
        {
            return AutoPeekLingerFieldBorder;
        }

        return ReferenceEquals(textBox, PeekAutoExitTextBox)
            ? PeekAutoExitFieldBorder
            : null;
    }

    private void RestoreAdvancedValue(TextBox textBox)
    {
        int value;

        if (ReferenceEquals(textBox, CopyTimeoutTextBox))
        {
            value = _settings.Correction.CopyTimeoutMs;
        }
        else if (ReferenceEquals(textBox, TextLimitKilobytesTextBox))
        {
            value = (int)Math.Max(
                1,
                ((long)_settings.Correction.TextLimitBytes
                    + TextLimitBytesPerThousandCharacters - 1)
                    / TextLimitBytesPerThousandCharacters);
        }
        else if (ReferenceEquals(textBox, ClipboardRestoreDelayTextBox))
        {
            value = _settings.Correction.ClipboardRestoreDelayMs;
        }
        else if (ReferenceEquals(textBox, AutoPeekLingerTextBox))
        {
            value = _settings.Peek.AutoPeekLingerMs;
        }
        else if (ReferenceEquals(textBox, PeekAutoExitTextBox))
        {
            value = _settings.Peek.AutoExitTimeoutSeconds;
        }
        else
        {
            return;
        }

        textBox.Text = value.ToString(CultureInfo.CurrentCulture);
    }

    private bool PersistSettings(
        AppSettings updatedSettings,
        AppSettings? persistBaseline = null)
    {
        var settingsBeforeChange = persistBaseline ?? _settings;

        if (updatedSettings == settingsBeforeChange)
        {
            return true;
        }

        try
        {
            _settingsStore.Save(updatedSettings);
            _settings = updatedSettings;
            _settingsChanged(_settings);
            return true;
        }
        catch (Exception exception) when (IsSettingsOperationException(exception))
        {
            _settings = settingsBeforeChange;
            ApplySettingsToControls();
            ShowWarning(AppStrings.Settings_Save_Failed);
            return false;
        }
    }

    private void OnImportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = AppStrings.Settings_Import_DialogTitle,
            Filter = AppStrings.Settings_JsonFileFilter,
            CheckFileExists = true,
            Multiselect = false,
            RestoreDirectory = true,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var result = _settingsStore.Import(dialog.FileName);

            switch (result.Status)
            {
                case SettingsLoadStatus.CreatedDefault:
                case SettingsLoadStatus.Loaded:
                case SettingsLoadStatus.MigratedUp:
                    _settingsImported(result.Settings);
                    break;
                case SettingsLoadStatus.NewerVersionIgnored:
                    ShowWarning(AppStrings.Settings_Import_NewerVersion);
                    break;
                case SettingsLoadStatus.CorruptFellBackToDefaults:
                    ShowWarning(AppStrings.Settings_Import_Corrupt);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(result.Status));
            }
        }
        catch (Exception exception) when (IsSettingsOperationException(exception))
        {
            ShowWarning(AppStrings.Settings_Import_Failed);
        }
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = AppStrings.Settings_Export_DialogTitle,
            Filter = AppStrings.Settings_JsonFileFilter,
            AddExtension = true,
            DefaultExt = ".json",
            FileName = "settings.json",
            RestoreDirectory = true,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _settingsStore.Export(_settings, dialog.FileName);
        }
        catch (Exception exception) when (IsSettingsOperationException(exception))
        {
            ShowWarning(AppStrings.Settings_Export_Failed);
        }
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings = _settingsStore.Reset();
            ApplySettingsToControls();
            _settingsChanged(_settings);
        }
        catch (Exception exception) when (IsSettingsOperationException(exception))
        {
            ShowWarning(AppStrings.Settings_Reset_Failed);
        }
    }

    private void ConfigureBrandLinks()
    {
        SupportButton.IsEnabled = BrandLinks.IsConfigured(BrandLinks.SupportUrl);
        SourceCodeButton.IsEnabled = BrandLinks.IsConfigured(BrandLinks.SourceUrl);
        ReportProblemButton.IsEnabled = BrandLinks.IsConfigured(BrandLinks.ReportUrl);
    }

    private void OnSupportClick(object sender, RoutedEventArgs e) =>
        OpenBrandLink(BrandLinks.SupportUrl);

    private void OnSourceCodeClick(object sender, RoutedEventArgs e) =>
        OpenBrandLink(BrandLinks.SourceUrl);

    private void OnReportProblemClick(object sender, RoutedEventArgs e) =>
        OpenBrandLink(BrandLinks.ReportUrl);

    private void OpenBrandLink(string url)
    {
        if (!BrandLinks.IsConfigured(url))
        {
            return;
        }

        try
        {
            _ = Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException)
        {
            ShowWarning(AppStrings.Settings_Link_Failed);
        }
    }

    private void ShowWarning(string message) =>
        _showNotification(message, true);

    private static bool IsSettingsOperationException(Exception exception) =>
        exception is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or SecurityException;

    private IEnumerable<ScrollViewer> GetPanels()
    {
        yield return GeneralPanel;
        yield return CorrectionPanel;
        yield return PeekPanel;
        yield return AdvancedPanel;
        yield return GuidePanel;
    }

    private void ConfigurePanelScrolling()
    {
        foreach (var panel in GetPanels())
        {
            panel.PreviewMouseWheel += OnPanelPreviewMouseWheel;
        }
    }

    private void OnPanelPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer panel
            || e.Delta == 0
            || panel.ScrollableHeight <= 0
            || NestedControlHandlesMouseWheel(panel, e))
        {
            return;
        }

        if (!CanScrollInWheelDirection(panel, e.Delta))
        {
            _panelScrollRemainders.Remove(panel);
            return;
        }

        var accumulatedPixels =
            _panelScrollRemainders.GetValueOrDefault(panel)
            + (e.Delta * PanelScrollPixelsPerWheelDelta);
        var scrollPixels = Math.Truncate(accumulatedPixels);

        _panelScrollRemainders[panel] = accumulatedPixels - scrollPixels;

        if (scrollPixels != 0)
        {
            var targetOffset = Math.Clamp(
                panel.VerticalOffset - scrollPixels,
                0,
                panel.ScrollableHeight);

            panel.ScrollToVerticalOffset(targetOffset);

            if (targetOffset is 0 || targetOffset == panel.ScrollableHeight)
            {
                _panelScrollRemainders.Remove(panel);
            }
        }

        e.Handled = true;
    }

    private static bool NestedControlHandlesMouseWheel(
        ScrollViewer panel,
        MouseWheelEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject originalSource)
        {
            return false;
        }

        DependencyObject? current = originalSource;

        while (current is not null && !ReferenceEquals(current, panel))
        {
            if (current is ComboBox { IsDropDownOpen: true })
            {
                return true;
            }

            if (current is ScrollViewer nestedScrollViewer
                && CanScrollInWheelDirection(nestedScrollViewer, e.Delta))
            {
                return true;
            }

            current = GetVisualOrLogicalParent(current);
        }

        return false;
    }

    private static bool CanScrollInWheelDirection(ScrollViewer panel, int delta) =>
        delta > 0
            ? panel.VerticalOffset > 0
            : panel.VerticalOffset < panel.ScrollableHeight;

    private static DependencyObject? GetVisualOrLogicalParent(
        DependencyObject element)
    {
        if (element is Visual
            or System.Windows.Media.Media3D.Visual3D)
        {
            return VisualTreeHelper.GetParent(element)
                ?? LogicalTreeHelper.GetParent(element);
        }

        return LogicalTreeHelper.GetParent(element);
    }

    private static void AnimatePanelEntry(UIElement panel)
    {
        var easing = new QuadraticEase
        {
            EasingMode = EasingMode.EaseOut,
        };
        var translateTransform = new TranslateTransform();

        panel.Opacity = 1;
        panel.RenderTransform = translateTransform;
        panel.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, PanelTransitionDuration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop,
            });
        translateTransform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(4, 0, PanelTransitionDuration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop,
            });
    }

    private void OnSystemThemeChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.HasShutdownStarted)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(ApplyBrandPalette);
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
        UpdateAboutVersionText();
        KeyboardProfileValueText.Text = FormatKeyboardProfile(
            _settings.Peek.KeyboardProfile);

        if (_isRecordingCorrectionHotkey)
        {
            CorrectionHotkeyKeyText.Text = AppStrings.Settings_Hotkey_RecordingPrompt;
            CorrectionHotkeyChangeButton.Content =
                AppStrings.Settings_Hotkey_CancelRecording;
        }
        else
        {
            CorrectionHotkeyChangeButton.Content = AppStrings.Settings_ChangeShortcut;
            ApplyCorrectionHotkeyToControls();
        }

        if (_isRecordingPeekHotkey)
        {
            PeekHotkeyKeyText.Text = AppStrings.Settings_Hotkey_RecordingPrompt;
            PeekHotkeyChangeButton.Content = AppStrings.Settings_Hotkey_CancelRecording;
        }
        else
        {
            PeekHotkeyChangeButton.Content = AppStrings.Settings_ChangeShortcut;
            ApplyPeekHotkeyToControls();
        }
    }

    private void UpdateAboutVersionText()
    {
        AboutVersionText.Text = string.Format(
            CultureInfo.CurrentUICulture,
            AppStrings.Settings_About_VersionFormat,
            ApplicationVersion);
    }

    private static string GetApplicationVersion()
    {
        var assembly = typeof(SettingsWindow).Assembly;
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";
        var metadataSeparatorIndex = version.IndexOf('+');

        return metadataSeparatorIndex >= 0
            ? version[..metadataSeparatorIndex]
            : version;
    }

    private void ApplyBrandPalette()
    {
        var useDarkPalette = _systemThemeProvider.IsDarkTheme;

        Resources[BrandAccentBrushKey] = CreateFrozenBrush(GetBrandColor(
            useDarkPalette ? BrandAccentDarkColorKey : BrandAccentLightColorKey));
        Resources[BrandAccentSoftBrushKey] = CreateFrozenBrush(GetBrandColor(
            useDarkPalette ? BrandAccentSoftDarkColorKey : BrandAccentSoftLightColorKey));
        Resources[BrandAccentInkBrushKey] = CreateFrozenBrush(GetBrandColor(
            useDarkPalette ? BrandAccentInkDarkColorKey : BrandAccentInkLightColorKey));
        Resources[BrandMutedBrushKey] = CreateFrozenBrush(GetBrandColor(
            useDarkPalette ? BrandMutedDarkColorKey : BrandMutedLightColorKey));
        Resources[KeycapShadowColorKey] = GetBrandColor(
            useDarkPalette ? KeycapShadowDarkColorKey : KeycapShadowLightColorKey);
    }

    private Color GetBrandColor(string resourceKey) =>
        (Color)FindResource(resourceKey);

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

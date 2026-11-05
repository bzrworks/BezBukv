using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using BezBukv.App.Resources;
using BezBukv.Core.Localization;
using BezBukv.Core.Platform;
using BezBukv.Core.Settings;
using DrawingRectangle = System.Drawing.Rectangle;

namespace BezBukv.App;

public partial class KeyboardProfilePickerWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string resourceKey] =>
        AppStrings.ResourceManager.GetString(
            resourceKey,
            CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException(
            $"Missing localized resource '{resourceKey}'.");

    private const double WorkAreaMargin = 12;
    private const string BrandAccentBrushKey = "KeyboardBrandAccentBrush";
    private const string BrandAccentLightColorKey = "KeyboardBrandAccentLightColor";
    private const string BrandAccentDarkColorKey = "KeyboardBrandAccentDarkColor";
    private const string NoSecondaryLayoutResourceKey =
        "KeyboardProfilePicker_NoSecondaryLayout";

    private readonly DrawingRectangle _targetWorkAreaPixels;
    private readonly IReadOnlyList<LayoutChoice> _installedLayouts;
    private readonly ISystemThemeProvider _systemThemeProvider;
    private readonly ILanguageService _languageService;
    private KeyboardProfile _selectedProfile = KeyboardProfile.Unknown;
    private string? _selectedPrimaryKlid;
    private string? _selectedSecondaryKlid;
    private bool _isListeningForThemeChanges;
    private bool _isUpdatingLayoutSelections;
    private bool _isCompletingSelection;
    private bool _positionedOnTargetMonitor;
    private bool _primaryLayoutItemHovered;
    private bool _secondaryLayoutItemHovered;
    private bool _secondaryNoneItemHovered;
    private bool _secondarySelectionChosen;

    public KeyboardProfilePickerWindow(
        DrawingRectangle targetWorkAreaPixels,
        IReadOnlyList<InstalledKeyboardLayout> installedLayouts,
        IReadOnlyList<string> selectedLayoutKlids,
        ISystemThemeProvider systemThemeProvider,
        ILanguageService languageService)
    {
        ArgumentNullException.ThrowIfNull(installedLayouts);
        ArgumentNullException.ThrowIfNull(selectedLayoutKlids);
        ArgumentNullException.ThrowIfNull(systemThemeProvider);
        ArgumentNullException.ThrowIfNull(languageService);

        _targetWorkAreaPixels = targetWorkAreaPixels;
        _installedLayouts = installedLayouts
            .Where(layout => !string.IsNullOrWhiteSpace(layout.Klid))
            .DistinctBy(layout => layout.Klid, StringComparer.OrdinalIgnoreCase)
            .Select(layout => new LayoutChoice(
                layout.Klid,
                string.IsNullOrWhiteSpace(layout.DisplayName)
                    ? layout.Klid
                    : layout.DisplayName))
            .ToArray();
        _systemThemeProvider = systemThemeProvider;
        _languageService = languageService;

        InitializeComponent();
        ApplyBrandPalette();
        InitializeLayoutLists();
        _languageService.LanguageChanged += OnLanguageChanged;
        _systemThemeProvider.ThemeChanged += OnSystemThemeChanged;
        _isListeningForThemeChanges = true;
    }

    public event Action<KeyboardProfile, IReadOnlyList<string>>? ProfileSelected;

    public event Action<IReadOnlyList<string>>? CustomCalibrationRequested;

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

        Activate();
        Focus();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _positionedOnTargetMonitor = CorrectionWindowPlacement.TryMoveToMonitor(
            this,
            _targetWorkAreaPixels);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        var workArea = _positionedOnTargetMonitor &&
            CorrectionWindowPlacement.TryGetWorkAreaInDips(
                this,
                _targetWorkAreaPixels,
                out var targetWorkArea)
            ? targetWorkArea
            : SystemParameters.WorkArea;
        var availableWidth = Math.Max(0, workArea.Width - (WorkAreaMargin * 2));
        var availableHeight = Math.Max(0, workArea.Height - (WorkAreaMargin * 2));

        Left = workArea.Left + WorkAreaMargin +
            Math.Max(0, (availableWidth - ActualWidth) / 2);
        Top = workArea.Top + WorkAreaMargin +
            Math.Max(0, (availableHeight - ActualHeight) / 2);
        Opacity = 1;
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (IsVisible && !IsActive)
            {
                Close();
            }
        });
    }

    private void OnAnsiClick(object sender, RoutedEventArgs e)
    {
        ShowLayoutStep(KeyboardProfile.Ansi);
    }

    private void OnIsoClick(object sender, RoutedEventArgs e)
    {
        ShowLayoutStep(KeyboardProfile.Iso);
    }

    private void OnCustomClick(object sender, RoutedEventArgs e)
    {
        ShowLayoutStep(KeyboardProfile.Custom);
    }

    private void InitializeLayoutLists()
    {
        _isUpdatingLayoutSelections = true;
        try
        {
            _selectedPrimaryKlid = null;
            _selectedSecondaryKlid = null;
            _secondarySelectionChosen = false;

            RebuildPrimaryLayouts();
            RebuildSecondaryLayouts();
            UpdateLayoutPreview();
        }
        finally
        {
            _isUpdatingLayoutSelections = false;
        }
    }

    private void ShowLayoutStep(KeyboardProfile keyboardProfile)
    {
        _selectedProfile = keyboardProfile;
        PickerHeading.Text = AppStrings.KeyboardProfilePicker_LayoutHeading;
        StandardStepGrid.Visibility = Visibility.Collapsed;
        LayoutStepGrid.Visibility = Visibility.Visible;
        PrimaryLayoutList.Focus();
    }

    private void OnPrimaryLayoutSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isUpdatingLayoutSelections
            || PrimaryLayoutList.SelectedItem is not LayoutChoice selection
            || selection.Klid is null)
        {
            return;
        }

        _selectedPrimaryKlid = selection.Klid;
        if (string.Equals(
            _selectedPrimaryKlid,
            _selectedSecondaryKlid,
            StringComparison.OrdinalIgnoreCase))
        {
            _selectedSecondaryKlid = null;
            _secondarySelectionChosen = false;
        }

        _isUpdatingLayoutSelections = true;
        try
        {
            RebuildSecondaryLayouts();
        }
        finally
        {
            _isUpdatingLayoutSelections = false;
        }

        UpdateLayoutPreview();
        TryCompleteSelection();
    }

    private void OnSecondaryLayoutSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isUpdatingLayoutSelections
            || SecondaryLayoutList.SelectedItem is not LayoutChoice selection)
        {
            return;
        }

        _selectedSecondaryKlid = selection.Klid;
        _secondarySelectionChosen = true;
        if (string.Equals(
            _selectedSecondaryKlid,
            _selectedPrimaryKlid,
            StringComparison.OrdinalIgnoreCase))
        {
            _selectedPrimaryKlid = null;
        }

        _isUpdatingLayoutSelections = true;
        try
        {
            RebuildPrimaryLayouts();
        }
        finally
        {
            _isUpdatingLayoutSelections = false;
        }

        UpdateLayoutPreview();
        TryCompleteSelection();
    }

    private void OnLayoutListPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox
            || ItemsControl.ContainerFromElement(
                listBox,
                e.OriginalSource as DependencyObject) is not ListBoxItem item
            || !item.IsSelected)
        {
            return;
        }

        TryCompleteSelection();
    }

    private void OnLayoutListPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space)
            || sender is not ListBox { SelectedItem: LayoutChoice })
        {
            return;
        }

        e.Handled = true;
        TryCompleteSelection();
    }

    private void OnLayoutItemMouseEnter(object sender, MouseEventArgs e)
    {
        SetLayoutItemHovered(sender, isHovered: true);
    }

    private void OnLayoutItemMouseLeave(object sender, MouseEventArgs e)
    {
        SetLayoutItemHovered(sender, isHovered: false);
    }

    private void SetLayoutItemHovered(object sender, bool isHovered)
    {
        if (sender is not ListBoxItem item
            || ItemsControl.ItemsControlFromItemContainer(item) is not ListBox listBox)
        {
            return;
        }

        if (ReferenceEquals(listBox, PrimaryLayoutList))
        {
            _primaryLayoutItemHovered = isHovered;
        }
        else if (ReferenceEquals(listBox, SecondaryLayoutList))
        {
            _secondaryLayoutItemHovered = isHovered;
            _secondaryNoneItemHovered = isHovered
                && item.Content is LayoutChoice { Klid: null };
        }
        else
        {
            return;
        }

        UpdateLayoutPreview();
    }

    private void RebuildPrimaryLayouts()
    {
        var primaryLayouts = _installedLayouts.Where(layout =>
            !string.Equals(
                layout.Klid,
                _selectedSecondaryKlid,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        PrimaryLayoutList.ItemsSource = primaryLayouts;
        PrimaryLayoutList.SelectedItem = _selectedPrimaryKlid is null
            ? null
            : primaryLayouts.FirstOrDefault(layout => string.Equals(
                layout.Klid,
                _selectedPrimaryKlid,
                StringComparison.OrdinalIgnoreCase));
    }

    private void RebuildSecondaryLayouts()
    {
        var secondaryLayouts = new List<LayoutChoice>
        {
            new(null, this[NoSecondaryLayoutResourceKey]),
        };
        secondaryLayouts.AddRange(_installedLayouts.Where(layout =>
            !string.Equals(
                layout.Klid,
                _selectedPrimaryKlid,
                StringComparison.OrdinalIgnoreCase)));

        SecondaryLayoutList.ItemsSource = secondaryLayouts;
        SecondaryLayoutList.SelectedItem = _secondarySelectionChosen
            ? _selectedSecondaryKlid is null
                ? secondaryLayouts[0]
                : secondaryLayouts.FirstOrDefault(layout => string.Equals(
                    layout.Klid,
                    _selectedSecondaryKlid,
                    StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private void UpdateLayoutPreview()
    {
        var isSelectedSingleLayout = _secondarySelectionChosen
            && _selectedSecondaryKlid is null;
        var isSingleLayout = _secondaryNoneItemHovered
            || (isSelectedSingleLayout && !_secondaryLayoutItemHovered);

        DualSlotIndicators.Visibility = isSingleLayout
            ? Visibility.Collapsed
            : Visibility.Visible;
        SingleSlotIndicator.Visibility = isSingleLayout
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateSlotIndicator(
            PrimarySlotIndicator,
            PrimarySlotFrame,
            PrimarySlotLabel,
            GetSlotPreviewState(
                _selectedPrimaryKlid is not null,
                _primaryLayoutItemHovered));
        UpdateSlotIndicator(
            SecondarySlotIndicator,
            SecondarySlotFrame,
            SecondarySlotLabel,
            GetSlotPreviewState(
                _selectedSecondaryKlid is not null,
                _secondaryLayoutItemHovered));
        UpdateSlotIndicator(
            SingleSlotIndicator,
            SingleSlotFrame,
            SingleSlotLabel,
            GetSlotPreviewState(
                isSelected: _selectedPrimaryKlid is not null,
                isHovered: _primaryLayoutItemHovered || _secondaryNoneItemHovered));
    }

    private static SlotPreviewState GetSlotPreviewState(
        bool isSelected,
        bool isHovered) =>
        isSelected
            ? SlotPreviewState.Selected
            : isHovered
                ? SlotPreviewState.Hovered
                : SlotPreviewState.Idle;

    private static void UpdateSlotIndicator(
        FrameworkElement indicator,
        Rectangle frame,
        TextBlock label,
        SlotPreviewState state)
    {
        indicator.Opacity = 1;
        frame.Visibility = state == SlotPreviewState.Idle
            ? Visibility.Collapsed
            : Visibility.Visible;
        frame.StrokeDashArray = state == SlotPreviewState.Hovered
            ? new DoubleCollection { 3, 2 }
            : null;
        label.SetResourceReference(
            TextBlock.ForegroundProperty,
            state == SlotPreviewState.Idle
                ? "TextFillColorTertiaryBrush"
                : BrandAccentBrushKey);
    }

    private void TryCompleteSelection()
    {
        if (_isCompletingSelection
            || _selectedProfile == KeyboardProfile.Unknown
            || _selectedPrimaryKlid is null
            || !_secondarySelectionChosen)
        {
            return;
        }

        IReadOnlyList<string> selectedLayoutKlids = _selectedSecondaryKlid is null
            ? [_selectedPrimaryKlid]
            : [_selectedPrimaryKlid, _selectedSecondaryKlid];

        _isCompletingSelection = true;
        try
        {
            if (_selectedProfile == KeyboardProfile.Custom)
            {
                CustomCalibrationRequested?.Invoke(selectedLayoutKlids);
            }
            else
            {
                ProfileSelected?.Invoke(_selectedProfile, selectedLayoutKlids);
            }
        }
        finally
        {
            _isCompletingSelection = false;
        }
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

        _isUpdatingLayoutSelections = true;
        try
        {
            RebuildSecondaryLayouts();
        }
        finally
        {
            _isUpdatingLayoutSelections = false;
        }

        if (_selectedProfile != KeyboardProfile.Unknown)
        {
            PickerHeading.Text = AppStrings.KeyboardProfilePicker_LayoutHeading;
        }
    }

    private void ApplyBrandPalette()
    {
        var colorKey = _systemThemeProvider.IsDarkTheme
            ? BrandAccentDarkColorKey
            : BrandAccentLightColorKey;

        Resources[BrandAccentBrushKey] = CreateFrozenBrush(
            (Color)Resources[colorKey]);
    }

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private sealed record LayoutChoice(string? Klid, string DisplayName);

    private enum SlotPreviewState
    {
        Idle,
        Hovered,
        Selected,
    }
}

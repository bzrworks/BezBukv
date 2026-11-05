using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using BezBukv.App.Resources;
using BezBukv.Core.Keyboard;
using BezBukv.Core.Localization;
using BezBukv.Core.Platform;
using BezBukv.Core.Scenarios;
using BezBukv.Core.Settings;
using DrawingRectangle = System.Drawing.Rectangle;

namespace BezBukv.App;

public partial class KeyboardCalibrationWizardWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string resourceKey] =>
        AppStrings.ResourceManager.GetString(
            resourceKey,
            CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException(
            $"Missing localized resource '{resourceKey}'.");

    private const double WorkAreaMargin = 12;
    private const double PreferredFieldWidth = 692;
    private const double WindowHorizontalChrome = 76;
    private const double FieldHorizontalChrome = 34;
    private const double PreferredUnitSize = 58;
    private const double MinimumReadableUnitSize = 34;
    private const double PreferredRecordedLabelFontSize = 12;
    private const double PreferredRecordedSymbolFontSize = 16;
    private const double PreferredActiveGlyphSize = 36;
    private const double PreferredAddGlyphSize = 23;
    private const double EscapeButtonGap = 3;
    private const string BrandAccentBrushKey = "KeyboardBrandAccentBrush";
    private const string BrandAccentLightColorKey = "KeyboardBrandAccentLightColor";
    private const string BrandAccentDarkColorKey = "KeyboardBrandAccentDarkColor";
    private const string EmptyStrokeBrushKey = "CalibrationEmptyStrokeBrush";
    private const string EmptyStrokeLightColorKey = "CalibrationEmptyStrokeLightColor";
    private const string EmptyStrokeDarkColorKey = "CalibrationEmptyStrokeDarkColor";
    private const string PrimaryTextBrushKey = "TextFillColorPrimaryBrush";
    private const string SecondaryTextBrushKey = "TextFillColorSecondaryBrush";
    private const string PauseIconGeometry = "M8,6 L8,18 M16,6 L16,18";
    private const string ResumeIconGeometry = "M9,6 L18,12 9,18 Z";
    private const int EscapeScanCode = 0x01;
    private const int EscapeVirtualKey = 0x1B;

    private readonly KeyboardCalibrationController _controller;
    private readonly IReadOnlyList<string> _selectedLayoutKlids;
    private readonly IKeyboardLayoutSymbolService _keyboardLayoutSymbolService;
    private readonly ISystemThemeProvider _systemThemeProvider;
    private readonly ILanguageService _languageService;
    private readonly Dictionary<KeyboardLayoutSymbolCacheKey, string> _keyboardLayoutSymbolCache = [];
    private readonly DrawingRectangle _targetWorkAreaPixels;
    private readonly bool _showGeometryDiagnostics;
    private bool _isListeningForThemeChanges;
    private bool _positionedOnTargetMonitor;
    private IReadOnlyList<CustomKeyboardRow>? _renderedRows;
    private double _renderedUnitSize = double.NaN;
    private bool _renderedShowEscapeButton;
    private bool _renderedIsSuspended;
    private bool _showInvalidGeometryHint;
    private Rect? _workArea;

    public KeyboardCalibrationWizardWindow(
        KeyboardCalibrationController controller,
        IReadOnlyList<string> selectedLayoutKlids,
        IKeyboardLayoutSymbolService keyboardLayoutSymbolService,
        ISystemThemeProvider systemThemeProvider,
        ILanguageService languageService,
        DrawingRectangle targetWorkAreaPixels,
        bool showGeometryDiagnostics)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(selectedLayoutKlids);
        ArgumentNullException.ThrowIfNull(keyboardLayoutSymbolService);
        ArgumentNullException.ThrowIfNull(systemThemeProvider);
        ArgumentNullException.ThrowIfNull(languageService);

        InitializeComponent();
        _controller = controller;
        _selectedLayoutKlids = selectedLayoutKlids
            .Take(PeekSettings.MaximumSelectedLayoutCount)
            .ToArray();
        _keyboardLayoutSymbolService = keyboardLayoutSymbolService;
        _systemThemeProvider = systemThemeProvider;
        _languageService = languageService;
        _targetWorkAreaPixels = targetWorkAreaPixels;
        _showGeometryDiagnostics = showGeometryDiagnostics;
        ApplyBrandPalette();
        _languageService.LanguageChanged += OnLanguageChanged;
        _systemThemeProvider.ThemeChanged += OnSystemThemeChanged;
        _isListeningForThemeChanges = true;
        RefreshSnapshot();
    }

    public event Action<CustomKeyboardLayout>? CalibrationFinished;

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

    internal void RefreshSnapshot()
    {
        _showInvalidGeometryHint = false;
        RefreshSnapshotCore();
    }

    private void RefreshSnapshotCore()
    {
        var snapshot = _controller.Snapshot;
        var isSuspended = snapshot.Status == KeyboardCalibrationStatus.Suspended;
        var rows = snapshot.CompletedRows
            .Concat([snapshot.CurrentRow])
            .ToArray();
        var hasEscape = rows
            .SelectMany(row => row.Cells)
            .Any(IsEscapeCell);
        var showEscapeButton = !hasEscape;
        var maximumRecordedWidthUnits = Math.Max(
            2,
            rows.Select(row => row.Cells.Sum(cell => cell.WidthUnits)).Max());
        var currentRowWidthUnits =
            snapshot.CursorPosition + 1 + (showEscapeButton ? 2 : 1);
        var dpi = VisualTreeHelper.GetDpi(this);
        var horizontalDpiScale = dpi.DpiScaleX;
        var verticalDpiScale = dpi.DpiScaleY;
        var preferredUnitSize = FloorToDevicePixel(
            PreferredUnitSize,
            horizontalDpiScale);
        var minimumReadableUnitSize = FloorToDevicePixel(
            MinimumReadableUnitSize,
            horizontalDpiScale);
        var currentRowFixedWidth = showEscapeButton
            ? CeilingToDevicePixel(EscapeButtonGap, horizontalDpiScale)
            : 0;
        var availableContentWidth = FloorToDevicePixel(
            Math.Max(
                0,
                PreviewField.MaxWidth -
                    FieldHorizontalChrome -
                    SystemParameters.VerticalScrollBarWidth),
            horizontalDpiScale);
        var preferredRowWidth = Math.Max(
            maximumRecordedWidthUnits * preferredUnitSize,
            (currentRowWidthUnits * preferredUnitSize) + currentRowFixedWidth);
        var isRowWidthConstrained = preferredRowWidth > availableContentWidth;
        PreviewField.Width = isRowWidthConstrained
            ? PreviewField.MaxWidth
            : double.NaN;
        var minimumReadableRowWidth = Math.Max(
            maximumRecordedWidthUnits * minimumReadableUnitSize,
            (currentRowWidthUnits * minimumReadableUnitSize) + currentRowFixedWidth);
        var maximumFittingUnitSize = Math.Min(
            availableContentWidth / maximumRecordedWidthUnits,
            Math.Max(0, availableContentWidth - currentRowFixedWidth) /
                currentRowWidthUnits);
        double horizontalUnitSize;
        if (preferredRowWidth <= availableContentWidth)
        {
            horizontalUnitSize = preferredUnitSize;
        }
        else if (minimumReadableRowWidth <= availableContentWidth)
        {
            horizontalUnitSize = Math.Max(
                minimumReadableUnitSize,
                maximumFittingUnitSize);
        }
        else
        {
            // Real keyboard rows should not reach this branch. Keeping the row
            // visible without horizontal scrolling is the final fallback.
            horizontalUnitSize = maximumFittingUnitSize;
        }

        horizontalUnitSize = FloorToDevicePixel(
            horizontalUnitSize,
            horizontalDpiScale);

        const double rowBottomMargin = 7;
        var verticalRowMargin = CeilingToDevicePixel(
            rowBottomMargin,
            verticalDpiScale);
        var availableRowsHeight = FloorToDevicePixel(
            Math.Max(0, RowsScrollViewer.MaxHeight),
            verticalDpiScale);
        var totalRowsMargin = rows.Length * verticalRowMargin;
        var preferredVerticalUnitSize = FloorToDevicePixel(
            PreferredUnitSize,
            verticalDpiScale);
        var minimumReadableVerticalUnitSize = FloorToDevicePixel(
            MinimumReadableUnitSize,
            verticalDpiScale);
        var preferredRowsHeight =
            (rows.Length * preferredVerticalUnitSize) + totalRowsMargin;
        var minimumReadableRowsHeight =
            (rows.Length * minimumReadableVerticalUnitSize) + totalRowsMargin;
        var maximumVerticallyFittingUnitSize =
            Math.Max(0, availableRowsHeight - totalRowsMargin) / rows.Length;
        double verticalUnitSize;
        if (preferredRowsHeight <= availableRowsHeight)
        {
            verticalUnitSize = preferredVerticalUnitSize;
        }
        else if (minimumReadableRowsHeight <= availableRowsHeight)
        {
            verticalUnitSize = Math.Max(
                minimumReadableVerticalUnitSize,
                maximumVerticallyFittingUnitSize);
        }
        else
        {
            // Keep every row visible without vertical scrolling, even when
            // doing so requires shrinking below the readable threshold.
            verticalUnitSize = maximumVerticallyFittingUnitSize;
        }

        verticalUnitSize = FloorToDevicePixel(
            verticalUnitSize,
            verticalDpiScale);
        var unitSize = Math.Min(horizontalUnitSize, verticalUnitSize);

        if (ShouldRefreshRows(
                rows,
                unitSize,
                showEscapeButton,
                isSuspended))
        {
            var rowsPanel = new StackPanel();
            foreach (var completedRow in snapshot.CompletedRows)
            {
                rowsPanel.Children.Add(CreateRow(
                    completedRow,
                    unitSize,
                    isCurrent: false,
                    showEscapeButton: false,
                    isSuspended: false));
            }

            rowsPanel.Children.Add(CreateRow(
                snapshot.CurrentRow,
                unitSize,
                isCurrent: true,
                showEscapeButton,
                isSuspended));
            RowsScrollViewer.Content = rowsPanel;

            _renderedRows = rows;
            _renderedUnitSize = unitSize;
            _renderedShowEscapeButton = showEscapeButton;
            _renderedIsSuspended = isSuspended;
        }
        var pauseResumeLabel = isSuspended
            ? AppStrings.KeyboardCalibration_Resume
            : AppStrings.KeyboardCalibration_Pause;
        PauseResumeButton.Content = CreateActionIcon(
            isSuspended ? ResumeIconGeometry : PauseIconGeometry);
        PauseResumeButton.ToolTip = pauseResumeLabel;
        PauseResumeButton.IsEnabled = snapshot.Status is
            KeyboardCalibrationStatus.Active or KeyboardCalibrationStatus.Suspended;

        if (isSuspended)
        {
            HintText.Text =
                AppStrings.KeyboardCalibration_PausedHint;
        }
        else if (snapshot.Status == KeyboardCalibrationStatus.Active)
        {
            HintText.Text = AppStrings.KeyboardCalibration_Hint;
        }

        CenterInWorkArea();

        if (_showGeometryDiagnostics)
        {
            System.Diagnostics.Trace.WriteLine(string.Format(
                CultureInfo.CurrentCulture,
                "Keyboard calibration geometry | Width: {0:F3} | rounded: {1:F3} | Left: {2:F3}",
                ActualWidth,
                RoundToDevicePixel(ActualWidth, horizontalDpiScale),
                Left));
        }
    }

    private Viewbox CreateActionIcon(string geometryData) =>
        new()
        {
            Style = FindResource("CalibrationActionIconViewboxStyle") as Style,
            Child = new Path
            {
                Data = Geometry.Parse(geometryData),
                Style = FindResource("CalibrationActionIconPathStyle") as Style,
            },
        };

    internal void CancelFromCapture()
    {
        CancelAndClose();
    }

    internal void PauseFromCapture()
    {
        _controller.Pause();
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

        MaxWidth = availableWidth;
        PreviewField.MaxWidth = Math.Max(
            0,
            availableWidth - WindowHorizontalChrome);
        PreviewField.MinWidth = Math.Min(
            PreferredFieldWidth,
            PreviewField.MaxWidth);

        UpdateLayout();
        var measuredVerticalChrome = Math.Max(
            0,
            ActualHeight - RowsScrollViewer.ActualHeight);

        MaxHeight = availableHeight;
        RowsScrollViewer.MaxHeight = Math.Max(
            0,
            availableHeight - measuredVerticalChrome);
        _workArea = workArea;
        RefreshSnapshot();
        Opacity = 1;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_controller.Status is KeyboardCalibrationStatus.Active or
            KeyboardCalibrationStatus.Suspended)
        {
            _controller.Cancel();
        }

        base.OnClosing(e);
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

    private StackPanel CreateRow(
        CustomKeyboardRow row,
        double unitSize,
        bool isCurrent,
        bool showEscapeButton,
        bool isSuspended)
    {
        var rowPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 7),
        };

        if (!isCurrent && row.Cells.Count == 0)
        {
            rowPanel.Height = unitSize;
        }

        foreach (var cell in row.Cells)
        {
            rowPanel.Children.Add(CreateRecordedCell(cell, unitSize));
        }

        if (isCurrent)
        {
            rowPanel.Children.Add(CreateActiveCell(unitSize, isSuspended));
            rowPanel.Children.Add(CreateAddButton(
                unitSize,
                showEscapeButton,
                isSuspended));
        }

        return rowPanel;
    }

    private FrameworkElement CreateRecordedCell(
        CustomKeyboardCell cell,
        double unitSize)
    {
        var slot = CreateSlot(cell.WidthUnits * unitSize, unitSize);
        if (cell.Key is null)
        {
            slot.Children.Add(new Rectangle
            {
                Margin = new Thickness(3),
                Fill = Brushes.Transparent,
                Stroke = FindResource(EmptyStrokeBrushKey) as Brush,
                StrokeThickness = 1.5,
                StrokeDashArray = [3, 2],
                RadiusX = 7,
                RadiusY = 7,
            });
            return slot;
        }

        var cap = new Border
        {
            Style = FindResource("CalibrationRecordedKeycapStyle") as Style,
        };
        var symbols = ResolveRecordedKeySymbols(cell.Key);
        var hasPrintableSymbols = symbols.Any(symbolPair =>
            !string.IsNullOrEmpty(symbolPair.Shift)
            || !string.IsNullOrEmpty(symbolPair.Base));
        var labelLayer = new Border
        {
            Style = FindResource("CalibrationRecordedKeycapStyle") as Style,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Opacity = 1,
            IsHitTestVisible = false,
            Child = hasPrintableSymbols
                ? CreateRecordedSymbolContent(symbols, unitSize)
                : CreateRecordedKeyLabel(cell.Key, unitSize),
        };
        slot.Children.Add(cap);
        slot.Children.Add(labelLayer);
        return slot;
    }

    private IReadOnlyList<RecordedKeySymbols> ResolveRecordedKeySymbols(
        CustomKeyboardKey key) =>
        _selectedLayoutKlids
            .Select(klid => ResolveRecordedKeySymbols(key, klid))
            .ToArray();

    private RecordedKeySymbols ResolveRecordedKeySymbols(
        CustomKeyboardKey key,
        string klid)
    {
        var baseSymbol = GetCachedKeyboardLayoutSymbol(
            key,
            klid,
            shiftPressed: false);
        var shiftSymbol = GetCachedKeyboardLayoutSymbol(
            key,
            klid,
            shiftPressed: true);

        return new RecordedKeySymbols(
            Base: baseSymbol,
            Shift: shiftSymbol);
    }

    private string GetCachedKeyboardLayoutSymbol(
        CustomKeyboardKey key,
        string klid,
        bool shiftPressed)
    {
        var cacheKey = new KeyboardLayoutSymbolCacheKey(
            key.ScanCode,
            key.Extended,
            klid,
            shiftPressed);
        if (_keyboardLayoutSymbolCache.TryGetValue(cacheKey, out var symbol))
        {
            return symbol;
        }

        symbol = _keyboardLayoutSymbolService.GetKeyboardLayoutSymbol(
            key.ScanCode,
            key.Extended,
            klid,
            shiftPressed);
        _keyboardLayoutSymbolCache.Add(cacheKey, symbol);
        return symbol;
    }

    private static TextBlock CreateRecordedKeyLabel(
        CustomKeyboardKey key,
        double unitSize)
    {
        var label = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = ScaleCellContent(
                PreferredRecordedLabelFontSize,
                unitSize),
            FontWeight = FontWeights.SemiBold,
            Text = key.VirtualKey is { } virtualKey
                ? KeyboardVirtualKeyLabel.GetLabel(virtualKey, key.IsAltGr)
                : string.Empty,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        label.SetResourceReference(
            TextBlock.ForegroundProperty,
            PrimaryTextBrushKey);
        return label;
    }

    private static Grid CreateRecordedSymbolContent(
        IReadOnlyList<RecordedKeySymbols> symbols,
        double unitSize)
    {
        var content = new Grid
        {
            Margin = new Thickness(3),
        };
        var visibleSymbols = GetVisibleRecordedSymbols(symbols);

        switch (visibleSymbols.Count)
        {
            case 1:
                AddRecordedSymbol(
                    content,
                    visibleSymbols[0],
                    unitSize,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Center);
                break;
            case 2:
                var diagonalSymbols = visibleSymbols
                    .OrderBy(symbol => symbol.Row)
                    .ThenBy(symbol => symbol.Column)
                    .ToArray();
                var diagonalMargin = new Thickness(
                    ScaleCellContent(6, unitSize),
                    ScaleCellContent(2, unitSize),
                    0,
                    0);
                AddRecordedSymbol(
                    content,
                    diagonalSymbols[0],
                    unitSize,
                    HorizontalAlignment.Left,
                    VerticalAlignment.Top,
                    diagonalMargin);
                AddRecordedSymbol(
                    content,
                    diagonalSymbols[1],
                    unitSize,
                    HorizontalAlignment.Right,
                    VerticalAlignment.Bottom,
                    new Thickness(
                        0,
                        0,
                        diagonalMargin.Left,
                        diagonalMargin.Top));
                break;
            default:
                AddFullRecordedSymbolContent(content, visibleSymbols, unitSize);
                break;
        }

        return content;
    }

    private static IReadOnlyList<RecordedSymbol> GetVisibleRecordedSymbols(
        IReadOnlyList<RecordedKeySymbols> symbols)
    {
        var visibleSymbols = new List<RecordedSymbol>(capacity: 4);
        var knownSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var column = 0; column < symbols.Count; column++)
        {
            AddIfDistinct(
                symbols[column].Base,
                isBase: true,
                row: 1,
                column,
                visibleSymbols,
                knownSymbols);
            AddIfDistinct(
                symbols[column].Shift,
                isBase: false,
                row: 0,
                column,
                visibleSymbols,
                knownSymbols);
        }

        return visibleSymbols;
    }

    private static void AddIfDistinct(
        string value,
        bool isBase,
        int row,
        int column,
        ICollection<RecordedSymbol> visibleSymbols,
        ISet<string> knownSymbols)
    {
        if (!string.IsNullOrEmpty(value) && knownSymbols.Add(value))
        {
            visibleSymbols.Add(new RecordedSymbol(
                value,
                isBase,
                row,
                column));
        }
    }

    private static void AddFullRecordedSymbolContent(
        Grid content,
        IReadOnlyList<RecordedSymbol> symbols,
        double unitSize)
    {
        content.RowDefinitions.Add(new RowDefinition());
        content.RowDefinitions.Add(new RowDefinition());
        content.ColumnDefinitions.Add(new ColumnDefinition());
        content.ColumnDefinitions.Add(new ColumnDefinition());

        foreach (var symbol in symbols)
        {
            AddRecordedSymbol(
                content,
                symbol,
                unitSize,
                HorizontalAlignment.Stretch,
                VerticalAlignment.Center,
                row: symbol.Row,
                column: symbol.Column);
        }
    }

    private static void AddRecordedSymbol(
        Grid content,
        RecordedSymbol symbol,
        double unitSize,
        HorizontalAlignment horizontalAlignment,
        VerticalAlignment verticalAlignment,
        Thickness? margin = null,
        int? row = null,
        int? column = null)
    {
        if (string.IsNullOrEmpty(symbol.Value))
        {
            return;
        }

        var symbolText = new TextBlock
        {
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = verticalAlignment,
            FontSize = ScaleCellContent(
                PreferredRecordedSymbolFontSize,
                unitSize),
            FontWeight = symbol.IsBase ? FontWeights.SemiBold : FontWeights.Normal,
            Margin = margin ?? default,
            Text = symbol.Value,
            TextAlignment = TextAlignment.Center,
        };
        symbolText.SetResourceReference(
            TextBlock.ForegroundProperty,
            symbol.IsBase ? PrimaryTextBrushKey : SecondaryTextBrushKey);
        if (row is { } targetRow)
        {
            Grid.SetRow(symbolText, targetRow);
        }

        if (column is { } targetColumn)
        {
            Grid.SetColumn(symbolText, targetColumn);
        }

        content.Children.Add(symbolText);
    }

    private Grid CreateActiveCell(double unitSize, bool isSuspended)
    {
        var slot = CreateSlot(unitSize, unitSize);
        var cap = new Border
        {
            Style = FindResource("CalibrationActiveKeycapStyle") as Style,
            Opacity = isSuspended ? 0.35 : 1,
        };

        var glyphSize = ScaleCellContent(PreferredActiveGlyphSize, unitSize);
        var icon = new Canvas
        {
            Width = 24,
            Height = 24,
        };
        var keyGlyph = new Rectangle
        {
            Width = 16,
            Height = 10,
            RadiusX = 2.2,
            RadiusY = 2.2,
            Fill = Brushes.Transparent,
            Stroke = Brushes.White,
            StrokeThickness = 1.8,
            StrokeLineJoin = PenLineJoin.Round,
        };
        const double keyGlyphLeft = 4;
        Canvas.SetLeft(keyGlyph, keyGlyphLeft);
        Canvas.SetTop(keyGlyph, 12);
        icon.Children.Add(keyGlyph);

        var tapGeometry = Geometry.Parse(
            "M12,2 L12,10 M8.2,6.3 L12,10.1 15.8,6.3");
        var tapGlyph = new Path
        {
            Data = tapGeometry,
            Fill = Brushes.Transparent,
            Stroke = Brushes.White,
            StrokeThickness = 1.8,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            RenderTransform = new TranslateTransform(),
        };
        var tapStroke = new Pen(Brushes.White, tapGlyph.StrokeThickness)
        {
            StartLineCap = tapGlyph.StrokeStartLineCap,
            EndLineCap = tapGlyph.StrokeEndLineCap,
            LineJoin = tapGlyph.StrokeLineJoin,
        };
        var tapBounds = tapGeometry.GetRenderBounds(tapStroke);
        var keyCenterX = keyGlyphLeft + (keyGlyph.Width / 2);
        var tapCenterX = tapBounds.Left + (tapBounds.Width / 2);
        Canvas.SetLeft(tapGlyph, keyCenterX - tapCenterX);
        icon.Children.Add(tapGlyph);

        cap.Child = new Viewbox
        {
            Width = glyphSize,
            Height = glyphSize,
            UseLayoutRounding = false,
            Stretch = Stretch.Uniform,
            Child = icon,
        };

        if (!isSuspended
            && _controller.Status == KeyboardCalibrationStatus.Active
            && SystemParameters.ClientAreaAnimation)
        {
            tapGlyph.Loaded += (_, _) =>
                ((Storyboard)FindResource("CalibrationTapStoryboard"))
                    .Begin(tapGlyph, HandoffBehavior.SnapshotAndReplace);
        }

        slot.Children.Add(cap);
        return slot;
    }

    private Grid CreateAddButton(
        double unitSize,
        bool showEscapeButton,
        bool isSuspended)
    {
        var addSlot = CreateSlot(unitSize, unitSize);
        var button = new Button
        {
            IsEnabled = !isSuspended,
            Margin = new Thickness(3),
            Opacity = isSuspended ? 0.35 : 1,
            Style = FindResource("CalibrationAddButtonStyle") as Style,
            ToolTip = AppStrings.KeyboardCalibration_AddEmptySlot_ToolTip,
        };
        button.Click += OnAddEmptySlotClick;

        button.Content = new Path
        {
            Width = ScaleCellContent(PreferredAddGlyphSize, unitSize),
            Height = ScaleCellContent(PreferredAddGlyphSize, unitSize),
            Data = Geometry.Parse("M12,6 L12,18 M6,12 L18,12"),
            Fill = Brushes.Transparent,
            Stroke = FindResource(BrandAccentBrushKey) as Brush,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Stretch = Stretch.Uniform,
        };
        addSlot.Children.Add(button);

        if (!showEscapeButton || isSuspended)
        {
            return addSlot;
        }

        var hoverRegion = CreateSlot(
            (unitSize * 2) + EscapeButtonGap,
            unitSize);
        hoverRegion.Background = Brushes.Transparent;
        addSlot.HorizontalAlignment = HorizontalAlignment.Left;
        addSlot.VerticalAlignment = VerticalAlignment.Top;
        hoverRegion.Children.Add(addSlot);

        var escapeButton = new Button
        {
            Height = unitSize - 6,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(unitSize + EscapeButtonGap, 3, 0, 0),
            MinWidth = unitSize - 6,
            MaxWidth = unitSize,
            FontSize = ScaleCellContent(
                PreferredRecordedLabelFontSize,
                unitSize),
            Content = AppStrings.KeyboardCalibration_AddEscape_Label,
            Style = FindResource("CalibrationEscapeButtonStyle") as Style,
            ToolTip = AppStrings.KeyboardCalibration_AddEscape_ToolTip,
            Opacity = 0,
            IsHitTestVisible = false,
            Focusable = false,
            RenderTransform = new TranslateTransform(-6, 0),
        };
        escapeButton.Click += OnAddEscapeClick;

        var fadeInStoryboard =
            (Storyboard)FindResource("CalibrationEscapeFadeInStoryboard");

        void UpdateEscapeVisibility(object sender, RoutedEventArgs e)
        {
            var shouldShow = button.IsMouseOver
                || button.IsKeyboardFocusWithin
                || (escapeButton.IsHitTestVisible
                    && (hoverRegion.IsMouseOver
                        || hoverRegion.IsKeyboardFocusWithin));
            if (shouldShow == escapeButton.IsHitTestVisible)
            {
                return;
            }

            escapeButton.IsHitTestVisible = shouldShow;
            escapeButton.Focusable = shouldShow;
            if (shouldShow)
            {
                fadeInStoryboard.Begin(
                    escapeButton,
                    HandoffBehavior.SnapshotAndReplace,
                    isControllable: true);
                return;
            }

            fadeInStoryboard.Remove(escapeButton);
            escapeButton.Opacity = 0;
        }

        button.MouseEnter += UpdateEscapeVisibility;
        button.GotKeyboardFocus += UpdateEscapeVisibility;
        hoverRegion.MouseLeave += UpdateEscapeVisibility;
        hoverRegion.GotKeyboardFocus += UpdateEscapeVisibility;
        hoverRegion.LostKeyboardFocus += UpdateEscapeVisibility;
        hoverRegion.Children.Add(escapeButton);
        return hoverRegion;
    }

    private static Grid CreateSlot(double width, double unitSize) =>
        new()
        {
            Width = width,
            Height = unitSize,
        };

    private static double FloorToDevicePixel(double value, double dpiScale) =>
        Math.Floor(value * dpiScale) / dpiScale;

    private static double CeilingToDevicePixel(double value, double dpiScale) =>
        Math.Ceiling(value * dpiScale) / dpiScale;

    private static double RoundToDevicePixel(double value, double dpiScale) =>
        Math.Round(value * dpiScale, MidpointRounding.AwayFromZero) / dpiScale;

    private static double ScaleCellContent(
        double preferredSize,
        double unitSize) =>
        preferredSize * unitSize / PreferredUnitSize;

    private bool ShouldRefreshRows(
        IReadOnlyList<CustomKeyboardRow> rows,
        double unitSize,
        bool showEscapeButton,
        bool isSuspended) =>
        _renderedRows is null
        || _renderedUnitSize != unitSize
        || _renderedShowEscapeButton != showEscapeButton
        || _renderedIsSuspended != isSuspended
        || !RowsEqual(_renderedRows, rows);

    private static bool RowsEqual(
        IReadOnlyList<CustomKeyboardRow> left,
        IReadOnlyList<CustomKeyboardRow> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var rowIndex = 0; rowIndex < left.Count; rowIndex++)
        {
            var leftCells = left[rowIndex].Cells;
            var rightCells = right[rowIndex].Cells;
            if (leftCells.Count != rightCells.Count)
            {
                return false;
            }

            for (var cellIndex = 0; cellIndex < leftCells.Count; cellIndex++)
            {
                if (leftCells[cellIndex] != rightCells[cellIndex])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void CenterInWorkArea()
    {
        if (_workArea is not { } workArea)
        {
            return;
        }

        UpdateLayout();

        var availableWidth = Math.Max(0, workArea.Width - (WorkAreaMargin * 2));
        var availableHeight = Math.Max(0, workArea.Height - (WorkAreaMargin * 2));
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = RoundToDevicePixel(
            workArea.Left + WorkAreaMargin +
                Math.Max(0, (availableWidth - ActualWidth) / 2),
            dpi.DpiScaleX);
        Top = RoundToDevicePixel(
            workArea.Top + WorkAreaMargin +
                Math.Max(0, (availableHeight - ActualHeight) / 2),
            dpi.DpiScaleY);
    }

    private void OnAddEmptySlotClick(object sender, RoutedEventArgs e)
    {
        _controller.AddEmptySlot();
        RefreshSnapshot();
    }

    private void OnAddEscapeClick(object sender, RoutedEventArgs e)
    {
        _controller.AddKey(
            EscapeScanCode,
            extended: false,
            virtualKey: EscapeVirtualKey);
        RefreshSnapshot();
    }

    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        _controller.Undo();
        RefreshSnapshot();
    }

    private void OnResetRowClick(object sender, RoutedEventArgs e)
    {
        _controller.ResetRow();
        RefreshSnapshot();
    }

    private void OnNextRowClick(object sender, RoutedEventArgs e)
    {
        _controller.NextRow();
        RefreshSnapshot();
    }

    private void OnPauseResumeClick(object sender, RoutedEventArgs e)
    {
        switch (_controller.Status)
        {
            case KeyboardCalibrationStatus.Active:
                _controller.Pause();
                break;
            case KeyboardCalibrationStatus.Suspended:
                _controller.Resume();
                break;
        }

        RefreshSnapshot();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        CancelAndClose();
    }

    private void OnFinishClick(object sender, RoutedEventArgs e)
    {
        var result = _controller.Finish();
        RefreshSnapshot();

        if (result is { Status: KeyboardCalibrationFinishStatus.Succeeded, Layout: { } layout })
        {
            CalibrationFinished?.Invoke(layout);
            return;
        }

        if (result.Status == KeyboardCalibrationFinishStatus.InvalidGeometry)
        {
            _showInvalidGeometryHint = true;
            HintText.Text = AppStrings.KeyboardCalibration_InvalidGeometry;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape
            || _controller.Status != KeyboardCalibrationStatus.Active)
        {
            return;
        }

        e.Handled = true;
        PauseFromCapture();
    }

    private void CancelAndClose()
    {
        _controller.Cancel();
        Close();
    }

    private static bool IsEscapeCell(CustomKeyboardCell cell) =>
        cell.Key is { } key
        && (key.VirtualKey == EscapeVirtualKey
            || (key.ScanCode == EscapeScanCode && !key.Extended));

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
        _renderedRows = null;
        RefreshSnapshotCore();
        if (_showInvalidGeometryHint)
        {
            HintText.Text = AppStrings.KeyboardCalibration_InvalidGeometry;
        }
    }

    private void ApplyBrandPalette()
    {
        var useDarkPalette = _systemThemeProvider.IsDarkTheme;
        ApplyPaletteBrush(
            BrandAccentBrushKey,
            useDarkPalette ? BrandAccentDarkColorKey : BrandAccentLightColorKey);
        ApplyPaletteBrush(
            EmptyStrokeBrushKey,
            useDarkPalette ? EmptyStrokeDarkColorKey : EmptyStrokeLightColorKey);
    }

    private void ApplyPaletteBrush(string brushKey, string colorKey) =>
        Resources[brushKey] = CreateFrozenBrush((Color)Resources[colorKey]);

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private readonly record struct KeyboardLayoutSymbolCacheKey(
        int ScanCode,
        bool Extended,
        string Klid,
        bool ShiftPressed);

    private readonly record struct RecordedKeySymbols(string Base, string Shift);

    private readonly record struct RecordedSymbol(
        string Value,
        bool IsBase,
        int Row,
        int Column);
}

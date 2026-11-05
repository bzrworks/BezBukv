using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BezBukv.Core.Keyboard;
using BezBukv.Core.Platform;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace BezBukv.Windows;

public partial class KeyboardPeekOverlayWindow : Window
{
    private const int ExtendedWindowStyleIndex = -20;
    private const long ExtendedWindowStyleToolWindow = 0x00000080L;
    private const long ExtendedWindowStyleTransparent = 0x00000020L;
    private const long ExtendedWindowStyleNoActivate = 0x08000000L;

    private const uint SetWindowPositionNoSize = 0x0001;
    private const uint SetWindowPositionNoActivate = 0x0010;
    private const uint SetWindowPositionNoOwnerOrder = 0x0200;
    private const uint SetWindowPositionShowWindow = 0x0040;

    private const int WorkAreaMargin = 8;
    private const int CaretOffset = 12;

    private static readonly nint TopmostWindow = new(-1);

    private readonly DrawingPoint? _caretPosition;
    private readonly DrawingRectangle _workArea;
    private readonly DrawingRectangle? _placementArea;
    private OverlayAppearanceOptions _appearanceOptions;

    private DoubleAnimation? _fadeOutAnimation;
    private bool _isContentRendered;
    private bool _isClosing;
    private bool _isClosed;

    public KeyboardPeekOverlayWindow(
        KeyboardOverlayGrid overlayGrid,
        DrawingPoint? caretPosition,
        DrawingRectangle workArea,
        OverlayAppearanceOptions appearanceOptions,
        DrawingRectangle? placementArea = null)
    {
        ArgumentNullException.ThrowIfNull(overlayGrid);
        ArgumentNullException.ThrowIfNull(appearanceOptions);

        InitializeComponent();

        _caretPosition = caretPosition;
        _workArea = workArea;
        _placementArea = placementArea;
        _appearanceOptions = appearanceOptions;
        ApplyScale(appearanceOptions.Scale);

        foreach (var cell in overlayGrid.Cells)
        {
            AddKeycap(cell);
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var windowHandle = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowExtendedStyle(windowHandle);
        _ = SetWindowExtendedStyle(
            windowHandle,
            extendedStyle |
                (nint)ExtendedWindowStyleToolWindow |
                (nint)ExtendedWindowStyleTransparent |
                (nint)ExtendedWindowStyleNoActivate);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        _isContentRendered = true;
        PositionWindow();

        Opacity = _appearanceOptions.Opacity;

        if (!_appearanceOptions.AnimationEnabled || _isClosing)
        {
            return;
        }

        BeginAnimation(
            OpacityProperty,
            CreateFadeAnimation(0, _appearanceOptions.Opacity));
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        if (_isContentRendered && _placementArea is not null)
        {
            PositionWindow();
        }
    }

    internal void UpdateAppearance(OverlayAppearanceOptions appearanceOptions)
    {
        ArgumentNullException.ThrowIfNull(appearanceOptions);

        if (_isClosed || _isClosing)
        {
            return;
        }

        _appearanceOptions = appearanceOptions;
        ApplyScale(appearanceOptions.Scale);
        BeginAnimation(OpacityProperty, null);
        Opacity = appearanceOptions.Opacity;

        if (_isContentRendered)
        {
            UpdateLayout();
            PositionWindow();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        CancelFadeOutAnimation();
        BeginAnimation(OpacityProperty, null);
        base.OnClosed(e);
    }

    internal void HideAndClose()
    {
        if (_isClosed || _isClosing)
        {
            return;
        }

        if (!_appearanceOptions.AnimationEnabled)
        {
            CloseImmediately();
            return;
        }

        _isClosing = true;
        _fadeOutAnimation = CreateFadeAnimation(to: 0);
        _fadeOutAnimation.FillBehavior = FillBehavior.HoldEnd;
        _fadeOutAnimation.Completed += OnFadeOutAnimationCompleted;
        BeginAnimation(OpacityProperty, _fadeOutAnimation);
    }

    internal void CloseImmediately()
    {
        if (_isClosed)
        {
            return;
        }

        _isClosing = true;
        CancelFadeOutAnimation();
        BeginAnimation(OpacityProperty, null);
        Close();
    }

    private void AddKeycap(KeyboardOverlayGridCell cell)
    {
        if (cell.IsSpacer)
        {
            return;
        }

        var keycap = CreateKeycap(
            cell.SymbolSlots,
            cell.DisplayLabel,
            cell.IsCenter);

        Grid.SetRow(keycap, cell.Offset.Row + 1);
        Grid.SetColumn(keycap, cell.Offset.Column + 1);
        KeyboardGrid.Children.Add(keycap);
    }

    private static Border CreateKeycap(
        IReadOnlyList<KeyboardOverlaySymbolSlot> symbolSlots,
        string? displayLabel,
        bool isCenter)
    {
        var visibleSymbols = GetVisibleSymbols(symbolSlots);
        var keycap = new Border
        {
            Width = isCenter ? 68 : 60,
            Height = isCenter ? 60 : 52,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(isCenter ? 2 : 1),
            CornerRadius = new CornerRadius(9),
        };
        keycap.SetResourceReference(
            Border.BackgroundProperty,
            "ControlFillColorDefaultBrush");
        keycap.SetResourceReference(
            Border.BorderBrushProperty,
            isCenter
                ? "AccentFillColorDefaultBrush"
                : "ControlStrokeColorDefaultBrush");

        keycap.Child = displayLabel is not null
            ? CreateKeycapLabel(displayLabel, isCenter, isNamedLabel: true)
            : visibleSymbols.Count switch
            {
                0 => CreateKeycapLabel(string.Empty, isCenter),
                1 => CreateKeycapLabel(visibleSymbols[0].Value, isCenter),
                2 => CreateDualSymbolContent(
                    visibleSymbols,
                    isCenter),
                _ => CreateFullSymbolContent(visibleSymbols, isCenter),
            };

        return keycap;
    }

    private static TextBlock CreateKeycapLabel(
        string label,
        bool isCenter,
        bool isNamedLabel = false)
    {
        var fontSize = isNamedLabel
            ? isCenter ? 13 : 12
            : label.Length switch
            {
                > 6 => 10,
                > 3 => 12,
                _ => isCenter ? 20 : 18,
            };

        var textBlock = new TextBlock
        {
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = fontSize,
            FontWeight = isCenter ? FontWeights.SemiBold : FontWeights.Normal,
            Text = label,
            TextAlignment = TextAlignment.Center,
            TextWrapping = isNamedLabel ? TextWrapping.Wrap : TextWrapping.NoWrap,
        };
        if (isNamedLabel)
        {
            textBlock.Margin = new Thickness(3, 0, 3, 0);
        }

        textBlock.SetResourceReference(
            TextBlock.ForegroundProperty,
            "TextFillColorPrimaryBrush");
        return textBlock;
    }

    private static Grid CreateDualSymbolContent(
        IReadOnlyList<OverlaySymbol> symbols,
        bool isCenter)
    {
        var content = new Grid();
        var diagonalSymbols = symbols
            .OrderBy(symbol => symbol.Row)
            .ThenBy(symbol => symbol.Column)
            .ToArray();

        var upperLeftText = new TextBlock
        {
            Margin = new Thickness(9, 5, 0, 0),
            HorizontalAlignment = WpfHorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            FontSize = isCenter ? 19 : 17,
            FontWeight = diagonalSymbols[0].IsBase
                ? FontWeights.SemiBold
                : FontWeights.Normal,
            Text = diagonalSymbols[0].Value,
        };
        upperLeftText.SetResourceReference(
            TextBlock.ForegroundProperty,
            diagonalSymbols[0].IsBase
                ? "TextFillColorPrimaryBrush"
                : "TextFillColorSecondaryBrush");

        var lowerRightText = new TextBlock
        {
            Margin = new Thickness(0, 0, 9, 5),
            HorizontalAlignment = WpfHorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            FontSize = isCenter ? 20 : 18,
            FontWeight = diagonalSymbols[1].IsBase
                ? FontWeights.SemiBold
                : FontWeights.Normal,
            Text = diagonalSymbols[1].Value,
        };
        lowerRightText.SetResourceReference(
            TextBlock.ForegroundProperty,
            diagonalSymbols[1].IsBase
                ? "TextFillColorPrimaryBrush"
                : "TextFillColorSecondaryBrush");

        content.Children.Add(upperLeftText);
        content.Children.Add(lowerRightText);
        return content;
    }

    private static Grid CreateFullSymbolContent(
        IReadOnlyList<OverlaySymbol> symbols,
        bool isCenter)
    {
        var content = new Grid
        {
            Margin = new Thickness(7, 4, 7, 4),
        };
        content.RowDefinitions.Add(new RowDefinition());
        content.RowDefinitions.Add(new RowDefinition());
        content.ColumnDefinitions.Add(new ColumnDefinition());
        content.ColumnDefinitions.Add(new ColumnDefinition());

        foreach (var symbol in symbols)
        {
            var symbolText = new TextBlock
            {
                HorizontalAlignment = WpfHorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = isCenter ? 18 : 16,
                FontWeight = symbol.IsBase
                    ? FontWeights.SemiBold
                    : FontWeights.Normal,
                Text = symbol.Value,
                TextAlignment = TextAlignment.Center,
            };
            symbolText.SetResourceReference(
                TextBlock.ForegroundProperty,
                symbol.IsBase
                    ? "TextFillColorPrimaryBrush"
                    : "TextFillColorSecondaryBrush");

            Grid.SetRow(symbolText, symbol.Row);
            Grid.SetColumn(symbolText, symbol.Column);
            content.Children.Add(symbolText);
        }

        return content;
    }

    private static IReadOnlyList<OverlaySymbol> GetVisibleSymbols(
        IReadOnlyList<KeyboardOverlaySymbolSlot> symbolSlots)
    {
        var visibleSymbols = new List<OverlaySymbol>(capacity: 4);
        var knownSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var slot in symbolSlots)
        {
            AddIfDistinct(
                slot.BaseSymbol,
                isBase: true,
                GetFullSymbolPosition(slot, shiftPressed: false),
                visibleSymbols,
                knownSymbols);
            AddIfDistinct(
                slot.ShiftSymbol,
                isBase: false,
                GetFullSymbolPosition(slot, shiftPressed: true),
                visibleSymbols,
                knownSymbols);
        }

        return visibleSymbols;
    }

    private static void AddIfDistinct(
        string symbol,
        bool isBase,
        (int Row, int Column) position,
        ICollection<OverlaySymbol> visibleSymbols,
        ISet<string> knownSymbols)
    {
        if (!string.IsNullOrEmpty(symbol) && knownSymbols.Add(symbol))
        {
            visibleSymbols.Add(new OverlaySymbol(
                symbol,
                isBase,
                position.Row,
                position.Column));
        }
    }

    private static (int Row, int Column) GetFullSymbolPosition(
        KeyboardOverlaySymbolSlot slot,
        bool shiftPressed) =>
        (shiftPressed ? 0 : 1, slot.Slot - 1);

    private DrawingPoint CalculatePosition(int windowWidth, int windowHeight)
    {
        var minimumX = _workArea.Left + WorkAreaMargin;
        var minimumY = _workArea.Top + WorkAreaMargin;
        var maximumX = Math.Max(
            minimumX,
            _workArea.Right - windowWidth - WorkAreaMargin);
        var maximumY = Math.Max(
            minimumY,
            _workArea.Bottom - windowHeight - WorkAreaMargin);

        if (_placementArea is DrawingRectangle placementArea)
        {
            var previewTargetX = placementArea.Width >= windowWidth
                ? placementArea.Left + ((placementArea.Width - windowWidth) / 2)
                : placementArea.Right - windowWidth;
            var previewTargetY =
                placementArea.Top + ((placementArea.Height - windowHeight) / 2);

            return new DrawingPoint(
                Math.Clamp(previewTargetX, minimumX, maximumX),
                Math.Clamp(previewTargetY, minimumY, maximumY));
        }

        if (_caretPosition is not DrawingPoint caretPosition)
        {
            return new DrawingPoint(maximumX, maximumY);
        }

        var targetX = caretPosition.X + CaretOffset;
        if (targetX > maximumX)
        {
            targetX = caretPosition.X - windowWidth - CaretOffset;
        }

        var targetY = caretPosition.Y + CaretOffset;
        if (targetY > maximumY)
        {
            targetY = caretPosition.Y - windowHeight - CaretOffset;
        }

        return new DrawingPoint(
            Math.Clamp(targetX, minimumX, maximumX),
            Math.Clamp(targetY, minimumY, maximumY));
    }

    private void ApplyScale(double scale)
    {
        OverlayRoot.LayoutTransform = scale == OverlayAppearanceOptions.DefaultScale
            ? Transform.Identity
            : new ScaleTransform(scale, scale);
    }

    private void PositionWindow()
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        if (!GetWindowRect(windowHandle, out var windowRect))
        {
            return;
        }

        var target = CalculatePosition(
            windowRect.Right - windowRect.Left,
            windowRect.Bottom - windowRect.Top);
        _ = SetWindowPos(
            windowHandle,
            TopmostWindow,
            target.X,
            target.Y,
            0,
            0,
            SetWindowPositionNoSize |
                SetWindowPositionNoActivate |
                SetWindowPositionNoOwnerOrder |
                SetWindowPositionShowWindow);
    }

    private DoubleAnimation CreateFadeAnimation(double to) =>
        new(
            to,
            new Duration(TimeSpan.FromMilliseconds(
                _appearanceOptions.AnimationDurationMs)))
        {
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut,
            },
        };

    private DoubleAnimation CreateFadeAnimation(double from, double to) =>
        new(
            from,
            to,
            new Duration(TimeSpan.FromMilliseconds(
                _appearanceOptions.AnimationDurationMs)))
        {
            EasingFunction = new CubicEase
            {
                EasingMode = EasingMode.EaseOut,
            },
            FillBehavior = FillBehavior.Stop,
        };

    private void OnFadeOutAnimationCompleted(object? sender, EventArgs eventArgs)
    {
        CancelFadeOutAnimation();

        if (!_isClosed)
        {
            Close();
        }
    }

    private void CancelFadeOutAnimation()
    {
        if (_fadeOutAnimation is null)
        {
            return;
        }

        _fadeOutAnimation.Completed -= OnFadeOutAnimationCompleted;
        _fadeOutAnimation = null;
    }

    private static nint GetWindowExtendedStyle(nint windowHandle) =>
        nint.Size == 8
            ? GetWindowLongPtr64(windowHandle, ExtendedWindowStyleIndex)
            : new nint(GetWindowLong32(windowHandle, ExtendedWindowStyleIndex));

    private static nint SetWindowExtendedStyle(nint windowHandle, nint extendedStyle) =>
        nint.Size == 8
            ? SetWindowLongPtr64(windowHandle, ExtendedWindowStyleIndex, extendedStyle)
            : new nint(SetWindowLong32(
                windowHandle,
                ExtendedWindowStyleIndex,
                extendedStyle.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr64(nint windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(nint windowHandle, int index, int newLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint windowHandle, int index, nint newLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint windowHandle, out NativeRectangle rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private readonly record struct OverlaySymbol(
        string Value,
        bool IsBase,
        int Row,
        int Column);
}

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using BezBukv.Core.Platform;
using DrawingRectangle = System.Drawing.Rectangle;

namespace BezBukv.App;

public partial class ResultWindow : Window
{
    private static readonly Duration OpeningAnimationDuration =
        new(TimeSpan.FromMilliseconds(220));
    private static readonly TimeSpan AutoCloseDelay = TimeSpan.FromSeconds(5);

    private const double WorkAreaMargin = 4;
    private const double OpeningOffset = 32;

    private readonly string _resultText;
    private readonly IClipboardService _clipboardService;
    private readonly Func<CancellationToken, ValueTask> _tryPasteAsync;
    private readonly DrawingRectangle _targetWorkAreaPixels;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly DispatcherTimer _autoCloseTimer = new()
    {
        Interval = AutoCloseDelay,
    };
    private bool _actionInProgress;
    private bool _autoCloseReady;
    private bool _autoCloseCancelled;
    private bool _positionedOnTargetMonitor;

    public ResultWindow(
        string resultText,
        IClipboardService clipboardService,
        Func<CancellationToken, ValueTask> tryPasteAsync,
        DrawingRectangle targetWorkAreaPixels)
    {
        ArgumentNullException.ThrowIfNull(resultText);
        ArgumentNullException.ThrowIfNull(clipboardService);
        ArgumentNullException.ThrowIfNull(tryPasteAsync);

        InitializeComponent();

        _resultText = resultText;
        _clipboardService = clipboardService;
        _tryPasteAsync = tryPasteAsync;
        _targetWorkAreaPixels = targetWorkAreaPixels;
        ResultTextBox.Text = resultText;

        _autoCloseTimer.Tick += OnAutoCloseTimerTick;
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
        var finalLeft = Math.Max(
            workArea.Left + WorkAreaMargin,
            workArea.Right - ActualWidth - WorkAreaMargin);
        var finalTop = Math.Max(
            workArea.Top + WorkAreaMargin,
            workArea.Bottom - ActualHeight - WorkAreaMargin);

        Left = finalLeft;
        Top = finalTop;
        Opacity = 1;

        var easing = new CubicEase
        {
            EasingMode = EasingMode.EaseOut,
        };

        BeginAnimation(
            LeftProperty,
            CreateOpeningAnimation(finalLeft + OpeningOffset, finalLeft, easing));
        BeginAnimation(
            TopProperty,
            CreateOpeningAnimation(finalTop + OpeningOffset, finalTop, easing));
        BeginAnimation(
            OpacityProperty,
            CreateOpeningAnimation(0, 1, easing));

        _autoCloseReady = true;

        if (!_autoCloseCancelled && !IsMouseOver)
        {
            _autoCloseTimer.Start();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoCloseTimer.Stop();
        _autoCloseTimer.Tick -= OnAutoCloseTimerTick;
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
        base.OnClosed(e);
    }

    private void OnAutoCloseTimerTick(object? sender, EventArgs e)
    {
        _autoCloseTimer.Stop();
        Close();
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (!_autoCloseCancelled)
        {
            _autoCloseTimer.Stop();
        }
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_autoCloseReady && !_autoCloseCancelled)
        {
            _autoCloseTimer.Start();
        }
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _autoCloseCancelled = true;
        _autoCloseTimer.Stop();
    }

    private async void OnCopyClick(object sender, RoutedEventArgs e)
    {
        var effectiveText = GetEffectiveText();

        await ExecuteActionAsync(async cancellationToken =>
        {
            await _clipboardService.SetUnicodeTextAsync(effectiveText, cancellationToken);
        });
    }

    private async void OnTryPasteClick(object sender, RoutedEventArgs e)
    {
        var effectiveText = GetEffectiveText();

        await ExecuteActionAsync(async cancellationToken =>
        {
            await _clipboardService.SetUnicodeTextAsync(effectiveText, cancellationToken);
            await _tryPasteAsync(cancellationToken);
        });
    }

    private string GetEffectiveText() =>
        ResultTextBox.SelectionLength > 0
            ? ResultTextBox.SelectedText
            : _resultText;

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

    private async Task ExecuteActionAsync(
        Func<CancellationToken, ValueTask> action)
    {
        if (_actionInProgress)
        {
            return;
        }

        _actionInProgress = true;
        CopyButton.IsEnabled = false;
        TryPasteButton.IsEnabled = false;

        try
        {
            await action(_lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            _actionInProgress = false;

            if (IsLoaded)
            {
                CopyButton.IsEnabled = true;
                TryPasteButton.IsEnabled = true;
            }
        }
    }

    private static DoubleAnimation CreateOpeningAnimation(
        double from,
        double to,
        IEasingFunction easingFunction) =>
        new(from, to, OpeningAnimationDuration)
        {
            EasingFunction = easingFunction,
            FillBehavior = FillBehavior.Stop,
        };
}

internal static class CorrectionWindowPlacement
{
    private const uint SetWindowPositionNoSize = 0x0001;
    private const uint SetWindowPositionNoZOrder = 0x0004;
    private const uint SetWindowPositionNoActivate = 0x0010;

    public static bool TryMoveToMonitor(
        Window window,
        DrawingRectangle workAreaPixels)
    {
        try
        {
            if (workAreaPixels.Width <= 0 || workAreaPixels.Height <= 0)
            {
                return false;
            }

            var windowHandle = new WindowInteropHelper(window).Handle;
            return windowHandle != IntPtr.Zero && SetWindowPos(
                windowHandle,
                IntPtr.Zero,
                workAreaPixels.Left,
                workAreaPixels.Top,
                0,
                0,
                SetWindowPositionNoSize |
                    SetWindowPositionNoZOrder |
                    SetWindowPositionNoActivate);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool TryGetWorkAreaInDips(
        Window window,
        DrawingRectangle workAreaPixels,
        out Rect workArea)
    {
        workArea = default;

        try
        {
            var transformFromDevice = PresentationSource
                .FromVisual(window)?
                .CompositionTarget?
                .TransformFromDevice;
            if (transformFromDevice is not { } transform)
            {
                return false;
            }

            var topLeft = transform.Transform(
                new Point(workAreaPixels.Left, workAreaPixels.Top));
            var bottomRight = transform.Transform(
                new Point(workAreaPixels.Right, workAreaPixels.Bottom));
            workArea = new Rect(topLeft, bottomRight);
            return workArea.Width > 0 && workArea.Height > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr windowInsertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}

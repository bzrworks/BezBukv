using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using BezBukv.App.Resources;
using DrawingRectangle = System.Drawing.Rectangle;

namespace BezBukv.App;

public partial class NotificationWindow : Window
{
    private static readonly Duration OpeningAnimationDuration =
        new(TimeSpan.FromMilliseconds(220));
    private static readonly TimeSpan AutoCloseDelay = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan WarningAutoCloseDelay = TimeSpan.FromSeconds(8);

    private const double WorkAreaMargin = 4;
    private const double OpeningOffset = 32;

    private readonly DispatcherTimer _autoCloseTimer = new();
    private readonly DrawingRectangle _targetWorkAreaPixels;
    private readonly bool _isWarning;
    private bool _positionedOnTargetMonitor;
    private bool _warningSoundPlayed;

    public NotificationWindow(
        string title,
        string message,
        bool isWarning,
        DrawingRectangle targetWorkAreaPixels)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        InitializeComponent();

        _targetWorkAreaPixels = targetWorkAreaPixels;
        _isWarning = isWarning;
        _autoCloseTimer.Interval = isWarning ? WarningAutoCloseDelay : AutoCloseDelay;
        Title = title;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        StatusGlyph.Text = isWarning
            ? AppStrings.Notification_WarningGlyph
            : AppStrings.Notification_InfoGlyph;
        StatusGlyph.FontWeight = isWarning ? FontWeights.SemiBold : FontWeights.Normal;
        StatusGlyphBorder.BorderThickness = isWarning ? new Thickness(2) : new Thickness(1);
        StatusGlyphBorder.Opacity = isWarning ? 1 : 0.72;

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

        if (_isWarning && !_warningSoundPlayed)
        {
            _warningSoundPlayed = true;
            System.Media.SystemSounds.Asterisk.Play();
        }

        _autoCloseTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoCloseTimer.Stop();
        _autoCloseTimer.Tick -= OnAutoCloseTimerTick;
        base.OnClosed(e);
    }

    private void OnAutoCloseTimerTick(object? sender, EventArgs e)
    {
        _autoCloseTimer.Stop();
        Close();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Close();
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

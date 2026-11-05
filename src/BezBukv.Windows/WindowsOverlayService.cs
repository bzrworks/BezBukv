using System.Diagnostics;
using System.Drawing;
using BezBukv.Core.Keyboard;
using BezBukv.Core.Platform;
using BezBukv.Core.Settings;

namespace BezBukv.Windows;

public sealed class WindowsOverlayService : IOverlayService
{
    private const int PeekCaretProbeIntervalMilliseconds = 10;
    private const int PeekCaretProbeCapMilliseconds = 500;

    private readonly KeyboardOverlayGridBuilder _overlayGridBuilder;
    private readonly WindowsFocusedElementService _focusedElementService;
    private readonly WindowsActiveWindowFocusService _activeWindowFocusService;
    private OverlayAppearanceOptions _appearanceOptions;
    private KeyboardProfile _keyboardProfile = KeyboardProfile.Unknown;
    private IReadOnlyList<string> _selectedLayoutKlids = [];
    private CustomKeyboardLayout? _customKeyboardLayout;

    private KeyboardPeekOverlayWindow? _currentWindow;
    private PhysicalKey? _currentKey;
    private KeyboardScanCodeIdentity? _currentIdentity;
    private KeyboardPeekOverlayWindow? _previewWindow;
    private long _lastDiagnosticShowTimestamp;
    private long _diagnosticShowSequence;

    public WindowsOverlayService(
        KeyboardOverlayGridBuilder overlayGridBuilder,
        WindowsFocusedElementService focusedElementService,
        WindowsActiveWindowFocusService activeWindowFocusService,
        OverlayAppearanceOptions appearanceOptions)
    {
        ArgumentNullException.ThrowIfNull(overlayGridBuilder);
        ArgumentNullException.ThrowIfNull(focusedElementService);
        ArgumentNullException.ThrowIfNull(activeWindowFocusService);
        ArgumentNullException.ThrowIfNull(appearanceOptions);

        _overlayGridBuilder = overlayGridBuilder;
        _focusedElementService = focusedElementService;
        _activeWindowFocusService = activeWindowFocusService;
        _appearanceOptions = appearanceOptions;
    }

    public void UpdateAppearance(OverlayAppearanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _appearanceOptions = options;
    }

    public void UpdateKeyboardProfile(
        KeyboardProfile keyboardProfile,
        IReadOnlyList<string> selectedLayoutKlids,
        CustomKeyboardLayout? customKeyboardLayout)
    {
        ArgumentNullException.ThrowIfNull(selectedLayoutKlids);

        if (_keyboardProfile == keyboardProfile
            && _selectedLayoutKlids.SequenceEqual(selectedLayoutKlids)
            && Equals(_customKeyboardLayout, customKeyboardLayout))
        {
            return;
        }

        _keyboardProfile = keyboardProfile;
        _selectedLayoutKlids = Array.AsReadOnly(selectedLayoutKlids.ToArray());
        _customKeyboardLayout = customKeyboardLayout;

        CloseCurrentWindowImmediately();
        ClosePreviewWindowImmediately();
    }

    public void ShowPreview(Rectangle placementArea, OverlayAppearanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (placementArea.Width <= 0 || placementArea.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(placementArea),
                placementArea,
                "Preview placement area must have positive dimensions.");
        }

        CloseCurrentWindowImmediately();
        ClosePreviewWindowImmediately();

        var overlayGrid = BuildPreviewOverlayGrid();
        if (overlayGrid is null)
        {
            return;
        }

        var overlayWindow = new KeyboardPeekOverlayWindow(
            overlayGrid,
            caretPosition: null,
            _activeWindowFocusService.GetActiveMonitorWorkArea(),
            CreatePreviewAppearanceOptions(options),
            placementArea);
        overlayWindow.Closed += OnPreviewWindowClosed;
        _previewWindow = overlayWindow;
        overlayWindow.Show();
    }

    public void UpdatePreviewAppearance(OverlayAppearanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _previewWindow?.UpdateAppearance(CreatePreviewAppearanceOptions(options));
    }

    public void HidePreview()
    {
        ClosePreviewWindowImmediately();
    }

    public void Show(PhysicalKey centerKey)
    {
        Show(centerKey, centerIdentity: null);
    }

    public void Show(KeyboardScanCodeIdentity centerIdentity)
    {
        Show(centerKey: null, centerIdentity);
    }

    private void Show(
        PhysicalKey? centerKey,
        KeyboardScanCodeIdentity? centerIdentity)
    {
        ClosePreviewWindowImmediately();

        if (_currentKey == centerKey
            && _currentIdentity == centerIdentity
            && _currentWindow is not null)
        {
            return;
        }

        CloseCurrentWindowImmediately();

        var overlayGrid = centerIdentity is { } customCenterIdentity
            ? BuildCustomOverlayGrid(customCenterIdentity)
            : BuildOverlayGrid(centerKey!.Value);
        if (overlayGrid is null)
        {
            return;
        }

        System.Drawing.Point? caretPosition =
            _focusedElementService.TryGetCaretScreenPoint(out var point)
                ? point
                : null;
        var overlayWindow = new KeyboardPeekOverlayWindow(
            overlayGrid,
            caretPosition,
            _activeWindowFocusService.GetActiveMonitorWorkArea(),
            _appearanceOptions);
        overlayWindow.Closed += OnOverlayWindowClosed;
        _currentKey = centerKey;
        _currentIdentity = centerIdentity;
        _currentWindow = overlayWindow;
        overlayWindow.Show();
    }

    public void ShowWithCaretDiagnostics(
        PhysicalKey centerKey,
        string source,
        IDiagnosticLogger diagnosticLogger)
    {
        ShowWithCaretDiagnostics(
            centerKey,
            centerIdentity: null,
            source,
            diagnosticLogger);
    }

    public void ShowWithCaretDiagnostics(
        KeyboardScanCodeIdentity centerIdentity,
        string source,
        IDiagnosticLogger diagnosticLogger)
    {
        ShowWithCaretDiagnostics(
            centerKey: null,
            centerIdentity,
            source,
            diagnosticLogger);
    }

    private void ShowWithCaretDiagnostics(
        PhysicalKey? centerKey,
        KeyboardScanCodeIdentity? centerIdentity,
        string source,
        IDiagnosticLogger diagnosticLogger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(diagnosticLogger);

        var diagnosticRun = StartDiagnosticRun(diagnosticLogger);
        if (!diagnosticRun.IsEnabled)
        {
            Show(centerKey, centerIdentity);
            return;
        }

        ClosePreviewWindowImmediately();

        if (_currentKey == centerKey
            && _currentIdentity == centerIdentity
            && _currentWindow is not null)
        {
            return;
        }

        CloseCurrentWindowImmediately();

        var overlayGrid = centerIdentity is { } customCenterIdentity
            ? BuildCustomOverlayGrid(customCenterIdentity)
            : BuildOverlayGrid(centerKey!.Value);
        if (overlayGrid is null)
        {
            return;
        }

        System.Drawing.Point? caretPosition =
            _focusedElementService.TryGetCaretScreenPointWithDiagnostics(
                out var point,
                out var resolution)
                ? point
                : null;
        var overlayWindow = new KeyboardPeekOverlayWindow(
            overlayGrid,
            caretPosition,
            _activeWindowFocusService.GetActiveMonitorWorkArea(),
            _appearanceOptions);
        overlayWindow.Closed += OnOverlayWindowClosed;
        _currentKey = centerKey;
        _currentIdentity = centerIdentity;
        _currentWindow = overlayWindow;

        var showTimestamp = Stopwatch.GetTimestamp();
        var showUtcTimestamp = DateTimeOffset.UtcNow;
        overlayWindow.Show();

        var previousShowTimestamp = Interlocked.Exchange(
            ref _lastDiagnosticShowTimestamp,
            showTimestamp);
        var previousShowGapMs = previousShowTimestamp == 0
            ? (double?)null
            : Stopwatch.GetElapsedTime(
                previousShowTimestamp,
                showTimestamp).TotalMilliseconds;
        var showSequence = Interlocked.Increment(ref _diagnosticShowSequence);

        _ = Task.Run(
            () => RunPeekCaretDiagnosticsAsync(
                diagnosticLogger,
                diagnosticRun,
                source,
                showSequence,
                showTimestamp,
                showUtcTimestamp,
                previousShowGapMs,
                caretPosition,
                resolution));
    }

    public void Hide()
    {
        _currentKey = null;
        _currentIdentity = null;
        _currentWindow?.HideAndClose();
    }

    public void Hide(PhysicalKey centerKey)
    {
        if (_currentKey == centerKey)
        {
            Hide();
        }
    }

    public void Hide(KeyboardScanCodeIdentity centerIdentity)
    {
        if (_currentIdentity == centerIdentity)
        {
            Hide();
        }
    }

    public void Dispose()
    {
        CloseCurrentWindowImmediately();
        ClosePreviewWindowImmediately();
    }

    private void OnOverlayWindowClosed(object? sender, EventArgs eventArgs)
    {
        if (!ReferenceEquals(_currentWindow, sender))
        {
            return;
        }

        _currentWindow = null;
        _currentKey = null;
        _currentIdentity = null;
    }

    private void CloseCurrentWindowImmediately()
    {
        var overlayWindow = _currentWindow;
        _currentWindow = null;
        _currentKey = null;
        _currentIdentity = null;

        if (overlayWindow is null)
        {
            return;
        }

        overlayWindow.Closed -= OnOverlayWindowClosed;
        overlayWindow.CloseImmediately();
    }

    private void OnPreviewWindowClosed(object? sender, EventArgs eventArgs)
    {
        if (!ReferenceEquals(_previewWindow, sender))
        {
            return;
        }

        _previewWindow = null;
    }

    private void ClosePreviewWindowImmediately()
    {
        var overlayWindow = _previewWindow;
        _previewWindow = null;

        if (overlayWindow is null)
        {
            return;
        }

        overlayWindow.Closed -= OnPreviewWindowClosed;
        overlayWindow.CloseImmediately();
    }

    private KeyboardOverlayGrid? BuildOverlayGrid(PhysicalKey centerKey) =>
        _keyboardProfile switch
        {
            KeyboardProfile.Ansi => _overlayGridBuilder.BuildPreset(
                KeyboardGeometryProfile.PcAnsi,
                centerKey,
                _selectedLayoutKlids),
            KeyboardProfile.Iso => _overlayGridBuilder.BuildPreset(
                KeyboardGeometryProfile.PcIso,
                centerKey,
                _selectedLayoutKlids),
            KeyboardProfile.Custom => BuildCustomOverlayGrid(centerKey),
            _ => null,
        };

    private KeyboardOverlayGrid? BuildPreviewOverlayGrid()
    {
        if (_keyboardProfile != KeyboardProfile.Custom)
        {
            return BuildOverlayGrid(PhysicalKey.KeyF);
        }

        if (_customKeyboardLayout is null)
        {
            return null;
        }

        var overlayGrid = _overlayGridBuilder.BuildCustomPreview(
            _customKeyboardLayout,
            _selectedLayoutKlids);

        return overlayGrid.Cells.Count == 0
            ? null
            : overlayGrid;
    }

    private KeyboardOverlayGrid? BuildCustomOverlayGrid(PhysicalKey centerKey)
    {
        if (_customKeyboardLayout is null
            || !PhysicalKeyScanCodeMapper.TryGetScanCode(
                centerKey,
                out var centerIdentity))
        {
            return null;
        }

        return BuildCustomOverlayGrid(centerIdentity);
    }

    private KeyboardOverlayGrid? BuildCustomOverlayGrid(
        KeyboardScanCodeIdentity centerIdentity)
    {
        if (_keyboardProfile != KeyboardProfile.Custom
            || _customKeyboardLayout is null)
        {
            return null;
        }

        var overlayGrid = _overlayGridBuilder.BuildCustom(
            _customKeyboardLayout,
            centerIdentity,
            _selectedLayoutKlids);

        return overlayGrid.Cells.Count == 0
            ? null
            : overlayGrid;
    }

    private async Task RunPeekCaretDiagnosticsAsync(
        IDiagnosticLogger diagnosticLogger,
        DiagnosticRunContext diagnosticRun,
        string source,
        long showSequence,
        long showTimestamp,
        DateTimeOffset showUtcTimestamp,
        double? previousShowGapMs,
        Point? overlayPoint,
        PeekCaretResolutionDiagnostic resolution)
    {
        try
        {
            var attemptCount = 0;
            var elapsedMs = 0d;
            var attempt = PeekCollapsedCaretProbeAttempt.Failed("not-attempted");
            var settledWithinCap = false;

            do
            {
                attemptCount++;
                attempt = _focusedElementService.ProbeCollapsedCaret();
                elapsedMs = Stopwatch.GetElapsedTime(showTimestamp).TotalMilliseconds;
                settledWithinCap = attempt.IsSettled &&
                    elapsedMs <= PeekCaretProbeCapMilliseconds;

                if (settledWithinCap || elapsedMs >= PeekCaretProbeCapMilliseconds)
                {
                    break;
                }

                var delayMs = Math.Min(
                    PeekCaretProbeIntervalMilliseconds,
                    Math.Max(1, PeekCaretProbeCapMilliseconds - (int)elapsedMs));
                await Task.Delay(delayMs).ConfigureAwait(false);
            }
            while (true);

            var settledPoint = settledWithinCap
                ? attempt.ScreenPoint
                : null;
            var settledRectangle = settledWithinCap
                ? attempt.Rectangle
                : null;
            var context = _focusedElementService.CaptureDiagnosticContext();

            LogDiagnostic(
                diagnosticLogger,
                diagnosticRun,
                "peek_overlay_shown",
                CreateShownFields(
                    source,
                    showSequence,
                    showUtcTimestamp,
                    previousShowGapMs,
                    overlayPoint,
                    resolution,
                    context));
            LogDiagnostic(
                diagnosticLogger,
                diagnosticRun,
                "peek_caret_settle",
                new Dictionary<string, object?>
                {
                    ["source"] = source,
                    ["showSequence"] = showSequence,
                    ["showUtcTimestamp"] = showUtcTimestamp,
                    ["previousShowGapMs"] = previousShowGapMs,
                    ["resolutionBranch"] = resolution.Branch,
                    ["placement"] = overlayPoint is null
                        ? "fallback-corner"
                        : "caret",
                    ["overlayPointX"] = overlayPoint?.X,
                    ["overlayPointY"] = overlayPoint?.Y,
                    ["focusedProcess"] = context.ProcessName,
                    ["focusedWindowClass"] = context.WindowClass,
                    ["fieldEmpty"] = context.IsFieldEmpty,
                    ["settled"] = settledWithinCap,
                    ["attemptCount"] = attemptCount,
                    ["settleTimeMs"] = settledWithinCap ? elapsedMs : null,
                    ["probeElapsedMs"] = elapsedMs,
                    ["probeIntervalMs"] = PeekCaretProbeIntervalMilliseconds,
                    ["probeCapMs"] = PeekCaretProbeCapMilliseconds,
                    ["completionOutcome"] = settledWithinCap
                        ? "settled"
                        : "cap-reached",
                    ["lastProbeOutcome"] = attempt.Outcome,
                    ["lastRectangleCount"] = attempt.RectangleCount,
                    ["lastRectX"] = attempt.Rectangle?.Left,
                    ["lastRectY"] = attempt.Rectangle?.Top,
                    ["lastRectWidth"] = attempt.Rectangle?.Width,
                    ["lastRectHeight"] = attempt.Rectangle?.Height,
                    ["settledPointX"] = settledPoint?.X,
                    ["settledPointY"] = settledPoint?.Y,
                    ["settledRectX"] = settledRectangle?.Left,
                    ["settledRectY"] = settledRectangle?.Top,
                    ["settledRectWidth"] = settledRectangle?.Width,
                    ["settledRectHeight"] = settledRectangle?.Height,
                    ["deltaX"] = overlayPoint is not null && settledPoint is not null
                        ? settledPoint.Value.X - overlayPoint.Value.X
                        : null,
                    ["deltaY"] = overlayPoint is not null && settledPoint is not null
                        ? settledPoint.Value.Y - overlayPoint.Value.Y
                        : null,
                });
        }
        catch (Exception)
        {
            // Diagnostic logging is best-effort and must not interrupt the main flow.
        }
    }

    private static IReadOnlyDictionary<string, object?> CreateShownFields(
        string source,
        long showSequence,
        DateTimeOffset showUtcTimestamp,
        double? previousShowGapMs,
        Point? overlayPoint,
        PeekCaretResolutionDiagnostic resolution,
        PeekFocusedElementDiagnosticContext context) =>
        new Dictionary<string, object?>
        {
            ["source"] = source,
            ["showSequence"] = showSequence,
            ["showUtcTimestamp"] = showUtcTimestamp,
            ["previousShowGapMs"] = previousShowGapMs,
            ["resolutionBranch"] = resolution.Branch,
            ["placement"] = overlayPoint is null
                ? "fallback-corner"
                : "caret",
            ["overlayPointX"] = overlayPoint?.X,
            ["overlayPointY"] = overlayPoint?.Y,
            ["resolvedRectX"] = resolution.Rectangle?.Left,
            ["resolvedRectY"] = resolution.Rectangle?.Top,
            ["resolvedRectWidth"] = resolution.Rectangle?.Width,
            ["resolvedRectHeight"] = resolution.Rectangle?.Height,
            ["focusedProcess"] = context.ProcessName,
            ["focusedWindowClass"] = context.WindowClass,
            ["fieldEmpty"] = context.IsFieldEmpty,
        };

    private static DiagnosticRunContext StartDiagnosticRun(
        IDiagnosticLogger diagnosticLogger)
    {
        try
        {
            return diagnosticLogger.StartRun();
        }
        catch (Exception)
        {
            return DiagnosticRunContext.Disabled;
        }
    }

    private static void LogDiagnostic(
        IDiagnosticLogger diagnosticLogger,
        DiagnosticRunContext diagnosticRun,
        string eventName,
        IReadOnlyDictionary<string, object?> fields)
    {
        try
        {
            diagnosticLogger.Log(diagnosticRun, eventName, fields);
        }
        catch (Exception)
        {
            // Diagnostic logging is best-effort and must not interrupt the main flow.
        }
    }

    private static OverlayAppearanceOptions CreatePreviewAppearanceOptions(
        OverlayAppearanceOptions options) =>
        new(
            opacity: options.Opacity,
            scale: options.Scale,
            animationEnabled: false,
            animationDurationMs: options.AnimationDurationMs);
}

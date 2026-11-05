using System.Diagnostics;
using BezBukv.Core.Conversion;
using BezBukv.Core.Platform;

namespace BezBukv.Core.Scenarios;

public enum LayoutCorrectionOutcome
{
    NoSelection,
    NotPlainText,
    ExceedsLimit,
    NothingToConvert,
    ClipboardUnavailable,
    InputError,
    AutoReplacementCompleted,
    ResultWindowRequired,
}

public sealed record LayoutCorrectionResult(
    LayoutCorrectionOutcome Outcome,
    string? ConvertedText,
    LayoutConversionDirection? ResultLayoutDirection = null,
    KeyboardInputLanguage? TargetInputLanguage = null);

public sealed class LayoutCorrectionScenario
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan DefaultClipboardRestoreDelay =
        TimeSpan.FromMilliseconds(300);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    private readonly IClipboardService _clipboardService;
    private readonly IInputSimulationService _inputSimulationService;
    private readonly IFocusedElementService _focusedElementService;
    private readonly IActiveApplicationService _activeApplicationService;
    private readonly IKeyboardLayoutService? _keyboardLayoutService;
    private readonly IDiagnosticLogger _diagnosticLogger;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _clipboardRestoreDelay;
    private readonly LayoutConversionDirection _direction;
    private readonly bool _pasteIntoUnknown;

    public LayoutCorrectionScenario(
        IClipboardService clipboardService,
        IInputSimulationService inputSimulationService,
        IFocusedElementService focusedElementService,
        IActiveApplicationService activeApplicationService,
        TimeSpan? timeout = null,
        LayoutConversionDirection direction = LayoutConversionDirection.Auto,
        bool pasteIntoUnknown = false,
        TimeSpan? clipboardRestoreDelay = null,
        IKeyboardLayoutService? keyboardLayoutService = null,
        IDiagnosticLogger? diagnosticLogger = null)
    {
        ArgumentNullException.ThrowIfNull(clipboardService);
        ArgumentNullException.ThrowIfNull(inputSimulationService);
        ArgumentNullException.ThrowIfNull(focusedElementService);
        ArgumentNullException.ThrowIfNull(activeApplicationService);

        var effectiveTimeout = timeout ?? DefaultTimeout;
        if (effectiveTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
        }

        var effectiveClipboardRestoreDelay =
            clipboardRestoreDelay ?? DefaultClipboardRestoreDelay;
        if (effectiveClipboardRestoreDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(clipboardRestoreDelay),
                "Clipboard restore delay must be positive.");
        }

        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
        }

        _clipboardService = clipboardService;
        _inputSimulationService = inputSimulationService;
        _focusedElementService = focusedElementService;
        _activeApplicationService = activeApplicationService;
        _keyboardLayoutService = keyboardLayoutService;
        _diagnosticLogger = diagnosticLogger ?? NoOpDiagnosticLogger.Instance;
        _timeout = effectiveTimeout;
        _clipboardRestoreDelay = effectiveClipboardRestoreDelay;
        _direction = direction;
        _pasteIntoUnknown = pasteIntoUnknown;
    }

    public ValueTask<LayoutCorrectionResult> RunAsync(
        CancellationToken cancellationToken = default) =>
        RunAsync(DiagnosticRunContext.Disabled, cancellationToken);

    public async ValueTask<LayoutCorrectionResult> RunAsync(
        DiagnosticRunContext diagnosticRun,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(diagnosticRun);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = await CaptureSnapshotBestEffortAsync(diagnosticRun, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var result = await RunCoreAsync(diagnosticRun, cancellationToken)
                .ConfigureAwait(false);

            if (result.Outcome == LayoutCorrectionOutcome.AutoReplacementCompleted &&
                snapshot is not null)
            {
                await RestoreSnapshotBestEffortAsync(
                        snapshot,
                        diagnosticRun,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                LogDiagnostic(
                    diagnosticRun,
                    "clipboard_restore",
                    new Dictionary<string, object?>
                    {
                        ["scheduled"] = false,
                        ["scheduledDelayMs"] = _clipboardRestoreDelay.TotalMilliseconds,
                        ["actualDelayMs"] = null,
                        ["outcome"] = "not_scheduled",
                        ["reason"] = snapshot is null
                            ? "snapshot_unavailable"
                            : "outcome_not_auto",
                        ["scenarioOutcome"] = ToDiagnosticValue(result.Outcome),
                    });
            }

            return result;
        }
        finally
        {
            snapshot?.Dispose();
        }
    }

    private async ValueTask<LayoutCorrectionResult> RunCoreAsync(
        DiagnosticRunContext diagnosticRun,
        CancellationToken cancellationToken)
    {
        var initialStateToken = _clipboardService.CaptureStateToken();
        var inputResult = _inputSimulationService.SendCopy();
        var sequenceAfterCopy = CaptureClipboardSequenceBestEffort(diagnosticRun);

        LogDiagnostic(
            diagnosticRun,
            "clipboard_copy",
            new Dictionary<string, object?>
            {
                ["sequenceBefore"] = initialStateToken.Value,
                ["sequenceAfter"] = sequenceAfterCopy,
                ["copySent"] = inputResult.Outcome == InputSimulationOutcome.Success,
                ["outcome"] = ToDiagnosticValue(inputResult.Outcome),
                ["errorCode"] = inputResult.PlatformErrorCode,
            });

        if (inputResult.Outcome != InputSimulationOutcome.Success)
        {
            return new(LayoutCorrectionOutcome.InputError, null);
        }

        var stopwatch = Stopwatch.StartNew();
        var clipboardChanged = false;

        while (stopwatch.Elapsed < _timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_clipboardService.HasChanged(initialStateToken))
            {
                clipboardChanged = true;
                var observedSequence = CaptureClipboardSequenceBestEffort(diagnosticRun);

                var readResult = await _clipboardService
                    .ReadUnicodeTextAsync(cancellationToken)
                    .ConfigureAwait(false);

                LogDiagnostic(
                    diagnosticRun,
                    "clipboard_read",
                    new Dictionary<string, object?>
                    {
                        ["sequenceBeforeCopy"] = initialStateToken.Value,
                        ["observedSequence"] = observedSequence,
                        ["outcome"] = ToDiagnosticValue(readResult.Outcome),
                    });

                if (readResult.Outcome != ClipboardTextReadOutcome.ClipboardUnavailable)
                {
                    return await HandleReadResultAsync(
                            readResult,
                            diagnosticRun,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            var remaining = _timeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(
                    remaining < PollInterval ? remaining : PollInterval,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        LogDiagnostic(
            diagnosticRun,
            "clipboard_read",
            new Dictionary<string, object?>
            {
                ["sequenceBeforeCopy"] = initialStateToken.Value,
                ["observedSequence"] = CaptureClipboardSequenceBestEffort(diagnosticRun),
                ["outcome"] = "timeout",
                ["clipboardChanged"] = clipboardChanged,
            });

        return new(
            clipboardChanged
                ? LayoutCorrectionOutcome.ClipboardUnavailable
                : LayoutCorrectionOutcome.NoSelection,
            null);
    }

    private async ValueTask<IClipboardSnapshot?> CaptureSnapshotBestEffortAsync(
        DiagnosticRunContext diagnosticRun,
        CancellationToken cancellationToken)
    {
        try
        {
            var captureResult = await _clipboardService
                .CaptureSnapshotAsync(cancellationToken)
                .ConfigureAwait(false);

            LogDiagnostic(
                diagnosticRun,
                "clipboard_snapshot",
                new Dictionary<string, object?>
                {
                    ["outcome"] = ToDiagnosticValue(captureResult.Outcome),
                    ["capturedContent"] = ToDiagnosticValue(captureResult.CapturedContent),
                    ["nonCapturedContent"] = ToDiagnosticValue(captureResult.NonCapturedContent),
                    ["capturedFormatCount"] = captureResult.CapturedFormatCount,
                    ["nonCapturedFormatCount"] = captureResult.NonCapturedFormatCount,
                });

            return captureResult.Outcome == ClipboardSnapshotCaptureOutcome.Captured
                ? captureResult.Snapshot
                : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogDiagnostic(
                diagnosticRun,
                "clipboard_snapshot",
                new Dictionary<string, object?>
                {
                    ["outcome"] = "cancelled",
                });
            throw;
        }
        catch (Exception exception)
        {
            LogDiagnostic(
                diagnosticRun,
                "clipboard_snapshot",
                new Dictionary<string, object?>
                {
                    ["outcome"] = "exception",
                    ["errorType"] = exception.GetType().Name,
                });
            return null;
        }
    }

    private async ValueTask RestoreSnapshotBestEffortAsync(
        IClipboardSnapshot snapshot,
        DiagnosticRunContext diagnosticRun,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await Task.Delay(_clipboardRestoreDelay, cancellationToken).ConfigureAwait(false);
            var actualDelayMs = stopwatch.Elapsed.TotalMilliseconds;
            var restoreResult = await _clipboardService
                .RestoreSnapshotAsync(snapshot, cancellationToken)
                .ConfigureAwait(false);

            LogDiagnostic(
                diagnosticRun,
                "clipboard_restore",
                new Dictionary<string, object?>
                {
                    ["scheduled"] = true,
                    ["scheduledDelayMs"] = _clipboardRestoreDelay.TotalMilliseconds,
                    ["actualDelayMs"] = actualDelayMs,
                    ["outcome"] = ToDiagnosticValue(restoreResult.Outcome),
                    ["restoredContent"] = ToDiagnosticValue(restoreResult.RestoredContent),
                    ["skippedContent"] = ToDiagnosticValue(restoreResult.SkippedContent),
                    ["restoredFormatCount"] = restoreResult.RestoredFormatCount,
                    ["skippedFormatCount"] = restoreResult.SkippedFormatCount,
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogDiagnostic(
                diagnosticRun,
                "clipboard_restore",
                new Dictionary<string, object?>
                {
                    ["scheduled"] = true,
                    ["scheduledDelayMs"] = _clipboardRestoreDelay.TotalMilliseconds,
                    ["actualDelayMs"] = stopwatch.Elapsed.TotalMilliseconds,
                    ["outcome"] = "cancelled",
                });
            throw;
        }
        catch (Exception exception)
        {
            LogDiagnostic(
                diagnosticRun,
                "clipboard_restore",
                new Dictionary<string, object?>
                {
                    ["scheduled"] = true,
                    ["scheduledDelayMs"] = _clipboardRestoreDelay.TotalMilliseconds,
                    ["actualDelayMs"] = stopwatch.Elapsed.TotalMilliseconds,
                    ["outcome"] = "exception",
                    ["errorType"] = exception.GetType().Name,
                });
        }
    }

    private async ValueTask<LayoutCorrectionResult> HandleReadResultAsync(
        ClipboardTextReadResult readResult,
        DiagnosticRunContext diagnosticRun,
        CancellationToken cancellationToken)
    {
        switch (readResult.Outcome)
        {
            case ClipboardTextReadOutcome.NoPlainText:
                return new(LayoutCorrectionOutcome.NotPlainText, null);

            case ClipboardTextReadOutcome.ExceedsLimit:
                return new(LayoutCorrectionOutcome.ExceedsLimit, null);

            case ClipboardTextReadOutcome.Text:
                return await ConvertAndStoreAsync(
                        readResult.Text!,
                        diagnosticRun,
                        cancellationToken)
                    .ConfigureAwait(false);

            case ClipboardTextReadOutcome.ClipboardUnavailable:
                return new(LayoutCorrectionOutcome.ClipboardUnavailable, null);

            default:
                throw new InvalidOperationException("Unknown clipboard read status.");
        }
    }

    private async ValueTask<LayoutCorrectionResult> ConvertAndStoreAsync(
        string text,
        DiagnosticRunContext diagnosticRun,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new(LayoutCorrectionOutcome.NoSelection, null);
        }

        if (!LayoutConverter.ContainsLatinOrCyrillicLetter(text))
        {
            return new(LayoutCorrectionOutcome.NothingToConvert, null);
        }

        var conversionResult = LayoutConverter.ConvertLayout(text, _direction);

        switch (conversionResult.Outcome)
        {
            case LayoutConversionOutcome.NoOp:
                return new(LayoutCorrectionOutcome.NothingToConvert, null);

            case LayoutConversionOutcome.Converted:
                var convertedText = conversionResult.ConvertedText!;
                LogConvertedPayload(
                    diagnosticRun,
                    convertedText,
                    conversionResult.ResultLayoutDirection);

                var writeResult = await _clipboardService
                    .SetUnicodeTextAsync(convertedText, cancellationToken)
                    .ConfigureAwait(false);

                LogDiagnostic(
                    diagnosticRun,
                    "clipboard_write",
                    new Dictionary<string, object?>
                    {
                        ["outcome"] = ToDiagnosticValue(writeResult.Outcome),
                    });

                if (writeResult.Outcome != ClipboardTextWriteOutcome.Success)
                {
                    return new(
                        writeResult.Outcome == ClipboardTextWriteOutcome.ClipboardUnavailable
                            ? LayoutCorrectionOutcome.ClipboardUnavailable
                            : LayoutCorrectionOutcome.InputError,
                        null);
                }

                var outcome = RouteConvertedText(diagnosticRun);
                if (outcome == LayoutCorrectionOutcome.AutoReplacementCompleted)
                {
                    SwitchKeyboardLayoutBestEffort(
                        diagnosticRun,
                        conversionResult.ResultLayoutDirection);
                }
                else
                {
                    LogLayoutSwitchNotRequested(
                        diagnosticRun,
                        conversionResult.ResultLayoutDirection,
                        outcome == LayoutCorrectionOutcome.ResultWindowRequired
                            ? "route_result"
                            : "route_input_error");
                }

                var resultWindowRequired =
                    outcome == LayoutCorrectionOutcome.ResultWindowRequired;

                return new(
                    outcome,
                    outcome is LayoutCorrectionOutcome.AutoReplacementCompleted or
                        LayoutCorrectionOutcome.ResultWindowRequired
                        ? convertedText
                        : null,
                    resultWindowRequired
                        ? conversionResult.ResultLayoutDirection
                        : null,
                    resultWindowRequired
                        ? ResolveTargetInputLanguage(
                            conversionResult.ResultLayoutDirection)
                        : null);

            default:
                throw new InvalidOperationException("Unknown layout conversion status.");
        }
    }

    private void SwitchKeyboardLayoutBestEffort(
        DiagnosticRunContext diagnosticRun,
        LayoutConversionDirection? resultLayoutDirection)
    {
        var targetLanguage = ResolveTargetInputLanguage(resultLayoutDirection);

        if (_keyboardLayoutService is null || targetLanguage is null)
        {
            LogDiagnostic(
                diagnosticRun,
                "layout_switch",
                new Dictionary<string, object?>
                {
                    ["source"] = "auto",
                    ["requested"] = false,
                    ["direction"] = resultLayoutDirection is null
                        ? null
                        : ToDiagnosticValue(resultLayoutDirection.Value),
                    ["targetLanguage"] = targetLanguage is null
                        ? null
                        : ToDiagnosticValue(targetLanguage.Value),
                    ["serviceEnabled"] = _keyboardLayoutService is not null,
                    ["outcome"] = "not_requested",
                    ["reason"] = targetLanguage is null
                        ? "target_language_unavailable"
                        : "service_disabled",
                });
            return;
        }

        try
        {
            var outcome = _keyboardLayoutService
                .SwitchActiveInputLanguage(targetLanguage.Value);

            LogDiagnostic(
                diagnosticRun,
                "layout_switch",
                new Dictionary<string, object?>
                {
                    ["source"] = "auto",
                    ["requested"] = true,
                    ["direction"] = ToDiagnosticValue(resultLayoutDirection!.Value),
                    ["targetLanguage"] = ToDiagnosticValue(targetLanguage.Value),
                    ["serviceEnabled"] = true,
                    ["outcome"] = ToDiagnosticValue(outcome),
                    ["reason"] = null,
                });
        }
        catch (Exception exception)
        {
            LogDiagnostic(
                diagnosticRun,
                "layout_switch",
                new Dictionary<string, object?>
                {
                    ["source"] = "auto",
                    ["requested"] = true,
                    ["direction"] = ToDiagnosticValue(resultLayoutDirection!.Value),
                    ["targetLanguage"] = ToDiagnosticValue(targetLanguage.Value),
                    ["serviceEnabled"] = true,
                    ["outcome"] = "exception",
                    ["reason"] = "service_exception",
                    ["errorType"] = exception.GetType().Name,
                });
        }
    }

    private LayoutCorrectionOutcome RouteConvertedText(
        DiagnosticRunContext diagnosticRun)
    {
        var activeApplication = _activeApplicationService.GetActiveApplication();

        LogDiagnostic(
            diagnosticRun,
            "active_application",
            new Dictionary<string, object?>
            {
                ["outcome"] = ToDiagnosticValue(activeApplication.Outcome),
                ["processName"] = activeApplication.ProcessName,
                ["windowHandle"] = activeApplication.WindowHandle,
                ["isTerminal"] = activeApplication.IsTerminal,
            });

        if (activeApplication.Outcome != ActiveApplicationDetectionOutcome.Detected)
        {
            LogRouteDecision(
                diagnosticRun,
                activeApplication,
                editability: null,
                route: "result",
                reason: "app_unavailable");
            return LayoutCorrectionOutcome.ResultWindowRequired;
        }

        if (activeApplication.IsTerminal)
        {
            LogRouteDecision(
                diagnosticRun,
                activeApplication,
                editability: null,
                route: "result",
                reason: "terminal");
            return LayoutCorrectionOutcome.ResultWindowRequired;
        }

        FocusedElementEditability editability;
        if (diagnosticRun.IsEnabled)
        {
            var diagnosticResult =
                _focusedElementService.GetEditabilityWithDiagnostics();
            editability = diagnosticResult.Editability;
            LogDiagnostic(
                diagnosticRun,
                "editability",
                new Dictionary<string, object?>
                {
                    ["outcome"] = ToDiagnosticValue(editability),
                    ["controlType"] = diagnosticResult.ControlType,
                    ["decisionBranch"] = diagnosticResult.DecisionBranch,
                    ["valuePatternAvailable"] = diagnosticResult.ValuePatternAvailable,
                    ["textPatternAvailable"] = diagnosticResult.TextPatternAvailable,
                });
        }
        else
        {
            editability = _focusedElementService.GetEditability();
        }

        var shouldPaste = editability == FocusedElementEditability.Editable ||
            (editability == FocusedElementEditability.Unknown && _pasteIntoUnknown);

        if (!shouldPaste)
        {
            LogRouteDecision(
                diagnosticRun,
                activeApplication,
                editability,
                route: "result",
                reason: editability == FocusedElementEditability.ReadOnly
                    ? "read_only"
                    : "unknown_blocked");
            return LayoutCorrectionOutcome.ResultWindowRequired;
        }

        var foregroundBefore = CaptureForegroundBestEffort(diagnosticRun);
        var pasteResult = _inputSimulationService.SendPaste(diagnosticRun.IsEnabled);
        var foregroundAfter = CaptureForegroundBestEffort(diagnosticRun);

        LogSendPaste(
            diagnosticRun,
            source: "auto",
            clickId: null,
            foregroundBefore,
            foregroundAfter,
            pasteResult);

        var pasteSucceeded = pasteResult.Outcome == InputSimulationOutcome.Success;
        LogRouteDecision(
            diagnosticRun,
            activeApplication,
            editability,
            route: pasteSucceeded ? "auto" : "input_error",
            reason: pasteSucceeded
                ? editability == FocusedElementEditability.Editable
                    ? "editable"
                    : "unknown_allowed"
                : "sendinput_failed");

        return pasteSucceeded
            ? LayoutCorrectionOutcome.AutoReplacementCompleted
            : LayoutCorrectionOutcome.InputError;
    }

    private void LogConvertedPayload(
        DiagnosticRunContext diagnosticRun,
        string convertedText,
        LayoutConversionDirection? resultLayoutDirection)
    {
        if (!diagnosticRun.IsEnabled)
        {
            return;
        }

        try
        {
            LogDiagnostic(
                diagnosticRun,
                "clipboard_converted_payload",
                new Dictionary<string, object?>
                {
                    ["payloadMetrics"] = diagnosticRun.MeasurePayload(
                        convertedText,
                        includeFingerprint: true),
                    ["direction"] = resultLayoutDirection is null
                        ? null
                        : ToDiagnosticValue(resultLayoutDirection.Value),
                });
        }
        catch (Exception)
        {
            // Diagnostic logging is best-effort and must not interrupt the main flow.
        }
    }

    private void LogLayoutSwitchNotRequested(
        DiagnosticRunContext diagnosticRun,
        LayoutConversionDirection? resultLayoutDirection,
        string reason)
    {
        var targetLanguage = ResolveTargetInputLanguage(resultLayoutDirection);

        LogDiagnostic(
            diagnosticRun,
            "layout_switch",
            new Dictionary<string, object?>
            {
                ["source"] = "auto",
                ["requested"] = false,
                ["direction"] = resultLayoutDirection is null
                    ? null
                    : ToDiagnosticValue(resultLayoutDirection.Value),
                ["targetLanguage"] = targetLanguage is null
                    ? null
                    : ToDiagnosticValue(targetLanguage.Value),
                ["serviceEnabled"] = _keyboardLayoutService is not null,
                ["outcome"] = "not_requested",
                ["reason"] = reason,
            });
    }

    private void LogRouteDecision(
        DiagnosticRunContext diagnosticRun,
        ActiveApplicationInfo activeApplication,
        FocusedElementEditability? editability,
        string route,
        string reason)
    {
        LogDiagnostic(
            diagnosticRun,
            "route_decision",
            new Dictionary<string, object?>
            {
                ["processName"] = activeApplication.ProcessName,
                ["isTerminal"] = activeApplication.IsTerminal,
                ["editability"] = editability is null
                    ? "not_checked"
                    : ToDiagnosticValue(editability.Value),
                ["pasteIntoUnknown"] = _pasteIntoUnknown,
                ["route"] = route,
                ["reason"] = reason,
            });
    }

    private void LogSendPaste(
        DiagnosticRunContext diagnosticRun,
        string source,
        string? clickId,
        ActiveApplicationInfo? foregroundBefore,
        ActiveApplicationInfo? foregroundAfter,
        InputSimulationResult pasteResult)
    {
        LogDiagnostic(
            diagnosticRun,
            "send_paste",
            new Dictionary<string, object?>
            {
                ["source"] = source,
                ["runId"] = diagnosticRun.RunId,
                ["clickId"] = clickId,
                ["foregroundProcessBefore"] = foregroundBefore?.ProcessName,
                ["foregroundWindowHandleBefore"] = foregroundBefore?.WindowHandle,
                ["foregroundProcessAfter"] = foregroundAfter?.ProcessName,
                ["foregroundWindowHandleAfter"] = foregroundAfter?.WindowHandle,
                ["physicalCtrlDown"] = pasteResult.PhysicalControlDown,
                ["plannedInputCount"] = pasteResult.PlannedInputCount,
                ["sentInputCount"] = pasteResult.SentInputCount,
                ["outcome"] = ToDiagnosticValue(pasteResult.Outcome),
                ["errorCode"] = pasteResult.PlatformErrorCode,
            });
    }

    private ActiveApplicationInfo? CaptureForegroundBestEffort(
        DiagnosticRunContext diagnosticRun)
    {
        if (!diagnosticRun.IsEnabled)
        {
            return null;
        }

        try
        {
            return _activeApplicationService.GetActiveApplication();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private ulong? CaptureClipboardSequenceBestEffort(
        DiagnosticRunContext diagnosticRun)
    {
        if (!diagnosticRun.IsEnabled)
        {
            return null;
        }

        try
        {
            return _clipboardService.CaptureStateToken().Value;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void LogDiagnostic(
        DiagnosticRunContext diagnosticRun,
        string eventName,
        IReadOnlyDictionary<string, object?> fields)
    {
        if (!diagnosticRun.IsEnabled)
        {
            return;
        }

        try
        {
            _diagnosticLogger.Log(diagnosticRun, eventName, fields);
        }
        catch (Exception)
        {
            // Diagnostic logging is best-effort and must not interrupt the main flow.
        }
    }

    private static string ToDiagnosticValue<T>(T value) where T : struct, Enum =>
        value.ToString().ToLowerInvariant();

    private static KeyboardInputLanguage? ResolveTargetInputLanguage(
        LayoutConversionDirection? resultLayoutDirection) =>
        resultLayoutDirection switch
        {
            LayoutConversionDirection.EnglishToRussian => KeyboardInputLanguage.Russian,
            LayoutConversionDirection.RussianToEnglish => KeyboardInputLanguage.English,
            _ => null,
        };
}

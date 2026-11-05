using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using BezBukv.Core.Platform;

namespace BezBukv.Windows;

public sealed class WindowsFocusedElementService : IFocusedElementService
{
    public FocusedElementEditability GetEditability() =>
        GetEditabilityCore(diagnosticCapture: null);

    public FocusedElementEditabilityDiagnosticResult GetEditabilityWithDiagnostics()
    {
        var diagnosticCapture = new EditabilityDiagnosticCapture();
        var editability = GetEditabilityCore(diagnosticCapture);
        return diagnosticCapture.CreateResult(editability);
    }

    private static FocusedElementEditability GetEditabilityCore(
        EditabilityDiagnosticCapture? diagnosticCapture)
    {
        try
        {
            var focusedElement = AutomationElement.FocusedElement;

            if (focusedElement is null)
            {
                diagnosticCapture?.SetDecisionBranch(
                    FocusedElementEditabilityDiagnosticValues.NoFocusBranch);
                return FocusedElementEditability.Unknown;
            }

            if (!focusedElement.Current.IsEnabled)
            {
                diagnosticCapture?.SetDecisionBranch(
                    FocusedElementEditabilityDiagnosticValues.NotEnabledBranch);
                return FocusedElementEditability.ReadOnly;
            }

            var controlType = focusedElement.Current.ControlType;
            diagnosticCapture?.SetControlType(controlType.ProgrammaticName);
            if (controlType == ControlType.Group ||
                controlType == ControlType.Pane)
            {
                diagnosticCapture?.SetDecisionBranch(
                    FocusedElementEditabilityDiagnosticValues
                        .ContainerControlTypeGateBranch);
                return FocusedElementEditability.Unknown;
            }

            if (controlType != ControlType.Edit &&
                controlType != ControlType.Document &&
                controlType != ControlType.ComboBox)
            {
                diagnosticCapture?.SetDecisionBranch(
                    FocusedElementEditabilityDiagnosticValues.ControlTypeGateBranch);
                return FocusedElementEditability.ReadOnly;
            }

            var valuePatternAvailable = focusedElement.TryGetCurrentPattern(
                ValuePattern.Pattern,
                out var valuePatternObject);
            diagnosticCapture?.SetValuePatternAvailable(valuePatternAvailable);
            if (valuePatternAvailable)
            {
                var valuePattern = (ValuePattern)valuePatternObject;
                diagnosticCapture?.SetDecisionBranch(
                    FocusedElementEditabilityDiagnosticValues.ValuePatternBranch);

                return valuePattern.Current.IsReadOnly
                    ? FocusedElementEditability.ReadOnly
                    : FocusedElementEditability.Editable;
            }

            var textPatternAvailable = focusedElement.TryGetCurrentPattern(
                TextPattern.Pattern,
                out var textPatternObject);
            diagnosticCapture?.SetTextPatternAvailable(textPatternAvailable);
            if (textPatternAvailable)
            {
                var textPattern = (TextPattern)textPatternObject;
                var isReadOnlyAttribute = textPattern.DocumentRange.GetAttributeValue(
                    TextPattern.IsReadOnlyAttribute);

                if (isReadOnlyAttribute is bool isReadOnly)
                {
                    diagnosticCapture?.SetDecisionBranch(
                        FocusedElementEditabilityDiagnosticValues.TextPatternBranch);
                    return isReadOnly
                        ? FocusedElementEditability.ReadOnly
                        : FocusedElementEditability.Editable;
                }
            }

            diagnosticCapture?.SetDecisionBranch(
                FocusedElementEditabilityDiagnosticValues.NoPatternBranch);
            return FocusedElementEditability.Unknown;
        }
        catch (Exception)
        {
            diagnosticCapture?.SetDecisionBranch(
                FocusedElementEditabilityDiagnosticValues.ExceptionBranch);
            return FocusedElementEditability.Unknown;
        }
    }

    private sealed class EditabilityDiagnosticCapture
    {
        private string _controlType =
            FocusedElementEditabilityDiagnosticValues.UnavailableControlType;
        private string _decisionBranch =
            FocusedElementEditabilityDiagnosticValues.ExceptionBranch;
        private bool? _valuePatternAvailable;
        private bool? _textPatternAvailable;

        internal void SetControlType(string? controlType)
        {
            _controlType = string.IsNullOrWhiteSpace(controlType)
                ? FocusedElementEditabilityDiagnosticValues.UnavailableControlType
                : controlType;
        }

        internal void SetDecisionBranch(string decisionBranch) =>
            _decisionBranch = decisionBranch;

        internal void SetValuePatternAvailable(bool valuePatternAvailable) =>
            _valuePatternAvailable = valuePatternAvailable;

        internal void SetTextPatternAvailable(bool textPatternAvailable) =>
            _textPatternAvailable = textPatternAvailable;

        internal FocusedElementEditabilityDiagnosticResult CreateResult(
            FocusedElementEditability editability) =>
            new(
                editability,
                _controlType,
                _decisionBranch,
                _valuePatternAvailable,
                _textPatternAvailable);
    }

    public bool TryGetCaretScreenPoint(out Point screenPoint)
    {
        return TryGetAutomationCaretScreenPoint(out screenPoint) ||
            TryGetSystemCaretScreenPoint(out screenPoint);
    }

    internal bool TryGetCaretScreenPointWithDiagnostics(
        out Point screenPoint,
        out PeekCaretResolutionDiagnostic diagnostic)
    {
        if (TryGetAutomationCaretScreenPointWithDiagnostics(
                out screenPoint,
                out diagnostic))
        {
            return true;
        }

        if (TryGetSystemCaretScreenPointWithDiagnostics(
                out screenPoint,
                out diagnostic))
        {
            return true;
        }

        screenPoint = default;
        diagnostic = new PeekCaretResolutionDiagnostic(
            "none→fallback",
            Rectangle: null);
        return false;
    }

    internal PeekFocusedElementDiagnosticContext CaptureDiagnosticContext()
    {
        try
        {
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement is null)
            {
                return PeekFocusedElementDiagnosticContext.Unavailable;
            }

            return new PeekFocusedElementDiagnosticContext(
                TryGetFocusedProcessName(focusedElement),
                TryGetFocusedWindowClass(focusedElement),
                TryGetIsFieldEmpty(focusedElement));
        }
        catch (Exception)
        {
            return PeekFocusedElementDiagnosticContext.Unavailable;
        }
    }

    internal PeekCollapsedCaretProbeAttempt ProbeCollapsedCaret()
    {
        try
        {
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement is null)
            {
                return PeekCollapsedCaretProbeAttempt.Failed(
                    "focused-element-unavailable");
            }

            if (!focusedElement.TryGetCurrentPattern(
                    TextPattern.Pattern,
                    out var textPatternObject))
            {
                return PeekCollapsedCaretProbeAttempt.Failed(
                    "text-pattern-unavailable");
            }

            var selection = ((TextPattern)textPatternObject).GetSelection();
            if (selection.Length != 1 ||
                selection[0].CompareEndpoints(
                    TextPatternRangeEndpoint.Start,
                    selection[0],
                    TextPatternRangeEndpoint.End) != 0)
            {
                return PeekCollapsedCaretProbeAttempt.Failed(
                    "selection-not-collapsed");
            }

            var rectangles = selection[0].GetBoundingRectangles();
            var rectangle = rectangles.Length == 1
                ? PeekCaretRectangleDiagnostic.From(rectangles[0])
                : null;

            if (rectangles.Length != 1)
            {
                return PeekCollapsedCaretProbeAttempt.Failed(
                    "rectangle-count-not-one",
                    rectangles.Length,
                    rectangle);
            }

            if (!IsReliableRectangle(rectangles[0]))
            {
                return PeekCollapsedCaretProbeAttempt.Failed(
                    "rectangle-unreliable",
                    rectangles.Length,
                    rectangle);
            }

            if (!IsThinCaretRectangle(rectangles[0]))
            {
                return PeekCollapsedCaretProbeAttempt.Failed(
                    "rectangle-not-thin",
                    rectangles.Length,
                    rectangle);
            }

            var screenPoint = new Point(
                checked((int)Math.Round(rectangles[0].Left)),
                checked((int)Math.Round(rectangles[0].Bottom)));
            return PeekCollapsedCaretProbeAttempt.Settled(
                screenPoint,
                rectangle!);
        }
        catch (Exception)
        {
            return PeekCollapsedCaretProbeAttempt.Failed("provider-exception");
        }
    }

    private static bool TryGetAutomationCaretScreenPoint(out Point screenPoint)
    {
        screenPoint = default;

        try
        {
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement is null ||
                !focusedElement.TryGetCurrentPattern(
                    TextPattern.Pattern,
                    out var textPatternObject))
            {
                return false;
            }

            var selection = ((TextPattern)textPatternObject).GetSelection();
            if (selection.Length != 1 ||
                selection[0].CompareEndpoints(
                    TextPatternRangeEndpoint.Start,
                    selection[0],
                    TextPatternRangeEndpoint.End) != 0)
            {
                return false;
            }

            if (TryGetCollapsedRangeCaretScreenPoint(
                    selection[0],
                    out screenPoint))
            {
                return true;
            }

            var caretRange = selection[0].Clone();
            var expandedBackward = caretRange.MoveEndpointByUnit(
                TextPatternRangeEndpoint.Start,
                TextUnit.Character,
                -1) != 0;
            if (!expandedBackward &&
                caretRange.MoveEndpointByUnit(
                    TextPatternRangeEndpoint.End,
                    TextUnit.Character,
                    1) == 0)
            {
                return false;
            }

            var rectangles = caretRange
                .GetBoundingRectangles()
                .Where(IsReliableRectangle)
                .ToArray();
            if (rectangles.Length == 0)
            {
                return false;
            }

            var rectangle = expandedBackward
                ? rectangles[^1]
                : rectangles[0];
            var caretX = expandedBackward ? rectangle.Right : rectangle.Left;
            screenPoint = new Point(
                checked((int)Math.Round(caretX)),
                checked((int)Math.Round(rectangle.Bottom)));
            return true;
        }
        catch (Exception)
        {
            screenPoint = default;
            return false;
        }
    }

    private static bool TryGetAutomationCaretScreenPointWithDiagnostics(
        out Point screenPoint,
        out PeekCaretResolutionDiagnostic diagnostic)
    {
        screenPoint = default;
        diagnostic = default!;

        try
        {
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement is null ||
                !focusedElement.TryGetCurrentPattern(
                    TextPattern.Pattern,
                    out var textPatternObject))
            {
                return false;
            }

            var selection = ((TextPattern)textPatternObject).GetSelection();
            if (selection.Length != 1 ||
                selection[0].CompareEndpoints(
                    TextPatternRangeEndpoint.Start,
                    selection[0],
                    TextPatternRangeEndpoint.End) != 0)
            {
                return false;
            }

            if (TryGetCollapsedRangeCaretScreenPointWithDiagnostics(
                    selection[0],
                    out screenPoint,
                    out var collapsedRectangle))
            {
                diagnostic = new PeekCaretResolutionDiagnostic(
                    "collapsed-valid",
                    collapsedRectangle);
                return true;
            }

            var caretRange = selection[0].Clone();
            var expandedBackward = caretRange.MoveEndpointByUnit(
                TextPatternRangeEndpoint.Start,
                TextUnit.Character,
                -1) != 0;
            if (!expandedBackward &&
                caretRange.MoveEndpointByUnit(
                    TextPatternRangeEndpoint.End,
                    TextUnit.Character,
                    1) == 0)
            {
                return false;
            }

            var rectangles = caretRange
                .GetBoundingRectangles()
                .Where(IsReliableRectangle)
                .ToArray();
            if (rectangles.Length == 0)
            {
                return false;
            }

            var rectangle = expandedBackward
                ? rectangles[^1]
                : rectangles[0];
            var caretX = expandedBackward ? rectangle.Right : rectangle.Left;
            screenPoint = new Point(
                checked((int)Math.Round(caretX)),
                checked((int)Math.Round(rectangle.Bottom)));
            diagnostic = new PeekCaretResolutionDiagnostic(
                expandedBackward
                    ? "expansion-backward"
                    : "expansion-forward",
                PeekCaretRectangleDiagnostic.From(rectangle));
            return true;
        }
        catch (Exception)
        {
            screenPoint = default;
            diagnostic = default!;
            return false;
        }
    }

    private static bool TryGetCollapsedRangeCaretScreenPoint(
        TextPatternRange caretRange,
        out Point screenPoint)
    {
        screenPoint = default;

        try
        {
            var rectangles = caretRange.GetBoundingRectangles();
            if (rectangles.Length != 1 ||
                !IsReliableRectangle(rectangles[0]))
            {
                return false;
            }

            screenPoint = new Point(
                checked((int)Math.Round(rectangles[0].Left)),
                checked((int)Math.Round(rectangles[0].Bottom)));
            return true;
        }
        catch (Exception)
        {
            screenPoint = default;
            return false;
        }
    }

    private static bool TryGetCollapsedRangeCaretScreenPointWithDiagnostics(
        TextPatternRange caretRange,
        out Point screenPoint,
        out PeekCaretRectangleDiagnostic? diagnosticRectangle)
    {
        screenPoint = default;
        diagnosticRectangle = null;

        try
        {
            var rectangles = caretRange.GetBoundingRectangles();
            if (rectangles.Length != 1 ||
                !IsReliableRectangle(rectangles[0]))
            {
                return false;
            }

            screenPoint = new Point(
                checked((int)Math.Round(rectangles[0].Left)),
                checked((int)Math.Round(rectangles[0].Bottom)));
            diagnosticRectangle = PeekCaretRectangleDiagnostic.From(rectangles[0]);
            return true;
        }
        catch (Exception)
        {
            screenPoint = default;
            diagnosticRectangle = null;
            return false;
        }
    }

    private static bool TryGetSystemCaretScreenPoint(out Point screenPoint)
    {
        screenPoint = default;

        try
        {
            var threadInfo = new GuiThreadInfo
            {
                Size = checked((uint)Marshal.SizeOf<GuiThreadInfo>()),
            };
            if (!GetGUIThreadInfo(0, ref threadInfo) ||
                threadInfo.CaretWindow == nint.Zero)
            {
                return false;
            }

            var caretPoint = new NativePoint
            {
                X = threadInfo.CaretRectangle.Left,
                Y = threadInfo.CaretRectangle.Bottom,
            };
            if (!ClientToScreen(threadInfo.CaretWindow, ref caretPoint))
            {
                return false;
            }

            screenPoint = new Point(caretPoint.X, caretPoint.Y);
            return true;
        }
        catch (Exception)
        {
            screenPoint = default;
            return false;
        }
    }

    private static bool TryGetSystemCaretScreenPointWithDiagnostics(
        out Point screenPoint,
        out PeekCaretResolutionDiagnostic diagnostic)
    {
        screenPoint = default;
        diagnostic = default!;

        try
        {
            var threadInfo = new GuiThreadInfo
            {
                Size = checked((uint)Marshal.SizeOf<GuiThreadInfo>()),
            };
            if (!GetGUIThreadInfo(0, ref threadInfo) ||
                threadInfo.CaretWindow == nint.Zero)
            {
                return false;
            }

            var caretPoint = new NativePoint
            {
                X = threadInfo.CaretRectangle.Left,
                Y = threadInfo.CaretRectangle.Bottom,
            };
            if (!ClientToScreen(threadInfo.CaretWindow, ref caretPoint))
            {
                return false;
            }

            screenPoint = new Point(caretPoint.X, caretPoint.Y);
            var width = threadInfo.CaretRectangle.Right -
                threadInfo.CaretRectangle.Left;
            var height = threadInfo.CaretRectangle.Bottom -
                threadInfo.CaretRectangle.Top;
            diagnostic = new PeekCaretResolutionDiagnostic(
                "system-caret",
                new PeekCaretRectangleDiagnostic(
                    caretPoint.X,
                    caretPoint.Y - height,
                    width,
                    height));
            return true;
        }
        catch (Exception)
        {
            screenPoint = default;
            diagnostic = default!;
            return false;
        }
    }

    private static bool IsReliableRectangle(System.Windows.Rect rectangle) =>
        !rectangle.IsEmpty &&
        double.IsFinite(rectangle.Left) &&
        double.IsFinite(rectangle.Top) &&
        double.IsFinite(rectangle.Width) &&
        double.IsFinite(rectangle.Height) &&
        rectangle.Width > 0 &&
        rectangle.Height > 0;

    private static bool IsThinCaretRectangle(System.Windows.Rect rectangle) =>
        rectangle.Width < rectangle.Height;

    private static string? TryGetFocusedProcessName(AutomationElement focusedElement)
    {
        try
        {
            var processId = focusedElement.Current.ProcessId;
            if (processId <= 0)
            {
                return null;
            }

            using var process = Process.GetProcessById(processId);
            return string.IsNullOrWhiteSpace(process.ProcessName)
                ? null
                : process.ProcessName;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? TryGetFocusedWindowClass(AutomationElement focusedElement)
    {
        try
        {
            var className = focusedElement.Current.ClassName;
            return string.IsNullOrWhiteSpace(className)
                ? null
                : className;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool? TryGetIsFieldEmpty(AutomationElement focusedElement)
    {
        try
        {
            if (focusedElement.TryGetCurrentPattern(
                    TextPattern.Pattern,
                    out var textPatternObject))
            {
                var documentRange = ((TextPattern)textPatternObject).DocumentRange;
                return documentRange.CompareEndpoints(
                    TextPatternRangeEndpoint.Start,
                    documentRange,
                    TextPatternRangeEndpoint.End) == 0;
            }

            if (focusedElement.TryGetCurrentPattern(
                    ValuePattern.Pattern,
                    out var valuePatternObject))
            {
                return string.IsNullOrEmpty(
                    ((ValuePattern)valuePatternObject).Current.Value);
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        internal uint Size;
        internal uint Flags;
        internal nint ActiveWindow;
        internal nint FocusWindow;
        internal nint CaptureWindow;
        internal nint MenuOwnerWindow;
        internal nint MoveSizeWindow;
        internal nint CaretWindow;
        internal NativeRectangle CaretRectangle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X;
        internal int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(
        uint threadId,
        ref GuiThreadInfo guiThreadInfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(
        nint window,
        ref NativePoint point);
}

internal sealed record PeekCaretResolutionDiagnostic(
    string Branch,
    PeekCaretRectangleDiagnostic? Rectangle);

internal sealed record PeekFocusedElementDiagnosticContext(
    string? ProcessName,
    string? WindowClass,
    bool? IsFieldEmpty)
{
    internal static PeekFocusedElementDiagnosticContext Unavailable { get; } =
        new(null, null, null);
}

internal sealed record PeekCollapsedCaretProbeAttempt(
    bool IsSettled,
    string Outcome,
    int? RectangleCount,
    Point? ScreenPoint,
    PeekCaretRectangleDiagnostic? Rectangle)
{
    internal static PeekCollapsedCaretProbeAttempt Settled(
        Point screenPoint,
        PeekCaretRectangleDiagnostic rectangle) =>
        new(true, "settled", 1, screenPoint, rectangle);

    internal static PeekCollapsedCaretProbeAttempt Failed(
        string outcome,
        int? rectangleCount = null,
        PeekCaretRectangleDiagnostic? rectangle = null) =>
        new(false, outcome, rectangleCount, null, rectangle);
}

internal sealed record PeekCaretRectangleDiagnostic(
    double Left,
    double Top,
    double Width,
    double Height)
{
    internal static PeekCaretRectangleDiagnostic From(
        System.Windows.Rect rectangle) =>
        new(rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height);
}

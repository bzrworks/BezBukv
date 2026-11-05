namespace BezBukv.Core.Platform;

public interface IFocusedElementService
{
    FocusedElementEditability GetEditability();

    FocusedElementEditabilityDiagnosticResult GetEditabilityWithDiagnostics();
}

public enum FocusedElementEditability
{
    Editable,
    ReadOnly,
    Unknown,
}

public sealed record FocusedElementEditabilityDiagnosticResult(
    FocusedElementEditability Editability,
    string ControlType,
    string DecisionBranch,
    bool? ValuePatternAvailable,
    bool? TextPatternAvailable);

public static class FocusedElementEditabilityDiagnosticValues
{
    public const string UnavailableControlType = "unavailable";

    public const string NoFocusBranch = "no_focus";

    public const string NotEnabledBranch = "not_enabled";

    public const string ControlTypeGateBranch = "control_type_gate";

    public const string ContainerControlTypeGateBranch =
        "container_control_type_gate";

    public const string ValuePatternBranch = "value_pattern";

    public const string TextPatternBranch = "text_pattern";

    public const string NoPatternBranch = "no_pattern";

    public const string ExceptionBranch = "exception";
}

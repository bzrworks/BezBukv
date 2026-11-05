namespace BezBukv.Core.Platform;

public interface IInputSimulationService
{
    InputSimulationResult SendCopy();

    InputSimulationResult SendPaste();

    InputSimulationResult SendPaste(bool includeDiagnosticDetails) => SendPaste();
}

public enum InputSimulationOutcome
{
    Success,
    PlatformError,
}

public sealed record InputSimulationResult
{
    private InputSimulationResult(
        InputSimulationOutcome outcome,
        int? platformErrorCode,
        bool? physicalControlDown,
        int? plannedInputCount,
        int? sentInputCount)
    {
        Outcome = outcome;
        PlatformErrorCode = platformErrorCode;
        PhysicalControlDown = physicalControlDown;
        PlannedInputCount = plannedInputCount;
        SentInputCount = sentInputCount;
    }

    public InputSimulationOutcome Outcome { get; }

    public int? PlatformErrorCode { get; }

    public bool? PhysicalControlDown { get; }

    public int? PlannedInputCount { get; }

    public int? SentInputCount { get; }

    public static InputSimulationResult Success { get; } =
        new(InputSimulationOutcome.Success, null, null, null, null);

    public static InputSimulationResult FromSuccessfulSend(
        bool physicalControlDown,
        int plannedInputCount,
        int sentInputCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(plannedInputCount);
        ArgumentOutOfRangeException.ThrowIfNegative(sentInputCount);

        return new InputSimulationResult(
            InputSimulationOutcome.Success,
            null,
            physicalControlDown,
            plannedInputCount,
            sentInputCount);
    }

    public static InputSimulationResult FromPlatformError(
        int platformErrorCode,
        bool? physicalControlDown = null,
        int? plannedInputCount = null,
        int? sentInputCount = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(plannedInputCount ?? 0);
        ArgumentOutOfRangeException.ThrowIfNegative(sentInputCount ?? 0);

        return new InputSimulationResult(
            InputSimulationOutcome.PlatformError,
            platformErrorCode,
            physicalControlDown,
            plannedInputCount,
            sentInputCount);
    }
}

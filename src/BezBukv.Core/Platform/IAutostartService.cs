namespace BezBukv.Core.Platform;

public interface IAutostartService
{
    bool IsSupported { get; }

    AutostartOperationStatus Enable();

    AutostartOperationStatus Disable();

    AutostartState GetState();
}

public enum AutostartOperationStatus
{
    Success,
    NotSupported,
    PlatformError,
}

public enum AutostartState
{
    Enabled,
    Disabled,
    NotSupported,
    PlatformError,
}

using BezBukv.Core.Platform;
using Windows.ApplicationModel;

namespace BezBukv.Windows;

public sealed class PackagedStartupTaskAutostartService : IAutostartService
{
    private const string StartupTaskId = "BezBukvStartup";

    public bool IsSupported => true;

    public AutostartOperationStatus Enable()
    {
        try
        {
            var state = GetStartupTask()
                .RequestEnableAsync()
                .AsTask()
                .GetAwaiter()
                .GetResult();

            return IsEnabled(state)
                ? AutostartOperationStatus.Success
                : AutostartOperationStatus.PlatformError;
        }
        catch (Exception)
        {
            return AutostartOperationStatus.PlatformError;
        }
    }

    public AutostartOperationStatus Disable()
    {
        try
        {
            GetStartupTask().Disable();
            return AutostartOperationStatus.Success;
        }
        catch (Exception)
        {
            return AutostartOperationStatus.PlatformError;
        }
    }

    public AutostartState GetState()
    {
        try
        {
            return GetStartupTask().State switch
            {
                StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy =>
                    AutostartState.Enabled,
                StartupTaskState.Disabled or StartupTaskState.DisabledByUser or
                    StartupTaskState.DisabledByPolicy => AutostartState.Disabled,
                _ => AutostartState.PlatformError,
            };
        }
        catch (Exception)
        {
            return AutostartState.PlatformError;
        }
    }

    private static StartupTask GetStartupTask() =>
        StartupTask
            .GetAsync(StartupTaskId)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    private static bool IsEnabled(StartupTaskState state) =>
        state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
}

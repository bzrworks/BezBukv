using BezBukv.Core.Platform;
using Microsoft.Win32;

namespace BezBukv.Windows;

public sealed class WindowsAutostartService : IAutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const int StartupApprovedValueLength = 12;
    private const string ValueName = "BezBukv";

    private readonly string? _autostartCommand = CreateAutostartCommand();

    public bool IsSupported => true;

    public AutostartOperationStatus Enable()
    {
        if (_autostartCommand is null)
        {
            return AutostartOperationStatus.PlatformError;
        }

        try
        {
            using var currentUser = RegistryKey.OpenBaseKey(
                RegistryHive.CurrentUser,
                RegistryView.Default);
            using var runKey = currentUser.CreateSubKey(RunKeyPath, writable: true);

            if (runKey is null)
            {
                return AutostartOperationStatus.PlatformError;
            }

            runKey.SetValue(ValueName, _autostartCommand, RegistryValueKind.String);

            using var startupApprovedKey = currentUser.OpenSubKey(
                StartupApprovedKeyPath,
                writable: true);
            startupApprovedKey?.DeleteValue(ValueName, throwOnMissingValue: false);

            return AutostartOperationStatus.Success;
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
            using var currentUser = RegistryKey.OpenBaseKey(
                RegistryHive.CurrentUser,
                RegistryView.Default);
            using var runKey = currentUser.OpenSubKey(RunKeyPath, writable: true);
            using var startupApprovedKey = currentUser.OpenSubKey(
                StartupApprovedKeyPath,
                writable: true);

            runKey?.DeleteValue(ValueName, throwOnMissingValue: false);
            startupApprovedKey?.DeleteValue(ValueName, throwOnMissingValue: false);

            return AutostartOperationStatus.Success;
        }
        catch (Exception)
        {
            return AutostartOperationStatus.PlatformError;
        }
    }

    public AutostartState GetState()
    {
        if (_autostartCommand is null)
        {
            return AutostartState.PlatformError;
        }

        try
        {
            using var currentUser = RegistryKey.OpenBaseKey(
                RegistryHive.CurrentUser,
                RegistryView.Default);
            using var runKey = currentUser.OpenSubKey(RunKeyPath, writable: false);
            var registeredCommand = runKey?.GetValue(
                ValueName,
                defaultValue: null,
                RegistryValueOptions.DoNotExpandEnvironmentNames) as string;

            if (!string.Equals(
                registeredCommand,
                _autostartCommand,
                StringComparison.OrdinalIgnoreCase))
            {
                return AutostartState.Disabled;
            }

            return IsStartupApproved(currentUser)
                ? AutostartState.Enabled
                : AutostartState.Disabled;
        }
        catch (Exception)
        {
            return AutostartState.PlatformError;
        }
    }

    private static bool IsStartupApproved(RegistryKey currentUser)
    {
        using var startupApprovedKey = currentUser.OpenSubKey(
            StartupApprovedKeyPath,
            writable: false);
        var value = startupApprovedKey?.GetValue(
            ValueName,
            defaultValue: null,
            RegistryValueOptions.DoNotExpandEnvironmentNames);

        if (value is null)
        {
            return true;
        }

        return startupApprovedKey!.GetValueKind(ValueName) == RegistryValueKind.Binary &&
            value is byte[] { Length: >= StartupApprovedValueLength } data &&
            data[0] is 0x02 or 0x06;
    }

    private static string? CreateAutostartCommand()
    {
        var executablePath = Environment.ProcessPath;
        return string.IsNullOrWhiteSpace(executablePath)
            ? null
            : $"\"{executablePath}\"";
    }
}

namespace BezBukv.Core.Platform;

public interface IActiveApplicationService
{
    ActiveApplicationInfo GetActiveApplication();
}

public enum ActiveApplicationDetectionOutcome
{
    Detected,
    Unavailable,
}

public sealed record ActiveApplicationInfo
{
    private ActiveApplicationInfo(
        ActiveApplicationDetectionOutcome outcome,
        string? processName,
        bool isTerminal,
        long? windowHandle)
    {
        Outcome = outcome;
        ProcessName = processName;
        IsTerminal = isTerminal;
        WindowHandle = windowHandle;
    }

    public ActiveApplicationDetectionOutcome Outcome { get; }

    public string? ProcessName { get; }

    public bool IsTerminal { get; }

    public long? WindowHandle { get; }

    public static ActiveApplicationInfo FromProcessName(
        string processName,
        long? windowHandle = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);

        var normalizedProcessName = processName.Trim();

        return new ActiveApplicationInfo(
            ActiveApplicationDetectionOutcome.Detected,
            normalizedProcessName,
            TerminalApplicationClassifier.IsTerminalProcessName(normalizedProcessName),
            windowHandle);
    }

    public static ActiveApplicationInfo Unavailable { get; } =
        new(ActiveApplicationDetectionOutcome.Unavailable, null, false, null);
}

public static class TerminalApplicationClassifier
{
    private static readonly HashSet<string> TerminalProcessNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "cmd",
            "conhost",
            "OpenConsole",
            "powershell",
            "pwsh",
            "WindowsTerminal",
            "wt",
        };

    public static bool IsTerminalProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var normalizedProcessName = processName.Trim();

        if (normalizedProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            normalizedProcessName = normalizedProcessName[..^4];
        }

        return TerminalProcessNames.Contains(normalizedProcessName);
    }
}

namespace BezBukv.Core.Platform;

public interface IDiagnosticLogger
{
    DiagnosticRunContext StartRun();

    void Log(
        DiagnosticRunContext run,
        string eventName,
        IReadOnlyDictionary<string, object?>? fields = null);
}

public sealed class NoOpDiagnosticLogger : IDiagnosticLogger
{
    private NoOpDiagnosticLogger()
    {
    }

    public static NoOpDiagnosticLogger Instance { get; } = new();

    public DiagnosticRunContext StartRun() => DiagnosticRunContext.Disabled;

    public void Log(
        DiagnosticRunContext run,
        string eventName,
        IReadOnlyDictionary<string, object?>? fields = null)
    {
    }
}

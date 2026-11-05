namespace BezBukv.Core.Platform;

public interface IAppPathsService
{
    string SettingsFilePath { get; }

    string DiagnosticsLogFilePath { get; }
}

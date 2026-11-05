using System.IO;
using BezBukv.Core.Platform;

namespace BezBukv.Windows;

public sealed class WindowsAppPathsService : IAppPathsService
{
    private const string AppDirectoryName = "BezBukv";
    private const string SettingsFileName = "settings.json";
    private const string DiagnosticsLogFileName = "diagnostics.log.jsonl";

    public WindowsAppPathsService()
    {
        var appDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDirectoryName);

        Directory.CreateDirectory(appDirectoryPath);

        SettingsFilePath = Path.Combine(appDirectoryPath, SettingsFileName);
        DiagnosticsLogFilePath = Path.Combine(appDirectoryPath, DiagnosticsLogFileName);
    }

    public string SettingsFilePath { get; }

    public string DiagnosticsLogFilePath { get; }
}

namespace BezBukv.Core.Settings;

public sealed record SettingsLoadResult(
    AppSettings Settings,
    SettingsLoadStatus Status);

public enum SettingsLoadStatus
{
    CreatedDefault,
    Loaded,
    MigratedUp,
    NewerVersionIgnored,
    CorruptFellBackToDefaults,
}

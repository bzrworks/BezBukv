using System.Text.Json.Serialization;
using BezBukv.Core.Localization;
using BezBukv.Core.Platform;

namespace BezBukv.Core.Settings;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 4;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public GeneralSettings General { get; init; } = new();

    public CorrectionSettings Correction { get; init; } = new();

    public PeekSettings Peek { get; init; } = new();
}

public sealed record GeneralSettings
{
    public bool Autostart { get; init; }

    public AppLanguage Language { get; init; } = AppLanguage.System;

    public AppTheme Theme { get; init; } = AppTheme.System;

    public bool LayoutAutoSwitchEnabled { get; init; } = true;
}

public enum AppTheme
{
    System,
    Light,
    Dark,
}

public sealed record CorrectionSettings
{
    public bool CorrectionEnabled { get; init; } = true;

    public GlobalHotkey? CorrectionHotkey { get; init; } =
        AppSettingsDefaults.CreateCorrectionHotkey();

    public CorrectionDirection Direction { get; init; } = CorrectionDirection.Auto;

    public int CopyTimeoutMs { get; init; } = 500;

    public int ClipboardRestoreDelayMs { get; init; } = 300;

    public int TextLimitBytes { get; init; } = ClipboardTextLimit.DefaultMaxBytes;

    public bool PasteIntoUnknown { get; init; }
}

public enum CorrectionDirection
{
    Auto,
    EnglishToRussian,
    RussianToEnglish,
}

public sealed record PeekSettings
{
    public const int MinimumAutoExitTimeoutSeconds = 10;

    public const int MaximumSelectedLayoutCount = 2;

    public bool PeekEnabled { get; init; } = true;

    public GlobalHotkey? ActivationHotkey { get; init; } =
        AppSettingsDefaults.CreatePeekHotkey();

    public KeyboardProfile KeyboardProfile { get; init; } = KeyboardProfile.Unknown;

    public bool AutoPeekEnabled { get; init; }

    public int AutoPeekLingerMs { get; init; } = 1500;

    public int AutoExitTimeoutSeconds { get; init; } = 120;

    public double Opacity { get; init; } = 0.80;

    public double Scale { get; init; } = 1.0;

    public int AnimationDurationMs { get; init; } =
        OverlayAppearanceOptions.DefaultAnimationDurationMs;

    public bool AnimationEnabled { get; init; } = true;

    public bool BlockNumberRow { get; init; } = true;

    public bool ShowServiceKeys { get; init; }

    /// <summary>
    /// Stable KLIDs of the keyboard layouts shown by Peek, ordered by visual slot.
    /// </summary>
    public IReadOnlyList<string> SelectedLayoutKlids { get; init; } = [];

    /// <summary>
    /// The user-calibrated physical keyboard layout. Non-null only for
    /// <see cref="KeyboardProfile.Custom"/>; null for every other profile.
    /// </summary>
    public CustomKeyboardLayout? CustomKeyboardLayout { get; init; }
}

public enum KeyboardProfile
{
    Unknown,
    Ansi,
    Iso,
    Custom,
}

/// <summary>
/// A user-calibrated physical keyboard captured for the <see cref="KeyboardProfile.Custom"/> profile.
/// Rows are ordered top-to-bottom; cells within a row are ordered left-to-right.
/// </summary>
public sealed record CustomKeyboardLayout
{
    public IReadOnlyList<CustomKeyboardRow> Rows { get; init; } = [];
}

/// <summary>
/// One calibrated row: an ordered left-to-right list of cells.
/// </summary>
public sealed record CustomKeyboardRow
{
    public IReadOnlyList<CustomKeyboardCell> Cells { get; init; } = [];
}

/// <summary>
/// A single cell of a calibrated row. The cell is either a key (<see cref="Key"/> non-null) or an empty
/// spacer (<see cref="Key"/> null). <see cref="WidthUnits"/> is the cell width measured in whole
/// standard-key widths (quanta), minimum 1.
/// </summary>
public sealed record CustomKeyboardCell
{
    public CustomKeyboardKey? Key { get; init; }

    public int WidthUnits { get; init; } = 1;
}

/// <summary>
/// A physical key captured during calibration. Raw platform codes allow storing keys that are absent
/// from the platform key enum; display symbols are resolved from the selected layouts at runtime.
/// </summary>
public sealed record CustomKeyboardKey
{
    public int ScanCode { get; init; }

    public bool Extended { get; init; }

    public int? VirtualKey { get; init; }

    [JsonIgnore]
    public bool IsAltGr { get; init; }
}

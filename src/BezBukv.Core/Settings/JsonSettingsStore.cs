using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BezBukv.Core.Localization;
using BezBukv.Core.Platform;

namespace BezBukv.Core.Settings;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly IAppPathsService _appPathsService;

    public JsonSettingsStore(IAppPathsService appPathsService)
    {
        ArgumentNullException.ThrowIfNull(appPathsService);

        _appPathsService = appPathsService;
    }

    public SettingsLoadResult Load() => LoadFromPath(
        _appPathsService.SettingsFilePath,
        persistFallbackDefaults: true);

    public void Save(AppSettings settings) =>
        WriteSettings(_appPathsService.SettingsFilePath, NormalizeForSave(settings));

    public void Export(AppSettings settings, string destinationPath) =>
        WriteSettings(destinationPath, NormalizeForSave(settings));

    public SettingsLoadResult Import(string sourcePath)
    {
        var result = LoadFromPath(sourcePath, persistFallbackDefaults: false);

        if (result.Status is SettingsLoadStatus.CreatedDefault
            or SettingsLoadStatus.Loaded
            or SettingsLoadStatus.MigratedUp)
        {
            WriteSettings(_appPathsService.SettingsFilePath, result.Settings);
        }

        return result;
    }

    public AppSettings Reset()
    {
        var settings = AppSettingsDefaults.Create();
        WriteSettings(_appPathsService.SettingsFilePath, settings);
        return settings;
    }

    private static SettingsLoadResult LoadFromPath(
        string filePath,
        bool persistFallbackDefaults)
    {
        var fullPath = GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            var defaults = AppSettingsDefaults.Create();
            if (persistFallbackDefaults)
            {
                WriteSettings(fullPath, defaults);
            }

            return new SettingsLoadResult(
                defaults,
                SettingsLoadStatus.CreatedDefault);
        }

        var json = File.ReadAllText(fullPath, Encoding.UTF8);

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !TryReadSchemaVersion(document.RootElement, out var schemaVersion))
            {
                return CorruptFallback(fullPath, persistFallbackDefaults);
            }

            if (schemaVersion > AppSettings.CurrentSchemaVersion)
            {
                return new SettingsLoadResult(
                    AppSettingsDefaults.Create(),
                    SettingsLoadStatus.NewerVersionIgnored);
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);

            if (settings is null)
            {
                return CorruptFallback(fullPath, persistFallbackDefaults);
            }

            settings = settings with
            {
                SchemaVersion = AppSettings.CurrentSchemaVersion,
            };
            settings = NormalizePositiveTimingsForLoad(
                settings,
                out var positiveTimingsMigrated);
            settings = NormalizeAutoExitTimeoutForLoad(
                settings,
                out var autoExitTimeoutMigrated);
            settings = NormalizeCorrectionHotkeyForLoad(
                settings,
                out var correctionHotkeyMigrated);
            settings = NormalizeCustomProfileForLoad(
                settings,
                out var customProfileMigrated);

            if (!AreValid(settings))
            {
                return CorruptFallback(fullPath, persistFallbackDefaults);
            }

            if (schemaVersion < AppSettings.CurrentSchemaVersion
                || positiveTimingsMigrated
                || autoExitTimeoutMigrated
                || correctionHotkeyMigrated
                || customProfileMigrated)
            {
                WriteSettings(fullPath, settings);

                return new SettingsLoadResult(
                    settings,
                    SettingsLoadStatus.MigratedUp);
            }

            return new SettingsLoadResult(settings, SettingsLoadStatus.Loaded);
        }
        catch (JsonException)
        {
            return CorruptFallback(fullPath, persistFallbackDefaults);
        }
        catch (NotSupportedException)
        {
            return CorruptFallback(fullPath, persistFallbackDefaults);
        }
    }

    private static AppSettings NormalizeForSave(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = settings with
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
        };

        if (!AreValid(normalized))
        {
            throw new ArgumentException(
                "Settings contain unsupported or invalid values.",
                nameof(settings));
        }

        return normalized;
    }

    private static AppSettings NormalizePositiveTimingsForLoad(
        AppSettings settings,
        out bool migrated)
    {
        var correction = settings.Correction;

        if (correction is null
            || (correction.CopyTimeoutMs > 0
                && correction.ClipboardRestoreDelayMs > 0))
        {
            migrated = false;
            return settings;
        }

        var defaults = AppSettingsDefaults.Create().Correction;
        migrated = true;

        return settings with
        {
            Correction = correction with
            {
                CopyTimeoutMs = correction.CopyTimeoutMs > 0
                    ? correction.CopyTimeoutMs
                    : defaults.CopyTimeoutMs,
                ClipboardRestoreDelayMs = correction.ClipboardRestoreDelayMs > 0
                    ? correction.ClipboardRestoreDelayMs
                    : defaults.ClipboardRestoreDelayMs,
            },
        };
    }

    private static AppSettings NormalizeCorrectionHotkeyForLoad(
        AppSettings settings,
        out bool migrated)
    {
        var correction = settings.Correction;

        if (correction is null
            || correction.CorrectionHotkey is null
            || !IsValidHotkey(correction.CorrectionHotkey)
            || IsValidCorrectionHotkey(correction.CorrectionHotkey))
        {
            migrated = false;
            return settings;
        }

        migrated = true;

        return settings with
        {
            Correction = correction with
            {
                CorrectionHotkey = AppSettingsDefaults.CreateCorrectionHotkey(),
            },
        };
    }

    private static AppSettings NormalizeAutoExitTimeoutForLoad(
        AppSettings settings,
        out bool migrated)
    {
        var peek = settings.Peek;

        if (peek is null
            || peek.AutoExitTimeoutSeconds >= PeekSettings.MinimumAutoExitTimeoutSeconds)
        {
            migrated = false;
            return settings;
        }

        migrated = true;

        return settings with
        {
            Peek = peek with
            {
                AutoExitTimeoutSeconds = PeekSettings.MinimumAutoExitTimeoutSeconds,
            },
        };
    }

    private static AppSettings NormalizeCustomProfileForLoad(
        AppSettings settings,
        out bool migrated)
    {
        var normalized = AppSettingsNormalizer.NormalizeCustomProfile(settings);
        migrated = !ReferenceEquals(normalized, settings);
        return normalized;
    }

    private static bool TryReadSchemaVersion(JsonElement root, out int schemaVersion)
    {
        schemaVersion = 0;

        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(
                    property.Name,
                    "schemaVersion",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return property.Value.ValueKind == JsonValueKind.Number
                && property.Value.TryGetInt32(out schemaVersion)
                && schemaVersion >= 0;
        }

        return true;
    }

    private static bool AreValid(AppSettings settings)
    {
        if (settings.SchemaVersion != AppSettings.CurrentSchemaVersion
            || settings.General is null
            || settings.Correction is null
            || settings.Peek is null)
        {
            return false;
        }

        if (!Enum.IsDefined(settings.General.Theme)
            || !Enum.IsDefined(settings.General.Language))
        {
            return false;
        }

        if ((settings.Correction.CorrectionHotkey is not null
                && !IsValidCorrectionHotkey(settings.Correction.CorrectionHotkey))
            || !Enum.IsDefined(settings.Correction.Direction)
            || settings.Correction.TextLimitBytes <= 0
            || settings.Correction.CopyTimeoutMs <= 0
            || settings.Correction.ClipboardRestoreDelayMs <= 0)
        {
            return false;
        }

        return Enum.IsDefined(settings.Peek.KeyboardProfile)
            && (settings.Peek.ActivationHotkey is null
                || IsValidHotkey(settings.Peek.ActivationHotkey))
            && settings.Peek.AutoPeekLingerMs > 0
            && settings.Peek.AutoExitTimeoutSeconds
                >= PeekSettings.MinimumAutoExitTimeoutSeconds
            && double.IsFinite(settings.Peek.Opacity)
            && settings.Peek.Opacity > 0
            && settings.Peek.Opacity <= 1
            && double.IsFinite(settings.Peek.Scale)
            && settings.Peek.Scale > 0
            && settings.Peek.AnimationDurationMs > 0
            && AreValidSelectedLayoutKlids(settings.Peek.SelectedLayoutKlids)
            && AreValidCustomProfile(
                settings.Peek.KeyboardProfile,
                settings.Peek.CustomKeyboardLayout);
    }

    private static bool AreValidSelectedLayoutKlids(IReadOnlyList<string>? layoutKlids) =>
        layoutKlids is { Count: <= PeekSettings.MaximumSelectedLayoutCount }
        && layoutKlids.All(layoutKlid => !string.IsNullOrWhiteSpace(layoutKlid));

    private static bool AreValidCustomProfile(
        KeyboardProfile profile,
        CustomKeyboardLayout? layout) =>
        profile == KeyboardProfile.Custom
            ? AppSettingsNormalizer.IsValidCustomLayout(layout)
            : layout is null;

    private static bool IsValidHotkey(GlobalHotkey? hotkey) =>
        hotkey is not null
        && Enum.IsDefined(hotkey.Key)
        && hotkey.Modifiers is not null
        && hotkey.Modifiers.All(Enum.IsDefined);

    private static bool IsValidCorrectionHotkey(GlobalHotkey? hotkey) =>
        IsValidHotkey(hotkey)
        && hotkey!.Modifiers.Count == 1
        && hotkey.Modifiers.Contains(HotkeyModifier.Primary);

    private static SettingsLoadResult CorruptFallback(
        string filePath,
        bool persistFallbackDefaults)
    {
        var defaults = AppSettingsDefaults.Create();
        if (persistFallbackDefaults)
        {
            WriteSettings(filePath, defaults);
        }

        return new SettingsLoadResult(
            defaults,
            SettingsLoadStatus.CorruptFellBackToDefaults);
    }

    private static void WriteSettings(string filePath, AppSettings settings)
    {
        var fullPath = GetFullPath(filePath);
        var directoryPath = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The settings path has no directory.");

        Directory.CreateDirectory(directoryPath);

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        var temporaryPath = Path.Combine(
            directoryPath,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        catch
        {
            TryDeleteTemporaryFile(temporaryPath);
            throw;
        }
    }

    private static string GetFullPath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Path.GetFullPath(filePath);
    }

    private static void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        options.Converters.Add(
            new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));

        return options;
    }
}

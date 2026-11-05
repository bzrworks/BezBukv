using BezBukv.Core.Localization;
using BezBukv.Core.Platform;
using BezBukv.Core.Settings;

namespace BezBukv.Core.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _temporaryDirectory;
    private readonly string _settingsFilePath;
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreTests()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"BezBukv.Core.Tests.{Guid.NewGuid():N}");
        _settingsFilePath = Path.Combine(_temporaryDirectory, "settings.json");
        _store = new JsonSettingsStore(new FakeAppPathsService(_settingsFilePath));
    }

    [Fact]
    public void SaveThenLoadPreservesValuesAndUsesReadableJson()
    {
        var settings = CreateCustomizedSettings();

        _store.Save(settings);
        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        AssertCustomizedValues(result.Settings);

        var json = File.ReadAllText(_settingsFilePath);
        Assert.Contains("\n", json);
        Assert.Contains("\"theme\": \"System\"", json);
        Assert.Contains("\"key\": \"F9\"", json);
        Assert.Contains("\"modifiers\": [", json);
        Assert.Contains("\"keyboardProfile\": \"Iso\"", json);
        Assert.Contains("\"showServiceKeys\": true", json);
    }

    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    public void ExplicitThemeRoundTrips(AppTheme theme)
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            General = defaults.General with
            {
                Theme = theme,
            },
        };

        _store.Save(settings);
        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Equal(theme, result.Settings.General.Theme);
        Assert.Contains(
            $"\"theme\": \"{theme}\"",
            File.ReadAllText(_settingsFilePath));
    }

    [Fact]
    public void RussianLanguageRoundTrips()
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            General = defaults.General with
            {
                Language = AppLanguage.Russian,
            },
        };

        _store.Save(settings);
        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Equal(AppLanguage.Russian, result.Settings.General.Language);
        Assert.Contains(
            "\"language\": \"Russian\"",
            File.ReadAllText(_settingsFilePath));
    }

    [Fact]
    public void MissingFileCreatesAndReturnsDefaults()
    {
        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.CreatedDefault, result.Status);
        Assert.True(File.Exists(_settingsFilePath));
        AssertDefaults(result.Settings);
    }

    [Fact]
    public void CurrentVersionWithMissingFieldsLoadsWithDefaults()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        const string partialJson = """
            {
              "schemaVersion": 4,
              "general": {
                "autostart": true
              },
              "correction": {},
              "peek": {}
            }
            """;
        File.WriteAllText(_settingsFilePath, partialJson);

        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.True(result.Settings.General.Autostart);
        Assert.Equal(AppLanguage.System, result.Settings.General.Language);
        Assert.Equal(ClipboardTextLimit.DefaultMaxBytes, result.Settings.Correction.TextLimitBytes);
        Assert.Equal(300, result.Settings.Correction.ClipboardRestoreDelayMs);
        Assert.False(result.Settings.Peek.AutoPeekEnabled);
        Assert.Equal(1500, result.Settings.Peek.AutoPeekLingerMs);
        Assert.Equal(KeyboardProfile.Unknown, result.Settings.Peek.KeyboardProfile);
        Assert.Empty(result.Settings.Peek.SelectedLayoutKlids);
        Assert.True(result.Settings.Peek.AnimationEnabled);
        Assert.Equal(
            OverlayAppearanceOptions.DefaultAnimationDurationMs,
            result.Settings.Peek.AnimationDurationMs);
        Assert.False(result.Settings.Peek.ShowServiceKeys);

        var correctionHotkey = Assert.IsType<GlobalHotkey>(
            result.Settings.Correction.CorrectionHotkey);
        Assert.Equal(HotkeyKey.F7, correctionHotkey.Key);
        var peekHotkey = Assert.IsType<GlobalHotkey>(
            result.Settings.Peek.ActivationHotkey);
        Assert.Equal(HotkeyKey.F8, peekHotkey.Key);
    }

    [Fact]
    public void ExplicitClipboardRestoreDelayOf120LoadsWithoutMigration()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        const string json = """
            {
              "schemaVersion": 4,
              "correction": {
                "clipboardRestoreDelayMs": 120
              }
            }
            """;
        File.WriteAllText(_settingsFilePath, json);

        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Equal(120, result.Settings.Correction.ClipboardRestoreDelayMs);
        Assert.Equal(json, File.ReadAllText(_settingsFilePath));
    }

    [Fact]
    public void NullCorrectionHotkeyIsValidAndRoundTripsWithoutMigration()
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            Correction = defaults.Correction with
            {
                CorrectionHotkey = null,
            },
        };

        _store.Save(settings);
        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Null(result.Settings.Correction.CorrectionHotkey);
        Assert.Contains(
            "\"correctionHotkey\": null",
            File.ReadAllText(_settingsFilePath));
    }

    [Fact]
    public void NullPeekActivationHotkeyIsValidAndRoundTripsWithoutMigration()
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            Peek = defaults.Peek with
            {
                ActivationHotkey = null,
            },
        };

        _store.Save(settings);
        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Null(result.Settings.Peek.ActivationHotkey);
        Assert.Contains(
            "\"activationHotkey\": null",
            File.ReadAllText(_settingsFilePath));
    }

    [Fact]
    public void ExplicitNullCorrectionHotkeyLoadsAsNull()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        const string json = """
            {
              "schemaVersion": 4,
              "correction": {
                "correctionHotkey": null
              }
            }
            """;
        File.WriteAllText(_settingsFilePath, json);

        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Null(result.Settings.Correction.CorrectionHotkey);
        Assert.Equal(json, File.ReadAllText(_settingsFilePath));
    }

    [Fact]
    public void CorruptFileFallsBackAndPersistsDefaults()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        const string corruptJson = """
            {
              "schemaVersion": 4,
              "peek": {
                "keyboardProfile": "Custom",
                "customKeyboardLayout": {
                  "rows": [
                    { "cells": [ { "key": { "scanCode": 30 } } ] }
                  ]
                }
              },
              definitely-not-json
            }
            """;
        File.WriteAllText(_settingsFilePath, corruptJson);

        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.CorruptFellBackToDefaults, result.Status);
        AssertDefaults(result.Settings);

        var persistedJson = File.ReadAllText(_settingsFilePath);
        Assert.NotEqual(corruptJson, persistedJson);
        Assert.Contains("\"keyboardProfile\": \"Unknown\"", persistedJson);
        Assert.Contains("\"customKeyboardLayout\": null", persistedJson);
        Assert.DoesNotContain("\"scanCode\": 30", persistedJson);

        var secondLoad = _store.Load();
        Assert.Equal(SettingsLoadStatus.Loaded, secondLoad.Status);
        AssertDefaults(secondLoad.Settings);
    }

    [Fact]
    public void CorruptImportDoesNotOverwriteSourceOrMainSettings()
    {
        var mainSettings = CreateCustomizedSettings();
        _store.Save(mainSettings);
        var mainJson = File.ReadAllText(_settingsFilePath);

        var sourcePath = Path.Combine(
            _temporaryDirectory,
            "imports",
            "corrupt-settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        const string corruptJson = "{ definitely-not-json";
        File.WriteAllText(sourcePath, corruptJson);

        var result = _store.Import(sourcePath);

        Assert.Equal(SettingsLoadStatus.CorruptFellBackToDefaults, result.Status);
        AssertDefaults(result.Settings);
        Assert.Equal(corruptJson, File.ReadAllText(sourcePath));
        Assert.Equal(mainJson, File.ReadAllText(_settingsFilePath));

        var mainResult = _store.Load();
        Assert.Equal(SettingsLoadStatus.Loaded, mainResult.Status);
        AssertCustomizedValues(mainResult.Settings);
    }

    [Fact]
    public void MissingImportDoesNotCreateSource()
    {
        var sourcePath = Path.Combine(
            _temporaryDirectory,
            "imports",
            "missing-settings.json");

        var result = _store.Import(sourcePath);

        Assert.Equal(SettingsLoadStatus.CreatedDefault, result.Status);
        AssertDefaults(result.Settings);
        Assert.False(File.Exists(sourcePath));
    }

    [Fact]
    public void OlderVersionMigratesUpPreservingValuesAndAddingDefaults()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        const string oldJson = """
            {
              "schemaVersion": 0,
              "general": {
                "autostart": true,
                "language": "System"
              },
              "correction": {
                "correctionEnabled": false,
                "copyTimeoutMs": 875
              },
              "peek": {
                "blockPunctuation": true
              }
            }
            """;
        File.WriteAllText(_settingsFilePath, oldJson);

        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.MigratedUp, result.Status);
        Assert.Equal(AppSettings.CurrentSchemaVersion, result.Settings.SchemaVersion);
        Assert.True(result.Settings.General.Autostart);
        Assert.Equal(AppLanguage.System, result.Settings.General.Language);
        Assert.False(result.Settings.Correction.CorrectionEnabled);
        Assert.Equal(875, result.Settings.Correction.CopyTimeoutMs);
        Assert.Equal(ClipboardTextLimit.DefaultMaxBytes, result.Settings.Correction.TextLimitBytes);
        Assert.True(result.Settings.Peek.AnimationEnabled);
        Assert.False(result.Settings.Peek.ShowServiceKeys);

        var migratedJson = File.ReadAllText(_settingsFilePath);
        Assert.Contains("\"schemaVersion\": 4", migratedJson);
        Assert.Contains("\"copyTimeoutMs\": 875", migratedJson);
        Assert.Contains("\"keyboardProfile\": \"Unknown\"", migratedJson);
        Assert.Contains("\"showServiceKeys\": false", migratedJson);
        Assert.DoesNotContain("blockPunctuation", migratedJson);
    }

    [Fact]
    public void VersionOneMigratesToCurrentVersionPreservingHotkeys()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        const string oldJson = """
            {
              "schemaVersion": 1,
              "correction": {
                "correctionHotkey": {
                  "key": "F9",
                  "modifiers": [ "Primary" ]
                }
              },
              "peek": {
                "activationHotkey": {
                  "key": "F10",
                  "modifiers": [ "Shift" ]
                }
              }
            }
            """;
        File.WriteAllText(_settingsFilePath, oldJson);

        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.MigratedUp, result.Status);
        Assert.Equal(AppSettings.CurrentSchemaVersion, result.Settings.SchemaVersion);

        var correctionHotkey = Assert.IsType<GlobalHotkey>(
            result.Settings.Correction.CorrectionHotkey);
        Assert.Equal(HotkeyKey.F9, correctionHotkey.Key);
        Assert.Equal(new[] { HotkeyModifier.Primary }, correctionHotkey.Modifiers);

        var peekHotkey = Assert.IsType<GlobalHotkey>(
            result.Settings.Peek.ActivationHotkey);
        Assert.Equal(HotkeyKey.F10, peekHotkey.Key);
        Assert.Equal(new[] { HotkeyModifier.Shift }, peekHotkey.Modifiers);

        var migratedJson = File.ReadAllText(_settingsFilePath);
        Assert.Contains("\"schemaVersion\": 4", migratedJson);
        Assert.Contains("\"key\": \"F9\"", migratedJson);
        Assert.Contains("\"key\": \"F10\"", migratedJson);

        var secondLoad = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, secondLoad.Status);
    }

    [Fact]
    public void VersionTwoWithoutKeyboardProfileMigratesToUnknown()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        const string oldJson = """
            {
              "schemaVersion": 2,
              "general": {
                "autostart": true
              },
              "peek": {
                "peekEnabled": false
              }
            }
            """;
        File.WriteAllText(_settingsFilePath, oldJson);

        var firstLoad = _store.Load();

        Assert.Equal(SettingsLoadStatus.MigratedUp, firstLoad.Status);
        Assert.Equal(AppSettings.CurrentSchemaVersion, firstLoad.Settings.SchemaVersion);
        Assert.Equal(KeyboardProfile.Unknown, firstLoad.Settings.Peek.KeyboardProfile);
        Assert.True(firstLoad.Settings.General.Autostart);
        Assert.False(firstLoad.Settings.Peek.PeekEnabled);

        var migratedJson = File.ReadAllText(_settingsFilePath);
        Assert.Contains("\"schemaVersion\": 4", migratedJson);
        Assert.Contains("\"keyboardProfile\": \"Unknown\"", migratedJson);

        var secondLoad = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, secondLoad.Status);
        Assert.Equal(KeyboardProfile.Unknown, secondLoad.Settings.Peek.KeyboardProfile);
    }

    [Fact]
    public void NewPeekSettingsDefaultsKeyboardProfileToUnknown()
    {
        var settings = new PeekSettings();

        Assert.Equal(KeyboardProfile.Unknown, settings.KeyboardProfile);
    }

    [Theory]
    [InlineData(0, 300)]
    [InlineData(500, 0)]
    public void SaveRejectsZeroForPositiveOnlyCorrectionTimings(
        int copyTimeoutMs,
        int clipboardRestoreDelayMs)
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            Correction = defaults.Correction with
            {
                CopyTimeoutMs = copyTimeoutMs,
                ClipboardRestoreDelayMs = clipboardRestoreDelayMs,
            },
        };

        Assert.Throws<ArgumentException>(() => _store.Save(settings));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SaveRejectsNonPositiveAutoPeekLingerMs(int autoPeekLingerMs)
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            Peek = defaults.Peek with
            {
                AutoPeekLingerMs = autoPeekLingerMs,
            },
        };

        Assert.Throws<ArgumentException>(() => _store.Save(settings));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(9)]
    public void SaveRejectsAutoExitTimeoutBelowMinimum(int autoExitTimeoutSeconds)
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            Peek = defaults.Peek with
            {
                AutoExitTimeoutSeconds = autoExitTimeoutSeconds,
            },
        };

        Assert.Throws<ArgumentException>(() => _store.Save(settings));
    }

    [Fact]
    public void SaveAcceptsMinimumAutoExitTimeout()
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            Peek = defaults.Peek with
            {
                AutoExitTimeoutSeconds = PeekSettings.MinimumAutoExitTimeoutSeconds,
            },
        };

        _store.Save(settings);
        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Equal(
            PeekSettings.MinimumAutoExitTimeoutSeconds,
            result.Settings.Peek.AutoExitTimeoutSeconds);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(9)]
    public void AutoExitTimeoutBelowMinimumInFileClampsWithoutResettingOtherSettings(
        int autoExitTimeoutSeconds)
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var json = $$"""
            {
              "schemaVersion": 3,
              "general": {
                "autostart": true
              },
              "peek": {
                "peekEnabled": false,
                "autoExitTimeoutSeconds": {{autoExitTimeoutSeconds}}
              }
            }
            """;
        File.WriteAllText(_settingsFilePath, json);

        var firstLoad = _store.Load();

        Assert.Equal(SettingsLoadStatus.MigratedUp, firstLoad.Status);
        Assert.True(firstLoad.Settings.General.Autostart);
        Assert.False(firstLoad.Settings.Peek.PeekEnabled);
        Assert.Equal(
            PeekSettings.MinimumAutoExitTimeoutSeconds,
            firstLoad.Settings.Peek.AutoExitTimeoutSeconds);

        var migratedJson = File.ReadAllText(_settingsFilePath);
        Assert.Contains(
            $"\"autoExitTimeoutSeconds\": {PeekSettings.MinimumAutoExitTimeoutSeconds}",
            migratedJson);

        var secondLoad = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, secondLoad.Status);
        Assert.Equal(migratedJson, File.ReadAllText(_settingsFilePath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SaveRejectsNonPositiveAnimationDurationMs(int animationDurationMs)
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            Peek = defaults.Peek with
            {
                AnimationDurationMs = animationDurationMs,
            },
        };

        Assert.Throws<ArgumentException>(() => _store.Save(settings));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveAnimationDurationInFileFallsBackAndPersistsDefaults(
        int animationDurationMs)
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var json = $$"""
            {
              "schemaVersion": 3,
              "peek": {
                "animationDurationMs": {{animationDurationMs}}
              }
            }
            """;
        File.WriteAllText(_settingsFilePath, json);

        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.CorruptFellBackToDefaults, result.Status);
        AssertDefaults(result.Settings);

        var persistedJson = File.ReadAllText(_settingsFilePath);
        Assert.NotEqual(json, persistedJson);

        var secondLoad = _store.Load();
        Assert.Equal(SettingsLoadStatus.Loaded, secondLoad.Status);
        AssertDefaults(secondLoad.Settings);
    }

    [Theory]
    [InlineData(0, 225, 500, 225)]
    [InlineData(725, 0, 725, 300)]
    [InlineData(-1, -2, 500, 300)]
    public void NonPositiveCorrectionTimingsMigrateWithoutResettingOtherSettings(
        int copyTimeoutMs,
        int clipboardRestoreDelayMs,
        int expectedCopyTimeoutMs,
        int expectedClipboardRestoreDelayMs)
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var json = $$"""
            {
              "schemaVersion": 3,
              "general": {
                "autostart": true,
                "language": "System",
                "layoutAutoSwitchEnabled": false
              },
              "correction": {
                "correctionEnabled": false,
                "correctionHotkey": {
                  "key": "F9",
                  "modifiers": [ "Primary" ]
                },
                "direction": "EnglishToRussian",
                "textLimitBytes": 32768,
                "copyTimeoutMs": {{copyTimeoutMs}},
                "pasteIntoUnknown": true,
                "clipboardRestoreDelayMs": {{clipboardRestoreDelayMs}}
              },
              "peek": {
                "peekEnabled": false,
                "autoExitTimeoutSeconds": 45,
                "opacity": 0.75,
                "scale": 1.25,
                "animationEnabled": false,
                "blockNumberRow": false,
                "showServiceKeys": true
              }
            }
            """;
        File.WriteAllText(_settingsFilePath, json);

        var firstLoad = _store.Load();

        Assert.Equal(SettingsLoadStatus.MigratedUp, firstLoad.Status);
        Assert.Equal(expectedCopyTimeoutMs, firstLoad.Settings.Correction.CopyTimeoutMs);
        Assert.Equal(
            expectedClipboardRestoreDelayMs,
            firstLoad.Settings.Correction.ClipboardRestoreDelayMs);
        Assert.True(firstLoad.Settings.General.Autostart);
        Assert.Equal(AppLanguage.System, firstLoad.Settings.General.Language);
        Assert.False(firstLoad.Settings.General.LayoutAutoSwitchEnabled);
        Assert.False(firstLoad.Settings.Correction.CorrectionEnabled);
        var correctionHotkey = Assert.IsType<GlobalHotkey>(
            firstLoad.Settings.Correction.CorrectionHotkey);
        Assert.Equal(HotkeyKey.F9, correctionHotkey.Key);
        Assert.Equal(
            new[] { HotkeyModifier.Primary },
            correctionHotkey.Modifiers);
        Assert.Equal(
            CorrectionDirection.EnglishToRussian,
            firstLoad.Settings.Correction.Direction);
        Assert.Equal(32_768, firstLoad.Settings.Correction.TextLimitBytes);
        Assert.True(firstLoad.Settings.Correction.PasteIntoUnknown);
        Assert.False(firstLoad.Settings.Peek.PeekEnabled);
        Assert.Equal(45, firstLoad.Settings.Peek.AutoExitTimeoutSeconds);
        Assert.Equal(0.75, firstLoad.Settings.Peek.Opacity);
        Assert.Equal(1.25, firstLoad.Settings.Peek.Scale);
        Assert.False(firstLoad.Settings.Peek.AnimationEnabled);
        Assert.False(firstLoad.Settings.Peek.BlockNumberRow);
        Assert.True(firstLoad.Settings.Peek.ShowServiceKeys);

        var migratedJson = File.ReadAllText(_settingsFilePath);
        Assert.Contains($"\"copyTimeoutMs\": {expectedCopyTimeoutMs}", migratedJson);
        Assert.Contains(
            $"\"clipboardRestoreDelayMs\": {expectedClipboardRestoreDelayMs}",
            migratedJson);

        var secondLoad = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, secondLoad.Status);
        Assert.Equal(expectedCopyTimeoutMs, secondLoad.Settings.Correction.CopyTimeoutMs);
        Assert.Equal(
            expectedClipboardRestoreDelayMs,
            secondLoad.Settings.Correction.ClipboardRestoreDelayMs);
        Assert.Equal(migratedJson, File.ReadAllText(_settingsFilePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\"Control\"")]
    [InlineData("\"Alt\"")]
    [InlineData("\"Primary\", \"Shift\"")]
    [InlineData("\"Primary\", \"Alt\"")]
    [InlineData("\"Primary\", \"Meta\"")]
    public void InvalidCorrectionHotkeyModifiersMigrateToDefaultWithoutResettingOtherSettings(
        string modifiersJson)
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var json = $$"""
            {
              "schemaVersion": 3,
              "general": {
                "autostart": true
              },
              "correction": {
                "correctionEnabled": false,
                "correctionHotkey": {
                  "key": "F9",
                  "modifiers": [ {{modifiersJson}} ]
                },
                "copyTimeoutMs": 875
              },
              "peek": {
                "peekEnabled": false
              }
            }
            """;
        File.WriteAllText(_settingsFilePath, json);

        var firstLoad = _store.Load();

        Assert.Equal(SettingsLoadStatus.MigratedUp, firstLoad.Status);
        var correctionHotkey = Assert.IsType<GlobalHotkey>(
            firstLoad.Settings.Correction.CorrectionHotkey);
        Assert.Equal(HotkeyKey.F7, correctionHotkey.Key);
        Assert.Equal(
            new[] { HotkeyModifier.Primary },
            correctionHotkey.Modifiers);
        Assert.True(firstLoad.Settings.General.Autostart);
        Assert.False(firstLoad.Settings.Correction.CorrectionEnabled);
        Assert.Equal(875, firstLoad.Settings.Correction.CopyTimeoutMs);
        Assert.False(firstLoad.Settings.Peek.PeekEnabled);

        var migratedJson = File.ReadAllText(_settingsFilePath);
        Assert.Contains("\"key\": \"F7\"", migratedJson);
        Assert.Contains("\"Primary\"", migratedJson);

        var secondLoad = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, secondLoad.Status);
        Assert.Equal(migratedJson, File.ReadAllText(_settingsFilePath));
    }

    [Fact]
    public void NewerVersionReturnsDefaultsWithoutOverwritingSource()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        const string newerJson = """
            {
              "schemaVersion": 5,
              "general": {
                "theme": "FutureTheme"
              },
              "futureProperty": "must remain"
            }
            """;
        File.WriteAllText(_settingsFilePath, newerJson);

        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.NewerVersionIgnored, result.Status);
        AssertDefaults(result.Settings);
        Assert.Equal(newerJson, File.ReadAllText(_settingsFilePath));
    }

    [Fact]
    public void ExportImportAndResetRoundTripThroughMainFile()
    {
        var settings = CreateCustomizedSettings();
        var exportPath = Path.Combine(_temporaryDirectory, "exports", "portable-settings.json");

        _store.Export(settings, exportPath);
        _store.Reset();
        var importResult = _store.Import(exportPath);
        var loadedAfterImport = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, importResult.Status);
        AssertCustomizedValues(importResult.Settings);
        Assert.Equal(SettingsLoadStatus.Loaded, loadedAfterImport.Status);
        AssertCustomizedValues(loadedAfterImport.Settings);

        var resetSettings = _store.Reset();
        var loadedAfterReset = _store.Load();

        AssertDefaults(resetSettings);
        AssertDefaults(loadedAfterReset.Settings);
    }

    [Fact]
    public void SaveFailureIsNotSilentlySwallowed()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var blockingFilePath = Path.Combine(_temporaryDirectory, "not-a-directory");
        File.WriteAllText(blockingFilePath, "content");
        var store = new JsonSettingsStore(
            new FakeAppPathsService(Path.Combine(blockingFilePath, "settings.json")));

        Assert.ThrowsAny<IOException>(() => store.Save(AppSettingsDefaults.Create()));
    }

    [Fact]
    public void VersionThreeMigratesToVersionFourPreservingProfile()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        const string oldJson = """
            {
              "schemaVersion": 3,
              "general": {
                "autostart": true
              },
              "peek": {
                "keyboardProfile": "Iso"
              }
            }
            """;
        File.WriteAllText(_settingsFilePath, oldJson);

        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.MigratedUp, result.Status);
        Assert.Equal(AppSettings.CurrentSchemaVersion, result.Settings.SchemaVersion);
        Assert.Equal(KeyboardProfile.Iso, result.Settings.Peek.KeyboardProfile);
        Assert.Null(result.Settings.Peek.CustomKeyboardLayout);
        Assert.Empty(result.Settings.Peek.SelectedLayoutKlids);
        Assert.True(result.Settings.General.Autostart);

        var migratedJson = File.ReadAllText(_settingsFilePath);
        Assert.Contains("\"schemaVersion\": 4", migratedJson);
        Assert.Contains("\"keyboardProfile\": \"Iso\"", migratedJson);
        Assert.Contains("\"customKeyboardLayout\": null", migratedJson);
        Assert.Contains("\"selectedLayoutKlids\": []", migratedJson);

        var secondLoad = _store.Load();
        Assert.Equal(SettingsLoadStatus.Loaded, secondLoad.Status);
    }

    [Fact]
    public void CustomProfileWithoutLayoutNormalizesToUnknownOnLoad()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        const string json = """
            {
              "schemaVersion": 4,
              "peek": {
                "keyboardProfile": "Custom"
              }
            }
            """;
        File.WriteAllText(_settingsFilePath, json);

        var firstLoad = _store.Load();

        Assert.Equal(SettingsLoadStatus.MigratedUp, firstLoad.Status);
        Assert.Equal(KeyboardProfile.Unknown, firstLoad.Settings.Peek.KeyboardProfile);

        var migratedJson = File.ReadAllText(_settingsFilePath);
        Assert.Contains("\"keyboardProfile\": \"Unknown\"", migratedJson);

        var secondLoad = _store.Load();
        Assert.Equal(SettingsLoadStatus.Loaded, secondLoad.Status);
        Assert.Equal(KeyboardProfile.Unknown, secondLoad.Settings.Peek.KeyboardProfile);
    }

    [Fact]
    public void CustomProfileWithLayoutHavingNoKeysNormalizesToUnknownOnLoad()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        const string json = """
            {
              "schemaVersion": 4,
              "peek": {
                "keyboardProfile": "Custom",
                "customKeyboardLayout": {
                  "rows": [
                    { "cells": [ { "widthUnits": 2 }, { "widthUnits": 1 } ] }
                  ]
                }
              }
            }
            """;
        File.WriteAllText(_settingsFilePath, json);

        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.MigratedUp, result.Status);
        Assert.Equal(KeyboardProfile.Unknown, result.Settings.Peek.KeyboardProfile);
        Assert.Null(result.Settings.Peek.CustomKeyboardLayout);
    }

    [Fact]
    public void CustomProfileWithValidLayoutRoundTripsAsCustom()
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            Peek = defaults.Peek with
            {
                KeyboardProfile = KeyboardProfile.Custom,
                CustomKeyboardLayout = CreateSampleCustomLayout(),
            },
        };

        _store.Save(settings);
        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Equal(KeyboardProfile.Custom, result.Settings.Peek.KeyboardProfile);

        var layout = result.Settings.Peek.CustomKeyboardLayout;
        Assert.NotNull(layout);
        Assert.Equal(2, layout.Rows.Count);

        var firstRow = layout.Rows[0];
        Assert.Equal(2, firstRow.Cells.Count);
        Assert.Equal(0x10, firstRow.Cells[0].Key!.ScanCode);
        Assert.False(firstRow.Cells[0].Key!.Extended);
        Assert.Null(firstRow.Cells[0].Key!.VirtualKey);
        Assert.Equal(1, firstRow.Cells[0].WidthUnits);
        Assert.Equal(2, firstRow.Cells[1].WidthUnits);
        Assert.True(firstRow.Cells[1].Key!.Extended);
        Assert.Equal(0x20, firstRow.Cells[1].Key!.VirtualKey);

        var secondRow = layout.Rows[1];
        Assert.Null(secondRow.Cells[0].Key);
        Assert.Equal(1, secondRow.Cells[0].WidthUnits);
        var letterKey = secondRow.Cells[1].Key!;
        Assert.Equal(0x1E, letterKey.ScanCode);

        var json = File.ReadAllText(_settingsFilePath);
        Assert.DoesNotContain("latinBaseSymbol", json);
        Assert.DoesNotContain("latinShiftSymbol", json);
        Assert.DoesNotContain("cyrillicBaseSymbol", json);
        Assert.DoesNotContain("cyrillicShiftSymbol", json);
    }

    [Fact]
    public void TransientAltGrMarkerIsNotPersistedInCustomLayout()
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            Peek = defaults.Peek with
            {
                KeyboardProfile = KeyboardProfile.Custom,
                CustomKeyboardLayout = new CustomKeyboardLayout
                {
                    Rows =
                    [
                        new CustomKeyboardRow
                        {
                            Cells =
                            [
                                new CustomKeyboardCell
                                {
                                    Key = new CustomKeyboardKey
                                    {
                                        ScanCode = 0x38,
                                        Extended = true,
                                        VirtualKey = 0xA5,
                                        IsAltGr = true,
                                    },
                                },
                            ],
                        },
                    ],
                },
            },
        };

        _store.Save(settings);
        var result = _store.Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        var loadedKey = Assert.Single(
            Assert.Single(result.Settings.Peek.CustomKeyboardLayout!.Rows).Cells).Key;
        Assert.NotNull(loadedKey);
        Assert.False(loadedKey.IsAltGr);
        Assert.DoesNotContain(
            "isAltGr",
            File.ReadAllText(_settingsFilePath),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaveRejectsCustomProfileWithoutValidLayout()
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            Peek = defaults.Peek with
            {
                KeyboardProfile = KeyboardProfile.Custom,
                CustomKeyboardLayout = null,
            },
        };

        Assert.Throws<ArgumentException>(() => _store.Save(settings));
    }

    [Fact]
    public void SaveRejectsCustomLayoutForNonCustomProfile()
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            Peek = defaults.Peek with
            {
                KeyboardProfile = KeyboardProfile.Ansi,
                CustomKeyboardLayout = CreateSampleCustomLayout(),
            },
        };

        Assert.Throws<ArgumentException>(() => _store.Save(settings));
    }

    [Fact]
    public void SaveRejectsCustomProfileWithLayoutHavingOnlySpacers()
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            Peek = defaults.Peek with
            {
                KeyboardProfile = KeyboardProfile.Custom,
                CustomKeyboardLayout = new CustomKeyboardLayout
                {
                    Rows =
                    [
                        new CustomKeyboardRow
                        {
                            Cells = [new CustomKeyboardCell { WidthUnits = 2 }],
                        },
                    ],
                },
            },
        };

        Assert.Throws<ArgumentException>(() => _store.Save(settings));
    }

    [Fact]
    public void SaveRejectsCustomProfileWithNonPositiveCellWidth()
    {
        var defaults = AppSettingsDefaults.Create();

        foreach (var cell in new[]
                 {
                     new CustomKeyboardCell
                     {
                         WidthUnits = 0,
                         Key = new CustomKeyboardKey { ScanCode = 0x10 },
                     },
                     new CustomKeyboardCell { WidthUnits = 0 },
                 })
        {
            var settings = defaults with
            {
                Peek = defaults.Peek with
                {
                    KeyboardProfile = KeyboardProfile.Custom,
                    CustomKeyboardLayout = new CustomKeyboardLayout
                    {
                        Rows =
                        [
                            new CustomKeyboardRow
                            {
                                Cells =
                                [
                                    cell,
                                    new CustomKeyboardCell
                                    {
                                        Key = new CustomKeyboardKey { ScanCode = 0x11 },
                                    },
                                ],
                            },
                        ],
                    },
                },
            };

            Assert.Throws<ArgumentException>(() => _store.Save(settings));
        }
    }

    [Fact]
    public void SelectedLayoutKlidsWithZeroOneOrTwoEntriesRoundTripInOrder()
    {
        IReadOnlyList<string>[] validLayoutSelections =
        [
            Array.Empty<string>(),
            new[] { "00000409" },
            new[] { "00000419", "00000409" },
        ];

        foreach (var selectedLayoutKlids in validLayoutSelections)
        {
            var defaults = AppSettingsDefaults.Create();
            var settings = defaults with
            {
                Peek = defaults.Peek with
                {
                    SelectedLayoutKlids = selectedLayoutKlids,
                },
            };

            _store.Save(settings);
            var result = _store.Load();

            Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
            Assert.Equal(selectedLayoutKlids, result.Settings.Peek.SelectedLayoutKlids);
        }
    }

    [Fact]
    public void SaveRejectsMoreThanTwoSelectedLayoutKlids()
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            Peek = defaults.Peek with
            {
                SelectedLayoutKlids = ["00000409", "00000419", "00000809"],
            },
        };

        Assert.Throws<ArgumentException>(() => _store.Save(settings));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SaveRejectsEmptySelectedLayoutKlid(string layoutKlid)
    {
        var defaults = AppSettingsDefaults.Create();
        var settings = defaults with
        {
            Peek = defaults.Peek with
            {
                SelectedLayoutKlids = [layoutKlid],
            },
        };

        Assert.Throws<ArgumentException>(() => _store.Save(settings));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private static CustomKeyboardLayout CreateSampleCustomLayout() =>
        new()
        {
            Rows =
            [
                new CustomKeyboardRow
                {
                    Cells =
                    [
                        new CustomKeyboardCell
                        {
                            Key = new CustomKeyboardKey { ScanCode = 0x10 },
                        },
                        new CustomKeyboardCell
                        {
                            WidthUnits = 2,
                            Key = new CustomKeyboardKey
                            {
                                ScanCode = 0x39,
                                Extended = true,
                                VirtualKey = 0x20,
                            },
                        },
                    ],
                },
                new CustomKeyboardRow
                {
                    Cells =
                    [
                        new CustomKeyboardCell { WidthUnits = 1 },
                        new CustomKeyboardCell
                        {
                            Key = new CustomKeyboardKey
                            {
                                ScanCode = 0x1E,
                            },
                        },
                    ],
                },
            ],
        };

    private static AppSettings CreateCustomizedSettings()
    {
        var defaults = AppSettingsDefaults.Create();

        return defaults with
        {
            General = defaults.General with
            {
                Autostart = true,
                Language = AppLanguage.System,
                LayoutAutoSwitchEnabled = false,
            },
            Correction = defaults.Correction with
            {
                CorrectionHotkey = new GlobalHotkey(
                    HotkeyKey.F9,
                    new[] { HotkeyModifier.Primary }),
                Direction = CorrectionDirection.EnglishToRussian,
                TextLimitBytes = 32_768,
                CopyTimeoutMs = 725,
                PasteIntoUnknown = true,
                ClipboardRestoreDelayMs = 225,
            },
            Peek = defaults.Peek with
            {
                KeyboardProfile = KeyboardProfile.Iso,
                AutoPeekEnabled = true,
                AutoPeekLingerMs = 800,
                AutoExitTimeoutSeconds = 45,
                Opacity = 0.75,
                Scale = 1.25,
                AnimationEnabled = false,
                AnimationDurationMs = 275,
                BlockNumberRow = true,
                ShowServiceKeys = true,
            },
        };
    }

    private static void AssertCustomizedValues(AppSettings settings)
    {
        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.True(settings.General.Autostart);
        Assert.Equal(AppLanguage.System, settings.General.Language);
        Assert.False(settings.General.LayoutAutoSwitchEnabled);
        var correctionHotkey = Assert.IsType<GlobalHotkey>(
            settings.Correction.CorrectionHotkey);
        Assert.Equal(HotkeyKey.F9, correctionHotkey.Key);
        Assert.Equal(
            new[] { HotkeyModifier.Primary },
            correctionHotkey.Modifiers);
        Assert.Equal(CorrectionDirection.EnglishToRussian, settings.Correction.Direction);
        Assert.Equal(32_768, settings.Correction.TextLimitBytes);
        Assert.Equal(725, settings.Correction.CopyTimeoutMs);
        Assert.True(settings.Correction.PasteIntoUnknown);
        Assert.Equal(225, settings.Correction.ClipboardRestoreDelayMs);
        Assert.Equal(KeyboardProfile.Iso, settings.Peek.KeyboardProfile);
        Assert.True(settings.Peek.AutoPeekEnabled);
        Assert.Equal(800, settings.Peek.AutoPeekLingerMs);
        Assert.Equal(45, settings.Peek.AutoExitTimeoutSeconds);
        Assert.Equal(0.75, settings.Peek.Opacity);
        Assert.Equal(1.25, settings.Peek.Scale);
        Assert.False(settings.Peek.AnimationEnabled);
        Assert.Equal(275, settings.Peek.AnimationDurationMs);
        Assert.True(settings.Peek.BlockNumberRow);
        Assert.True(settings.Peek.ShowServiceKeys);
    }

    private static void AssertDefaults(AppSettings settings)
    {
        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.False(settings.General.Autostart);
        Assert.Equal(AppTheme.System, settings.General.Theme);
        Assert.Equal(AppLanguage.System, settings.General.Language);
        Assert.True(settings.General.LayoutAutoSwitchEnabled);
        Assert.True(settings.Correction.CorrectionEnabled);
        var correctionHotkey = Assert.IsType<GlobalHotkey>(
            settings.Correction.CorrectionHotkey);
        Assert.Equal(HotkeyKey.F7, correctionHotkey.Key);
        Assert.Equal(
            new[] { HotkeyModifier.Primary },
            correctionHotkey.Modifiers);
        Assert.Equal(CorrectionDirection.Auto, settings.Correction.Direction);
        Assert.Equal(ClipboardTextLimit.DefaultMaxBytes, settings.Correction.TextLimitBytes);
        Assert.Equal(500, settings.Correction.CopyTimeoutMs);
        Assert.False(settings.Correction.PasteIntoUnknown);
        Assert.Equal(300, settings.Correction.ClipboardRestoreDelayMs);
        Assert.True(settings.Peek.PeekEnabled);
        var peekHotkey = Assert.IsType<GlobalHotkey>(settings.Peek.ActivationHotkey);
        Assert.Equal(HotkeyKey.F8, peekHotkey.Key);
        Assert.Equal(
            new[] { HotkeyModifier.Primary },
            peekHotkey.Modifiers);
        Assert.False(settings.Peek.AutoPeekEnabled);
        Assert.Equal(KeyboardProfile.Unknown, settings.Peek.KeyboardProfile);
        Assert.Empty(settings.Peek.SelectedLayoutKlids);
        Assert.Equal(1500, settings.Peek.AutoPeekLingerMs);
        Assert.Equal(120, settings.Peek.AutoExitTimeoutSeconds);
        Assert.Equal(0.80, settings.Peek.Opacity);
        Assert.Equal(1.0, settings.Peek.Scale);
        Assert.True(settings.Peek.AnimationEnabled);
        Assert.Equal(
            OverlayAppearanceOptions.DefaultAnimationDurationMs,
            settings.Peek.AnimationDurationMs);
        Assert.True(settings.Peek.BlockNumberRow);
        Assert.False(settings.Peek.ShowServiceKeys);
    }

    private sealed class FakeAppPathsService(string settingsFilePath) : IAppPathsService
    {
        public string SettingsFilePath { get; } = settingsFilePath;

        public string DiagnosticsLogFilePath { get; } = Path.Combine(
            Path.GetDirectoryName(settingsFilePath) ?? string.Empty,
            "diagnostics.log.jsonl");
    }
}

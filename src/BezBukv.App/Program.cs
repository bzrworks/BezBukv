using System.Globalization;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using BezBukv.App.Resources;
using BezBukv.Core.Keyboard;
using BezBukv.Core.Localization;
using BezBukv.Core.Platform;
using BezBukv.Core.Resources;
using BezBukv.Core.Scenarios;
using BezBukv.Core.Settings;
using BezBukv.Windows;

namespace BezBukv.App;

internal static class Program
{
    private const string BrandAccentLightColorKey = "SettingsBrandAccentLightColor";
    private const string BrandAccentDarkColorKey = "SettingsBrandAccentDarkColor";

    private static readonly object[] FluentSystemAccentResourceKeys =
    [
        SystemColors.AccentColorKey,
        SystemColors.AccentColorLight1Key,
        SystemColors.AccentColorLight2Key,
        SystemColors.AccentColorLight3Key,
        SystemColors.AccentColorDark1Key,
        SystemColors.AccentColorDark2Key,
        SystemColors.AccentColorDark3Key,
    ];

    private static readonly string[] FluentSystemAccentColorResourceKeys =
    [
        "SystemAccentColor",
        "SystemAccentColorLight1",
        "SystemAccentColorLight2",
        "SystemAccentColorLight3",
        "SystemAccentColorDark1",
        "SystemAccentColorDark2",
        "SystemAccentColorDark3",
        "SystemAccentColorSecondary",
    ];

    private static readonly (string Key, double Opacity)[] FluentAccentBrushResources =
    [
        ("AccentFillColorSelectedTextBackgroundBrush", 1d),
        ("AccentFillColorDefaultBrush", 1d),
        ("AccentFillColorSecondaryBrush", 0.9d),
        ("AccentFillColorTertiaryBrush", 0.8d),
        ("AccentFillColorDisabledBrush", 0.4d),
        ("AccentTextFillColorPrimaryBrush", 1d),
        ("AccentTextFillColorSecondaryBrush", 0.9d),
        ("AccentTextFillColorTertiaryBrush", 0.8d),
        ("AccentTextFillColorDisabledBrush", 0.4d),
        ("SystemFillColorAttentionBrush", 1d),
        ("SliderThumbBackground", 1d),
        ("SliderThumbBackgroundPointerOver", 0.9d),
    ];

    [STAThread]
    private static void Main(string[] args)
    {
        var diagnosticsEnabled = Array.Exists(
            args,
            argument => string.Equals(
                argument,
                "--diagnostics",
                StringComparison.OrdinalIgnoreCase));
        using var singleInstanceMutex = diagnosticsEnabled
            ? null
            : new Mutex(
                initiallyOwned: false,
                name: @"Local\BezBukv.App.SingleInstance");
        var ownsSingleInstanceMutex = false;

        if (singleInstanceMutex is not null)
        {
            try
            {
                ownsSingleInstanceMutex = singleInstanceMutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                ownsSingleInstanceMutex = true;
            }

            if (!ownsSingleInstanceMutex)
            {
                return;
            }
        }

        try
        {
            var diagnosticHotkey = new GlobalHotkey(
                HotkeyKey.F7,
                [HotkeyModifier.Primary, HotkeyModifier.Shift]);
            var application = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown,
            };
            LoadBrandAccentResources(application);

            ILanguageService languageService = new LanguageService();

            var appPathsService = new WindowsAppPathsService();
            IDiagnosticLogger diagnosticLogger = diagnosticsEnabled
                ? new WindowsJsonLinesDiagnosticLogger(appPathsService.DiagnosticsLogFilePath)
                : NoOpDiagnosticLogger.Instance;
            var settingsStore = new JsonSettingsStore(appPathsService);
            IAutostartService autostartService =
                WindowsPackageIdentity.IsCurrentProcessPackaged()
                    ? new PackagedStartupTaskAutostartService()
                    : new WindowsAutostartService();
            AppSettings currentSettings;
            string? settingsLoadWarning;

            try
            {
                var settingsLoadResult = settingsStore.Load();
                currentSettings = AppSettingsNormalizer.NormalizeCustomProfile(
                    settingsLoadResult.Settings);
                currentSettings = DisableAutoPeekForUnknownKeyboardProfile(currentSettings);
                if (currentSettings != settingsLoadResult.Settings)
                {
                    settingsStore.Save(currentSettings);
                }

                languageService.Set(currentSettings.General.Language);
                settingsLoadWarning = settingsLoadResult.Status switch
                {
                    SettingsLoadStatus.NewerVersionIgnored =>
                        AppStrings.Settings_Load_NewerVersion,
                    SettingsLoadStatus.CorruptFellBackToDefaults =>
                        AppStrings.Settings_Load_Corrupt,
                    _ => null,
                };
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                currentSettings = AppSettingsDefaults.Create();
                languageService.Set(currentSettings.General.Language);
                settingsLoadWarning = AppStrings.Settings_Load_Failed;
            }

            using ISystemThemeProvider systemThemeProvider = new WindowsSystemThemeProvider();
            systemThemeProvider.SetTheme(currentSettings.General.Theme);
            ApplyBrandAccentResources(application, systemThemeProvider.IsDarkTheme);
            systemThemeProvider.ThemeChanged += OnApplicationThemeChanged;
#pragma warning disable WPF0001
            application.ThemeMode = GetThemeMode(currentSettings.General.Theme);
#pragma warning restore WPF0001
            using var backgroundAppMenuService =
                new WindowsBackgroundAppMenuService(systemThemeProvider);
            var clipboardService = new WindowsClipboardService(
                currentSettings.Correction.TextLimitBytes);
            IInputSimulationService inputSimulationService = new WindowsInputSimulationService();
            var focusedElementService = new WindowsFocusedElementService();
            IActiveApplicationService activeApplicationService =
                new WindowsActiveApplicationService();
            IKeyboardLayoutService keyboardLayoutService =
                new WindowsKeyboardLayoutService();
            IInstalledKeyboardLayoutService installedKeyboardLayoutService =
                new WindowsInstalledKeyboardLayoutService();
            IKeyboardLayoutSymbolService keyboardLayoutSymbolService =
                new WindowsKeyboardLayoutSymbolService();
            var activeWindowFocusService = new WindowsActiveWindowFocusService();
            var overlayGridBuilder = new KeyboardOverlayGridBuilder(
                keyboardLayoutSymbolService);
            var overlayAppearanceOptions = new OverlayAppearanceOptions(
                opacity: currentSettings.Peek.Opacity,
                scale: currentSettings.Peek.Scale,
                animationEnabled: currentSettings.Peek.AnimationEnabled,
                animationDurationMs: currentSettings.Peek.AnimationDurationMs);
            var windowsOverlayService = new WindowsOverlayService(
                overlayGridBuilder,
                focusedElementService,
                activeWindowFocusService,
                overlayAppearanceOptions);
            using IOverlayService overlayService = windowsOverlayService;
            overlayService.UpdateKeyboardProfile(
                currentSettings.Peek.KeyboardProfile,
                currentSettings.Peek.SelectedLayoutKlids,
                currentSettings.Peek.CustomKeyboardLayout);
            var layoutCorrectionScenario = new LayoutCorrectionScenario(
                clipboardService,
                inputSimulationService,
                focusedElementService,
                activeApplicationService,
                timeout: TimeSpan.FromMilliseconds(
                    currentSettings.Correction.CopyTimeoutMs),
                pasteIntoUnknown: currentSettings.Correction.PasteIntoUnknown,
                clipboardRestoreDelay: TimeSpan.FromMilliseconds(
                    currentSettings.Correction.ClipboardRestoreDelayMs),
                keyboardLayoutService: currentSettings.General.LayoutAutoSwitchEnabled
                    ? keyboardLayoutService
                    : null,
                diagnosticLogger: diagnosticLogger);
            using IGlobalHotkeyService globalHotkeyService = new WindowsGlobalHotkeyService();
            using IGlobalHotkeyService focusedElementDiagnosticHotkeyService =
                new WindowsGlobalHotkeyService();
            using var keyboardCaptureService =
                new WindowsKeyboardCaptureService(
                    new DispatcherSynchronizationContext(application.Dispatcher),
                    blockNumberRow: currentSettings.Peek.BlockNumberRow,
                    showServiceKeys: currentSettings.Peek.ShowServiceKeys);
            keyboardCaptureService.UpdateCustomHoldPeekLayout(
                GetCustomHoldPeekLayout(currentSettings.Peek));
            using var keyboardCaptureArbiter =
                new KeyboardCaptureArbiter(
                    keyboardCaptureService,
                    currentSettings.Peek.BlockNumberRow,
                    currentSettings.Peek.ShowServiceKeys);
            using var keyboardPeekModeController =
                new KeyboardPeekModeController(
                    keyboardCaptureArbiter,
                    currentSettings.Peek.ActivationHotkey,
                    autoExitTimeout: TimeSpan.FromSeconds(
                        currentSettings.Peek.AutoExitTimeoutSeconds));
            using var autoPeekController =
                new AutoPeekController(
                    keyboardCaptureService,
                    TimeSpan.FromMilliseconds(currentSettings.Peek.AutoPeekLingerMs),
                    blockNumberRow: currentSettings.Peek.BlockNumberRow,
                    showServiceKeys: currentSettings.Peek.ShowServiceKeys);
            autoPeekController.UpdateKeyboardProfile(
                currentSettings.Peek.KeyboardProfile,
                currentSettings.Peek.CustomKeyboardLayout);
            using IGlobalHotkeyService keyboardPeekModeHotkeyService =
                new WindowsGlobalHotkeyService();
            using var shutdown = new CancellationTokenSource();
            var currentScenarioRun = Task.CompletedTask;
            ResultWindow? currentResultWindow = null;
            NotificationWindow? currentNotificationWindow = null;
            SettingsWindow? currentSettingsWindow = null;
            GuideWindow? currentGuideWindow = null;
            KeyboardProfilePickerWindow? currentKeyboardProfilePickerWindow = null;
            KeyboardCalibrationWizardWindow? currentKeyboardCalibrationWizardWindow = null;
            KeyboardCalibrationController? currentKeyboardCalibrationController = null;
            var pendingKeyboardProfilePickerIntent =
                KeyboardProfilePickerIntent.SelectProfileOnly;
            var keyboardCalibrationPickerIntent =
                KeyboardProfilePickerIntent.SelectProfileOnly;
            IReadOnlyList<string> keyboardCalibrationSelectedLayoutKlids = [];
            var keyboardPeekModeMonitor = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100),
            };
            var scenarioRunInProgress = 0;
            var correctionHotkeyHandlerSubscribed = false;
            GlobalHotkey? registeredCorrectionHotkey = null;
            var peekHotkeyHandlerSubscribed = false;
            GlobalHotkey? registeredPeekHotkey = null;
            var isPaused = false;
            var isHotkeyRecordingCaptureActive = false;
            EventHandler<KeyboardCaptureEventArgs>? hotkeyRecordingCaptureHandler = null;
            var isKeyboardCalibrationCaptureActive = false;
            var keyboardCalibrationStartedCapture = false;
            var peekOverlayOwner = PeekOverlayOwner.None;
            PhysicalKey? peekOverlayKey = null;
            KeyboardScanCodeIdentity? peekOverlayIdentity = null;
            var autoPeekOverlayVersion = 0;

            backgroundAppMenuService.PauseToggleRequested += OnPauseToggleRequested;
            backgroundAppMenuService.AutoPeekToggleRequested += OnAutoPeekToggleRequested;
            backgroundAppMenuService.SettingsRequested += OnSettingsRequested;
            languageService.LanguageChanged += OnLanguageChanged;
            backgroundAppMenuService.ExitRequested += (_, _) =>
            {
                shutdown.Cancel();
                application.Shutdown();
            };
            UpdateTrayMenuLabels();
            UpdateTrayMenuState();

            if (currentSettings.Correction.CorrectionEnabled
                && currentSettings.Correction.CorrectionHotkey is not null)
            {
                globalHotkeyService.HotkeyPressed += OnHotkeyPressed;
                correctionHotkeyHandlerSubscribed = true;
            }

            if (diagnosticsEnabled)
            {
                focusedElementDiagnosticHotkeyService.HotkeyPressed +=
                    OnFocusedElementDiagnosticHotkeyPressed;
            }

            if (currentSettings.Peek.PeekEnabled
                && currentSettings.Peek.ActivationHotkey is not null)
            {
                keyboardPeekModeHotkeyService.HotkeyPressed += OnKeyboardPeekModeHotkeyPressed;
                peekHotkeyHandlerSubscribed = true;
            }

            keyboardCaptureService.KeyChanged += OnKeyboardCaptureKeyChanged;
            keyboardPeekModeMonitor.Tick += OnKeyboardPeekModeMonitorTick;
            autoPeekController.ShowOverlayRequested += OnAutoPeekShowOverlayRequested;
            autoPeekController.ShowCustomOverlayRequested +=
                OnAutoPeekShowCustomOverlayRequested;
            autoPeekController.HideOverlayRequested += OnAutoPeekHideOverlayRequested;

            if (IsAutoPeekEffectivelyEnabled(currentSettings))
            {
                autoPeekController.Enable();
                keyboardCaptureArbiter.SetAutoPeekCaptureRequired(true);
            }

            var hotkeyRegistrationFailures =
                new List<(string FunctionName, GlobalHotkey Hotkey,
                    GlobalHotkeyRegistrationStatus Status)>();
            if (currentSettings.Correction.CorrectionEnabled
                && currentSettings.Correction.CorrectionHotkey is { } correctionHotkey)
            {
                var registrationStatus = globalHotkeyService.Register(correctionHotkey);
                if (registrationStatus == GlobalHotkeyRegistrationStatus.Success)
                {
                    registeredCorrectionHotkey = correctionHotkey;
                }
                else
                {
                    hotkeyRegistrationFailures.Add((
                        AppStrings.Hotkey_CorrectionFunction,
                        correctionHotkey,
                        registrationStatus));
                    DisableCorrectionAfterHotkeyRegistrationFailure();
                }
            }

            if (diagnosticsEnabled)
            {
                _ = focusedElementDiagnosticHotkeyService.Register(diagnosticHotkey);
            }

            if (currentSettings.Peek.PeekEnabled
                && currentSettings.Peek.ActivationHotkey is { } peekHotkey)
            {
                var registrationStatus = keyboardPeekModeHotkeyService.Register(peekHotkey);
                if (registrationStatus == GlobalHotkeyRegistrationStatus.Success)
                {
                    registeredPeekHotkey = peekHotkey;
                }
                else
                {
                    hotkeyRegistrationFailures.Add((
                        AppStrings.Hotkey_PeekFunction,
                        peekHotkey,
                        registrationStatus));
                    DisablePeekAfterHotkeyRegistrationFailure();
                }
            }

            UpdateTrayMenuState();

            if (hotkeyRegistrationFailures.Count == 0)
            {
                ShowNotificationWindow(
                    AppStrings.Notification_Title,
                    AppStrings.Notification_Welcome,
                    false);
            }
            else
            {
                ShowNotificationWindow(
                    AppStrings.Notification_Title,
                    FormatHotkeyRegistrationFailures(hotkeyRegistrationFailures),
                    true);
            }

            if (settingsLoadWarning is not null)
            {
                ShowNotificationWindow(
                    AppStrings.Notification_Title,
                    settingsLoadWarning,
                    true);
            }

            try
            {
                application.Run();
            }
            finally
            {
                systemThemeProvider.ThemeChanged -= OnApplicationThemeChanged;
                StopKeyboardCalibrationSession(closeWindow: true);
                backgroundAppMenuService.PauseToggleRequested -= OnPauseToggleRequested;
                backgroundAppMenuService.AutoPeekToggleRequested -= OnAutoPeekToggleRequested;
                backgroundAppMenuService.SettingsRequested -= OnSettingsRequested;
                languageService.LanguageChanged -= OnLanguageChanged;
                keyboardPeekModeMonitor.Stop();
                keyboardPeekModeMonitor.Tick -= OnKeyboardPeekModeMonitorTick;
                autoPeekController.Disable();
                keyboardCaptureArbiter.SetAutoPeekCaptureRequired(false);
                autoPeekController.ShowOverlayRequested -= OnAutoPeekShowOverlayRequested;
                autoPeekController.ShowCustomOverlayRequested -=
                    OnAutoPeekShowCustomOverlayRequested;
                autoPeekController.HideOverlayRequested -= OnAutoPeekHideOverlayRequested;
                autoPeekController.Dispose();
                keyboardCaptureService.KeyChanged -= OnKeyboardCaptureKeyChanged;
                keyboardPeekModeHotkeyService.HotkeyPressed -= OnKeyboardPeekModeHotkeyPressed;
                keyboardPeekModeController.Dispose();
                keyboardCaptureArbiter.Dispose();
                peekOverlayOwner = PeekOverlayOwner.None;
                peekOverlayKey = null;
                peekOverlayIdentity = null;
                overlayService.Hide();
                keyboardCaptureService.Dispose();
                shutdown.Cancel();
            }

            try
            {
                currentScenarioRun.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
            {
            }

            void OnHotkeyPressed(object? sender, EventArgs eventArgs)
            {
                var diagnosticRun = StartDiagnosticRun();

                if (isPaused)
                {
                    LogDiagnostic(
                        diagnosticRun,
                        "hotkey_pressed",
                        new Dictionary<string, object?>
                        {
                            ["accepted"] = false,
                            ["suppressed"] = true,
                            ["reason"] = "paused",
                        });
                    return;
                }

                if (Interlocked.CompareExchange(ref scenarioRunInProgress, 1, 0) != 0)
                {
                    LogDiagnostic(
                        diagnosticRun,
                        "hotkey_pressed",
                        new Dictionary<string, object?>
                        {
                            ["accepted"] = false,
                            ["suppressed"] = true,
                            ["reason"] = "guard_busy",
                        });
                    return;
                }

                LogDiagnostic(
                    diagnosticRun,
                    "hotkey_pressed",
                    new Dictionary<string, object?>
                    {
                        ["accepted"] = true,
                        ["suppressed"] = false,
                        ["reason"] = "accepted",
                    });

                currentScenarioRun = RunScenarioAndNotifyAsync(diagnosticRun);
            }

            void OnPauseToggleRequested(object? sender, EventArgs eventArgs)
            {
                if (isPaused)
                {
                    ResumeApplication();
                    return;
                }

                PauseApplication();
            }

            void OnAutoPeekToggleRequested(object? sender, EventArgs eventArgs)
            {
                var enableAutoPeek = !currentSettings.Peek.AutoPeekEnabled;
                if (enableAutoPeek
                    && currentSettings.Peek.KeyboardProfile == KeyboardProfile.Unknown)
                {
                    UpdateTrayMenuState();
                    ShowKeyboardProfilePickerWindow(
                        KeyboardProfilePickerIntent.EnableAutoPeek);
                    return;
                }

                var updatedSettings = currentSettings with
                {
                    Peek = currentSettings.Peek with
                    {
                        AutoPeekEnabled = enableAutoPeek,
                    },
                };

                try
                {
                    settingsStore.Save(updatedSettings);
                    OnSettingsChanged(updatedSettings);
                }
                catch (Exception exception) when (
                    IsSettingsOperationException(exception))
                {
                    ShowNotificationWindow(
                        AppStrings.Notification_Title,
                        AppStrings.Settings_Save_Failed,
                        true);
                    UpdateTrayMenuState();
                }
            }

            void PauseApplication()
            {
                if (isPaused)
                {
                    return;
                }

                currentKeyboardCalibrationWizardWindow?.CancelFromCapture();

                isPaused = true;

                DisableCorrectionHotkey();
                if (diagnosticsEnabled)
                {
                    focusedElementDiagnosticHotkeyService.Unregister();
                }

                DisablePeekHotkey();
                DisableAutoPeek();
                _ = keyboardCaptureArbiter.StopAllCapture();
                currentSettingsWindow?.UpdateRuntimeState(
                    isPaused,
                    registeredCorrectionHotkey is not null,
                    registeredPeekHotkey is not null);

                keyboardPeekModeMonitor.Stop();
                Interlocked.Increment(ref autoPeekOverlayVersion);
                peekOverlayOwner = PeekOverlayOwner.None;
                peekOverlayKey = null;
                peekOverlayIdentity = null;
                overlayService.Hide();

                UpdateTrayMenuState();
            }

            void ResumeApplication()
            {
                if (!isPaused)
                {
                    return;
                }

                var hotkeyRegistrationFailures =
                    new List<(string FunctionName, GlobalHotkey Hotkey,
                        GlobalHotkeyRegistrationStatus Status)>();

                if (currentSettings.Correction.CorrectionEnabled
                    && currentSettings.Correction.CorrectionHotkey is { } correctionHotkey)
                {
                    var registrationResult = TryRegisterCorrectionHotkey(correctionHotkey);
                    if (!registrationResult.Succeeded)
                    {
                        hotkeyRegistrationFailures.Add((
                            AppStrings.Hotkey_CorrectionFunction,
                            correctionHotkey,
                            registrationResult.RegistrationStatus));
                        DisableCorrectionAfterHotkeyRegistrationFailure();
                    }
                }
                else
                {
                    DisableCorrectionHotkey();
                }

                if (diagnosticsEnabled)
                {
                    _ = focusedElementDiagnosticHotkeyService.Register(diagnosticHotkey);
                }

                if (currentSettings.Peek.PeekEnabled
                    && currentSettings.Peek.ActivationHotkey is { } peekHotkey)
                {
                    var registrationResult = TryRegisterPeekHotkey(peekHotkey);
                    if (!registrationResult.Succeeded)
                    {
                        hotkeyRegistrationFailures.Add((
                            AppStrings.Hotkey_PeekFunction,
                            peekHotkey,
                            registrationResult.RegistrationStatus));
                        DisablePeekAfterHotkeyRegistrationFailure();
                    }
                }
                else
                {
                    DisablePeekHotkey();
                }

                if (hotkeyRegistrationFailures.Count > 0)
                {
                    ShowNotificationWindow(
                        AppStrings.Notification_Title,
                        FormatHotkeyRegistrationFailures(hotkeyRegistrationFailures),
                        true);
                }

                if (IsAutoPeekEffectivelyEnabled(currentSettings))
                {
                    EnableAutoPeek();
                }
                else
                {
                    DisableAutoPeek();
                }

                isPaused = false;
                currentSettingsWindow?.UpdateRuntimeState(
                    isPaused,
                    registeredCorrectionHotkey is not null,
                    registeredPeekHotkey is not null);
                UpdateTrayMenuState();
            }

            void OnSettingsRequested(object? sender, EventArgs eventArgs)
            {
                _ = application.Dispatcher.BeginInvoke(
                    ShowSettingsWindow,
                    DispatcherPriority.ApplicationIdle);
            }

            void ShowSettingsWindow()
            {
                if (currentSettingsWindow is not null)
                {
                    currentSettingsWindow.ShowAndActivate();
                    return;
                }

                var settingsWindow = new SettingsWindow(
                    settingsStore,
                    currentSettings,
                    isPaused,
                    registeredCorrectionHotkey is not null,
                    registeredPeekHotkey is not null,
                    autostartService,
                    overlayService,
                    systemThemeProvider,
                    languageService,
                    TryRegisterCorrectionHotkey,
                    StartCorrectionHotkeyRecordingCapture,
                    _ => StopCorrectionHotkeyRecordingCapture(),
                    FindCorrectionHotkeyInternalConflict,
                    TryRegisterPeekHotkey,
                    StartCorrectionHotkeyRecordingCapture,
                    _ => StopCorrectionHotkeyRecordingCapture(),
                    FindPeekHotkeyInternalConflict,
                    (message, isWarning) => ShowNotificationWindow(
                        AppStrings.Notification_Title,
                        message,
                        isWarning),
                    () => ShowKeyboardProfilePickerWindow(
                        KeyboardProfilePickerIntent.EnableAutoPeek),
                    () => ShowKeyboardProfilePickerWindow(
                        KeyboardProfilePickerIntent.SelectProfileOnly),
                    RetryCorrectionHotkeyRegistration,
                    RetryPeekHotkeyRegistration,
                    OnSettingsChanged,
                    OnSettingsImported,
                    OnMainFeatureEnableRequestedWhilePaused);
                settingsWindow.GuideWindowRequested += OnGuideWindowRequested;
                settingsWindow.Closed += OnSettingsWindowClosed;
                currentSettingsWindow = settingsWindow;
                settingsWindow.ShowAndActivate();
            }

            void OnGuideWindowRequested(object? sender, EventArgs eventArgs)
            {
                ShowGuideWindow();
            }

            void ShowGuideWindow()
            {
                if (currentGuideWindow is not null)
                {
                    currentGuideWindow.ShowAndActivate();
                    return;
                }

                var guideWindow = new GuideWindow(
                    languageService,
                    systemThemeProvider);
                guideWindow.Closed += OnGuideWindowClosed;
                currentGuideWindow = guideWindow;
                guideWindow.Show();
            }

            void OnGuideWindowClosed(object? sender, EventArgs eventArgs)
            {
                if (sender is GuideWindow closedGuideWindow
                    && ReferenceEquals(currentGuideWindow, closedGuideWindow))
                {
                    closedGuideWindow.Closed -= OnGuideWindowClosed;
                    currentGuideWindow = null;
                }
            }

            KeyboardCaptureStartStatus StartCorrectionHotkeyRecordingCapture(
                EventHandler<KeyboardCaptureEventArgs> handler)
            {
                if (isHotkeyRecordingCaptureActive)
                {
                    return KeyboardCaptureStartStatus.AlreadyCapturing;
                }

                EventHandler<KeyboardCaptureEventArgs> filteredHandler =
                    (sender, eventArgs) =>
                    {
                        if (eventArgs.Key != PhysicalKey.Unknown)
                        {
                            handler(sender, eventArgs);
                        }
                    };
                keyboardCaptureService.KeyChanged += filteredHandler;
                var startStatus = keyboardCaptureArbiter.StartRecordingCapture();
                if (startStatus == KeyboardCaptureStartStatus.PlatformError)
                {
                    keyboardCaptureService.KeyChanged -= filteredHandler;
                    return startStatus;
                }

                hotkeyRecordingCaptureHandler = filteredHandler;
                isHotkeyRecordingCaptureActive = true;
                Interlocked.Increment(ref autoPeekOverlayVersion);
                peekOverlayOwner = PeekOverlayOwner.None;
                peekOverlayKey = null;
                peekOverlayIdentity = null;
                overlayService.Hide();
                return startStatus;
            }

            void StopCorrectionHotkeyRecordingCapture()
            {
                if (hotkeyRecordingCaptureHandler is { } filteredHandler)
                {
                    keyboardCaptureService.KeyChanged -= filteredHandler;
                    hotkeyRecordingCaptureHandler = null;
                }

                if (!isHotkeyRecordingCaptureActive)
                {
                    return;
                }

                isHotkeyRecordingCaptureActive = false;
                _ = keyboardCaptureArbiter.StopRecordingCapture();
            }

            CorrectionHotkeyInternalConflict FindCorrectionHotkeyInternalConflict(
                GlobalHotkey hotkey)
            {
                if (currentSettings.Peek.ActivationHotkey is { } peekHotkey
                    && hotkey.Equals(peekHotkey))
                {
                    return CorrectionHotkeyInternalConflict.Peek;
                }

                return diagnosticsEnabled && hotkey.Equals(diagnosticHotkey)
                    ? CorrectionHotkeyInternalConflict.Diagnostics
                    : CorrectionHotkeyInternalConflict.None;
            }

            PeekHotkeyInternalConflict FindPeekHotkeyInternalConflict(
                GlobalHotkey hotkey)
            {
                if (currentSettings.Correction.CorrectionHotkey is { } correctionHotkey
                    && hotkey.Equals(correctionHotkey))
                {
                    return PeekHotkeyInternalConflict.Correction;
                }

                return diagnosticsEnabled && hotkey.Equals(diagnosticHotkey)
                    ? PeekHotkeyInternalConflict.Diagnostics
                    : PeekHotkeyInternalConflict.None;
            }

            void OnSettingsWindowClosed(object? sender, EventArgs eventArgs)
            {
                if (sender is SettingsWindow closedSettingsWindow
                    && ReferenceEquals(currentSettingsWindow, closedSettingsWindow))
                {
                    closedSettingsWindow.GuideWindowRequested -=
                        OnGuideWindowRequested;
                    currentSettingsWindow = null;
                }
            }

            void OnSettingsChanged(AppSettings updatedSettings)
            {
                ApplySettings(updatedSettings, freshHotkeyApplication: false);
            }

            void OnSettingsImported(AppSettings importedSettings)
            {
                ApplySettings(importedSettings, freshHotkeyApplication: true);
            }

            void ApplySettings(
                AppSettings updatedSettings,
                bool freshHotkeyApplication)
            {
                var normalizedSettings = AppSettingsNormalizer.NormalizeCustomProfile(
                    updatedSettings);
                normalizedSettings = DisableAutoPeekForUnknownKeyboardProfile(
                    normalizedSettings);
                if (freshHotkeyApplication || normalizedSettings != updatedSettings)
                {
                    settingsStore.Save(normalizedSettings);
                    updatedSettings = normalizedSettings;
                }

                var previousSettings = currentSettings;
                currentSettings = updatedSettings;

                if (previousSettings.General.Language != updatedSettings.General.Language)
                {
                    languageService.Set(updatedSettings.General.Language);
                }

                if (previousSettings.General.Theme != updatedSettings.General.Theme)
                {
                    systemThemeProvider.SetTheme(updatedSettings.General.Theme);
                    ApplyBrandAccentResources(
                        application,
                        systemThemeProvider.IsDarkTheme);
#pragma warning disable WPF0001
                    application.ThemeMode = GetThemeMode(updatedSettings.General.Theme);
#pragma warning restore WPF0001
                }

                if (freshHotkeyApplication)
                {
                    ApplyImportedHotkeyGating(updatedSettings);
                }
                else
                {
                    ApplyCorrectionHotkeyGating(previousSettings, updatedSettings);
                    ApplyPeekHotkeyGating(previousSettings, updatedSettings);
                }
                currentSettingsWindow?.UpdateSettings(
                    currentSettings,
                    isPaused,
                    registeredCorrectionHotkey is not null,
                    registeredPeekHotkey is not null);
                ApplyOverlayAppearance(previousSettings, updatedSettings);
                overlayService.UpdateKeyboardProfile(
                    updatedSettings.Peek.KeyboardProfile,
                    updatedSettings.Peek.SelectedLayoutKlids,
                    updatedSettings.Peek.CustomKeyboardLayout);
                keyboardCaptureService.UpdateCustomHoldPeekLayout(
                    GetCustomHoldPeekLayout(updatedSettings.Peek));
                autoPeekController.UpdateKeyboardProfile(
                    updatedSettings.Peek.KeyboardProfile,
                    updatedSettings.Peek.CustomKeyboardLayout);
                ApplyKeyboardCaptureBlocking(previousSettings, updatedSettings);
                ApplyAutoPeekSettings(previousSettings, currentSettings);
                ApplyClipboardTextLimit(previousSettings, updatedSettings);
                ApplyLayoutCorrectionScenario(previousSettings, updatedSettings);
                ApplyPeekAutoExitTimeout(previousSettings, updatedSettings);
                UpdateTrayMenuState();
            }

            void ApplyImportedHotkeyGating(AppSettings importedSettings)
            {
                var hotkeyRegistrationFailures =
                    new List<(string FunctionName, GlobalHotkey Hotkey,
                        GlobalHotkeyRegistrationStatus Status)>();

                DisableCorrectionHotkey();
                DisablePeekHotkey();
                keyboardPeekModeController.UpdateActivationHotkey(
                    importedSettings.Peek.ActivationHotkey);

                if (!isPaused
                    && importedSettings.Correction.CorrectionEnabled
                    && importedSettings.Correction.CorrectionHotkey
                        is { } correctionHotkey)
                {
                    var registrationResult =
                        TryRegisterCorrectionHotkey(correctionHotkey);
                    if (!registrationResult.Succeeded)
                    {
                        hotkeyRegistrationFailures.Add((
                            AppStrings.Hotkey_CorrectionFunction,
                            correctionHotkey,
                            registrationResult.RegistrationStatus));
                        DisableCorrectionAfterHotkeyRegistrationFailure();
                    }
                }

                if (!isPaused
                    && importedSettings.Peek.PeekEnabled
                    && importedSettings.Peek.ActivationHotkey is { } peekHotkey)
                {
                    var registrationResult = TryRegisterPeekHotkey(peekHotkey);
                    if (!registrationResult.Succeeded)
                    {
                        hotkeyRegistrationFailures.Add((
                            AppStrings.Hotkey_PeekFunction,
                            peekHotkey,
                            registrationResult.RegistrationStatus));
                        DisablePeekAfterHotkeyRegistrationFailure();
                    }
                }

                if (hotkeyRegistrationFailures.Count > 0)
                {
                    ShowNotificationWindow(
                        AppStrings.Notification_Title,
                        FormatHotkeyRegistrationFailures(hotkeyRegistrationFailures),
                        true);
                }
            }

            void OnApplicationThemeChanged(object? sender, EventArgs eventArgs)
            {
                if (currentSettings.General.Theme != AppTheme.System
                    || application.Dispatcher.HasShutdownStarted
                    || application.Dispatcher.HasShutdownFinished)
                {
                    return;
                }

                void ApplyAccentResources() => ApplyBrandAccentResources(
                    application,
                    systemThemeProvider.IsDarkTheme);

                if (application.Dispatcher.CheckAccess())
                {
                    ApplyAccentResources();
                    return;
                }

                application.Dispatcher.Invoke(
                    ApplyAccentResources,
                    DispatcherPriority.Send);
            }

            void OnMainFeatureEnableRequestedWhilePaused()
            {
                ResumeApplication();
            }

            void RetryCorrectionHotkeyRegistration()
            {
                if (!isPaused && currentSettings.Correction.CorrectionEnabled)
                {
                    EnableCorrectionHotkey(currentSettings);
                }

                UpdateMainFeatureRuntimeState();
            }

            void RetryPeekHotkeyRegistration()
            {
                if (!isPaused && currentSettings.Peek.PeekEnabled)
                {
                    EnablePeekHotkey(currentSettings);

                    if (IsAutoPeekEffectivelyEnabled(currentSettings))
                    {
                        EnableAutoPeek();
                    }
                }

                UpdateMainFeatureRuntimeState();
            }

            void UpdateTrayMenuState()
            {
                var autoPeekEnabled = !isPaused
                    && IsAutoPeekEffectivelyEnabled(currentSettings);
                var tooltipText = isPaused
                    ? string.Format(
                        CultureInfo.CurrentCulture,
                        AppStrings.Tray_Tooltip_Paused,
                        AppStrings.Notification_Title)
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        AppStrings.Tray_Tooltip_Running,
                        AppStrings.Notification_Title);

                backgroundAppMenuService.UpdateState(
                    isPaused,
                    autoPeekEnabled,
                    tooltipText);
            }

            void UpdateMainFeatureRuntimeState()
            {
                currentSettingsWindow?.UpdateRuntimeState(
                    isPaused,
                    registeredCorrectionHotkey is not null,
                    registeredPeekHotkey is not null);
                UpdateTrayMenuState();
            }

            void UpdateTrayMenuLabels()
            {
                backgroundAppMenuService.Show(
                    AppStrings.Tray_Pause,
                    AppStrings.Tray_AutoPeek,
                    AppStrings.Tray_Settings,
                    AppStrings.Tray_Exit);
            }

            void OnLanguageChanged(object? sender, EventArgs eventArgs)
            {
                if (application.Dispatcher.HasShutdownStarted
                    || application.Dispatcher.HasShutdownFinished)
                {
                    return;
                }

                void RefreshTrayMenu()
                {
                    UpdateTrayMenuLabels();
                    UpdateTrayMenuState();
                }

                if (!application.Dispatcher.CheckAccess())
                {
                    _ = application.Dispatcher.BeginInvoke(RefreshTrayMenu);
                    return;
                }

                RefreshTrayMenu();
            }

            void ApplyKeyboardCaptureBlocking(
                AppSettings previousSettings,
                AppSettings updatedSettings)
            {
                if (previousSettings.Peek.BlockNumberRow ==
                        updatedSettings.Peek.BlockNumberRow
                    && previousSettings.Peek.ShowServiceKeys ==
                        updatedSettings.Peek.ShowServiceKeys)
                {
                    return;
                }

                keyboardCaptureArbiter.UpdateBlockingOptions(
                    updatedSettings.Peek.BlockNumberRow,
                    updatedSettings.Peek.ShowServiceKeys);
            }

            void ApplyAutoPeekSettings(
                AppSettings previousSettings,
                AppSettings updatedSettings)
            {
                if (previousSettings.Peek.AutoPeekLingerMs !=
                        updatedSettings.Peek.AutoPeekLingerMs
                    && updatedSettings.Peek.AutoPeekLingerMs > 0)
                {
                    autoPeekController.UpdateLingerDuration(
                        TimeSpan.FromMilliseconds(updatedSettings.Peek.AutoPeekLingerMs));
                }

                if (previousSettings.Peek.BlockNumberRow !=
                        updatedSettings.Peek.BlockNumberRow
                    || previousSettings.Peek.ShowServiceKeys !=
                        updatedSettings.Peek.ShowServiceKeys)
                {
                    autoPeekController.UpdateShowFlags(
                        updatedSettings.Peek.BlockNumberRow,
                        updatedSettings.Peek.ShowServiceKeys);
                }

                if (isPaused)
                {
                    DisableAutoPeek();
                    return;
                }

                var wasEnabled = IsAutoPeekEffectivelyEnabled(previousSettings);
                var isEnabled = IsAutoPeekEffectivelyEnabled(updatedSettings);
                if (wasEnabled == isEnabled)
                {
                    return;
                }

                if (isEnabled)
                {
                    EnableAutoPeek();
                    return;
                }

                DisableAutoPeek();
            }

            void EnableAutoPeek()
            {
                autoPeekController.Enable();
                keyboardCaptureArbiter.SetAutoPeekCaptureRequired(true);
            }

            void DisableAutoPeek()
            {
                autoPeekController.Disable();
                keyboardCaptureArbiter.SetAutoPeekCaptureRequired(false);
            }

            void ApplyClipboardTextLimit(
                AppSettings previousSettings,
                AppSettings updatedSettings)
            {
                if (previousSettings.Correction.TextLimitBytes ==
                    updatedSettings.Correction.TextLimitBytes)
                {
                    return;
                }

                clipboardService.UpdateMaxTextSizeInBytes(
                    updatedSettings.Correction.TextLimitBytes);
            }

            void ApplyLayoutCorrectionScenario(
                AppSettings previousSettings,
                AppSettings updatedSettings)
            {
                if (previousSettings.Correction.PasteIntoUnknown ==
                        updatedSettings.Correction.PasteIntoUnknown
                    && previousSettings.Correction.ClipboardRestoreDelayMs ==
                        updatedSettings.Correction.ClipboardRestoreDelayMs
                    && previousSettings.Correction.CopyTimeoutMs ==
                        updatedSettings.Correction.CopyTimeoutMs
                    && previousSettings.General.LayoutAutoSwitchEnabled ==
                        updatedSettings.General.LayoutAutoSwitchEnabled)
                {
                    return;
                }

                layoutCorrectionScenario = new LayoutCorrectionScenario(
                    clipboardService,
                    inputSimulationService,
                    focusedElementService,
                    activeApplicationService,
                    timeout: TimeSpan.FromMilliseconds(
                        updatedSettings.Correction.CopyTimeoutMs),
                    pasteIntoUnknown: updatedSettings.Correction.PasteIntoUnknown,
                    clipboardRestoreDelay: TimeSpan.FromMilliseconds(
                        updatedSettings.Correction.ClipboardRestoreDelayMs),
                    keyboardLayoutService: updatedSettings.General.LayoutAutoSwitchEnabled
                        ? keyboardLayoutService
                        : null,
                    diagnosticLogger: diagnosticLogger);
            }

            void ApplyPeekAutoExitTimeout(
                AppSettings previousSettings,
                AppSettings updatedSettings)
            {
                if (previousSettings.Peek.AutoExitTimeoutSeconds ==
                        updatedSettings.Peek.AutoExitTimeoutSeconds
                    || updatedSettings.Peek.AutoExitTimeoutSeconds <= 0)
                {
                    return;
                }

                keyboardPeekModeController.UpdateAutoExitTimeout(
                    TimeSpan.FromSeconds(updatedSettings.Peek.AutoExitTimeoutSeconds));
            }

            void ApplyOverlayAppearance(
                AppSettings previousSettings,
                AppSettings updatedSettings)
            {
                if (previousSettings.Peek.Opacity == updatedSettings.Peek.Opacity
                    && previousSettings.Peek.Scale == updatedSettings.Peek.Scale
                    && previousSettings.Peek.AnimationEnabled ==
                        updatedSettings.Peek.AnimationEnabled
                    && previousSettings.Peek.AnimationDurationMs ==
                        updatedSettings.Peek.AnimationDurationMs)
                {
                    return;
                }

                overlayService.UpdateAppearance(new OverlayAppearanceOptions(
                    opacity: updatedSettings.Peek.Opacity,
                    scale: updatedSettings.Peek.Scale,
                    animationEnabled: updatedSettings.Peek.AnimationEnabled,
                    animationDurationMs: updatedSettings.Peek.AnimationDurationMs));
            }

            void ApplyCorrectionHotkeyGating(
                AppSettings previousSettings,
                AppSettings updatedSettings)
            {
                if (isPaused)
                {
                    DisableCorrectionHotkey();
                    return;
                }

                var wasEnabled = previousSettings.Correction.CorrectionEnabled;
                var isEnabled = updatedSettings.Correction.CorrectionEnabled;
                var updatedHotkey = updatedSettings.Correction.CorrectionHotkey;
                var hotkeyChanged = !Equals(
                    previousSettings.Correction.CorrectionHotkey,
                    updatedHotkey);

                if (!isEnabled)
                {
                    DisableCorrectionHotkey();
                    return;
                }

                if (wasEnabled != isEnabled)
                {
                    EnableCorrectionHotkey(updatedSettings);
                    return;
                }

                if (!hotkeyChanged)
                {
                    return;
                }

                if (updatedHotkey is null)
                {
                    DisableCorrectionHotkey();
                    return;
                }

                var registrationResult = TryRegisterCorrectionHotkey(updatedHotkey);
                if (registrationResult.Succeeded)
                {
                    return;
                }

                var previousSettingRestored = RestorePreviousCorrectionHotkeySetting(
                    previousSettings,
                    updatedSettings);
                ShowNotificationWindow(
                    AppStrings.Notification_Title,
                    previousSettingRestored
                        ? FormatCorrectionHotkeyChangeFailure(registrationResult)
                        : AppStrings.Settings_Hotkey_PersistRollbackFailed,
                    true);
            }

            void EnableCorrectionHotkey(AppSettings settings)
            {
                var hotkey = settings.Correction.CorrectionHotkey;
                if (hotkey is null)
                {
                    DisableCorrectionHotkey();
                    return;
                }

                var registrationResult = TryRegisterCorrectionHotkey(hotkey);
                if (registrationResult.Succeeded)
                {
                    return;
                }

                DisableCorrectionAfterHotkeyRegistrationFailure();
                ShowNotificationWindow(
                    AppStrings.Notification_Title,
                    FormatCorrectionHotkeyChangeFailure(registrationResult),
                    true);
            }

            void DisableCorrectionAfterHotkeyRegistrationFailure()
            {
                DisableCorrectionHotkey();
                currentSettingsWindow?.UpdateSettings(
                    currentSettings,
                    isPaused,
                    registeredCorrectionHotkey is not null,
                    registeredPeekHotkey is not null);
                UpdateTrayMenuState();
            }

            CorrectionHotkeyRegistrationResult TryRegisterCorrectionHotkey(
                GlobalHotkey hotkey)
            {
                if (registeredCorrectionHotkey?.Equals(hotkey) == true)
                {
                    return new CorrectionHotkeyRegistrationResult(
                        GlobalHotkeyRegistrationStatus.Success,
                        true,
                        true);
                }

                var previousHotkey = registeredCorrectionHotkey;
                var registrationStatus = globalHotkeyService.Register(hotkey);
                if (registrationStatus == GlobalHotkeyRegistrationStatus.Success)
                {
                    registeredCorrectionHotkey = hotkey;

                    if (!correctionHotkeyHandlerSubscribed)
                    {
                        globalHotkeyService.HotkeyPressed += OnHotkeyPressed;
                        correctionHotkeyHandlerSubscribed = true;
                    }

                    UpdateMainFeatureRuntimeState();

                    return new CorrectionHotkeyRegistrationResult(
                        registrationStatus,
                        previousHotkey is not null,
                        true);
                }

                registeredCorrectionHotkey = null;
                var previousHotkeyRestored = previousHotkey is not null
                    && globalHotkeyService.Register(previousHotkey) ==
                        GlobalHotkeyRegistrationStatus.Success;

                if (previousHotkeyRestored)
                {
                    registeredCorrectionHotkey = previousHotkey;
                }
                else
                {
                    if (correctionHotkeyHandlerSubscribed)
                    {
                        globalHotkeyService.HotkeyPressed -= OnHotkeyPressed;
                        correctionHotkeyHandlerSubscribed = false;
                    }

                    globalHotkeyService.Unregister();
                }

                UpdateMainFeatureRuntimeState();

                return new CorrectionHotkeyRegistrationResult(
                    registrationStatus,
                    previousHotkey is not null,
                    previousHotkeyRestored);
            }

            bool RestorePreviousCorrectionHotkeySetting(
                AppSettings previousSettings,
                AppSettings updatedSettings)
            {
                var restoredSettings = updatedSettings with
                {
                    Correction = updatedSettings.Correction with
                    {
                        CorrectionHotkey = previousSettings.Correction.CorrectionHotkey,
                    },
                };

                var saved = true;

                try
                {
                    settingsStore.Save(restoredSettings);
                }
                catch (Exception exception) when (
                    exception is ArgumentException
                        or IOException
                        or UnauthorizedAccessException
                        or NotSupportedException
                        or SecurityException)
                {
                    saved = false;
                }

                currentSettings = restoredSettings;
                currentSettingsWindow?.UpdateSettings(
                    currentSettings,
                    isPaused,
                    registeredCorrectionHotkey is not null,
                    registeredPeekHotkey is not null);
                return saved;
            }

            void DisableCorrectionHotkey()
            {
                if (correctionHotkeyHandlerSubscribed)
                {
                    globalHotkeyService.HotkeyPressed -= OnHotkeyPressed;
                    correctionHotkeyHandlerSubscribed = false;
                }

                globalHotkeyService.Unregister();
                registeredCorrectionHotkey = null;
            }

            void ApplyPeekHotkeyGating(
                AppSettings previousSettings,
                AppSettings updatedSettings)
            {
                var wasEnabled = previousSettings.Peek.PeekEnabled;
                var isEnabled = updatedSettings.Peek.PeekEnabled;
                var updatedHotkey = updatedSettings.Peek.ActivationHotkey;
                var hotkeyChanged = !Equals(
                    previousSettings.Peek.ActivationHotkey,
                    updatedHotkey);

                if (hotkeyChanged)
                {
                    keyboardPeekModeController.UpdateActivationHotkey(updatedHotkey);
                }

                if (isPaused)
                {
                    DisablePeekHotkey();
                    return;
                }

                if (!isEnabled)
                {
                    DisablePeekHotkey();
                    return;
                }

                if (wasEnabled != isEnabled)
                {
                    EnablePeekHotkey(updatedSettings);
                    return;
                }

                if (!hotkeyChanged)
                {
                    return;
                }

                if (updatedHotkey is null)
                {
                    DisablePeekHotkey();
                    return;
                }

                var registrationResult = TryRegisterPeekHotkey(updatedHotkey);
                if (registrationResult.Succeeded)
                {
                    return;
                }

                var previousSettingRestored = RestorePreviousPeekHotkeySetting(
                    previousSettings,
                    updatedSettings);
                ShowNotificationWindow(
                    AppStrings.Notification_Title,
                    previousSettingRestored
                        ? FormatPeekHotkeyChangeFailure(registrationResult)
                        : AppStrings.Settings_Hotkey_PersistRollbackFailed,
                    true);
            }

            void EnablePeekHotkey(AppSettings settings)
            {
                var hotkey = settings.Peek.ActivationHotkey;
                if (hotkey is null)
                {
                    DisablePeekHotkey();
                    return;
                }

                var registrationResult = TryRegisterPeekHotkey(hotkey);
                if (registrationResult.Succeeded)
                {
                    return;
                }

                DisablePeekAfterHotkeyRegistrationFailure();
                ShowNotificationWindow(
                    AppStrings.Notification_Title,
                    FormatPeekHotkeyChangeFailure(registrationResult),
                    true);
            }

            void DisablePeekAfterHotkeyRegistrationFailure()
            {
                DisablePeekHotkey();
                currentSettingsWindow?.UpdateSettings(
                    currentSettings,
                    isPaused,
                    registeredCorrectionHotkey is not null,
                    registeredPeekHotkey is not null);
                UpdateTrayMenuState();
            }

            PeekHotkeyRegistrationResult TryRegisterPeekHotkey(GlobalHotkey hotkey)
            {
                if (registeredPeekHotkey?.Equals(hotkey) == true)
                {
                    return new PeekHotkeyRegistrationResult(
                        GlobalHotkeyRegistrationStatus.Success,
                        true,
                        true);
                }

                var previousHotkey = registeredPeekHotkey;
                var registrationStatus = keyboardPeekModeHotkeyService.Register(hotkey);
                if (registrationStatus == GlobalHotkeyRegistrationStatus.Success)
                {
                    registeredPeekHotkey = hotkey;

                    if (!peekHotkeyHandlerSubscribed)
                    {
                        keyboardPeekModeHotkeyService.HotkeyPressed +=
                            OnKeyboardPeekModeHotkeyPressed;
                        peekHotkeyHandlerSubscribed = true;
                    }

                    UpdateMainFeatureRuntimeState();

                    return new PeekHotkeyRegistrationResult(
                        registrationStatus,
                        previousHotkey is not null,
                        true);
                }

                registeredPeekHotkey = null;
                var previousHotkeyRestored = previousHotkey is not null
                    && keyboardPeekModeHotkeyService.Register(previousHotkey) ==
                        GlobalHotkeyRegistrationStatus.Success;

                if (previousHotkeyRestored)
                {
                    registeredPeekHotkey = previousHotkey;
                }
                else
                {
                    if (peekHotkeyHandlerSubscribed)
                    {
                        keyboardPeekModeHotkeyService.HotkeyPressed -=
                            OnKeyboardPeekModeHotkeyPressed;
                        peekHotkeyHandlerSubscribed = false;
                    }

                    keyboardPeekModeHotkeyService.Unregister();
                }

                UpdateMainFeatureRuntimeState();

                return new PeekHotkeyRegistrationResult(
                    registrationStatus,
                    previousHotkey is not null,
                    previousHotkeyRestored);
            }

            bool RestorePreviousPeekHotkeySetting(
                AppSettings previousSettings,
                AppSettings updatedSettings)
            {
                var restoredSettings = updatedSettings with
                {
                    Peek = updatedSettings.Peek with
                    {
                        ActivationHotkey = previousSettings.Peek.ActivationHotkey,
                    },
                };

                var saved = true;

                try
                {
                    settingsStore.Save(restoredSettings);
                }
                catch (Exception exception) when (IsSettingsOperationException(exception))
                {
                    saved = false;
                }

                currentSettings = restoredSettings;
                keyboardPeekModeController.UpdateActivationHotkey(
                    currentSettings.Peek.ActivationHotkey);
                currentSettingsWindow?.UpdateSettings(
                    currentSettings,
                    isPaused,
                    registeredCorrectionHotkey is not null,
                    registeredPeekHotkey is not null);
                return saved;
            }

            void DisablePeekHotkey()
            {
                if (peekHotkeyHandlerSubscribed)
                {
                    keyboardPeekModeHotkeyService.HotkeyPressed -=
                        OnKeyboardPeekModeHotkeyPressed;
                    peekHotkeyHandlerSubscribed = false;
                }

                keyboardPeekModeHotkeyService.Unregister();
                registeredPeekHotkey = null;
            }

            void OnFocusedElementDiagnosticHotkeyPressed(object? sender, EventArgs eventArgs)
            {
                if (isPaused)
                {
                    return;
                }

                var diagnosticRun = StartDiagnosticRun();
                var editabilityDiagnostic =
                    focusedElementService.GetEditabilityWithDiagnostics();
                var editability = editabilityDiagnostic.Editability;
                var activeApplication = activeApplicationService.GetActiveApplication();

                LogDiagnostic(
                    diagnosticRun,
                    "editability_marker",
                    new Dictionary<string, object?>
                    {
                        ["outcome"] = editability.ToString().ToLowerInvariant(),
                        ["controlType"] = editabilityDiagnostic.ControlType,
                        ["decisionBranch"] = editabilityDiagnostic.DecisionBranch,
                        ["valuePatternAvailable"] =
                            editabilityDiagnostic.ValuePatternAvailable,
                        ["textPatternAvailable"] =
                            editabilityDiagnostic.TextPatternAvailable,
                        ["processName"] = activeApplication.ProcessName,
                        ["isTerminal"] = activeApplication.IsTerminal,
                    });

                ShowNotificationWindow(
                    AppStrings.Notification_Title,
                    FormatFocusedElementDiagnosticResult(editability, activeApplication),
                    editability == FocusedElementEditability.Unknown ||
                        activeApplication.Outcome == ActiveApplicationDetectionOutcome.Unavailable);
            }

            void OnKeyboardPeekModeHotkeyPressed(object? sender, EventArgs eventArgs)
            {
                if (isPaused)
                {
                    return;
                }

                var activationOutcome = keyboardPeekModeController.Activate();
                if (activationOutcome is KeyboardPeekModeActivationOutcome.Activated or
                    KeyboardPeekModeActivationOutcome.AlreadyActive)
                {
                    keyboardPeekModeMonitor.Start();
                    return;
                }

                StopKeyboardPeekOverlayTracking();
            }

            void OnKeyboardCaptureKeyChanged(
                object? sender,
                KeyboardCaptureEventArgs eventArgs)
            {
                if (isPaused
                    || isHotkeyRecordingCaptureActive
                    || isKeyboardCalibrationCaptureActive)
                {
                    return;
                }

                if (!keyboardPeekModeController.IsActive)
                {
                    StopKeyboardPeekOverlayTracking();
                    return;
                }

                if (eventArgs.Key != PhysicalKey.Unknown
                    && keyboardPeekModeController.IsActivationChordKey(eventArgs.Key))
                {
                    return;
                }

                if (eventArgs.State == KeyboardKeyState.Down)
                {
                    if (eventArgs.Key == PhysicalKey.Unknown)
                    {
                        var centerIdentity = new KeyboardScanCodeIdentity(
                            eventArgs.ScanCode,
                            eventArgs.Extended);
                        if (currentSettings.Peek.KeyboardProfile == KeyboardProfile.Custom
                            && KeyboardPeekBlockingPolicy.ShouldShowOverlay(
                                centerIdentity,
                                currentSettings.Peek.CustomKeyboardLayout))
                        {
                            peekOverlayOwner = PeekOverlayOwner.HotkeyPeek;
                            peekOverlayKey = null;
                            peekOverlayIdentity = centerIdentity;
                            ShowCustomPeekOverlay(centerIdentity, "hold");
                        }

                        return;
                    }

                    if (KeyboardPeekBlockingPolicy.ShouldShowOverlay(
                            eventArgs.Key,
                            currentSettings.Peek.BlockNumberRow,
                            currentSettings.Peek.ShowServiceKeys))
                    {
                        if (currentSettings.Peek.KeyboardProfile == KeyboardProfile.Unknown)
                        {
                            peekOverlayOwner = PeekOverlayOwner.None;
                            peekOverlayKey = null;
                            peekOverlayIdentity = null;
                            overlayService.Hide();
                            ShowKeyboardProfilePickerWindow(
                                KeyboardProfilePickerIntent.SelectProfileOnly);
                            return;
                        }

                        peekOverlayOwner = PeekOverlayOwner.HotkeyPeek;
                        if (currentSettings.Peek.KeyboardProfile == KeyboardProfile.Custom)
                        {
                            var centerIdentity = new KeyboardScanCodeIdentity(
                                eventArgs.ScanCode,
                                eventArgs.Extended);
                            peekOverlayKey = null;
                            peekOverlayIdentity = centerIdentity;
                            ShowCustomPeekOverlay(centerIdentity, "hold");
                        }
                        else
                        {
                            peekOverlayKey = eventArgs.Key;
                            peekOverlayIdentity = null;
                            ShowPeekOverlay(eventArgs.Key, "hold");
                        }
                    }

                    return;
                }

                if (peekOverlayOwner != PeekOverlayOwner.HotkeyPeek)
                {
                    return;
                }

                if (peekOverlayIdentity is { } trackedIdentity)
                {
                    var centerIdentity = new KeyboardScanCodeIdentity(
                        eventArgs.ScanCode,
                        eventArgs.Extended);
                    if (trackedIdentity == centerIdentity)
                    {
                        peekOverlayOwner = PeekOverlayOwner.None;
                        peekOverlayKey = null;
                        peekOverlayIdentity = null;
                        overlayService.Hide(centerIdentity);
                    }

                    return;
                }

                if (eventArgs.Key == PhysicalKey.Unknown)
                {
                    return;
                }

                if (peekOverlayKey == eventArgs.Key)
                {
                    peekOverlayOwner = PeekOverlayOwner.None;
                    peekOverlayKey = null;
                    peekOverlayIdentity = null;
                    overlayService.Hide(eventArgs.Key);
                }
            }

            void OnKeyboardPeekModeMonitorTick(object? sender, EventArgs eventArgs)
            {
                if (!keyboardPeekModeController.IsActive)
                {
                    StopKeyboardPeekOverlayTracking();
                }
            }

            void StopKeyboardPeekOverlayTracking()
            {
                keyboardPeekModeMonitor.Stop();

                if (peekOverlayOwner != PeekOverlayOwner.HotkeyPeek)
                {
                    return;
                }

                peekOverlayOwner = PeekOverlayOwner.None;
                peekOverlayKey = null;
                peekOverlayIdentity = null;
                overlayService.Hide();
            }

            void OnAutoPeekShowOverlayRequested(PhysicalKey key)
            {
                DispatchOverlayAction(() =>
                {
                    if (isPaused
                        || isHotkeyRecordingCaptureActive
                        || isKeyboardCalibrationCaptureActive
                        || !autoPeekController.IsEnabled
                        || keyboardPeekModeController.IsActive)
                    {
                        return;
                    }

                    Interlocked.Increment(ref autoPeekOverlayVersion);
                    peekOverlayOwner = PeekOverlayOwner.AutoPeek;
                    peekOverlayKey = key;
                    peekOverlayIdentity = null;
                    ShowPeekOverlay(key, "auto");
                });
            }

            void OnAutoPeekShowCustomOverlayRequested(
                KeyboardScanCodeIdentity identity)
            {
                DispatchOverlayAction(() =>
                {
                    if (isPaused
                        || isHotkeyRecordingCaptureActive
                        || isKeyboardCalibrationCaptureActive
                        || !autoPeekController.IsEnabled
                        || keyboardPeekModeController.IsActive)
                    {
                        return;
                    }

                    Interlocked.Increment(ref autoPeekOverlayVersion);
                    peekOverlayOwner = PeekOverlayOwner.AutoPeek;
                    peekOverlayKey = null;
                    peekOverlayIdentity = identity;
                    ShowCustomPeekOverlay(identity, "auto");
                });
            }

            void OnAutoPeekHideOverlayRequested()
            {
                var requestedVersion = Volatile.Read(ref autoPeekOverlayVersion);
                DispatchOverlayAction(() =>
                {
                    if (requestedVersion != Volatile.Read(ref autoPeekOverlayVersion)
                        || peekOverlayOwner != PeekOverlayOwner.AutoPeek)
                    {
                        return;
                    }

                    peekOverlayOwner = PeekOverlayOwner.None;
                    peekOverlayKey = null;
                    peekOverlayIdentity = null;
                    overlayService.Hide();
                });
            }

            void DispatchOverlayAction(Action action)
            {
                if (application.Dispatcher.CheckAccess())
                {
                    action();
                    return;
                }

                if (application.Dispatcher.HasShutdownStarted
                    || application.Dispatcher.HasShutdownFinished)
                {
                    return;
                }

                _ = application.Dispatcher.BeginInvoke(action);
            }

            void ShowPeekOverlay(PhysicalKey key, string source)
            {
                if (!diagnosticsEnabled)
                {
                    overlayService.Show(key);
                    return;
                }

                windowsOverlayService.ShowWithCaretDiagnostics(
                    key,
                    source,
                    diagnosticLogger);
            }

            void ShowCustomPeekOverlay(
                KeyboardScanCodeIdentity identity,
                string source)
            {
                if (!diagnosticsEnabled)
                {
                    overlayService.Show(identity);
                    return;
                }

                windowsOverlayService.ShowWithCaretDiagnostics(
                    identity,
                    source,
                    diagnosticLogger);
            }

            async Task RunScenarioAndNotifyAsync(DiagnosticRunContext diagnosticRun)
            {
                try
                {
                    var result = await layoutCorrectionScenario
                        .RunAsync(diagnosticRun, shutdown.Token)
                        .ConfigureAwait(false);

                    await application.Dispatcher.InvokeAsync(() =>
                    {
                        if (result.Outcome == LayoutCorrectionOutcome.ResultWindowRequired)
                        {
                            ShowResultWindow(result, diagnosticRun);
                            return;
                        }

                        if (result.Outcome == LayoutCorrectionOutcome.AutoReplacementCompleted)
                        {
                            return;
                        }

                        ShowNotificationWindow(
                            AppStrings.Notification_Title,
                            FormatLayoutCorrectionOutcome(result.Outcome),
                            IsWarning(result.Outcome));
                    }).Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
                {
                }
                finally
                {
                    Interlocked.Exchange(ref scenarioRunInProgress, 0);
                }
            }

            void ShowResultWindow(
                LayoutCorrectionResult result,
                DiagnosticRunContext diagnosticRun)
            {
                var convertedText = result.ConvertedText;
                if (string.IsNullOrEmpty(convertedText))
                {
                    ShowNotificationWindow(
                        AppStrings.Notification_Title,
                        AppStrings.Correction_InputError,
                        true);
                    return;
                }

                var sourceWindowToken = activeWindowFocusService.CaptureActiveWindow();
                var sourceMonitorWorkArea = activeWindowFocusService
                    .GetActiveMonitorWorkArea(sourceWindowToken);
                currentResultWindow?.Close();

                var resultWindow = new ResultWindow(
                    convertedText,
                    clipboardService,
                    cancellationToken =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var clickId = CreateDiagnosticClickId(diagnosticRun);
                        var foregroundBeforeRestore = CaptureForegroundBestEffort(diagnosticRun);
                        var focusRestored = activeWindowFocusService
                            .TryRestoreFocus(sourceWindowToken);
                        var foregroundAfterRestore = CaptureForegroundBestEffort(diagnosticRun);

                        LogDiagnostic(
                            diagnosticRun,
                            "manual_try_paste",
                            new Dictionary<string, object?>
                            {
                                ["runId"] = diagnosticRun.RunId,
                                ["clickId"] = clickId,
                                ["clickReceived"] = true,
                                ["accepted"] = focusRestored,
                                ["suppressed"] = !focusRestored,
                                ["sourceWindowTokenAvailable"] = sourceWindowToken is not null,
                                ["tryRestoreFocus"] = focusRestored,
                                ["foregroundProcessBefore"] = foregroundBeforeRestore?.ProcessName,
                                ["foregroundWindowHandleBefore"] = foregroundBeforeRestore?.WindowHandle,
                                ["foregroundProcessAfter"] = foregroundAfterRestore?.ProcessName,
                                ["foregroundWindowHandleAfter"] = foregroundAfterRestore?.WindowHandle,
                                ["layoutSwitch"] = focusRestored
                                    ? "pending_paste_success"
                                    : "not_requested",
                                ["reason"] = "manual_try_paste",
                                ["suppressionReason"] = focusRestored
                                    ? null
                                    : sourceWindowToken is null
                                        ? "source_window_unavailable"
                                        : "focus_restore_failed",
                            });

                        InputSimulationResult? pasteResult = null;
                        if (focusRestored)
                        {
                            var foregroundBeforePaste = CaptureForegroundBestEffort(diagnosticRun);
                            pasteResult = inputSimulationService
                                .SendPaste(diagnosticRun.IsEnabled);
                            var foregroundAfterPaste = CaptureForegroundBestEffort(diagnosticRun);

                            LogSendPaste(
                                diagnosticRun,
                                source: "manual",
                                clickId,
                                foregroundBeforePaste,
                                foregroundAfterPaste,
                                pasteResult);
                        }

                        SwitchKeyboardLayoutAfterManualPasteBestEffort(
                            diagnosticRun,
                            result,
                            sourceWindowToken is not null,
                            focusRestored,
                            pasteResult);

                        return ValueTask.CompletedTask;
                    },
                    sourceMonitorWorkArea);

                resultWindow.Closed += OnResultWindowClosed;
                currentResultWindow = resultWindow;
                resultWindow.Show();
            }

            void SwitchKeyboardLayoutAfterManualPasteBestEffort(
                DiagnosticRunContext diagnosticRun,
                LayoutCorrectionResult result,
                bool sourceWindowTokenAvailable,
                bool focusRestored,
                InputSimulationResult? pasteResult)
            {
                var serviceEnabled = currentSettings.General.LayoutAutoSwitchEnabled;
                var direction = result.ResultLayoutDirection;
                var targetLanguage = result.TargetInputLanguage;

                if (!focusRestored ||
                    pasteResult?.Outcome != InputSimulationOutcome.Success ||
                    !serviceEnabled ||
                    targetLanguage is null)
                {
                    var reason = !focusRestored
                        ? sourceWindowTokenAvailable
                            ? "focus_restore_failed"
                            : "source_window_unavailable"
                        : pasteResult?.Outcome != InputSimulationOutcome.Success
                            ? "send_paste_failed"
                            : !serviceEnabled
                                ? "service_disabled"
                                : "target_language_unavailable";

                    LogDiagnostic(
                        diagnosticRun,
                        "layout_switch",
                        new Dictionary<string, object?>
                        {
                            ["source"] = "manual",
                            ["requested"] = false,
                            ["direction"] = direction?.ToString().ToLowerInvariant(),
                            ["targetLanguage"] = targetLanguage?.ToString().ToLowerInvariant(),
                            ["serviceEnabled"] = serviceEnabled,
                            ["outcome"] = "not_requested",
                            ["reason"] = reason,
                        });
                    return;
                }

                try
                {
                    var outcome = keyboardLayoutService
                        .SwitchActiveInputLanguage(targetLanguage.Value);

                    LogDiagnostic(
                        diagnosticRun,
                        "layout_switch",
                        new Dictionary<string, object?>
                        {
                            ["source"] = "manual",
                            ["requested"] = true,
                            ["direction"] = direction?.ToString().ToLowerInvariant(),
                            ["targetLanguage"] = targetLanguage.Value
                                .ToString()
                                .ToLowerInvariant(),
                            ["serviceEnabled"] = true,
                            ["outcome"] = outcome.ToString().ToLowerInvariant(),
                            ["reason"] = null,
                        });
                }
                catch (Exception exception)
                {
                    LogDiagnostic(
                        diagnosticRun,
                        "layout_switch",
                        new Dictionary<string, object?>
                        {
                            ["source"] = "manual",
                            ["requested"] = true,
                            ["direction"] = direction?.ToString().ToLowerInvariant(),
                            ["targetLanguage"] = targetLanguage.Value
                                .ToString()
                                .ToLowerInvariant(),
                            ["serviceEnabled"] = true,
                            ["outcome"] = "exception",
                            ["reason"] = "service_exception",
                            ["errorType"] = exception.GetType().Name,
                        });
                }
            }

            DiagnosticRunContext StartDiagnosticRun()
            {
                try
                {
                    return diagnosticLogger.StartRun();
                }
                catch (Exception)
                {
                    return DiagnosticRunContext.Disabled;
                }
            }

            string CreateDiagnosticClickId(DiagnosticRunContext diagnosticRun)
            {
                if (!diagnosticRun.IsEnabled)
                {
                    return string.Empty;
                }

                try
                {
                    return diagnosticRun.CreateClickId();
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }

            ActiveApplicationInfo? CaptureForegroundBestEffort(
                DiagnosticRunContext diagnosticRun)
            {
                if (!diagnosticRun.IsEnabled)
                {
                    return null;
                }

                try
                {
                    return activeApplicationService.GetActiveApplication();
                }
                catch (Exception)
                {
                    return null;
                }
            }

            void LogSendPaste(
                DiagnosticRunContext diagnosticRun,
                string source,
                string? clickId,
                ActiveApplicationInfo? foregroundBefore,
                ActiveApplicationInfo? foregroundAfter,
                InputSimulationResult pasteResult)
            {
                LogDiagnostic(
                    diagnosticRun,
                    "send_paste",
                    new Dictionary<string, object?>
                    {
                        ["source"] = source,
                        ["runId"] = diagnosticRun.RunId,
                        ["clickId"] = clickId,
                        ["foregroundProcessBefore"] = foregroundBefore?.ProcessName,
                        ["foregroundWindowHandleBefore"] = foregroundBefore?.WindowHandle,
                        ["foregroundProcessAfter"] = foregroundAfter?.ProcessName,
                        ["foregroundWindowHandleAfter"] = foregroundAfter?.WindowHandle,
                        ["physicalCtrlDown"] = pasteResult.PhysicalControlDown,
                        ["plannedInputCount"] = pasteResult.PlannedInputCount,
                        ["sentInputCount"] = pasteResult.SentInputCount,
                        ["outcome"] = pasteResult.Outcome.ToString().ToLowerInvariant(),
                        ["errorCode"] = pasteResult.PlatformErrorCode,
                    });
            }

            void LogDiagnostic(
                DiagnosticRunContext diagnosticRun,
                string eventName,
                IReadOnlyDictionary<string, object?> fields)
            {
                if (!diagnosticRun.IsEnabled)
                {
                    return;
                }

                try
                {
                    diagnosticLogger.Log(diagnosticRun, eventName, fields);
                }
                catch (Exception)
                {
                    // Diagnostic logging is best-effort and must not interrupt the main flow.
                }
            }

            void OnResultWindowClosed(object? sender, EventArgs eventArgs)
            {
                if (ReferenceEquals(currentResultWindow, sender))
                {
                    currentResultWindow = null;
                }
            }

            void ShowNotificationWindow(string title, string message, bool isWarning)
            {
                if (!application.Dispatcher.CheckAccess())
                {
                    application.Dispatcher.Invoke(
                        () => ShowNotificationWindow(title, message, isWarning));
                    return;
                }

                var sourceWindowToken = activeWindowFocusService.CaptureActiveWindow();
                var sourceMonitorWorkArea = activeWindowFocusService
                    .GetActiveMonitorWorkArea(sourceWindowToken);
                currentNotificationWindow?.Close();

                var notificationWindow = new NotificationWindow(
                    title,
                    message,
                    isWarning,
                    sourceMonitorWorkArea);
                notificationWindow.Closed += OnNotificationWindowClosed;
                currentNotificationWindow = notificationWindow;
                notificationWindow.Show();
            }

            void ShowKeyboardProfilePickerWindow(
                KeyboardProfilePickerIntent pickerIntent)
            {
                if (!application.Dispatcher.CheckAccess())
                {
                    application.Dispatcher.Invoke(
                        () => ShowKeyboardProfilePickerWindow(pickerIntent));
                    return;
                }

                if (pickerIntent == KeyboardProfilePickerIntent.EnableAutoPeek)
                {
                    pendingKeyboardProfilePickerIntent = pickerIntent;
                }

                if (currentKeyboardCalibrationWizardWindow is not null)
                {
                    currentKeyboardCalibrationWizardWindow.ShowAndActivate();
                    return;
                }

                if (currentKeyboardProfilePickerWindow is not null)
                {
                    currentKeyboardProfilePickerWindow.ShowAndActivate();
                    return;
                }

                var sourceWindowToken = activeWindowFocusService.CaptureActiveWindow();
                var sourceMonitorWorkArea = activeWindowFocusService
                    .GetActiveMonitorWorkArea(sourceWindowToken);
                IReadOnlyList<InstalledKeyboardLayout> installedLayouts =
                    installedKeyboardLayoutService.GetInstalledKeyboardLayouts();
                if (diagnosticsEnabled)
                {
                    installedLayouts =
                    [
                        .. installedLayouts,
                    new("f0000001", "Diagnostic Layout 1"),
                    new("f0000002", "Diagnostic Layout 2"),
                    new("f0000003", "Diagnostic Layout 3"),
                    new("f0000004", "Diagnostic Layout 4"),
                    new("f0000005", "Diagnostic Layout 5"),
                    new("f0000006", "Diagnostic Layout 6"),
                ];
                }

                var pickerWindow = new KeyboardProfilePickerWindow(
                    sourceMonitorWorkArea,
                    installedLayouts,
                    currentSettings.Peek.SelectedLayoutKlids,
                    systemThemeProvider,
                    languageService);
                pickerWindow.ProfileSelected += OnKeyboardProfileSelected;
                pickerWindow.CustomCalibrationRequested +=
                    OnKeyboardCustomCalibrationRequested;
                pickerWindow.Closed += OnKeyboardProfilePickerWindowClosed;
                currentKeyboardProfilePickerWindow = pickerWindow;
                pickerWindow.ShowAndActivate();
            }

            void OnKeyboardProfileSelected(
                KeyboardProfile keyboardProfile,
                IReadOnlyList<string> selectedLayoutKlids)
            {
                if (keyboardProfile is not (KeyboardProfile.Ansi or KeyboardProfile.Iso))
                {
                    return;
                }

                var autoPeekEnabled = currentSettings.Peek.AutoPeekEnabled
                    || pendingKeyboardProfilePickerIntent ==
                        KeyboardProfilePickerIntent.EnableAutoPeek;
                var updatedSettings = currentSettings with
                {
                    Peek = currentSettings.Peek with
                    {
                        KeyboardProfile = keyboardProfile,
                        SelectedLayoutKlids = selectedLayoutKlids.ToArray(),
                        CustomKeyboardLayout = null,
                        AutoPeekEnabled = autoPeekEnabled,
                    },
                };

                try
                {
                    settingsStore.Save(updatedSettings);
                    OnSettingsChanged(updatedSettings);
                    currentKeyboardProfilePickerWindow?.Close();
                }
                catch (Exception exception) when (IsSettingsOperationException(exception))
                {
                    ShowNotificationWindow(
                        AppStrings.Notification_Title,
                        AppStrings.Settings_Save_Failed,
                        true);
                }
            }

            void OnKeyboardProfilePickerWindowClosed(object? sender, EventArgs eventArgs)
            {
                if (sender is not KeyboardProfilePickerWindow closedPickerWindow)
                {
                    return;
                }

                closedPickerWindow.ProfileSelected -= OnKeyboardProfileSelected;
                closedPickerWindow.CustomCalibrationRequested -=
                    OnKeyboardCustomCalibrationRequested;
                closedPickerWindow.Closed -= OnKeyboardProfilePickerWindowClosed;

                if (ReferenceEquals(currentKeyboardProfilePickerWindow, closedPickerWindow))
                {
                    currentKeyboardProfilePickerWindow = null;
                    pendingKeyboardProfilePickerIntent =
                        KeyboardProfilePickerIntent.SelectProfileOnly;
                }
            }

            void OnKeyboardCustomCalibrationRequested(
                IReadOnlyList<string> selectedLayoutKlids)
            {
                if (currentKeyboardProfilePickerWindow is null)
                {
                    return;
                }

                var sourceWindowToken = activeWindowFocusService.CaptureActiveWindow();
                var sourceMonitorWorkArea = activeWindowFocusService
                    .GetActiveMonitorWorkArea(sourceWindowToken);
                var pickerIntent = pendingKeyboardProfilePickerIntent;
                keyboardCalibrationSelectedLayoutKlids = selectedLayoutKlids.ToArray();

                currentKeyboardProfilePickerWindow.Close();
                ShowKeyboardCalibrationWizardWindow(
                    sourceMonitorWorkArea,
                    pickerIntent);
            }

            void ShowKeyboardCalibrationWizardWindow(
                System.Drawing.Rectangle sourceMonitorWorkArea,
                KeyboardProfilePickerIntent pickerIntent)
            {
                if (currentKeyboardCalibrationWizardWindow is not null)
                {
                    currentKeyboardCalibrationWizardWindow.ShowAndActivate();
                    return;
                }

                if (isHotkeyRecordingCaptureActive)
                {
                    ShowNotificationWindow(
                        AppStrings.Notification_Title,
                        AppStrings.KeyboardCalibration_CaptureFailed,
                        true);
                    return;
                }

                var controller = new KeyboardCalibrationController(
                    inactivityTimeout: TimeSpan.FromSeconds(
                        currentSettings.Peek.AutoExitTimeoutSeconds));
                var wizardWindow = new KeyboardCalibrationWizardWindow(
                    controller,
                    keyboardCalibrationSelectedLayoutKlids,
                    keyboardLayoutSymbolService,
                    systemThemeProvider,
                    languageService,
                    sourceMonitorWorkArea,
                    diagnosticsEnabled);
                currentKeyboardCalibrationController = controller;
                currentKeyboardCalibrationWizardWindow = wizardWindow;
                keyboardCalibrationPickerIntent = pickerIntent;
                controller.StatusChanged += OnKeyboardCalibrationStatusChanged;
                wizardWindow.CalibrationFinished += OnKeyboardCalibrationFinished;
                wizardWindow.Closed += OnKeyboardCalibrationWizardWindowClosed;
                keyboardCaptureService.KeyChanged += OnKeyboardCalibrationKeyChanged;
                isKeyboardCalibrationCaptureActive = true;

                keyboardCaptureService.UpdateBlockingAllEnabled(true);
                keyboardCaptureService.UpdateCalibrationModeEnabled(true);
                var startStatus = keyboardCaptureService.StartCapture();
                keyboardCalibrationStartedCapture =
                    startStatus == KeyboardCaptureStartStatus.Started;
                if (startStatus == KeyboardCaptureStartStatus.PlatformError)
                {
                    StopKeyboardCalibrationSession(closeWindow: true);
                    ShowNotificationWindow(
                        AppStrings.Notification_Title,
                        AppStrings.KeyboardCalibration_CaptureFailed,
                        true);
                    return;
                }

                Interlocked.Increment(ref autoPeekOverlayVersion);
                peekOverlayOwner = PeekOverlayOwner.None;
                peekOverlayKey = null;
                peekOverlayIdentity = null;
                overlayService.Hide();
                wizardWindow.ShowAndActivate();
            }

            void OnKeyboardCalibrationKeyChanged(
                object? sender,
                KeyboardCaptureEventArgs eventArgs)
            {
                var controller = currentKeyboardCalibrationController;
                var wizardWindow = currentKeyboardCalibrationWizardWindow;
                if (!isKeyboardCalibrationCaptureActive
                    || controller is null
                    || wizardWindow is null)
                {
                    return;
                }

                if (controller.Status != KeyboardCalibrationStatus.Active)
                {
                    return;
                }

                if (eventArgs.State == KeyboardKeyState.Down
                    && IsCalibrationEscape(eventArgs))
                {
                    wizardWindow.PauseFromCapture();
                    return;
                }

                controller.ProcessKeyEvent(eventArgs);
                wizardWindow.RefreshSnapshot();
            }

            void OnKeyboardCalibrationStatusChanged(object? sender, EventArgs eventArgs)
            {
                if (sender is not KeyboardCalibrationController controller
                    || application.Dispatcher.HasShutdownStarted)
                {
                    return;
                }

                if (application.Dispatcher.CheckAccess())
                {
                    ApplyKeyboardCalibrationStatus(controller);
                    return;
                }

                _ = application.Dispatcher.BeginInvoke(
                    () => ApplyKeyboardCalibrationStatus(controller));
            }

            void ApplyKeyboardCalibrationStatus(
                KeyboardCalibrationController controller)
            {
                var wizardWindow = currentKeyboardCalibrationWizardWindow;
                if (!isKeyboardCalibrationCaptureActive
                    || !ReferenceEquals(currentKeyboardCalibrationController, controller)
                    || wizardWindow is null)
                {
                    return;
                }

                if (controller.Status == KeyboardCalibrationStatus.Active)
                {
                    keyboardCaptureService.UpdateBlockingAllEnabled(true);
                    keyboardCaptureService.UpdateCalibrationModeEnabled(true);
                }
                else
                {
                    keyboardCaptureService.UpdateCalibrationModeEnabled(false);
                    keyboardCaptureService.UpdateBlockingAllEnabled(false);
                }

                wizardWindow.RefreshSnapshot();
            }

            void OnKeyboardCalibrationFinished(CustomKeyboardLayout layout)
            {
                var autoPeekEnabled = currentSettings.Peek.AutoPeekEnabled
                    || keyboardCalibrationPickerIntent ==
                        KeyboardProfilePickerIntent.EnableAutoPeek;
                var updatedSettings = currentSettings with
                {
                    Peek = currentSettings.Peek with
                    {
                        KeyboardProfile = KeyboardProfile.Custom,
                        SelectedLayoutKlids = keyboardCalibrationSelectedLayoutKlids,
                        CustomKeyboardLayout = layout,
                        AutoPeekEnabled = autoPeekEnabled,
                    },
                };

                try
                {
                    settingsStore.Save(updatedSettings);
                    currentKeyboardCalibrationWizardWindow?.Close();
                    OnSettingsChanged(updatedSettings);
                }
                catch (Exception exception) when (IsSettingsOperationException(exception))
                {
                    currentKeyboardCalibrationWizardWindow?.Close();
                    ShowNotificationWindow(
                        AppStrings.Notification_Title,
                        AppStrings.Settings_Save_Failed,
                        true);
                }
            }

            void OnKeyboardCalibrationWizardWindowClosed(
                object? sender,
                EventArgs eventArgs)
            {
                if (sender is KeyboardCalibrationWizardWindow closedWizardWindow
                    && ReferenceEquals(
                        currentKeyboardCalibrationWizardWindow,
                        closedWizardWindow))
                {
                    StopKeyboardCalibrationSession(closeWindow: false);
                }
            }

            void StopKeyboardCalibrationSession(bool closeWindow)
            {
                var wizardWindow = currentKeyboardCalibrationWizardWindow;
                var controller = currentKeyboardCalibrationController;
                currentKeyboardCalibrationWizardWindow = null;
                currentKeyboardCalibrationController = null;
                keyboardCalibrationPickerIntent =
                    KeyboardProfilePickerIntent.SelectProfileOnly;
                keyboardCalibrationSelectedLayoutKlids = [];

                if (wizardWindow is not null)
                {
                    wizardWindow.CalibrationFinished -= OnKeyboardCalibrationFinished;
                    wizardWindow.Closed -= OnKeyboardCalibrationWizardWindowClosed;
                }

                if (controller is not null)
                {
                    controller.StatusChanged -= OnKeyboardCalibrationStatusChanged;
                }

                try
                {
                    if (closeWindow && wizardWindow is not null)
                    {
                        wizardWindow.Close();
                    }
                }
                finally
                {
                    if (isKeyboardCalibrationCaptureActive)
                    {
                        isKeyboardCalibrationCaptureActive = false;
                        keyboardCaptureService.KeyChanged -=
                            OnKeyboardCalibrationKeyChanged;
                        keyboardCaptureService.UpdateCalibrationModeEnabled(false);
                        keyboardCaptureService.UpdateBlockingAllEnabled(false);

                        if (keyboardCalibrationStartedCapture)
                        {
                            _ = keyboardCaptureService.StopCapture();
                        }
                    }

                    keyboardCalibrationStartedCapture = false;
                    controller?.Dispose();
                }
            }

            static bool IsCalibrationEscape(KeyboardCaptureEventArgs eventArgs) =>
                eventArgs.Key == PhysicalKey.Escape
                || eventArgs.VirtualKey == 0x1B
                || (eventArgs.ScanCode == 0x01 && !eventArgs.Extended);

            void OnNotificationWindowClosed(object? sender, EventArgs eventArgs)
            {
                if (ReferenceEquals(currentNotificationWindow, sender))
                {
                    currentNotificationWindow = null;
                }
            }
        }
        finally
        {
            if (ownsSingleInstanceMutex)
            {
                singleInstanceMutex!.ReleaseMutex();
            }
        }
    }

    private static string FormatHotkeyRegistrationFailures(
        IEnumerable<(string FunctionName, GlobalHotkey Hotkey,
            GlobalHotkeyRegistrationStatus Status)> failures)
    {
        return string.Join(
            Environment.NewLine,
            failures
                .Select(static failure => FormatHotkeyRegistrationFailure(
                    failure.FunctionName,
                    failure.Hotkey,
                    failure.Status))
                .Append(AppStrings.Hotkey_ChangeShortcutHint));
    }

    private static string FormatHotkeyRegistrationFailure(
        string functionName,
        GlobalHotkey hotkey,
        GlobalHotkeyRegistrationStatus registrationStatus)
    {
        var messageFormat = registrationStatus switch
        {
            GlobalHotkeyRegistrationStatus.HotkeyUnavailable => AppStrings.Hotkey_Unavailable,
            GlobalHotkeyRegistrationStatus.UnsupportedHotkey => AppStrings.Hotkey_RegistrationFailed,
            GlobalHotkeyRegistrationStatus.PlatformError => AppStrings.Hotkey_RegistrationFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(registrationStatus)),
        };

        return string.Format(
            CultureInfo.CurrentCulture,
            messageFormat,
            functionName,
            FormatHotkey(hotkey));
    }

    private static string FormatHotkey(GlobalHotkey hotkey)
    {
        var separator = $" {AppStrings.Settings_Hotkey_Separator} ";
        return string.Join(
            separator,
            hotkey.Modifiers
                .Select(FormatHotkeyModifier)
                .Append(FormatHotkeyKey(hotkey.Key)));
    }

    private static string FormatHotkeyModifier(HotkeyModifier modifier) =>
        modifier switch
        {
            HotkeyModifier.Primary => AppStrings.Settings_Hotkey_Control,
            HotkeyModifier.Control => AppStrings.Settings_Hotkey_Control,
            HotkeyModifier.Alt => AppStrings.Settings_Hotkey_Alt,
            HotkeyModifier.Shift => AppStrings.Settings_Hotkey_Shift,
            HotkeyModifier.Meta => AppStrings.Settings_Hotkey_Meta,
            _ => throw new ArgumentOutOfRangeException(nameof(modifier)),
        };

    private static string FormatHotkeyKey(HotkeyKey key)
    {
        if (key is >= HotkeyKey.Digit0 and <= HotkeyKey.Digit9)
        {
            return ((int)key - (int)HotkeyKey.Digit0)
                .ToString(CultureInfo.InvariantCulture);
        }

        return key.ToString();
    }

    private static string FormatCorrectionHotkeyChangeFailure(
        CorrectionHotkeyRegistrationResult result)
    {
        if (result.HadPreviousRegistration && !result.PreviousRegistrationRestored)
        {
            return AppStrings.Settings_Hotkey_RestoreFailed;
        }

        return result.RegistrationStatus switch
        {
            GlobalHotkeyRegistrationStatus.HotkeyUnavailable =>
                AppStrings.Settings_Hotkey_Unavailable,
            GlobalHotkeyRegistrationStatus.UnsupportedHotkey or
                GlobalHotkeyRegistrationStatus.PlatformError =>
                AppStrings.Settings_Hotkey_RegistrationFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
    }

    private static string FormatPeekHotkeyChangeFailure(
        PeekHotkeyRegistrationResult result)
    {
        if (result.HadPreviousRegistration && !result.PreviousRegistrationRestored)
        {
            return AppStrings.Settings_Hotkey_RestoreFailed;
        }

        return result.RegistrationStatus switch
        {
            GlobalHotkeyRegistrationStatus.HotkeyUnavailable =>
                AppStrings.Settings_Hotkey_Unavailable,
            GlobalHotkeyRegistrationStatus.UnsupportedHotkey or
                GlobalHotkeyRegistrationStatus.PlatformError =>
                AppStrings.Settings_Hotkey_RegistrationFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };
    }

    private static string FormatFocusedElementEditability(
        FocusedElementEditability editability)
    {
        return editability switch
        {
            FocusedElementEditability.Editable => AppStrings.FocusedElement_Editable,
            FocusedElementEditability.ReadOnly => AppStrings.FocusedElement_ReadOnly,
            FocusedElementEditability.Unknown => AppStrings.FocusedElement_Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(editability)),
        };
    }

    private static string FormatFocusedElementDiagnosticResult(
        FocusedElementEditability editability,
        ActiveApplicationInfo activeApplication)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            AppStrings.FocusedElementDiagnostic_Result,
            FormatFocusedElementEditability(editability),
            FormatActiveApplicationIdentity(activeApplication),
            FormatTerminalStatus(activeApplication));
    }

    private static string FormatActiveApplicationIdentity(
        ActiveApplicationInfo activeApplication)
    {
        return activeApplication.Outcome switch
        {
            ActiveApplicationDetectionOutcome.Detected => activeApplication.ProcessName!,
            ActiveApplicationDetectionOutcome.Unavailable => AppStrings.ActiveApplication_Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(activeApplication)),
        };
    }

    private static string FormatTerminalStatus(ActiveApplicationInfo activeApplication)
    {
        if (activeApplication.Outcome == ActiveApplicationDetectionOutcome.Unavailable)
        {
            return AppStrings.ActiveApplication_TerminalUnknown;
        }

        return activeApplication.IsTerminal
            ? AppStrings.ActiveApplication_TerminalTrue
            : AppStrings.ActiveApplication_TerminalFalse;
    }

    private static string FormatLayoutCorrectionOutcome(LayoutCorrectionOutcome outcome)
    {
        return outcome switch
        {
            LayoutCorrectionOutcome.NoSelection => CoreStrings.Error_NoSelection,
            LayoutCorrectionOutcome.NotPlainText => CoreStrings.Error_UnsupportedText,
            LayoutCorrectionOutcome.ExceedsLimit => CoreStrings.Error_TextExceedsLimit,
            LayoutCorrectionOutcome.NothingToConvert => AppStrings.Correction_NothingToConvert,
            LayoutCorrectionOutcome.ClipboardUnavailable =>
                CoreStrings.Error_ClipboardUnavailable,
            LayoutCorrectionOutcome.InputError => AppStrings.Correction_InputError,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
    }

    private static bool IsWarning(LayoutCorrectionOutcome outcome) =>
        outcome is not LayoutCorrectionOutcome.NothingToConvert;

    private static bool IsAutoPeekEffectivelyEnabled(AppSettings settings) =>
        settings.Peek.PeekEnabled
        && settings.Peek.AutoPeekEnabled
        && settings.Peek.KeyboardProfile != KeyboardProfile.Unknown;

    private static AppSettings DisableAutoPeekForUnknownKeyboardProfile(
        AppSettings settings) =>
        settings.Peek.KeyboardProfile == KeyboardProfile.Unknown
            && settings.Peek.AutoPeekEnabled
                ? settings with
                {
                    Peek = settings.Peek with
                    {
                        AutoPeekEnabled = false,
                    },
                }
                : settings;

    private static void LoadBrandAccentResources(Application application)
    {
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "/BezBukv.App;component/Themes/BrandAccentResources.xaml",
                UriKind.Relative),
        });
    }

    private static void ApplyBrandAccentResources(
        Application application,
        bool useDarkPalette)
    {
        var accentColor = (Color)application.FindResource(
            useDarkPalette
                ? BrandAccentDarkColorKey
                : BrandAccentLightColorKey);

        foreach (var resourceKey in FluentSystemAccentResourceKeys)
        {
            application.Resources[resourceKey] = accentColor;
        }

        foreach (var resourceKey in FluentSystemAccentColorResourceKeys)
        {
            application.Resources[resourceKey] = accentColor;
        }

        foreach (var (resourceKey, opacity) in FluentAccentBrushResources)
        {
            var brush = new SolidColorBrush(accentColor)
            {
                Opacity = opacity,
            };
            brush.Freeze();
            application.Resources[resourceKey] = brush;
        }
    }

#pragma warning disable WPF0001
    private static ThemeMode GetThemeMode(AppTheme theme) =>
        theme switch
        {
            AppTheme.System => ThemeMode.System,
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => throw new ArgumentOutOfRangeException(nameof(theme)),
        };
#pragma warning restore WPF0001

    private static CustomKeyboardLayout? GetCustomHoldPeekLayout(
        PeekSettings peekSettings) =>
        peekSettings.KeyboardProfile == KeyboardProfile.Custom
            ? peekSettings.CustomKeyboardLayout
            : null;

    private static bool IsSettingsOperationException(Exception exception) =>
        exception is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or SecurityException;

    private enum PeekOverlayOwner
    {
        None,
        AutoPeek,
        HotkeyPeek,
    }

    private enum KeyboardProfilePickerIntent
    {
        SelectProfileOnly,
        EnableAutoPeek,
    }

    private sealed class KeyboardCaptureArbiter : IKeyboardCaptureService
    {
        private readonly WindowsKeyboardCaptureService _captureService;
        private readonly object _sync = new();
        private bool _blockNumberRow;
        private bool _showServiceKeys;
        private bool _autoPeekCaptureRequired;
        private int _isHotkeyCaptureActive;
        private int _isRecordingCaptureActive;
        private bool _disposed;

        public KeyboardCaptureArbiter(
            WindowsKeyboardCaptureService captureService,
            bool blockNumberRow,
            bool showServiceKeys)
        {
            ArgumentNullException.ThrowIfNull(captureService);

            _captureService = captureService;
            _blockNumberRow = blockNumberRow;
            _showServiceKeys = showServiceKeys;
            _captureService.UpdateBlockingAllEnabled(false);
            _captureService.UpdateBlockingEnabled(false);
            _captureService.KeyChanged += OnKeyChanged;
        }

        public event EventHandler<KeyboardCaptureEventArgs>? KeyChanged;

        public bool IsCapturing => Volatile.Read(ref _isHotkeyCaptureActive) != 0;

        public void UpdateActivationChordKeys(IReadOnlySet<PhysicalKey> keys)
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _captureService.UpdateActivationChordKeys(keys);
            }
        }

        public bool IsActivationChordHeld() =>
            _captureService.IsActivationChordHeld();

        public KeyboardCaptureStartStatus StartCapture()
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (IsCapturing)
                {
                    return KeyboardCaptureStartStatus.AlreadyCapturing;
                }

                _captureService.UpdateBlockingOptions(
                    _blockNumberRow,
                    _showServiceKeys);
                _captureService.UpdateBlockingEnabled(true);

                var startStatus = _captureService.StartCapture();
                if (startStatus == KeyboardCaptureStartStatus.PlatformError)
                {
                    _captureService.UpdateBlockingEnabled(false);
                    return startStatus;
                }

                Volatile.Write(ref _isHotkeyCaptureActive, 1);
                return KeyboardCaptureStartStatus.Started;
            }
        }

        public KeyboardCaptureStopStatus StopCapture()
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (!IsCapturing)
                {
                    return KeyboardCaptureStopStatus.AlreadyStopped;
                }

                if (_autoPeekCaptureRequired || IsRecordingCaptureActive)
                {
                    Volatile.Write(ref _isHotkeyCaptureActive, 0);
                    _captureService.UpdateBlockingEnabled(false);
                    return KeyboardCaptureStopStatus.Stopped;
                }

                var stopStatus = _captureService.StopCapture();
                if (stopStatus is KeyboardCaptureStopStatus.Stopped or
                    KeyboardCaptureStopStatus.AlreadyStopped)
                {
                    Volatile.Write(ref _isHotkeyCaptureActive, 0);
                    _captureService.UpdateBlockingEnabled(false);
                }

                return stopStatus;
            }
        }

        public void SetAutoPeekCaptureRequired(bool isRequired)
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (_autoPeekCaptureRequired == isRequired)
                {
                    return;
                }

                _autoPeekCaptureRequired = isRequired;
                if (isRequired)
                {
                    if (!IsCapturing)
                    {
                        _captureService.UpdateBlockingEnabled(false);
                    }

                    _ = _captureService.StartCapture();
                    return;
                }

                if (IsCapturing)
                {
                    return;
                }

                if (IsRecordingCaptureActive)
                {
                    return;
                }

                _captureService.UpdateBlockingEnabled(false);
                _ = _captureService.StopCapture();
            }
        }

        public KeyboardCaptureStartStatus StartRecordingCapture()
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (IsRecordingCaptureActive)
                {
                    return KeyboardCaptureStartStatus.AlreadyCapturing;
                }

                _captureService.UpdateBlockingAllEnabled(true);
                var startStatus = _captureService.StartCapture();
                if (startStatus == KeyboardCaptureStartStatus.PlatformError)
                {
                    _captureService.UpdateBlockingAllEnabled(false);
                    return startStatus;
                }

                Volatile.Write(ref _isRecordingCaptureActive, 1);
                return startStatus;
            }
        }

        public KeyboardCaptureStopStatus StopRecordingCapture()
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (!IsRecordingCaptureActive)
                {
                    return KeyboardCaptureStopStatus.AlreadyStopped;
                }

                Volatile.Write(ref _isRecordingCaptureActive, 0);
                _captureService.UpdateBlockingAllEnabled(false);

                if (IsCapturing)
                {
                    _captureService.UpdateBlockingEnabled(true);
                    return KeyboardCaptureStopStatus.Stopped;
                }

                _captureService.UpdateBlockingEnabled(false);
                if (_autoPeekCaptureRequired)
                {
                    return KeyboardCaptureStopStatus.Stopped;
                }

                return _captureService.StopCapture();
            }
        }

        public KeyboardCaptureStopStatus StopAllCapture()
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                _autoPeekCaptureRequired = false;
                _captureService.UpdateBlockingEnabled(false);

                if (IsRecordingCaptureActive)
                {
                    Volatile.Write(ref _isHotkeyCaptureActive, 0);
                    return KeyboardCaptureStopStatus.Stopped;
                }

                var stopStatus = _captureService.StopCapture();
                if (stopStatus is KeyboardCaptureStopStatus.Stopped or
                    KeyboardCaptureStopStatus.AlreadyStopped)
                {
                    Volatile.Write(ref _isHotkeyCaptureActive, 0);
                }

                return stopStatus;
            }
        }

        public void UpdateBlockingOptions(
            bool blockNumberRow,
            bool showServiceKeys)
        {
            lock (_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                _blockNumberRow = blockNumberRow;
                _showServiceKeys = showServiceKeys;
                if (IsCapturing)
                {
                    _captureService.UpdateBlockingOptions(
                        blockNumberRow,
                        showServiceKeys);
                }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _autoPeekCaptureRequired = false;
                Volatile.Write(ref _isHotkeyCaptureActive, 0);
                Volatile.Write(ref _isRecordingCaptureActive, 0);
                _captureService.UpdateBlockingAllEnabled(false);
                _captureService.UpdateBlockingEnabled(false);
                _ = _captureService.StopCapture();
                _captureService.KeyChanged -= OnKeyChanged;
                KeyChanged = null;
                _disposed = true;
            }

            GC.SuppressFinalize(this);
        }

        private void OnKeyChanged(
            object? sender,
            KeyboardCaptureEventArgs eventArgs)
        {
            if (IsRecordingCaptureActive || eventArgs.Key == PhysicalKey.Unknown)
            {
                return;
            }

            KeyChanged?.Invoke(this, eventArgs);
        }

        private bool IsRecordingCaptureActive =>
            Volatile.Read(ref _isRecordingCaptureActive) != 0;
    }

}

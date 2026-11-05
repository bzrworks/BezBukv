using BezBukv.Core.Platform;
using BezBukv.Core.Scenarios;

namespace BezBukv.Core.Tests;

public sealed class KeyboardPeekModeControllerTests
{
    private static readonly GlobalHotkey DefaultActivationHotkey = new(
        HotkeyKey.F8,
        [HotkeyModifier.Primary]);

    [Fact]
    public void ActivationStartsCaptureAndArmsSessionTimers()
    {
        var captureService = new FakeKeyboardCaptureService();
        var timeProvider = new ManualTimeProvider();
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey,
            timeProvider: timeProvider);

        var outcome = controller.Activate();

        Assert.Equal(KeyboardPeekModeActivationOutcome.Activated, outcome);
        Assert.True(controller.IsActive);
        Assert.Equal(1, captureService.StartCallCount);
        Assert.Equal(0, captureService.StopCallCount);
        Assert.Equal(2, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void RepeatedActivationDoesNotExitOrResetSafetyTimeout()
    {
        var captureService = new FakeKeyboardCaptureService();
        var timeProvider = new ManualTimeProvider();
        var timeout = TimeSpan.FromSeconds(10);
        captureService.SetPhysicallyDownKeys(
            PhysicalKey.F8,
            PhysicalKey.ControlLeft);
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey,
            timeout,
            timeProvider);
        _ = controller.Activate();
        timeProvider.Advance(TimeSpan.FromSeconds(6));

        var outcome = controller.Activate();
        timeProvider.Advance(TimeSpan.FromSeconds(4));

        Assert.Equal(KeyboardPeekModeActivationOutcome.AlreadyActive, outcome);
        Assert.False(controller.IsActive);
        Assert.Equal(1, captureService.StartCallCount);
        Assert.Equal(1, captureService.StopCallCount);
    }

    [Theory]
    [InlineData(PhysicalKey.F8)]
    [InlineData(PhysicalKey.ControlLeft)]
    [InlineData(PhysicalKey.ControlRight)]
    public void ActivationChordKeyUpStopsActiveCapture(PhysicalKey releasedKey)
    {
        var captureService = new FakeKeyboardCaptureService();
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey);
        _ = controller.Activate();

        captureService.RaiseKeyChanged(releasedKey, KeyboardKeyState.Up);

        Assert.False(controller.IsActive);
        Assert.Equal(1, captureService.StopCallCount);
    }

    [Fact]
    public void ActivationChordKeyDownDoesNotStopActiveCapture()
    {
        var captureService = new FakeKeyboardCaptureService();
        captureService.SetPhysicallyDownKeys(
            PhysicalKey.F8,
            PhysicalKey.ControlLeft);
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey);
        _ = controller.Activate();

        captureService.RaiseKeyChanged(PhysicalKey.F8, KeyboardKeyState.Down);
        captureService.RaiseKeyChanged(PhysicalKey.ControlLeft, KeyboardKeyState.Down);

        Assert.True(controller.IsActive);
        Assert.Equal(0, captureService.StopCallCount);
    }

    [Fact]
    public void ConfiguredActivationChordControlsReleaseDetection()
    {
        var captureService = new FakeKeyboardCaptureService();
        using var controller = new KeyboardPeekModeController(
            captureService,
            new GlobalHotkey(HotkeyKey.F, [HotkeyModifier.Alt]));
        _ = controller.Activate();

        captureService.RaiseKeyChanged(PhysicalKey.F8, KeyboardKeyState.Up);
        Assert.True(controller.IsActive);

        captureService.RaiseKeyChanged(PhysicalKey.AltRight, KeyboardKeyState.Up);

        Assert.False(controller.IsActive);
        Assert.Equal(1, captureService.StopCallCount);
    }

    [Fact]
    public void EscapeAndUnrelatedKeyEventsDoNotStopActiveCapture()
    {
        var captureService = new FakeKeyboardCaptureService();
        captureService.SetPhysicallyDownKeys(
            PhysicalKey.F8,
            PhysicalKey.ControlLeft);
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey);
        _ = controller.Activate();

        captureService.RaiseKeyChanged(PhysicalKey.Escape, KeyboardKeyState.Down);
        captureService.RaiseKeyChanged(PhysicalKey.Escape, KeyboardKeyState.Up);
        captureService.RaiseKeyChanged(PhysicalKey.KeyA, KeyboardKeyState.Down);
        captureService.RaiseKeyChanged(PhysicalKey.KeyA, KeyboardKeyState.Up);

        Assert.True(controller.IsActive);
        Assert.Equal(0, captureService.StopCallCount);
    }

    [Fact]
    public void HeldChordKeepsSessionAliveAcrossPollsAndOldTimeoutThreshold()
    {
        var captureService = new FakeKeyboardCaptureService();
        captureService.SetPhysicallyDownKeys(
            PhysicalKey.F8,
            PhysicalKey.ControlLeft);
        var timeProvider = new ManualTimeProvider();
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey,
            timeProvider: timeProvider);
        _ = controller.Activate();

        timeProvider.Advance(TimeSpan.FromMilliseconds(150));
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));
        timeProvider.Advance(TimeSpan.FromSeconds(15));

        Assert.True(controller.IsActive);
        Assert.Equal(0, captureService.StopCallCount);
        Assert.Equal(4, captureService.IsActivationChordHeldCallCount);
        Assert.Equal(2, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void LivenessPollStopsCaptureWhenChordIsNotHeld()
    {
        var captureService = new FakeKeyboardCaptureService();
        var timeProvider = new ManualTimeProvider();
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey,
            timeProvider: timeProvider);
        _ = controller.Activate();

        timeProvider.Advance(TimeSpan.FromMilliseconds(149));
        Assert.True(controller.IsActive);

        timeProvider.Advance(TimeSpan.FromMilliseconds(1));

        Assert.False(controller.IsActive);
        Assert.Equal(1, captureService.IsActivationChordHeldCallCount);
        Assert.Equal(1, captureService.StopCallCount);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void MissingKeyUpEventIsRecoveredByLivenessPoll()
    {
        var captureService = new FakeKeyboardCaptureService();
        captureService.SetPhysicallyDownKeys(
            PhysicalKey.F8,
            PhysicalKey.ControlRight);
        var timeProvider = new ManualTimeProvider();
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey,
            timeProvider: timeProvider);
        _ = controller.Activate();

        captureService.SetPhysicallyDownKeys();
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        Assert.False(controller.IsActive);
        Assert.Equal(1, captureService.IsActivationChordHeldCallCount);
        Assert.Equal(1, captureService.StopCallCount);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void FallbackTimeoutStopsCaptureWhenLivenessStaysHeld()
    {
        var captureService = new FakeKeyboardCaptureService();
        captureService.SetPhysicallyDownKeys(
            PhysicalKey.F8,
            PhysicalKey.ControlLeft);
        var timeProvider = new ManualTimeProvider();
        var timeout = TimeSpan.FromSeconds(10);
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey,
            timeout,
            timeProvider);
        _ = controller.Activate();

        timeProvider.Advance(timeout - TimeSpan.FromMilliseconds(1));
        Assert.True(controller.IsActive);

        timeProvider.Advance(TimeSpan.FromMilliseconds(1));

        Assert.False(controller.IsActive);
        Assert.True(captureService.IsActivationChordHeldCallCount > 0);
        Assert.Equal(1, captureService.StopCallCount);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void ReleaseExitCancelsSafetyTimeoutAndFurtherEventsDoNotStopAgain()
    {
        var captureService = new FakeKeyboardCaptureService();
        var timeProvider = new ManualTimeProvider();
        var timeout = TimeSpan.FromSeconds(10);
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey,
            timeout,
            timeProvider);
        _ = controller.Activate();

        captureService.RaiseKeyChanged(PhysicalKey.F8, KeyboardKeyState.Up);
        var pollCallCountAfterRelease = captureService.IsActivationChordHeldCallCount;
        timeProvider.Advance(timeout);
        captureService.RaiseKeyChanged(PhysicalKey.ControlLeft, KeyboardKeyState.Up);

        Assert.False(controller.IsActive);
        Assert.Equal(1, captureService.StopCallCount);
        Assert.Equal(
            pollCallCountAfterRelease,
            captureService.IsActivationChordHeldCallCount);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void FailedStartLeavesModeInactive()
    {
        var captureService = new FakeKeyboardCaptureService
        {
            StartStatus = KeyboardCaptureStartStatus.PlatformError,
        };
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey);

        var outcome = controller.Activate();

        Assert.Equal(KeyboardPeekModeActivationOutcome.ActivationFailed, outcome);
        Assert.False(controller.IsActive);
        Assert.Equal(0, captureService.StopCallCount);
    }

    [Fact]
    public void FailedReleaseStopLeavesModeActiveForSafetyTimeoutFallback()
    {
        var captureService = new FakeKeyboardCaptureService
        {
            StopStatus = KeyboardCaptureStopStatus.PlatformError,
        };
        var timeProvider = new ManualTimeProvider();
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey,
            timeProvider: timeProvider);
        _ = controller.Activate();

        captureService.RaiseKeyChanged(PhysicalKey.F8, KeyboardKeyState.Up);

        Assert.True(controller.IsActive);
        Assert.Equal(1, captureService.StopCallCount);
        Assert.Equal(2, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void DisposeStopsActiveCaptureOnce()
    {
        var captureService = new FakeKeyboardCaptureService();
        var timeProvider = new ManualTimeProvider();
        captureService.SetPhysicallyDownKeys(
            PhysicalKey.F8,
            PhysicalKey.ControlLeft);
        var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey,
            timeProvider: timeProvider);
        _ = controller.Activate();

        controller.Dispose();
        controller.Dispose();
        var pollCallCountAfterDispose = captureService.IsActivationChordHeldCallCount;
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        Assert.False(captureService.IsCapturing);
        Assert.Equal(1, captureService.StopCallCount);
        Assert.Equal(0, captureService.KeyChangedSubscriberCount);
        Assert.Equal(
            pollCallCountAfterDispose,
            captureService.IsActivationChordHeldCallCount);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
        Assert.Throws<ObjectDisposedException>(() => controller.Activate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveAutoExitTimeoutIsRejected(int timeoutMilliseconds)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new KeyboardPeekModeController(
                new FakeKeyboardCaptureService(),
                DefaultActivationHotkey,
                TimeSpan.FromMilliseconds(timeoutMilliseconds)));

        Assert.Equal("autoExitTimeout", exception.ParamName);
    }

    [Fact]
    public void NullActivationHotkeyDisablesActivation()
    {
        var captureService = new FakeKeyboardCaptureService();
        using var controller = new KeyboardPeekModeController(
            captureService,
            activationHotkey: null);

        var outcome = controller.Activate();

        Assert.Equal(KeyboardPeekModeActivationOutcome.ActivationFailed, outcome);
        Assert.False(controller.IsActive);
        Assert.Equal(0, captureService.StartCallCount);
    }

    [Fact]
    public void ClearingHotkeyKeepsHoldModeInactiveDuringSharedCapture()
    {
        var captureService = new FakeKeyboardCaptureService
        {
            StartStatus = KeyboardCaptureStartStatus.AlreadyCapturing,
        };
        _ = captureService.StartCapture();
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey);

        controller.UpdateActivationHotkey(activationHotkey: null);

        Assert.False(controller.IsActive);
        Assert.True(captureService.IsCapturing);
        Assert.Equal(0, captureService.StopCallCount);
    }

    [Fact]
    public void NonNullHotkeyActivatesDuringSharedCapture()
    {
        var captureService = new FakeKeyboardCaptureService
        {
            StartStatus = KeyboardCaptureStartStatus.AlreadyCapturing,
        };
        _ = captureService.StartCapture();
        var timeProvider = new ManualTimeProvider();
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey,
            timeProvider: timeProvider);

        var outcome = controller.Activate();

        Assert.Equal(KeyboardPeekModeActivationOutcome.Activated, outcome);
        Assert.True(controller.IsActive);
        Assert.Equal(2, captureService.StartCallCount);
        Assert.Equal(2, timeProvider.ActiveTimerCount);

        captureService.RaiseKeyChanged(PhysicalKey.F8, KeyboardKeyState.Up);

        Assert.False(controller.IsActive);
        Assert.Equal(1, captureService.StopCallCount);
    }

    [Theory]
    [InlineData(HotkeyKey.A, PhysicalKey.KeyA)]
    [InlineData(HotkeyKey.Digit0, PhysicalKey.Digit0)]
    [InlineData(HotkeyKey.F24, PhysicalKey.F24)]
    public void ActivationChordIsDerivedFromGlobalHotkey(
        HotkeyKey hotkeyKey,
        PhysicalKey expectedPhysicalKey)
    {
        var captureService = new FakeKeyboardCaptureService();
        using var controller = new KeyboardPeekModeController(
            captureService,
            new GlobalHotkey(
                hotkeyKey,
                [HotkeyModifier.Primary, HotkeyModifier.Shift]));

        Assert.True(controller.IsActivationChordKey(expectedPhysicalKey));
        Assert.True(controller.IsActivationChordKey(PhysicalKey.ControlLeft));
        Assert.True(controller.IsActivationChordKey(PhysicalKey.ControlRight));
        Assert.True(controller.IsActivationChordKey(PhysicalKey.ShiftLeft));
        Assert.True(controller.IsActivationChordKey(PhysicalKey.ShiftRight));
        Assert.False(controller.IsActivationChordKey(PhysicalKey.AltLeft));
        Assert.True(captureService.ActivationChordKeys.SetEquals(
            [
                expectedPhysicalKey,
                PhysicalKey.ControlLeft,
                PhysicalKey.ControlRight,
                PhysicalKey.ShiftLeft,
                PhysicalKey.ShiftRight,
            ]));
    }

    [Theory]
    [InlineData(PhysicalKey.ControlLeft)]
    [InlineData(PhysicalKey.ControlRight)]
    public void ActivationChordIsHeldWithEitherModifierSide(
        PhysicalKey heldControlKey)
    {
        var captureService = new FakeKeyboardCaptureService();
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey);
        captureService.SetPhysicallyDownKeys(PhysicalKey.F8, heldControlKey);

        Assert.True(captureService.IsActivationChordHeld());
    }

    [Theory]
    [InlineData(PhysicalKey.F8)]
    [InlineData(PhysicalKey.ControlLeft)]
    public void ActivationChordIsNotHeldWhenARequiredPartIsUp(
        PhysicalKey heldKey)
    {
        var captureService = new FakeKeyboardCaptureService();
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey);
        captureService.SetPhysicallyDownKeys(heldKey);

        Assert.False(captureService.IsActivationChordHeld());
    }

    [Fact]
    public void ActivationChordRequiresEveryModifierGroup()
    {
        var captureService = new FakeKeyboardCaptureService();
        using var controller = new KeyboardPeekModeController(
            captureService,
            new GlobalHotkey(
                HotkeyKey.F8,
                [HotkeyModifier.Primary, HotkeyModifier.Shift]));
        captureService.SetPhysicallyDownKeys(
            PhysicalKey.F8,
            PhysicalKey.ControlRight);

        Assert.False(captureService.IsActivationChordHeld());

        captureService.SetPhysicallyDownKeys(
            PhysicalKey.F8,
            PhysicalKey.ControlRight,
            PhysicalKey.ShiftLeft);

        Assert.True(captureService.IsActivationChordHeld());
    }

    [Fact]
    public void EmptyActivationChordIsNotHeld()
    {
        var captureService = new FakeKeyboardCaptureService();
        using var controller = new KeyboardPeekModeController(
            captureService,
            activationHotkey: null);
        captureService.SetPhysicallyDownKeys(
            PhysicalKey.F8,
            PhysicalKey.ControlLeft);

        Assert.False(captureService.IsActivationChordHeld());
    }

    [Fact]
    public void HotkeyUpdateCancelsSessionTimersAndPolling()
    {
        var captureService = new FakeKeyboardCaptureService();
        captureService.SetPhysicallyDownKeys(
            PhysicalKey.F8,
            PhysicalKey.ControlLeft);
        var timeProvider = new ManualTimeProvider();
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey,
            timeProvider: timeProvider);
        _ = controller.Activate();

        controller.UpdateActivationHotkey(
            new GlobalHotkey(HotkeyKey.F9, [HotkeyModifier.Primary]));
        var pollCallCountAfterUpdate = captureService.IsActivationChordHeldCallCount;
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        Assert.False(controller.IsActive);
        Assert.Equal(1, captureService.StopCallCount);
        Assert.Equal(
            pollCallCountAfterUpdate,
            captureService.IsActivationChordHeldCallCount);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void ExternalCaptureStopCancelsSessionTimersOnNextPoll()
    {
        var captureService = new FakeKeyboardCaptureService();
        captureService.SetPhysicallyDownKeys(
            PhysicalKey.F8,
            PhysicalKey.ControlLeft);
        var timeProvider = new ManualTimeProvider();
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey,
            timeProvider: timeProvider);
        _ = controller.Activate();

        captureService.SimulateExternalCaptureStop();
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));
        var pollCallCountAfterTeardown = captureService.IsActivationChordHeldCallCount;
        timeProvider.Advance(TimeSpan.FromMinutes(5));

        Assert.False(controller.IsActive);
        Assert.Equal(0, captureService.StopCallCount);
        Assert.Equal(
            pollCallCountAfterTeardown,
            captureService.IsActivationChordHeldCallCount);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void UpdatedActivationHotkeyControlsReleaseDetection()
    {
        var captureService = new FakeKeyboardCaptureService();
        using var controller = new KeyboardPeekModeController(
            captureService,
            DefaultActivationHotkey);
        _ = controller.Activate();

        controller.UpdateActivationHotkey(
            new GlobalHotkey(HotkeyKey.F9, [HotkeyModifier.Primary]));

        Assert.False(controller.IsActive);
        Assert.Equal(1, captureService.StopCallCount);
        Assert.False(controller.IsActivationChordKey(PhysicalKey.F8));
        Assert.True(controller.IsActivationChordKey(PhysicalKey.F9));
        Assert.True(captureService.ActivationChordKeys.SetEquals(
            [
                PhysicalKey.F9,
                PhysicalKey.ControlLeft,
                PhysicalKey.ControlRight,
            ]));

        _ = controller.Activate();
        captureService.RaiseKeyChanged(PhysicalKey.F8, KeyboardKeyState.Up);
        Assert.True(controller.IsActive);

        captureService.RaiseKeyChanged(PhysicalKey.F9, KeyboardKeyState.Up);

        Assert.False(controller.IsActive);
        Assert.Equal(2, captureService.StopCallCount);
    }

    [Fact]
    public void CaptureEventCarriesPortablePhysicalKeyAndState()
    {
        var captureService = new FakeKeyboardCaptureService();
        KeyboardCaptureEventArgs? receivedEvent = null;
        captureService.KeyChanged += (_, eventArgs) => receivedEvent = eventArgs;

        captureService.RaiseKeyChanged(PhysicalKey.KeyF, KeyboardKeyState.Down);

        Assert.NotNull(receivedEvent);
        Assert.Equal(PhysicalKey.KeyF, receivedEvent.Key);
        Assert.Equal(KeyboardKeyState.Down, receivedEvent.State);
    }

    [Fact]
    public void CalibrationModeCarriesRawCodesForKnownAndUnknownPhysicalKeys()
    {
        var captureService = new FakeKeyboardCaptureService();
        var receivedEvents = new List<KeyboardCaptureEventArgs>();
        captureService.KeyChanged += (_, eventArgs) => receivedEvents.Add(eventArgs);

        captureService.RaiseRawKeyChanged(
            PhysicalKey.Unknown,
            KeyboardKeyState.Down,
            scanCode: 0x56,
            extended: false,
            virtualKey: 0xE2);

        Assert.Empty(receivedEvents);

        captureService.UpdateCalibrationModeEnabled(true);
        captureService.RaiseRawKeyChanged(
            PhysicalKey.ArrowLeft,
            KeyboardKeyState.Down,
            scanCode: 0x4B,
            extended: true,
            virtualKey: 0x25);
        captureService.RaiseRawKeyChanged(
            PhysicalKey.Unknown,
            KeyboardKeyState.Up,
            scanCode: 0x56,
            extended: false,
            virtualKey: 0xE2);

        Assert.Collection(
            receivedEvents,
            eventArgs =>
            {
                Assert.Equal(PhysicalKey.ArrowLeft, eventArgs.Key);
                Assert.Equal(KeyboardKeyState.Down, eventArgs.State);
                Assert.Equal(0x4B, eventArgs.ScanCode);
                Assert.True(eventArgs.Extended);
                Assert.Equal(0x25, eventArgs.VirtualKey);
            },
            eventArgs =>
            {
                Assert.Equal(PhysicalKey.Unknown, eventArgs.Key);
                Assert.Equal(KeyboardKeyState.Up, eventArgs.State);
                Assert.Equal(0x56, eventArgs.ScanCode);
                Assert.False(eventArgs.Extended);
                Assert.Equal(0xE2, eventArgs.VirtualKey);
            });
    }

    private sealed class FakeKeyboardCaptureService : IKeyboardCaptureService
    {
        private EventHandler<KeyboardCaptureEventArgs>? _keyChanged;

        public event EventHandler<KeyboardCaptureEventArgs>? KeyChanged
        {
            add => _keyChanged += value;
            remove => _keyChanged -= value;
        }

        public KeyboardCaptureStartStatus StartStatus { get; init; } =
            KeyboardCaptureStartStatus.Started;

        public KeyboardCaptureStopStatus StopStatus { get; init; } =
            KeyboardCaptureStopStatus.Stopped;

        public bool IsCapturing { get; private set; }

        public bool IsCalibrationModeEnabled { get; private set; }

        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public int IsActivationChordHeldCallCount { get; private set; }

        public IReadOnlySet<PhysicalKey> ActivationChordKeys { get; private set; } =
            new HashSet<PhysicalKey>();

        public IReadOnlySet<PhysicalKey> PhysicallyDownKeys { get; private set; } =
            new HashSet<PhysicalKey>();

        public int KeyChangedSubscriberCount =>
            _keyChanged?.GetInvocationList().Length ?? 0;

        public void RaiseKeyChanged(PhysicalKey key, KeyboardKeyState state)
        {
            RaiseRawKeyChanged(key, state, 0, false, 0);
        }

        public void RaiseRawKeyChanged(
            PhysicalKey key,
            KeyboardKeyState state,
            int scanCode,
            bool extended,
            int virtualKey)
        {
            if (key == PhysicalKey.Unknown && !IsCalibrationModeEnabled)
            {
                return;
            }

            _keyChanged?.Invoke(
                this,
                new KeyboardCaptureEventArgs(
                    key,
                    state,
                    scanCode,
                    extended,
                    virtualKey));
        }

        public void UpdateActivationChordKeys(IReadOnlySet<PhysicalKey> keys)
        {
            ActivationChordKeys = new HashSet<PhysicalKey>(keys);
        }

        public void UpdateCalibrationModeEnabled(bool isEnabled)
        {
            IsCalibrationModeEnabled = isEnabled;
        }

        public bool IsActivationChordHeld()
        {
            IsActivationChordHeldCallCount++;

            if (ActivationChordKeys.Count == 0 ||
                !IsModifierGroupHeld(PhysicalKey.ControlLeft, PhysicalKey.ControlRight) ||
                !IsModifierGroupHeld(PhysicalKey.AltLeft, PhysicalKey.AltRight) ||
                !IsModifierGroupHeld(PhysicalKey.ShiftLeft, PhysicalKey.ShiftRight) ||
                !IsModifierGroupHeld(PhysicalKey.MetaLeft, PhysicalKey.MetaRight))
            {
                return false;
            }

            var hasMainKey = false;
            foreach (var key in ActivationChordKeys)
            {
                if (IsModifierKey(key))
                {
                    continue;
                }

                hasMainKey = true;
                if (!PhysicallyDownKeys.Contains(key))
                {
                    return false;
                }
            }

            return hasMainKey;
        }

        public void SetPhysicallyDownKeys(params PhysicalKey[] keys)
        {
            PhysicallyDownKeys = new HashSet<PhysicalKey>(keys);
        }

        public void SimulateExternalCaptureStop()
        {
            IsCapturing = false;
        }

        public KeyboardCaptureStartStatus StartCapture()
        {
            StartCallCount++;
            if (StartStatus is KeyboardCaptureStartStatus.Started or
                KeyboardCaptureStartStatus.AlreadyCapturing)
            {
                IsCapturing = true;
            }

            return StartStatus;
        }

        public KeyboardCaptureStopStatus StopCapture()
        {
            StopCallCount++;
            if (StopStatus is KeyboardCaptureStopStatus.Stopped or
                KeyboardCaptureStopStatus.AlreadyStopped)
            {
                IsCapturing = false;
            }

            return StopStatus;
        }

        public void Dispose()
        {
            IsCapturing = false;
            _keyChanged = null;
        }

        private bool IsModifierGroupHeld(PhysicalKey leftKey, PhysicalKey rightKey) =>
            (!ActivationChordKeys.Contains(leftKey) &&
             !ActivationChordKeys.Contains(rightKey)) ||
            PhysicallyDownKeys.Contains(leftKey) ||
            PhysicallyDownKeys.Contains(rightKey);

        private static bool IsModifierKey(PhysicalKey key) =>
            key is PhysicalKey.ControlLeft or PhysicalKey.ControlRight or
                PhysicalKey.AltLeft or PhysicalKey.AltRight or
                PhysicalKey.ShiftLeft or PhysicalKey.ShiftRight or
                PhysicalKey.MetaLeft or PhysicalKey.MetaRight;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];

        public int ActiveTimerCount => _timers.Count(timer => timer.IsActive);

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, dueTime, period);
            _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan elapsed)
        {
            foreach (var timer in _timers.ToArray())
            {
                timer.Advance(elapsed);
            }
        }

        private sealed class ManualTimer : ITimer
        {
            private readonly TimerCallback _callback;
            private readonly object? _state;
            private TimeSpan _remaining;
            private TimeSpan _period;
            private bool _disposed;

            public ManualTimer(
                TimerCallback callback,
                object? state,
                TimeSpan dueTime,
                TimeSpan period)
            {
                _callback = callback;
                _state = state;
                _remaining = dueTime;
                _period = period;
            }

            public bool IsActive =>
                !_disposed && _remaining != Timeout.InfiniteTimeSpan;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (_disposed)
                {
                    return false;
                }

                _remaining = dueTime;
                _period = period;
                return true;
            }

            public void Advance(TimeSpan elapsed)
            {
                if (!IsActive)
                {
                    return;
                }

                _remaining -= elapsed;
                if (_remaining > TimeSpan.Zero)
                {
                    return;
                }

                _callback(_state);
                if (_disposed)
                {
                    return;
                }

                _remaining = _period;
            }

            public void Dispose()
            {
                _disposed = true;
                _remaining = Timeout.InfiniteTimeSpan;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}

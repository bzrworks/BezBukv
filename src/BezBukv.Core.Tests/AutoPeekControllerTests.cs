using BezBukv.Core.Keyboard;
using BezBukv.Core.Platform;
using BezBukv.Core.Scenarios;
using BezBukv.Core.Settings;

namespace BezBukv.Core.Tests;

public sealed class AutoPeekControllerTests
{
    [Fact]
    public void UnknownDownInCustomLayoutRequestsIdentityThenHidesAfterLinger()
    {
        var captureService = new FakeKeyboardCaptureService();
        var timeProvider = new ManualTimeProvider();
        var lingerDuration = TimeSpan.FromSeconds(5);
        var identity = new KeyboardScanCodeIdentity(0x56, false);
        using var controller = new AutoPeekController(
            captureService,
            lingerDuration,
            timeProvider);
        controller.UpdateKeyboardProfile(
            KeyboardProfile.Custom,
            CreateCustomLayout(identity));
        var shownIdentities = new List<KeyboardScanCodeIdentity>();
        var hideCount = 0;
        controller.ShowCustomOverlayRequested += shownIdentities.Add;
        controller.HideOverlayRequested += () => hideCount++;
        controller.Enable();

        captureService.RaiseKeyChanged(
            PhysicalKey.Unknown,
            KeyboardKeyState.Down,
            identity.ScanCode,
            identity.Extended);

        Assert.Equal([identity], shownIdentities);
        Assert.Equal(0, hideCount);
        Assert.Equal(1, timeProvider.ActiveTimerCount);

        timeProvider.Advance(lingerDuration);

        Assert.Equal(1, hideCount);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void UnknownDownOutsideCustomLayoutDoesNotRequestShow()
    {
        var captureService = new FakeKeyboardCaptureService();
        var layoutIdentity = new KeyboardScanCodeIdentity(0x56, false);
        using var controller = new AutoPeekController(
            captureService,
            TimeSpan.FromSeconds(5));
        controller.UpdateKeyboardProfile(
            KeyboardProfile.Custom,
            CreateCustomLayout(layoutIdentity));
        var showCount = 0;
        controller.ShowCustomOverlayRequested += _ => showCount++;
        controller.Enable();

        captureService.RaiseKeyChanged(
            PhysicalKey.Unknown,
            KeyboardKeyState.Down,
            scanCode: 0x7F,
            extended: true);

        Assert.Equal(0, showCount);
    }

    [Theory]
    [InlineData(KeyboardProfile.Unknown)]
    [InlineData(KeyboardProfile.Ansi)]
    [InlineData(KeyboardProfile.Iso)]
    public void UnknownDownOutsideCustomProfileDoesNotRequestShow(
        KeyboardProfile keyboardProfile)
    {
        var captureService = new FakeKeyboardCaptureService();
        var identity = new KeyboardScanCodeIdentity(0x56, false);
        using var controller = new AutoPeekController(
            captureService,
            TimeSpan.FromSeconds(5));
        controller.UpdateKeyboardProfile(
            keyboardProfile,
            CreateCustomLayout(identity));
        var showCount = 0;
        controller.ShowCustomOverlayRequested += _ => showCount++;
        controller.Enable();

        captureService.RaiseKeyChanged(
            PhysicalKey.Unknown,
            KeyboardKeyState.Down,
            identity.ScanCode,
            identity.Extended);

        Assert.Equal(0, showCount);
    }

    [Fact]
    public void NamedKeyDownInCustomProfileRequestsRawIdentityThenHidesAfterLinger()
    {
        var captureService = new FakeKeyboardCaptureService();
        var timeProvider = new ManualTimeProvider();
        var lingerDuration = TimeSpan.FromSeconds(5);
        var identity = new KeyboardScanCodeIdentity(0x3B, false);
        using var controller = new AutoPeekController(
            captureService,
            lingerDuration,
            timeProvider,
            showServiceKeys: true);
        controller.UpdateKeyboardProfile(
            KeyboardProfile.Custom,
            CreateCustomLayout(identity));
        var shownKeys = new List<PhysicalKey>();
        var shownIdentities = new List<KeyboardScanCodeIdentity>();
        var hideCount = 0;
        controller.ShowOverlayRequested += shownKeys.Add;
        controller.ShowCustomOverlayRequested += shownIdentities.Add;
        controller.HideOverlayRequested += () => hideCount++;
        controller.Enable();

        captureService.RaiseKeyChanged(
            PhysicalKey.F1,
            KeyboardKeyState.Down,
            identity.ScanCode,
            identity.Extended);

        Assert.Empty(shownKeys);
        Assert.Equal([identity], shownIdentities);
        Assert.Equal(0, hideCount);
        Assert.Equal(1, timeProvider.ActiveTimerCount);

        timeProvider.Advance(lingerDuration);

        Assert.Equal(1, hideCount);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Theory]
    [InlineData(KeyboardProfile.Ansi)]
    [InlineData(KeyboardProfile.Iso)]
    public void NamedKeyDownInPresetProfileKeepsUsingPhysicalKeyShowPath(
        KeyboardProfile keyboardProfile)
    {
        var captureService = new FakeKeyboardCaptureService();
        var identity = new KeyboardScanCodeIdentity(0x3B, false);
        using var controller = new AutoPeekController(
            captureService,
            TimeSpan.FromSeconds(5),
            showServiceKeys: true);
        controller.UpdateKeyboardProfile(keyboardProfile, CreateCustomLayout(identity));
        var shownKeys = new List<PhysicalKey>();
        var shownIdentityCount = 0;
        controller.ShowOverlayRequested += shownKeys.Add;
        controller.ShowCustomOverlayRequested += _ => shownIdentityCount++;
        controller.Enable();

        captureService.RaiseKeyChanged(
            PhysicalKey.F1,
            KeyboardKeyState.Down,
            identity.ScanCode,
            identity.Extended);

        Assert.Equal([PhysicalKey.F1], shownKeys);
        Assert.Equal(0, shownIdentityCount);
    }

    [Fact]
    public void EnabledLetterDownRequestsShowThenHideAfterLingerDuration()
    {
        var captureService = new FakeKeyboardCaptureService();
        var timeProvider = new ManualTimeProvider();
        var lingerDuration = TimeSpan.FromSeconds(5);
        using var controller = new AutoPeekController(
            captureService,
            lingerDuration,
            timeProvider);
        var shownKeys = new List<PhysicalKey>();
        var hideCount = 0;
        controller.ShowOverlayRequested += shownKeys.Add;
        controller.HideOverlayRequested += () => hideCount++;
        controller.Enable();

        captureService.RaiseKeyChanged(PhysicalKey.KeyA, KeyboardKeyState.Down);

        Assert.Equal([PhysicalKey.KeyA], shownKeys);
        Assert.Equal(0, hideCount);
        Assert.Equal(1, timeProvider.ActiveTimerCount);

        timeProvider.Advance(lingerDuration);

        Assert.Equal(1, hideCount);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void NumberRowDownRespectsShowFlag(
        bool blockNumberRow,
        int expectedShowCount)
    {
        var captureService = new FakeKeyboardCaptureService();
        using var controller = new AutoPeekController(
            captureService,
            TimeSpan.FromSeconds(5),
            blockNumberRow: blockNumberRow);
        var shownKeys = new List<PhysicalKey>();
        controller.ShowOverlayRequested += shownKeys.Add;
        controller.Enable();

        captureService.RaiseKeyChanged(PhysicalKey.Digit1, KeyboardKeyState.Down);

        Assert.Equal(expectedShowCount, shownKeys.Count);
    }

    [Fact]
    public void PunctuationDownAlwaysRequestsShow()
    {
        var captureService = new FakeKeyboardCaptureService();
        using var controller = new AutoPeekController(
            captureService,
            TimeSpan.FromSeconds(5));
        var shownKeys = new List<PhysicalKey>();
        controller.ShowOverlayRequested += shownKeys.Add;
        controller.Enable();

        captureService.RaiseKeyChanged(PhysicalKey.Comma, KeyboardKeyState.Down);

        Assert.Equal([PhysicalKey.Comma], shownKeys);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void ServiceKeyDownRespectsShowFlag(
        bool showServiceKeys,
        int expectedShowCount)
    {
        var captureService = new FakeKeyboardCaptureService();
        using var controller = new AutoPeekController(
            captureService,
            TimeSpan.FromSeconds(5),
            showServiceKeys: showServiceKeys);
        var shownKeys = new List<PhysicalKey>();
        controller.ShowOverlayRequested += shownKeys.Add;
        controller.Enable();

        captureService.RaiseKeyChanged(PhysicalKey.ArrowLeft, KeyboardKeyState.Down);

        Assert.Equal(expectedShowCount, shownKeys.Count);
    }

    [Fact]
    public void NewSuitableDownReplacesKeyAndRearmsLingerTimer()
    {
        var captureService = new FakeKeyboardCaptureService();
        var timeProvider = new ManualTimeProvider();
        var lingerDuration = TimeSpan.FromSeconds(10);
        using var controller = new AutoPeekController(
            captureService,
            lingerDuration,
            timeProvider);
        var shownKeys = new List<PhysicalKey>();
        var hideCount = 0;
        controller.ShowOverlayRequested += shownKeys.Add;
        controller.HideOverlayRequested += () => hideCount++;
        controller.Enable();

        captureService.RaiseKeyChanged(PhysicalKey.KeyA, KeyboardKeyState.Down);
        timeProvider.Advance(TimeSpan.FromSeconds(6));
        captureService.RaiseKeyChanged(PhysicalKey.KeyB, KeyboardKeyState.Down);
        timeProvider.Advance(TimeSpan.FromSeconds(4));

        Assert.Equal([PhysicalKey.KeyA, PhysicalKey.KeyB], shownKeys);
        Assert.Equal(0, hideCount);
        Assert.Equal(1, timeProvider.ActiveTimerCount);

        timeProvider.Advance(TimeSpan.FromSeconds(6));

        Assert.Equal(1, hideCount);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void DisabledControllerIgnoresKeyEvents()
    {
        var captureService = new FakeKeyboardCaptureService();
        var timeProvider = new ManualTimeProvider();
        using var controller = new AutoPeekController(
            captureService,
            TimeSpan.FromSeconds(5),
            timeProvider,
            blockNumberRow: true,
            showServiceKeys: true);
        var showCount = 0;
        var hideCount = 0;
        controller.ShowOverlayRequested += _ => showCount++;
        controller.HideOverlayRequested += () => hideCount++;

        captureService.RaiseKeyChanged(PhysicalKey.KeyA, KeyboardKeyState.Down);
        captureService.RaiseKeyChanged(PhysicalKey.Digit1, KeyboardKeyState.Down);
        captureService.RaiseKeyChanged(PhysicalKey.Comma, KeyboardKeyState.Down);
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.False(controller.IsEnabled);
        Assert.Equal(0, showCount);
        Assert.Equal(0, hideCount);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void DisableWithActiveOverlayRequestsHideAndCancelsTimer()
    {
        var captureService = new FakeKeyboardCaptureService();
        var timeProvider = new ManualTimeProvider();
        var lingerDuration = TimeSpan.FromSeconds(5);
        using var controller = new AutoPeekController(
            captureService,
            lingerDuration,
            timeProvider);
        var hideCount = 0;
        controller.HideOverlayRequested += () => hideCount++;
        controller.Enable();
        captureService.RaiseKeyChanged(PhysicalKey.KeyA, KeyboardKeyState.Down);

        controller.Disable();
        timeProvider.Advance(lingerDuration);
        captureService.RaiseKeyChanged(PhysicalKey.KeyB, KeyboardKeyState.Down);

        Assert.False(controller.IsEnabled);
        Assert.Equal(1, hideCount);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
    }

    [Fact]
    public void KeyUpEventsAreIgnored()
    {
        var captureService = new FakeKeyboardCaptureService();
        var timeProvider = new ManualTimeProvider();
        var lingerDuration = TimeSpan.FromSeconds(5);
        using var controller = new AutoPeekController(
            captureService,
            lingerDuration,
            timeProvider,
            blockNumberRow: true,
            showServiceKeys: true);
        var showCount = 0;
        var hideCount = 0;
        controller.ShowOverlayRequested += _ => showCount++;
        controller.HideOverlayRequested += () => hideCount++;
        controller.Enable();

        captureService.RaiseKeyChanged(PhysicalKey.KeyA, KeyboardKeyState.Down);
        captureService.RaiseKeyChanged(PhysicalKey.KeyA, KeyboardKeyState.Up);
        captureService.RaiseKeyChanged(PhysicalKey.Digit1, KeyboardKeyState.Up);
        captureService.RaiseKeyChanged(PhysicalKey.Comma, KeyboardKeyState.Up);

        Assert.Equal(1, showCount);
        Assert.Equal(0, hideCount);
        Assert.Equal(1, timeProvider.ActiveTimerCount);

        timeProvider.Advance(lingerDuration);

        Assert.Equal(1, hideCount);
    }

    [Fact]
    public void UpdatedLingerDurationIsUsedWhenTimerIsRearmed()
    {
        var captureService = new FakeKeyboardCaptureService();
        var timeProvider = new ManualTimeProvider();
        using var controller = new AutoPeekController(
            captureService,
            TimeSpan.FromSeconds(10),
            timeProvider);
        var hideCount = 0;
        controller.HideOverlayRequested += () => hideCount++;
        controller.Enable();
        captureService.RaiseKeyChanged(PhysicalKey.KeyA, KeyboardKeyState.Down);
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        controller.UpdateLingerDuration(TimeSpan.FromSeconds(3));
        captureService.RaiseKeyChanged(PhysicalKey.KeyB, KeyboardKeyState.Down);
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(0, hideCount);

        timeProvider.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(1, hideCount);
    }

    [Fact]
    public void UpdatedShowFlagsAreUsedByLaterKeyEvents()
    {
        var captureService = new FakeKeyboardCaptureService();
        using var controller = new AutoPeekController(
            captureService,
            TimeSpan.FromSeconds(5));
        var shownKeys = new List<PhysicalKey>();
        controller.ShowOverlayRequested += shownKeys.Add;
        controller.Enable();

        captureService.RaiseKeyChanged(PhysicalKey.Digit1, KeyboardKeyState.Down);
        captureService.RaiseKeyChanged(PhysicalKey.Comma, KeyboardKeyState.Down);
        captureService.RaiseKeyChanged(PhysicalKey.ControlLeft, KeyboardKeyState.Down);
        controller.UpdateShowFlags(blockNumberRow: true, showServiceKeys: false);
        captureService.RaiseKeyChanged(PhysicalKey.Digit2, KeyboardKeyState.Down);
        captureService.RaiseKeyChanged(PhysicalKey.Period, KeyboardKeyState.Down);
        captureService.RaiseKeyChanged(PhysicalKey.F1, KeyboardKeyState.Down);
        controller.UpdateShowFlags(blockNumberRow: true, showServiceKeys: true);
        captureService.RaiseKeyChanged(PhysicalKey.ArrowRight, KeyboardKeyState.Down);

        Assert.Equal(
            [
                PhysicalKey.Comma,
                PhysicalKey.Digit2,
                PhysicalKey.Period,
                PhysicalKey.ArrowRight,
            ],
            shownKeys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveLingerDurationIsRejectedByConstructor(
        int durationMilliseconds)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new AutoPeekController(
                new FakeKeyboardCaptureService(),
                TimeSpan.FromMilliseconds(durationMilliseconds)));

        Assert.Equal("lingerDuration", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveLingerDurationIsRejectedByLiveUpdate(
        int durationMilliseconds)
    {
        using var controller = new AutoPeekController(
            new FakeKeyboardCaptureService(),
            TimeSpan.FromSeconds(5));

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => controller.UpdateLingerDuration(
                TimeSpan.FromMilliseconds(durationMilliseconds)));

        Assert.Equal("lingerDuration", exception.ParamName);
    }

    [Fact]
    public void DisposeUnsubscribesAndCancelsTimerIdempotently()
    {
        var captureService = new FakeKeyboardCaptureService();
        var timeProvider = new ManualTimeProvider();
        var lingerDuration = TimeSpan.FromSeconds(5);
        var controller = new AutoPeekController(
            captureService,
            lingerDuration,
            timeProvider);
        var showCount = 0;
        var hideCount = 0;
        controller.ShowOverlayRequested += _ => showCount++;
        controller.HideOverlayRequested += () => hideCount++;
        controller.Enable();
        captureService.RaiseKeyChanged(PhysicalKey.KeyA, KeyboardKeyState.Down);

        controller.Dispose();
        controller.Dispose();
        captureService.RaiseKeyChanged(PhysicalKey.KeyB, KeyboardKeyState.Down);
        timeProvider.Advance(lingerDuration);

        Assert.False(controller.IsEnabled);
        Assert.Equal(0, captureService.KeyChangedSubscriberCount);
        Assert.Equal(0, timeProvider.ActiveTimerCount);
        Assert.Equal(1, showCount);
        Assert.Equal(0, hideCount);
        Assert.Equal(0, captureService.StartCallCount);
        Assert.Equal(0, captureService.StopCallCount);
        Assert.Throws<ObjectDisposedException>(() => controller.Enable());
    }

    private sealed class FakeKeyboardCaptureService : IKeyboardCaptureService
    {
        private EventHandler<KeyboardCaptureEventArgs>? _keyChanged;

        public event EventHandler<KeyboardCaptureEventArgs>? KeyChanged
        {
            add => _keyChanged += value;
            remove => _keyChanged -= value;
        }

        public bool IsCapturing { get; private set; }

        public bool IsCalibrationModeEnabled { get; private set; }

        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public int KeyChangedSubscriberCount =>
            _keyChanged?.GetInvocationList().Length ?? 0;

        public void RaiseKeyChanged(
            PhysicalKey key,
            KeyboardKeyState state,
            int scanCode = 0,
            bool extended = false,
            int virtualKey = 0)
        {
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
        }

        public void UpdateCalibrationModeEnabled(bool isEnabled)
        {
            IsCalibrationModeEnabled = isEnabled;
        }

        public bool IsActivationChordHeld() => false;

        public KeyboardCaptureStartStatus StartCapture()
        {
            StartCallCount++;
            IsCapturing = true;
            return KeyboardCaptureStartStatus.Started;
        }

        public KeyboardCaptureStopStatus StopCapture()
        {
            StopCallCount++;
            IsCapturing = false;
            return KeyboardCaptureStopStatus.Stopped;
        }

        public void Dispose()
        {
            IsCapturing = false;
            _keyChanged = null;
        }
    }

    private static CustomKeyboardLayout CreateCustomLayout(
        KeyboardScanCodeIdentity identity) =>
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
                            Key = new CustomKeyboardKey
                            {
                                ScanCode = identity.ScanCode,
                                Extended = identity.Extended,
                            },
                        },
                    ],
                },
            ],
        };

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

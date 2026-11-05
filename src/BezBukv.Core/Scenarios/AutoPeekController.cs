using BezBukv.Core.Keyboard;
using BezBukv.Core.Platform;
using BezBukv.Core.Settings;

namespace BezBukv.Core.Scenarios;

public sealed class AutoPeekController : IDisposable
{
    private readonly IKeyboardCaptureService _keyboardCaptureService;
    private readonly TimeProvider _timeProvider;
    private readonly object _sync = new();
    private TimeSpan _lingerDuration;
    private bool _blockNumberRow;
    private bool _showServiceKeys;
    private KeyboardProfile _keyboardProfile = KeyboardProfile.Unknown;
    private CustomKeyboardLayout? _customKeyboardLayout;
    private bool _isEnabled;
    private bool _isOverlayActive;
    private ITimer? _lingerTimer;
    private long _lingerTimerVersion;
    private bool _disposed;

    public AutoPeekController(
        IKeyboardCaptureService keyboardCaptureService,
        TimeSpan lingerDuration,
        TimeProvider? timeProvider = null,
        bool blockNumberRow = false,
        bool showServiceKeys = false)
    {
        ArgumentNullException.ThrowIfNull(keyboardCaptureService);

        if (lingerDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lingerDuration),
                "Linger duration must be positive.");
        }

        _keyboardCaptureService = keyboardCaptureService;
        _lingerDuration = lingerDuration;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _blockNumberRow = blockNumberRow;
        _showServiceKeys = showServiceKeys;
        _keyboardCaptureService.KeyChanged += OnKeyChanged;
    }

    public event Action<PhysicalKey>? ShowOverlayRequested;

    public event Action<KeyboardScanCodeIdentity>? ShowCustomOverlayRequested;

    public event Action? HideOverlayRequested;

    public bool IsEnabled
    {
        get
        {
            lock (_sync)
            {
                return !_disposed && _isEnabled;
            }
        }
    }

    public void Enable()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _isEnabled = true;
        }
    }

    public void Disable()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _isEnabled = false;

            if (!_isOverlayActive)
            {
                CancelLingerTimer();
                return;
            }

            _isOverlayActive = false;
            CancelLingerTimer();
            HideOverlayRequested?.Invoke();
        }
    }

    public void UpdateLingerDuration(TimeSpan lingerDuration)
    {
        if (lingerDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lingerDuration),
                "Linger duration must be positive.");
        }

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _lingerDuration = lingerDuration;
        }
    }

    public void UpdateShowFlags(bool blockNumberRow, bool showServiceKeys)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _blockNumberRow = blockNumberRow;
            _showServiceKeys = showServiceKeys;
        }
    }

    public void UpdateKeyboardProfile(
        KeyboardProfile keyboardProfile,
        CustomKeyboardLayout? customKeyboardLayout)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _keyboardProfile = keyboardProfile;
            _customKeyboardLayout = keyboardProfile == KeyboardProfile.Custom
                ? customKeyboardLayout
                : null;
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

            _keyboardCaptureService.KeyChanged -= OnKeyChanged;
            _isEnabled = false;
            _isOverlayActive = false;
            CancelLingerTimer();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    private void ArmLingerTimer()
    {
        CancelLingerTimer();
        var timerVersion = _lingerTimerVersion;
        _lingerTimer = _timeProvider.CreateTimer(
            static state =>
            {
                var timerState = (LingerTimerState)state!;
                timerState.Controller.OnLingerElapsed(timerState.Version);
            },
            new LingerTimerState(this, timerVersion),
            _lingerDuration,
            Timeout.InfiniteTimeSpan);
    }

    private void CancelLingerTimer()
    {
        _lingerTimerVersion++;
        var timer = _lingerTimer;
        _lingerTimer = null;
        timer?.Dispose();
    }

    private void OnLingerElapsed(long timerVersion)
    {
        lock (_sync)
        {
            if (_disposed ||
                !_isEnabled ||
                !_isOverlayActive ||
                timerVersion != _lingerTimerVersion)
            {
                return;
            }

            _isOverlayActive = false;
            CancelLingerTimer();
            HideOverlayRequested?.Invoke();
        }
    }

    private void OnKeyChanged(object? sender, KeyboardCaptureEventArgs eventArgs)
    {
        if (eventArgs.State != KeyboardKeyState.Down)
        {
            return;
        }

        lock (_sync)
        {
            if (_disposed || !_isEnabled)
            {
                return;
            }

            if (eventArgs.Key == PhysicalKey.Unknown)
            {
                var centerIdentity = new KeyboardScanCodeIdentity(
                    eventArgs.ScanCode,
                    eventArgs.Extended);
                if (_keyboardProfile != KeyboardProfile.Custom
                    || !KeyboardPeekBlockingPolicy.ShouldShowOverlay(
                        centerIdentity,
                        _customKeyboardLayout))
                {
                    return;
                }

                ArmLingerTimer();
                _isOverlayActive = true;
                ShowCustomOverlayRequested?.Invoke(centerIdentity);
                return;
            }

            if (!KeyboardPeekBlockingPolicy.ShouldShowOverlay(
                    eventArgs.Key,
                    _blockNumberRow,
                    _showServiceKeys))
            {
                return;
            }

            ArmLingerTimer();
            _isOverlayActive = true;
            if (_keyboardProfile == KeyboardProfile.Custom)
            {
                var centerIdentity = new KeyboardScanCodeIdentity(
                    eventArgs.ScanCode,
                    eventArgs.Extended);
                ShowCustomOverlayRequested?.Invoke(centerIdentity);
            }
            else
            {
                ShowOverlayRequested?.Invoke(eventArgs.Key);
            }
        }
    }

    private sealed record LingerTimerState(
        AutoPeekController Controller,
        long Version);
}

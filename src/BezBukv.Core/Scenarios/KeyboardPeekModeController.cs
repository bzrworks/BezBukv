using BezBukv.Core.Platform;

namespace BezBukv.Core.Scenarios;

public sealed class KeyboardPeekModeController : IDisposable
{
    public static readonly TimeSpan DefaultAutoExitTimeout = TimeSpan.FromSeconds(120);

    private static readonly TimeSpan LivenessPollInterval =
        TimeSpan.FromMilliseconds(150);

    private readonly IKeyboardCaptureService _keyboardCaptureService;
    private HashSet<PhysicalKey> _activationChordKeys;
    private readonly TimeProvider _timeProvider;
    private TimeSpan _autoExitTimeout;
    private readonly object _sync = new();
    private ITimer? _autoExitTimer;
    private ITimer? _livenessPollTimer;
    private bool _isActive;
    private bool _disposed;

    public KeyboardPeekModeController(
        IKeyboardCaptureService keyboardCaptureService,
        GlobalHotkey? activationHotkey,
        TimeSpan? autoExitTimeout = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(keyboardCaptureService);

        var effectiveAutoExitTimeout = autoExitTimeout ?? DefaultAutoExitTimeout;
        if (effectiveAutoExitTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(autoExitTimeout),
                "Auto-exit timeout must be positive.");
        }

        _keyboardCaptureService = keyboardCaptureService;
        _activationChordKeys = CreateActivationChordKeys(activationHotkey);
        _keyboardCaptureService.UpdateActivationChordKeys(_activationChordKeys);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _autoExitTimeout = effectiveAutoExitTimeout;
        _keyboardCaptureService.KeyChanged += OnKeyChanged;
    }

    public bool IsActive
    {
        get
        {
            lock (_sync)
            {
                return !_disposed && IsActiveCore();
            }
        }
    }

    public void UpdateAutoExitTimeout(TimeSpan autoExitTimeout)
    {
        if (autoExitTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(autoExitTimeout),
                "Auto-exit timeout must be positive.");
        }

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _autoExitTimeout = autoExitTimeout;
        }
    }

    public void UpdateActivationHotkey(GlobalHotkey? activationHotkey)
    {
        var activationChordKeys = CreateActivationChordKeys(activationHotkey);

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (IsActiveCore())
            {
                _ = DeactivateCore();
            }

            _activationChordKeys = activationChordKeys;
            _keyboardCaptureService.UpdateActivationChordKeys(_activationChordKeys);
        }
    }

    public bool IsActivationChordKey(PhysicalKey key)
    {
        lock (_sync)
        {
            return !_disposed && _activationChordKeys.Contains(key);
        }
    }

    public KeyboardPeekModeActivationOutcome Activate()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_activationChordKeys.Count == 0)
            {
                return KeyboardPeekModeActivationOutcome.ActivationFailed;
            }

            if (IsActiveCore())
            {
                return KeyboardPeekModeActivationOutcome.AlreadyActive;
            }

            var startStatus = _keyboardCaptureService.StartCapture();
            if (startStatus is KeyboardCaptureStartStatus.Started or
                KeyboardCaptureStartStatus.AlreadyCapturing)
            {
                _isActive = true;

                try
                {
                    ArmSessionTimers();
                }
                catch
                {
                    _ = DeactivateCore();
                    throw;
                }

                return KeyboardPeekModeActivationOutcome.Activated;
            }

            return startStatus switch
            {
                KeyboardCaptureStartStatus.PlatformError =>
                    KeyboardPeekModeActivationOutcome.ActivationFailed,
                _ => throw new InvalidOperationException("Unknown capture start status."),
            };
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

            try
            {
                if (IsActiveCore())
                {
                    _ = DeactivateCore();
                }
            }
            finally
            {
                CancelSessionTimers();
                _isActive = false;
                _disposed = true;
            }
        }

        GC.SuppressFinalize(this);
    }

    private KeyboardCaptureStopStatus DeactivateCore()
    {
        if (!IsActiveCore())
        {
            return KeyboardCaptureStopStatus.AlreadyStopped;
        }

        return _keyboardCaptureService.StopCapture() switch
        {
            KeyboardCaptureStopStatus.Stopped => HandleSuccessfulDeactivation(
                KeyboardCaptureStopStatus.Stopped),
            KeyboardCaptureStopStatus.AlreadyStopped => HandleSuccessfulDeactivation(
                KeyboardCaptureStopStatus.AlreadyStopped),
            KeyboardCaptureStopStatus.PlatformError => KeyboardCaptureStopStatus.PlatformError,
            _ => throw new InvalidOperationException("Unknown capture stop status."),
        };
    }

    private KeyboardCaptureStopStatus HandleSuccessfulDeactivation(
        KeyboardCaptureStopStatus stopStatus)
    {
        _isActive = false;
        CancelSessionTimers();
        return stopStatus;
    }

    private void ArmSessionTimers()
    {
        CancelSessionTimers();

        try
        {
            _livenessPollTimer = _timeProvider.CreateTimer(
                static state => ((KeyboardPeekModeController)state!).OnLivenessPoll(),
                this,
                LivenessPollInterval,
                LivenessPollInterval);
            _autoExitTimer = _timeProvider.CreateTimer(
                static state => ((KeyboardPeekModeController)state!).OnAutoExitTimeout(),
                this,
                _autoExitTimeout,
                Timeout.InfiniteTimeSpan);
        }
        catch
        {
            CancelSessionTimers();
            throw;
        }
    }

    private void CancelSessionTimers()
    {
        var autoExitTimer = _autoExitTimer;
        _autoExitTimer = null;
        autoExitTimer?.Dispose();

        var livenessPollTimer = _livenessPollTimer;
        _livenessPollTimer = null;
        livenessPollTimer?.Dispose();
    }

    private void OnLivenessPoll()
    {
        lock (_sync)
        {
            if (_disposed || !IsActiveCore())
            {
                return;
            }

            if (!_keyboardCaptureService.IsActivationChordHeld())
            {
                _ = DeactivateCore();
            }
        }
    }

    private void OnAutoExitTimeout()
    {
        lock (_sync)
        {
            if (_disposed || !IsActiveCore())
            {
                return;
            }

            _ = DeactivateCore();
        }
    }

    private void OnKeyChanged(object? sender, KeyboardCaptureEventArgs eventArgs)
    {
        if (eventArgs.State != KeyboardKeyState.Up)
        {
            return;
        }

        lock (_sync)
        {
            if (_disposed
                || !_activationChordKeys.Contains(eventArgs.Key)
                || !IsActiveCore())
            {
                return;
            }

            _ = DeactivateCore();
        }
    }

    private bool IsActiveCore()
    {
        if (!_isActive || _keyboardCaptureService.IsCapturing)
        {
            return _isActive;
        }

        _isActive = false;
        CancelSessionTimers();
        return false;
    }

    private static HashSet<PhysicalKey> CreateActivationChordKeys(
        GlobalHotkey? hotkey)
    {
        if (hotkey is null)
        {
            return [];
        }

        var keys = new HashSet<PhysicalKey>
        {
            MapHotkeyKey(hotkey.Key),
        };

        foreach (var modifier in hotkey.Modifiers)
        {
            switch (modifier)
            {
                case HotkeyModifier.Primary:
                case HotkeyModifier.Control:
                    keys.Add(PhysicalKey.ControlLeft);
                    keys.Add(PhysicalKey.ControlRight);
                    break;
                case HotkeyModifier.Alt:
                    keys.Add(PhysicalKey.AltLeft);
                    keys.Add(PhysicalKey.AltRight);
                    break;
                case HotkeyModifier.Shift:
                    keys.Add(PhysicalKey.ShiftLeft);
                    keys.Add(PhysicalKey.ShiftRight);
                    break;
                case HotkeyModifier.Meta:
                    keys.Add(PhysicalKey.MetaLeft);
                    keys.Add(PhysicalKey.MetaRight);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(hotkey));
            }
        }

        return keys;
    }

    private static PhysicalKey MapHotkeyKey(HotkeyKey key) =>
        key switch
        {
            HotkeyKey.A => PhysicalKey.KeyA,
            HotkeyKey.B => PhysicalKey.KeyB,
            HotkeyKey.C => PhysicalKey.KeyC,
            HotkeyKey.D => PhysicalKey.KeyD,
            HotkeyKey.E => PhysicalKey.KeyE,
            HotkeyKey.F => PhysicalKey.KeyF,
            HotkeyKey.G => PhysicalKey.KeyG,
            HotkeyKey.H => PhysicalKey.KeyH,
            HotkeyKey.I => PhysicalKey.KeyI,
            HotkeyKey.J => PhysicalKey.KeyJ,
            HotkeyKey.K => PhysicalKey.KeyK,
            HotkeyKey.L => PhysicalKey.KeyL,
            HotkeyKey.M => PhysicalKey.KeyM,
            HotkeyKey.N => PhysicalKey.KeyN,
            HotkeyKey.O => PhysicalKey.KeyO,
            HotkeyKey.P => PhysicalKey.KeyP,
            HotkeyKey.Q => PhysicalKey.KeyQ,
            HotkeyKey.R => PhysicalKey.KeyR,
            HotkeyKey.S => PhysicalKey.KeyS,
            HotkeyKey.T => PhysicalKey.KeyT,
            HotkeyKey.U => PhysicalKey.KeyU,
            HotkeyKey.V => PhysicalKey.KeyV,
            HotkeyKey.W => PhysicalKey.KeyW,
            HotkeyKey.X => PhysicalKey.KeyX,
            HotkeyKey.Y => PhysicalKey.KeyY,
            HotkeyKey.Z => PhysicalKey.KeyZ,
            HotkeyKey.Digit0 => PhysicalKey.Digit0,
            HotkeyKey.Digit1 => PhysicalKey.Digit1,
            HotkeyKey.Digit2 => PhysicalKey.Digit2,
            HotkeyKey.Digit3 => PhysicalKey.Digit3,
            HotkeyKey.Digit4 => PhysicalKey.Digit4,
            HotkeyKey.Digit5 => PhysicalKey.Digit5,
            HotkeyKey.Digit6 => PhysicalKey.Digit6,
            HotkeyKey.Digit7 => PhysicalKey.Digit7,
            HotkeyKey.Digit8 => PhysicalKey.Digit8,
            HotkeyKey.Digit9 => PhysicalKey.Digit9,
            HotkeyKey.F1 => PhysicalKey.F1,
            HotkeyKey.F2 => PhysicalKey.F2,
            HotkeyKey.F3 => PhysicalKey.F3,
            HotkeyKey.F4 => PhysicalKey.F4,
            HotkeyKey.F5 => PhysicalKey.F5,
            HotkeyKey.F6 => PhysicalKey.F6,
            HotkeyKey.F7 => PhysicalKey.F7,
            HotkeyKey.F8 => PhysicalKey.F8,
            HotkeyKey.F9 => PhysicalKey.F9,
            HotkeyKey.F10 => PhysicalKey.F10,
            HotkeyKey.F11 => PhysicalKey.F11,
            HotkeyKey.F12 => PhysicalKey.F12,
            HotkeyKey.F13 => PhysicalKey.F13,
            HotkeyKey.F14 => PhysicalKey.F14,
            HotkeyKey.F15 => PhysicalKey.F15,
            HotkeyKey.F16 => PhysicalKey.F16,
            HotkeyKey.F17 => PhysicalKey.F17,
            HotkeyKey.F18 => PhysicalKey.F18,
            HotkeyKey.F19 => PhysicalKey.F19,
            HotkeyKey.F20 => PhysicalKey.F20,
            HotkeyKey.F21 => PhysicalKey.F21,
            HotkeyKey.F22 => PhysicalKey.F22,
            HotkeyKey.F23 => PhysicalKey.F23,
            HotkeyKey.F24 => PhysicalKey.F24,
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };
}

public enum KeyboardPeekModeActivationOutcome
{
    Activated,
    AlreadyActive,
    ActivationFailed,
}

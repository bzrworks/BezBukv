using System.Runtime.InteropServices;
using BezBukv.Core.Keyboard;
using BezBukv.Core.Platform;
using BezBukv.Core.Scenarios;
using BezBukv.Core.Settings;

namespace BezBukv.Windows;

public sealed class WindowsKeyboardCaptureService : IKeyboardCaptureService
{
    private const int WhKeyboardLowLevel = 13;
    private const int KeyStateDownMask = 0x8000;

    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmSystemKeyDown = 0x0104;
    private const uint WmSystemKeyUp = 0x0105;

    private const uint LlkhfExtended = 0x00000001;
    private const uint LlkhfInjected = 0x00000010;

    private const uint VirtualKeyLeftControl = 0xA2;
    private const uint VirtualKeyPause = 0x13;
    private const uint VirtualKeyPrintScreen = 0x2C;
    private const uint VirtualKeyF1 = 0x70;
    private const uint VirtualKeyF24 = 0x87;

    private const uint SyntheticAltGrControlScanCode = 0x021D;

    private readonly SynchronizationContext _eventContext;
    private IReadOnlySet<PhysicalKey> _activationChordKeys = new HashSet<PhysicalKey>();
    private BlockingOptions _blockingOptions;
    private CustomKeyboardLayout? _customHoldPeekLayout;
    private readonly LowLevelKeyboardProcedure _hookCallback;
    private nint _hookHandle;
    private int _captureGeneration;
    private int _isBlockingAll;
    private int _isBlockingEnabled = 1;
    private int _isCalibrationModeEnabled;
    private int _isCapturing;
    private int _pendingSyntheticAltGrControl;
    private bool _disposed;

    public WindowsKeyboardCaptureService(
        SynchronizationContext eventContext,
        bool blockNumberRow,
        bool showServiceKeys)
    {
        ArgumentNullException.ThrowIfNull(eventContext);
        _eventContext = eventContext;
        _blockingOptions = new BlockingOptions(blockNumberRow, showServiceKeys);
        _hookCallback = HookCallback;
    }

    public event EventHandler<KeyboardCaptureEventArgs>? KeyChanged;

    public bool IsCapturing => Volatile.Read(ref _isCapturing) != 0;

    public void UpdateActivationChordKeys(IReadOnlySet<PhysicalKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Volatile.Write(
            ref _activationChordKeys,
            new HashSet<PhysicalKey>(keys));
    }

    public void UpdateCalibrationModeEnabled(bool isEnabled)
    {
        Volatile.Write(ref _isCalibrationModeEnabled, isEnabled ? 1 : 0);
        if (!isEnabled)
        {
            Interlocked.Exchange(ref _pendingSyntheticAltGrControl, 0);
        }
    }

    public bool IsActivationChordHeld()
    {
        try
        {
            return IsActivationChordHeld(Volatile.Read(ref _activationChordKeys));
        }
        catch
        {
            // Physical-state polling is advisory and must not disrupt the process.
            return false;
        }
    }

    public void UpdateBlockingOptions(bool blockNumberRow, bool showServiceKeys)
    {
        Volatile.Write(
            ref _blockingOptions,
            new BlockingOptions(blockNumberRow, showServiceKeys));
    }

    public void UpdateBlockingEnabled(bool isEnabled)
    {
        Volatile.Write(ref _isBlockingEnabled, isEnabled ? 1 : 0);
    }

    public void UpdateBlockingAllEnabled(bool isEnabled)
    {
        Volatile.Write(ref _isBlockingAll, isEnabled ? 1 : 0);
    }

    public void UpdateCustomHoldPeekLayout(CustomKeyboardLayout? customKeyboardLayout)
    {
        Volatile.Write(ref _customHoldPeekLayout, customKeyboardLayout);
    }

    public KeyboardCaptureStartStatus StartCapture()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hookHandle != 0)
        {
            return KeyboardCaptureStartStatus.AlreadyCapturing;
        }

        var moduleHandle = GetModuleHandle(null);
        var hookHandle = SetWindowsHookEx(
            WhKeyboardLowLevel,
            _hookCallback,
            moduleHandle,
            0);
        if (hookHandle == 0)
        {
            return KeyboardCaptureStartStatus.PlatformError;
        }

        _hookHandle = hookHandle;
        Interlocked.Increment(ref _captureGeneration);
        Volatile.Write(ref _isCapturing, 1);
        return KeyboardCaptureStartStatus.Started;
    }

    public KeyboardCaptureStopStatus StopCapture()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return StopCaptureCore();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _ = StopCaptureCore();
        KeyChanged = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private KeyboardCaptureStopStatus StopCaptureCore()
    {
        if (_hookHandle == 0)
        {
            Volatile.Write(ref _isCapturing, 0);
            return KeyboardCaptureStopStatus.AlreadyStopped;
        }

        if (!UnhookWindowsHookEx(_hookHandle))
        {
            return KeyboardCaptureStopStatus.PlatformError;
        }

        _hookHandle = 0;
        Volatile.Write(ref _isCapturing, 0);
        Interlocked.Exchange(ref _pendingSyntheticAltGrControl, 0);
        Interlocked.Increment(ref _captureGeneration);
        return KeyboardCaptureStopStatus.Stopped;
    }

    private nint HookCallback(int code, nuint message, nint data)
    {
        if (code >= 0)
        {
            try
            {
                if (ProcessHookEvent((uint)message, data))
                {
                    return 1;
                }
            }
            catch
            {
                // The capture callback must never disrupt the global hook chain.
            }
        }

        return CallNextHookEx(0, code, message, data);
    }

    private bool ProcessHookEvent(uint message, nint dataPointer)
    {
        if (!TryGetKeyState(message, out var state))
        {
            return false;
        }

        var data = Marshal.PtrToStructure<LowLevelKeyboardInput>(dataPointer);
        if (ShouldIgnore(data))
        {
            return false;
        }

        var isCalibrationModeEnabled =
            Volatile.Read(ref _isCalibrationModeEnabled) != 0;
        if (isCalibrationModeEnabled && IsSyntheticAltGrControl(data))
        {
            Volatile.Write(
                ref _pendingSyntheticAltGrControl,
                state == KeyboardKeyState.Down ? 1 : 0);
            return Volatile.Read(ref _isBlockingAll) != 0;
        }

        var followsSyntheticAltGrControl = isCalibrationModeEnabled &&
            Interlocked.Exchange(ref _pendingSyntheticAltGrControl, 0) != 0;
        if (!isCalibrationModeEnabled)
        {
            Interlocked.Exchange(ref _pendingSyntheticAltGrControl, 0);
        }

        var hasKnownPhysicalKey = TryTranslatePhysicalKey(data, out var physicalKey);
        var generation = Volatile.Read(ref _captureGeneration);
        var eventArgs = new KeyboardCaptureEventArgs(
            physicalKey,
            state,
            (int)data.ScanCode,
            (data.Flags & LlkhfExtended) != 0,
            (int)data.VirtualKey,
            isAltGr: followsSyntheticAltGrControl &&
                state == KeyboardKeyState.Down &&
                physicalKey == PhysicalKey.AltRight);
        _eventContext.Post(
            static pendingState =>
            {
                var pendingEvent = (PendingKeyEvent)pendingState!;
                pendingEvent.Owner.DispatchKeyEvent(pendingEvent);
            },
            new PendingKeyEvent(this, generation, eventArgs));

        if (Volatile.Read(ref _isBlockingAll) != 0)
        {
            return hasKnownPhysicalKey || isCalibrationModeEnabled;
        }

        if (Volatile.Read(ref _isBlockingEnabled) == 0)
        {
            return false;
        }

        if (!hasKnownPhysicalKey)
        {
            return KeyboardPeekBlockingPolicy.ShouldShowOverlay(
                new KeyboardScanCodeIdentity(
                    (int)data.ScanCode,
                    (data.Flags & LlkhfExtended) != 0),
                Volatile.Read(ref _customHoldPeekLayout));
        }

        var blockingOptions = Volatile.Read(ref _blockingOptions);
        return KeyboardPeekBlockingPolicy.ShouldBlock(
            physicalKey,
            state,
            Volatile.Read(ref _activationChordKeys),
            blockingOptions.BlockNumberRow,
            blockingOptions.ShowServiceKeys);
    }

    private void DispatchKeyEvent(PendingKeyEvent pendingEvent)
    {
        if (!IsCapturing ||
            pendingEvent.Generation != Volatile.Read(ref _captureGeneration))
        {
            return;
        }

        KeyChanged?.Invoke(this, pendingEvent.EventArgs);
    }

    private static bool ShouldIgnore(LowLevelKeyboardInput data) =>
        data.ExtraInfo == (nuint)WindowsSyntheticInput.Signature ||
        (data.Flags & LlkhfInjected) != 0;

    private static bool IsSyntheticAltGrControl(LowLevelKeyboardInput data) =>
        data.VirtualKey == VirtualKeyLeftControl &&
        data.ScanCode == SyntheticAltGrControlScanCode;

    private static bool TryGetKeyState(uint message, out KeyboardKeyState state)
    {
        switch (message)
        {
            case WmKeyDown:
            case WmSystemKeyDown:
                state = KeyboardKeyState.Down;
                return true;

            case WmKeyUp:
            case WmSystemKeyUp:
                state = KeyboardKeyState.Up;
                return true;

            default:
                state = default;
                return false;
        }
    }

    private static bool TryTranslatePhysicalKey(
        LowLevelKeyboardInput data,
        out PhysicalKey key)
    {
        if (data.VirtualKey is >= VirtualKeyF1 and <= VirtualKeyF24)
        {
            key = PhysicalKey.F1 + (int)(data.VirtualKey - VirtualKeyF1);
            return true;
        }

        if (data.VirtualKey == VirtualKeyPause)
        {
            key = PhysicalKey.Pause;
            return true;
        }

        if (data.VirtualKey == VirtualKeyPrintScreen)
        {
            key = PhysicalKey.PrintScreen;
            return true;
        }

        var isExtended = (data.Flags & LlkhfExtended) != 0;
        key = (data.ScanCode, isExtended) switch
        {
            (0x1C, true) => PhysicalKey.NumpadEnter,
            (0x1D, true) => PhysicalKey.ControlRight,
            (0x35, true) => PhysicalKey.NumpadDivide,
            (0x38, true) => PhysicalKey.AltRight,
            (0x47, true) => PhysicalKey.Home,
            (0x48, true) => PhysicalKey.ArrowUp,
            (0x49, true) => PhysicalKey.PageUp,
            (0x4B, true) => PhysicalKey.ArrowLeft,
            (0x4D, true) => PhysicalKey.ArrowRight,
            (0x4F, true) => PhysicalKey.End,
            (0x50, true) => PhysicalKey.ArrowDown,
            (0x51, true) => PhysicalKey.PageDown,
            (0x52, true) => PhysicalKey.Insert,
            (0x53, true) => PhysicalKey.Delete,
            (0x5B, true) => PhysicalKey.MetaLeft,
            (0x5C, true) => PhysicalKey.MetaRight,
            (0x5D, true) => PhysicalKey.ContextMenu,
            (0x01, false) => PhysicalKey.Escape,
            (0x02, false) => PhysicalKey.Digit1,
            (0x03, false) => PhysicalKey.Digit2,
            (0x04, false) => PhysicalKey.Digit3,
            (0x05, false) => PhysicalKey.Digit4,
            (0x06, false) => PhysicalKey.Digit5,
            (0x07, false) => PhysicalKey.Digit6,
            (0x08, false) => PhysicalKey.Digit7,
            (0x09, false) => PhysicalKey.Digit8,
            (0x0A, false) => PhysicalKey.Digit9,
            (0x0B, false) => PhysicalKey.Digit0,
            (0x0C, false) => PhysicalKey.Minus,
            (0x0D, false) => PhysicalKey.Equal,
            (0x0E, false) => PhysicalKey.Backspace,
            (0x0F, false) => PhysicalKey.Tab,
            (0x10, false) => PhysicalKey.KeyQ,
            (0x11, false) => PhysicalKey.KeyW,
            (0x12, false) => PhysicalKey.KeyE,
            (0x13, false) => PhysicalKey.KeyR,
            (0x14, false) => PhysicalKey.KeyT,
            (0x15, false) => PhysicalKey.KeyY,
            (0x16, false) => PhysicalKey.KeyU,
            (0x17, false) => PhysicalKey.KeyI,
            (0x18, false) => PhysicalKey.KeyO,
            (0x19, false) => PhysicalKey.KeyP,
            (0x1A, false) => PhysicalKey.BracketLeft,
            (0x1B, false) => PhysicalKey.BracketRight,
            (0x1C, false) => PhysicalKey.Enter,
            (0x1D, false) => PhysicalKey.ControlLeft,
            (0x1E, false) => PhysicalKey.KeyA,
            (0x1F, false) => PhysicalKey.KeyS,
            (0x20, false) => PhysicalKey.KeyD,
            (0x21, false) => PhysicalKey.KeyF,
            (0x22, false) => PhysicalKey.KeyG,
            (0x23, false) => PhysicalKey.KeyH,
            (0x24, false) => PhysicalKey.KeyJ,
            (0x25, false) => PhysicalKey.KeyK,
            (0x26, false) => PhysicalKey.KeyL,
            (0x27, false) => PhysicalKey.Semicolon,
            (0x28, false) => PhysicalKey.Quote,
            (0x29, false) => PhysicalKey.Backquote,
            (0x2A, false) => PhysicalKey.ShiftLeft,
            (0x2B, false) => PhysicalKey.Backslash,
            (0x2C, false) => PhysicalKey.KeyZ,
            (0x2D, false) => PhysicalKey.KeyX,
            (0x2E, false) => PhysicalKey.KeyC,
            (0x2F, false) => PhysicalKey.KeyV,
            (0x30, false) => PhysicalKey.KeyB,
            (0x31, false) => PhysicalKey.KeyN,
            (0x32, false) => PhysicalKey.KeyM,
            (0x33, false) => PhysicalKey.Comma,
            (0x34, false) => PhysicalKey.Period,
            (0x35, false) => PhysicalKey.Slash,
            (0x36, false) => PhysicalKey.ShiftRight,
            (0x37, false) => PhysicalKey.NumpadMultiply,
            (0x38, false) => PhysicalKey.AltLeft,
            (0x39, false) => PhysicalKey.Space,
            (0x3A, false) => PhysicalKey.CapsLock,
            (0x45, _) => PhysicalKey.NumLock,
            (0x46, false) => PhysicalKey.ScrollLock,
            (0x47, false) => PhysicalKey.Numpad7,
            (0x48, false) => PhysicalKey.Numpad8,
            (0x49, false) => PhysicalKey.Numpad9,
            (0x4A, false) => PhysicalKey.NumpadSubtract,
            (0x4B, false) => PhysicalKey.Numpad4,
            (0x4C, false) => PhysicalKey.Numpad5,
            (0x4D, false) => PhysicalKey.Numpad6,
            (0x4E, false) => PhysicalKey.NumpadAdd,
            (0x4F, false) => PhysicalKey.Numpad1,
            (0x50, false) => PhysicalKey.Numpad2,
            (0x51, false) => PhysicalKey.Numpad3,
            (0x52, false) => PhysicalKey.Numpad0,
            (0x53, false) => PhysicalKey.NumpadDecimal,
            _ => PhysicalKey.Unknown,
        };

        return key != PhysicalKey.Unknown;
    }

    private static bool IsActivationChordHeld(
        IReadOnlySet<PhysicalKey> activationChordKeys)
    {
        if (activationChordKeys.Count == 0 ||
            !IsModifierGroupHeld(
                activationChordKeys,
                PhysicalKey.ControlLeft,
                PhysicalKey.ControlRight) ||
            !IsModifierGroupHeld(
                activationChordKeys,
                PhysicalKey.AltLeft,
                PhysicalKey.AltRight) ||
            !IsModifierGroupHeld(
                activationChordKeys,
                PhysicalKey.ShiftLeft,
                PhysicalKey.ShiftRight) ||
            !IsModifierGroupHeld(
                activationChordKeys,
                PhysicalKey.MetaLeft,
                PhysicalKey.MetaRight))
        {
            return false;
        }

        var hasMainKey = false;
        foreach (var key in activationChordKeys)
        {
            if (IsModifierKey(key))
            {
                continue;
            }

            hasMainKey = true;
            if (!IsKeyPhysicallyDown(key))
            {
                return false;
            }
        }

        return hasMainKey;
    }

    private static bool IsModifierGroupHeld(
        IReadOnlySet<PhysicalKey> activationChordKeys,
        PhysicalKey leftKey,
        PhysicalKey rightKey) =>
        (!activationChordKeys.Contains(leftKey) &&
         !activationChordKeys.Contains(rightKey)) ||
        IsKeyPhysicallyDown(leftKey) ||
        IsKeyPhysicallyDown(rightKey);

    private static bool IsModifierKey(PhysicalKey key) =>
        key is PhysicalKey.ControlLeft or PhysicalKey.ControlRight or
            PhysicalKey.AltLeft or PhysicalKey.AltRight or
            PhysicalKey.ShiftLeft or PhysicalKey.ShiftRight or
            PhysicalKey.MetaLeft or PhysicalKey.MetaRight;

    private static bool IsKeyPhysicallyDown(PhysicalKey key)
    {
        var virtualKey = MapVirtualKey(key);
        return virtualKey != 0 &&
            (GetAsyncKeyState(virtualKey) & KeyStateDownMask) != 0;
    }

    private static int MapVirtualKey(PhysicalKey key)
    {
        if (key is >= PhysicalKey.F1 and <= PhysicalKey.F24)
        {
            return (int)VirtualKeyF1 + (key - PhysicalKey.F1);
        }

        return key switch
        {
            PhysicalKey.Digit0 => 0x30,
            PhysicalKey.Digit1 => 0x31,
            PhysicalKey.Digit2 => 0x32,
            PhysicalKey.Digit3 => 0x33,
            PhysicalKey.Digit4 => 0x34,
            PhysicalKey.Digit5 => 0x35,
            PhysicalKey.Digit6 => 0x36,
            PhysicalKey.Digit7 => 0x37,
            PhysicalKey.Digit8 => 0x38,
            PhysicalKey.Digit9 => 0x39,
            PhysicalKey.KeyA => 0x41,
            PhysicalKey.KeyB => 0x42,
            PhysicalKey.KeyC => 0x43,
            PhysicalKey.KeyD => 0x44,
            PhysicalKey.KeyE => 0x45,
            PhysicalKey.KeyF => 0x46,
            PhysicalKey.KeyG => 0x47,
            PhysicalKey.KeyH => 0x48,
            PhysicalKey.KeyI => 0x49,
            PhysicalKey.KeyJ => 0x4A,
            PhysicalKey.KeyK => 0x4B,
            PhysicalKey.KeyL => 0x4C,
            PhysicalKey.KeyM => 0x4D,
            PhysicalKey.KeyN => 0x4E,
            PhysicalKey.KeyO => 0x4F,
            PhysicalKey.KeyP => 0x50,
            PhysicalKey.KeyQ => 0x51,
            PhysicalKey.KeyR => 0x52,
            PhysicalKey.KeyS => 0x53,
            PhysicalKey.KeyT => 0x54,
            PhysicalKey.KeyU => 0x55,
            PhysicalKey.KeyV => 0x56,
            PhysicalKey.KeyW => 0x57,
            PhysicalKey.KeyX => 0x58,
            PhysicalKey.KeyY => 0x59,
            PhysicalKey.KeyZ => 0x5A,
            PhysicalKey.ShiftLeft => 0xA0,
            PhysicalKey.ShiftRight => 0xA1,
            PhysicalKey.ControlLeft => 0xA2,
            PhysicalKey.ControlRight => 0xA3,
            PhysicalKey.AltLeft => 0xA4,
            PhysicalKey.AltRight => 0xA5,
            PhysicalKey.MetaLeft => 0x5B,
            PhysicalKey.MetaRight => 0x5C,
            _ => 0,
        };
    }

    private sealed record PendingKeyEvent(
        WindowsKeyboardCaptureService Owner,
        int Generation,
        KeyboardCaptureEventArgs EventArgs);

    private sealed record BlockingOptions(
        bool BlockNumberRow,
        bool ShowServiceKeys);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint LowLevelKeyboardProcedure(int code, nuint message, nint data);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct LowLevelKeyboardInput
    {
        public readonly uint VirtualKey;
        public readonly uint ScanCode;
        public readonly uint Flags;
        public readonly uint Time;
        public readonly nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(
        int hookType,
        LowLevelKeyboardProcedure callback,
        nint module,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(
        nint hook,
        int code,
        nuint message,
        nint data);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}

namespace BezBukv.Core.Platform;

public interface IKeyboardCaptureService : IDisposable
{
    event EventHandler<KeyboardCaptureEventArgs>? KeyChanged;

    bool IsCapturing { get; }

    void UpdateActivationChordKeys(IReadOnlySet<PhysicalKey> keys);

    void UpdateCalibrationModeEnabled(bool isEnabled)
    {
    }

    bool IsActivationChordHeld();

    KeyboardCaptureStartStatus StartCapture();

    KeyboardCaptureStopStatus StopCapture();
}

public enum KeyboardCaptureStartStatus
{
    Started,
    AlreadyCapturing,
    PlatformError,
}

public enum KeyboardCaptureStopStatus
{
    Stopped,
    AlreadyStopped,
    PlatformError,
}

public sealed class KeyboardCaptureEventArgs(
    PhysicalKey key,
    KeyboardKeyState state,
    int scanCode = 0,
    bool extended = false,
    int virtualKey = 0,
    bool isAltGr = false) : EventArgs
{
    public PhysicalKey Key { get; } = key;

    public KeyboardKeyState State { get; } = state;

    public int ScanCode { get; } = scanCode;

    public bool Extended { get; } = extended;

    public int VirtualKey { get; } = virtualKey;

    public bool IsAltGr { get; } = isAltGr;
}

public enum KeyboardKeyState
{
    Down,
    Up,
}

public enum PhysicalKey
{
    Unknown,
    Escape,
    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12,
    F13,
    F14,
    F15,
    F16,
    F17,
    F18,
    F19,
    F20,
    F21,
    F22,
    F23,
    F24,
    PrintScreen,
    ScrollLock,
    Pause,
    Backquote,
    Digit1,
    Digit2,
    Digit3,
    Digit4,
    Digit5,
    Digit6,
    Digit7,
    Digit8,
    Digit9,
    Digit0,
    Minus,
    Equal,
    Backspace,
    Tab,
    KeyQ,
    KeyW,
    KeyE,
    KeyR,
    KeyT,
    KeyY,
    KeyU,
    KeyI,
    KeyO,
    KeyP,
    BracketLeft,
    BracketRight,
    Backslash,
    CapsLock,
    KeyA,
    KeyS,
    KeyD,
    KeyF,
    KeyG,
    KeyH,
    KeyJ,
    KeyK,
    KeyL,
    Semicolon,
    Quote,
    Enter,
    ShiftLeft,
    KeyZ,
    KeyX,
    KeyC,
    KeyV,
    KeyB,
    KeyN,
    KeyM,
    Comma,
    Period,
    Slash,
    ShiftRight,
    ControlLeft,
    MetaLeft,
    AltLeft,
    Space,
    AltRight,
    MetaRight,
    ContextMenu,
    ControlRight,
    Insert,
    Home,
    PageUp,
    Delete,
    End,
    PageDown,
    ArrowRight,
    ArrowLeft,
    ArrowDown,
    ArrowUp,
    NumLock,
    NumpadDivide,
    NumpadMultiply,
    NumpadSubtract,
    Numpad7,
    Numpad8,
    Numpad9,
    NumpadAdd,
    Numpad4,
    Numpad5,
    Numpad6,
    Numpad1,
    Numpad2,
    Numpad3,
    NumpadEnter,
    Numpad0,
    NumpadDecimal,
}

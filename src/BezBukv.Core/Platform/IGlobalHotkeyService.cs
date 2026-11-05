namespace BezBukv.Core.Platform;

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? HotkeyPressed;

    GlobalHotkeyRegistrationStatus Register(GlobalHotkey hotkey);

    void Unregister();
}

public enum GlobalHotkeyRegistrationStatus
{
    Success,
    HotkeyUnavailable,
    UnsupportedHotkey,
    PlatformError,
}

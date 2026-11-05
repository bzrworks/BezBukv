using System.Runtime.InteropServices;
using System.Windows.Forms;
using BezBukv.Core.Platform;

namespace BezBukv.Windows;

public sealed class WindowsGlobalHotkeyService : NativeWindow, IGlobalHotkeyService
{
    private const int HotkeyId = 0xB27;
    private const int WindowMessageHotkey = 0x0312;
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private const uint ModifierAlt = 0x0001;
    private const uint ModifierControl = 0x0002;
    private const uint ModifierShift = 0x0004;
    private const uint ModifierWindows = 0x0008;
    private const uint ModifierNoRepeat = 0x4000;

    private const uint VirtualKey0 = 0x30;
    private const uint VirtualKeyA = 0x41;
    private const uint VirtualKeyF1 = 0x70;

    private static readonly IntPtr MessageOnlyWindow = new(-3);

    private bool _registered;
    private bool _disposed;

    public WindowsGlobalHotkeyService()
    {
        CreateHandle(new CreateParams
        {
            Caption = "BezBukv global hotkey",
            Parent = MessageOnlyWindow,
        });
    }

    public event EventHandler? HotkeyPressed;

    public GlobalHotkeyRegistrationStatus Register(GlobalHotkey hotkey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(hotkey);

        if (!TryUnregisterCurrentHotkey())
        {
            return GlobalHotkeyRegistrationStatus.PlatformError;
        }

        if (!TryTranslate(hotkey, out var modifiers, out var virtualKey))
        {
            return GlobalHotkeyRegistrationStatus.UnsupportedHotkey;
        }

        if (!RegisterHotKey(Handle, HotkeyId, modifiers | ModifierNoRepeat, virtualKey))
        {
            return Marshal.GetLastWin32Error() == ErrorHotkeyAlreadyRegistered
                ? GlobalHotkeyRegistrationStatus.HotkeyUnavailable
                : GlobalHotkeyRegistrationStatus.PlatformError;
        }

        _registered = true;
        return GlobalHotkeyRegistrationStatus.Success;
    }

    public void Unregister()
    {
        if (_disposed)
        {
            return;
        }

        _ = TryUnregisterCurrentHotkey();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Unregister();

        DestroyHandle();
        HotkeyPressed = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WindowMessageHotkey && message.WParam == (IntPtr)HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        base.WndProc(ref message);
    }

    private bool TryUnregisterCurrentHotkey()
    {
        if (!_registered)
        {
            return true;
        }

        if (!UnregisterHotKey(Handle, HotkeyId))
        {
            return false;
        }

        _registered = false;
        return true;
    }

    private static bool TryTranslate(
        GlobalHotkey hotkey,
        out uint modifiers,
        out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (hotkey.Modifiers is null || !TryTranslateKey(hotkey.Key, out virtualKey))
        {
            return false;
        }

        foreach (var modifier in hotkey.Modifiers)
        {
            switch (modifier)
            {
                case HotkeyModifier.Primary:
                case HotkeyModifier.Control:
                    modifiers |= ModifierControl;
                    break;

                case HotkeyModifier.Alt:
                    modifiers |= ModifierAlt;
                    break;

                case HotkeyModifier.Shift:
                    modifiers |= ModifierShift;
                    break;

                case HotkeyModifier.Meta:
                    modifiers |= ModifierWindows;
                    break;

                default:
                    virtualKey = 0;
                    return false;
            }
        }

        return true;
    }

    private static bool TryTranslateKey(HotkeyKey key, out uint virtualKey)
    {
        if (key is >= HotkeyKey.A and <= HotkeyKey.Z)
        {
            virtualKey = VirtualKeyA + (uint)key - (uint)HotkeyKey.A;
            return true;
        }

        if (key is >= HotkeyKey.Digit0 and <= HotkeyKey.Digit9)
        {
            virtualKey = VirtualKey0 + (uint)key - (uint)HotkeyKey.Digit0;
            return true;
        }

        if (key is >= HotkeyKey.F1 and <= HotkeyKey.F24)
        {
            virtualKey = VirtualKeyF1 + (uint)key - (uint)HotkeyKey.F1;
            return true;
        }

        virtualKey = 0;
        return false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}

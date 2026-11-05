using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BezBukv.Core.Platform;

namespace BezBukv.Windows;

public sealed class WindowsActiveWindowFocusService : IActiveWindowFocusService
{
    public ActiveWindowFocusToken? CaptureActiveWindow()
    {
        try
        {
            var window = GetForegroundWindow();
            return window == IntPtr.Zero
                ? null
                : new WindowsActiveWindowFocusToken(window);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public bool TryRestoreFocus(ActiveWindowFocusToken? token)
    {
        if (token is not WindowsActiveWindowFocusToken windowsToken)
        {
            return false;
        }

        try
        {
            return SetForegroundWindow(windowsToken.Window);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public Rectangle GetActiveMonitorWorkArea()
    {
        try
        {
            var activeWindow = GetForegroundWindow();
            return GetMonitorWorkArea(activeWindow);
        }
        catch (Exception)
        {
            return SystemInformation.WorkingArea;
        }
    }

    public Rectangle GetActiveMonitorWorkArea(ActiveWindowFocusToken? token)
    {
        try
        {
            return token is WindowsActiveWindowFocusToken windowsToken
                ? GetMonitorWorkArea(windowsToken.Window)
                : SystemInformation.WorkingArea;
        }
        catch (Exception)
        {
            return SystemInformation.WorkingArea;
        }
    }

    private static Rectangle GetMonitorWorkArea(IntPtr window) =>
        window == IntPtr.Zero
            ? SystemInformation.WorkingArea
            : Screen.FromHandle(window).WorkingArea;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    private sealed class WindowsActiveWindowFocusToken(IntPtr window) :
        ActiveWindowFocusToken
    {
        public IntPtr Window { get; } = window;
    }
}

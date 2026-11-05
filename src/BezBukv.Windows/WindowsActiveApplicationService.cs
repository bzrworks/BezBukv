using System.Diagnostics;
using System.Runtime.InteropServices;
using BezBukv.Core.Platform;

namespace BezBukv.Windows;

public sealed class WindowsActiveApplicationService : IActiveApplicationService
{
    public ActiveApplicationInfo GetActiveApplication()
    {
        try
        {
            var foregroundWindow = GetForegroundWindow();

            if (foregroundWindow == IntPtr.Zero)
            {
                return ActiveApplicationInfo.Unavailable;
            }

            _ = GetWindowThreadProcessId(foregroundWindow, out var processId);

            if (processId == 0 || processId > int.MaxValue)
            {
                return ActiveApplicationInfo.Unavailable;
            }

            using var process = Process.GetProcessById((int)processId);
            var processName = process.ProcessName;

            return string.IsNullOrWhiteSpace(processName)
                ? ActiveApplicationInfo.Unavailable
                : ActiveApplicationInfo.FromProcessName(
                    processName,
                    foregroundWindow.ToInt64());
        }
        catch (Exception)
        {
            return ActiveApplicationInfo.Unavailable;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr window,
        out uint processId);
}

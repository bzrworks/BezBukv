using System.Runtime.InteropServices;
using BezBukv.Core.Platform;

namespace BezBukv.Windows;

public sealed class WindowsKeyboardLayoutService : IKeyboardLayoutService
{
    private const uint WmInputLangChangeRequest = 0x0050;
    private const uint SmtoAbortIfHung = 0x0002;
    private const uint MessageTimeoutMilliseconds = 1000;
    private const ushort EnglishUnitedStatesLanguageId = 0x0409;
    private const ushort RussianLanguageId = 0x0419;
    private const ushort EnglishPrimaryLanguageId = 0x09;
    private const ushort RussianPrimaryLanguageId = 0x19;

    public KeyboardLayoutSwitchOutcome SwitchActiveInputLanguage(
        KeyboardInputLanguage targetLanguage)
    {
        try
        {
            var targetLookup = FindTargetWindow(out var targetWindow);
            if (targetLookup == TargetWindowLookupOutcome.NoTargetWindow)
            {
                return KeyboardLayoutSwitchOutcome.NoTargetWindow;
            }

            if (targetLookup == TargetWindowLookupOutcome.Failed)
            {
                return KeyboardLayoutSwitchOutcome.Failed;
            }

            var layoutLookup = FindInstalledLayout(targetLanguage, out var targetLayout);
            if (layoutLookup == InstalledLayoutLookupOutcome.TargetLanguageUnavailable)
            {
                return KeyboardLayoutSwitchOutcome.TargetLanguageUnavailable;
            }

            if (layoutLookup == InstalledLayoutLookupOutcome.Failed)
            {
                return KeyboardLayoutSwitchOutcome.Failed;
            }

            var delivered = SendMessageTimeoutW(
                targetWindow,
                WmInputLangChangeRequest,
                UIntPtr.Zero,
                targetLayout,
                SmtoAbortIfHung,
                MessageTimeoutMilliseconds,
                out _);

            return delivered != IntPtr.Zero
                ? KeyboardLayoutSwitchOutcome.Success
                : KeyboardLayoutSwitchOutcome.Failed;
        }
        catch (Exception)
        {
            return KeyboardLayoutSwitchOutcome.Failed;
        }
    }

    private static TargetWindowLookupOutcome FindTargetWindow(out IntPtr targetWindow)
    {
        targetWindow = IntPtr.Zero;

        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero || !IsWindow(foregroundWindow))
        {
            return TargetWindowLookupOutcome.NoTargetWindow;
        }

        var foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);
        if (foregroundThreadId == 0)
        {
            return TargetWindowLookupOutcome.Failed;
        }

        var threadInfo = new GuiThreadInfo
        {
            Size = (uint)Marshal.SizeOf<GuiThreadInfo>(),
        };

        targetWindow = GetGUIThreadInfo(foregroundThreadId, ref threadInfo) &&
                       threadInfo.FocusWindow != IntPtr.Zero &&
                       IsWindow(threadInfo.FocusWindow)
            ? threadInfo.FocusWindow
            : foregroundWindow;

        return TargetWindowLookupOutcome.Found;
    }

    private static InstalledLayoutLookupOutcome FindInstalledLayout(
        KeyboardInputLanguage targetLanguage,
        out IntPtr targetLayout)
    {
        targetLayout = IntPtr.Zero;

        if (!TryGetLanguageIds(
                targetLanguage,
                out var exactLanguageId,
                out var primaryLanguageId))
        {
            return InstalledLayoutLookupOutcome.Failed;
        }

        var layoutCount = GetKeyboardLayoutList(0, null);
        if (layoutCount <= 0)
        {
            return InstalledLayoutLookupOutcome.Failed;
        }

        var layouts = new IntPtr[layoutCount];
        var copiedLayoutCount = GetKeyboardLayoutList(layouts.Length, layouts);
        if (copiedLayoutCount <= 0)
        {
            return InstalledLayoutLookupOutcome.Failed;
        }

        targetLayout = layouts
            .Take(copiedLayoutCount)
            .FirstOrDefault(layout => GetLanguageId(layout) == exactLanguageId);

        if (targetLayout == IntPtr.Zero)
        {
            targetLayout = layouts
                .Take(copiedLayoutCount)
                .FirstOrDefault(layout => GetPrimaryLanguageId(layout) == primaryLanguageId);
        }

        return targetLayout == IntPtr.Zero
            ? InstalledLayoutLookupOutcome.TargetLanguageUnavailable
            : InstalledLayoutLookupOutcome.Found;
    }

    private static bool TryGetLanguageIds(
        KeyboardInputLanguage language,
        out ushort exactLanguageId,
        out ushort primaryLanguageId)
    {
        switch (language)
        {
            case KeyboardInputLanguage.English:
                exactLanguageId = EnglishUnitedStatesLanguageId;
                primaryLanguageId = EnglishPrimaryLanguageId;
                return true;
            case KeyboardInputLanguage.Russian:
                exactLanguageId = RussianLanguageId;
                primaryLanguageId = RussianPrimaryLanguageId;
                return true;
            default:
                exactLanguageId = 0;
                primaryLanguageId = 0;
                return false;
        }
    }

    private static ushort GetLanguageId(IntPtr layout)
    {
        return unchecked((ushort)((nuint)layout & 0xFFFF));
    }

    private static ushort GetPrimaryLanguageId(IntPtr layout)
    {
        return (ushort)(GetLanguageId(layout) & 0x03FF);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        internal uint Size;
        internal uint Flags;
        internal IntPtr ActiveWindow;
        internal IntPtr FocusWindow;
        internal IntPtr CaptureWindow;
        internal IntPtr MenuOwnerWindow;
        internal IntPtr MoveSizeWindow;
        internal IntPtr CaretWindow;
        internal Rect CaretRect;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    private enum InstalledLayoutLookupOutcome
    {
        Found,
        TargetLanguageUnavailable,
        Failed,
    }

    private enum TargetWindowLookupOutcome
    {
        Found,
        NoTargetWindow,
        Failed,
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(
        IntPtr window,
        out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(
        uint threadId,
        ref GuiThreadInfo guiThreadInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetKeyboardLayoutList(
        int count,
        [Out] IntPtr[]? layouts);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeoutW(
        IntPtr window,
        uint message,
        UIntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeoutMilliseconds,
        out UIntPtr result);
}

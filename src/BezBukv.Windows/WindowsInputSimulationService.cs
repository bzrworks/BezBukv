using System.Runtime.InteropServices;
using BezBukv.Core.Platform;

namespace BezBukv.Windows;

public sealed class WindowsInputSimulationService : IInputSimulationService
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const int KeyStateDownMask = 0x8000;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyC = 0x43;
    private const ushort VirtualKeyV = 0x56;

    public InputSimulationResult SendCopy()
    {
        return SendControlShortcut(VirtualKeyC, includeDiagnosticDetails: false);
    }

    public InputSimulationResult SendPaste()
    {
        return SendPaste(includeDiagnosticDetails: false);
    }

    public InputSimulationResult SendPaste(bool includeDiagnosticDetails)
    {
        return SendControlShortcut(VirtualKeyV, includeDiagnosticDetails);
    }

    private static InputSimulationResult SendControlShortcut(
        ushort virtualKey,
        bool includeDiagnosticDetails)
    {
        var isControlPhysicallyDown =
            (GetAsyncKeyState(VirtualKeyControl) & KeyStateDownMask) != 0;

        Input[] inputs = isControlPhysicallyDown
            ?
            [
                CreateKeyboardInput(virtualKey, 0),
                CreateKeyboardInput(virtualKey, KeyEventKeyUp),
            ]
            :
            [
                CreateKeyboardInput(VirtualKeyControl, 0),
                CreateKeyboardInput(virtualKey, 0),
                CreateKeyboardInput(virtualKey, KeyEventKeyUp),
                CreateKeyboardInput(VirtualKeyControl, KeyEventKeyUp),
            ];

        var sentInputCount = SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<Input>());

        if (sentInputCount == inputs.Length)
        {
            return includeDiagnosticDetails
                ? InputSimulationResult.FromSuccessfulSend(
                    isControlPhysicallyDown,
                    inputs.Length,
                    checked((int)sentInputCount))
                : InputSimulationResult.Success;
        }

        var errorCode = Marshal.GetLastWin32Error();
        return includeDiagnosticDetails
            ? InputSimulationResult.FromPlatformError(
                errorCode,
                isControlPhysicallyDown,
                inputs.Length,
                checked((int)sentInputCount))
            : InputSimulationResult.FromPlatformError(errorCode);
    }

    private static Input CreateKeyboardInput(ushort virtualKey, uint flags)
    {
        return new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Flags = flags,
                    ExtraInfo = (UIntPtr)WindowsSyntheticInput.Signature,
                },
            },
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int DeltaX;
        public int DeltaY;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }
}

internal static class WindowsSyntheticInput
{
    internal const uint Signature = 0xB6B60601;
}

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AutoClicker.Core;

public sealed class InputInjector : IInputInjector
{
    private const uint InputMouse = 0;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;

    public void Click()
    {
        var inputs = new[]
        {
            new Input { Type = InputMouse, Mouse = new MouseInput { Flags = MouseEventLeftDown } },
            new Input { Type = InputMouse, Mouse = new MouseInput { Flags = MouseEventLeftUp } }
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows blocked or rejected the click input.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}

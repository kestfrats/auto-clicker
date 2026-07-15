using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AutoClicker.Core;

public sealed class HotkeyService : IDisposable
{
    private const int ToggleHotkeyId = 1;
    private const int EmergencyStopHotkeyId = 2;
    private const uint ModifierNoRepeat = 0x4000;
    private const uint WmHotkey = 0x0312;
    private const uint WmQuit = 0x0012;

    private readonly Action _onToggle;
    private readonly Action _onEmergencyStop;
    private readonly object _gate = new();
    private Thread? _messageThread;
    private uint _messageThreadId;

    public HotkeyService(Action onToggle, Action onEmergencyStop)
    {
        _onToggle = onToggle;
        _onEmergencyStop = onEmergencyStop;
    }

    public void Register(HotkeyKey toggleHotkey, HotkeyKey emergencyStopHotkey)
    {
        if (toggleHotkey == emergencyStopHotkey)
        {
            throw new InvalidOperationException("Start/stop and emergency stop hotkeys must be different.");
        }

        Stop();

        var ready = new ManualResetEventSlim(false);
        Exception? registrationError = null;

        var thread = new Thread(() =>
        {
            _messageThreadId = GetCurrentThreadId();

            if (!RegisterHotKey(IntPtr.Zero, ToggleHotkeyId, ModifierNoRepeat, (uint)toggleHotkey))
            {
                registrationError = new Win32Exception(Marshal.GetLastWin32Error(), $"Could not register {toggleHotkey}.");
                ready.Set();
                return;
            }

            if (!RegisterHotKey(IntPtr.Zero, EmergencyStopHotkeyId, ModifierNoRepeat, (uint)emergencyStopHotkey))
            {
                registrationError = new Win32Exception(Marshal.GetLastWin32Error(), $"Could not register {emergencyStopHotkey}.");
                UnregisterHotKey(IntPtr.Zero, ToggleHotkeyId);
                ready.Set();
                return;
            }

            ready.Set();

            try
            {
                while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                {
                    if (message.Message != WmHotkey)
                    {
                        continue;
                    }

                    var id = checked((int)message.WParam);
                    if (id == ToggleHotkeyId)
                    {
                        _onToggle();
                    }
                    else if (id == EmergencyStopHotkeyId)
                    {
                        _onEmergencyStop();
                    }
                }
            }
            finally
            {
                UnregisterHotKey(IntPtr.Zero, ToggleHotkeyId);
                UnregisterHotKey(IntPtr.Zero, EmergencyStopHotkeyId);
            }
        })
        {
            IsBackground = true,
            Name = "AutoClicker Hotkey Listener"
        };

        lock (_gate)
        {
            _messageThread = thread;
        }

        thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(3)))
        {
            Stop();
            throw new TimeoutException("The hotkey listener did not start in time.");
        }

        if (registrationError is not null)
        {
            Stop();
            throw registrationError;
        }
    }

    public void Stop()
    {
        Thread? thread;
        uint threadId;

        lock (_gate)
        {
            thread = _messageThread;
            threadId = _messageThreadId;
            _messageThread = null;
            _messageThreadId = 0;
        }

        if (thread is null)
        {
            return;
        }

        if (threadId != 0)
        {
            PostThreadMessage(threadId, WmQuit, UIntPtr.Zero, IntPtr.Zero);
        }

        if (thread.IsAlive && !thread.Join(TimeSpan.FromSeconds(1)))
        {
            throw new TimeoutException("The hotkey listener did not shut down in time.");
        }
    }

    public void Dispose()
    {
        Stop();
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out NativeMessage message, IntPtr windowHandle, uint messageFilterMin, uint messageFilterMax);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint threadId, uint message, UIntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr WindowHandle;
        public uint Message;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int PointX;
        public int PointY;
    }
}

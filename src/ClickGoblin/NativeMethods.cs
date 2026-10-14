using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace ClickGoblin
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT { public uint type; public MOUSEINPUT mouse; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, flags, time;
            public UIntPtr extraInfo;
        }
        [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("winmm.dll")] internal static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] internal static extern uint timeEndPeriod(uint period);
        [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        internal static INPUT[] Event(int button, bool down)
        {
            uint flag = button == 1 ? 8u : button == 2 ? 32u : button >= 3 ? 128u : 2u;
            return new INPUT[] { new INPUT { mouse = new MOUSEINPUT {
                flags = down ? flag : flag << 1, mouseData = button == 3 ? 1u : button == 4 ? 2u : 0u
            } } };
        }
        internal static Func<bool, bool> Sender(int button)
        {
            INPUT[] down = Event(button, true), up = Event(button, false);
            return delegate(bool pressed) { return SendInput(1, pressed ? down : up, Marshal.SizeOf(typeof(INPUT))) == 1; };
        }
        internal static Func<bool> AppGate()
        {
            uint target = 0, self = (uint)Process.GetCurrentProcess().Id;
            return delegate {
                uint foreground;
                GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
                if (foreground == 0 || foreground == self) return false;
                if (target == 0) target = foreground;
                return foreground == target;
            };
        }
    }

}

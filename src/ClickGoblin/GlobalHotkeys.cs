using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ClickGoblin
{
    // Keeps only the assigned shortcuts and their held state; no keystroke history.
    internal sealed class HotkeyMatcher
    {
        private readonly int[] bindings;
        private readonly HashSet<int> held = new HashSet<int>();
        internal HotkeyMatcher(int[] values) { bindings = (int[])values.Clone(); }
        internal bool Process(int key, bool down, uint modifiers, out int command)
        {
            command = -1;
            if (!down) return held.Remove(key);
            if (held.Contains(key)) return true;
            for (int i = 0; i < bindings.Length; i++)
                if ((bindings[i] & 65535) == key && (HotkeyRules.Modifiers(bindings[i]) & 15) == modifiers)
                { held.Add(key); command = i; return true; }
            return false;
        }
    }

    internal sealed class GlobalHotkeys : IDisposable
    {
        internal const int CommandMessage = 0x8001;
        private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
        private readonly HookProc callback;
        private readonly HotkeyMatcher matcher;
        private readonly IntPtr window;
        private IntPtr hook;
        internal bool Ready { get { return hook != IntPtr.Zero; } }
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern IntPtr GetModuleHandle(string name);
        internal GlobalHotkeys(IntPtr target, int[] bindings)
        {
            window = target; matcher = new HotkeyMatcher(bindings); callback = Receive;
            hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        }
        private static bool IsDown(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }
        private IntPtr Receive(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0)
            {
                int kind = message.ToInt32();
                if (kind == 0x100 || kind == 0x104 || kind == 0x101 || kind == 0x105)
                {
                    uint modifiers = (IsDown(0x11) ? 2u : 0u) | (IsDown(0x12) ? 1u : 0u) | (IsDown(0x10) ? 4u : 0u) | (IsDown(0x5B) || IsDown(0x5C) ? 8u : 0u);
                    int action;
                    if (matcher.Process(Marshal.ReadInt32(data), kind == 0x100 || kind == 0x104, modifiers, out action))
                    {
                        if (action >= 0) PostMessage(window, CommandMessage, new IntPtr(407 + action), IntPtr.Zero);
                        return new IntPtr(1);
                    }
                }
            }
            return CallNextHookEx(hook, code, message, data);
        }
        public void Dispose() { if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; } }
    }
}

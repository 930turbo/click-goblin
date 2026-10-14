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
    internal static class HotkeyRules
    {
        internal static bool Valid(int data)
        {
            Keys key = (Keys)data & Keys.KeyCode;
            bool modified = ((Keys)data & (Keys.Control | Keys.Alt)) != 0;
            return (key >= Keys.F1 && key <= Keys.F24) ||
                (modified && ((key >= Keys.A && key <= Keys.Z) || (key >= Keys.D0 && key <= Keys.D9) || key == Keys.Up || key == Keys.Down || key == Keys.Oemplus || key == Keys.OemMinus));
        }
        internal static string Text(int data) { return new KeysConverter().ConvertToString((Keys)data); }
        internal static uint Modifiers(int data)
        {
            return 0x4000u | (((Keys)data & Keys.Control) != 0 ? 2u : 0u) |
                (((Keys)data & Keys.Alt) != 0 ? 1u : 0u) | (((Keys)data & Keys.Shift) != 0 ? 4u : 0u);
        }
    }
}

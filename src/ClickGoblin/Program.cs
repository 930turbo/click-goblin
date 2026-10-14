using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

[assembly: System.Reflection.AssemblyTitle("Click Goblin")]
[assembly: System.Reflection.AssemblyProduct("Click Goblin")]
[assembly: System.Reflection.AssemblyVersion("4.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("4.0.0.0")]

namespace ClickGoblin
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool first;
            using (Mutex mutex = new Mutex(true, "Local\\LightClick.Desktop.1", out first))
            {
                if (!first) { MessageBox.Show("Click Goblin (or an older LightClick) is already open.", "Click Goblin"); return 0; }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            return 0;
        }
    }

}

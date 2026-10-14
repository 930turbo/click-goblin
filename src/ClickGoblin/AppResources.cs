using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ClickGoblin
{
    internal static class AppFont
    {
        private static readonly System.Collections.Generic.List<PrivateFontCollection> collections = new System.Collections.Generic.List<PrivateFontCollection>();
        private static readonly FontFamily regular = Load("Outfit-Regular.ttf");
        private static readonly FontFamily semibold = Load("Outfit-SemiBold.ttf");
        [DllImport("gdi32.dll")] static extern IntPtr AddFontMemResourceEx(IntPtr data, uint size, IntPtr reserved, ref uint count);
        private static FontFamily Load(string name)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) throw new InvalidOperationException("Missing bundled font: " + name);
                byte[] bytes = new byte[stream.Length];
                int position = 0;
                while (position < bytes.Length) position += stream.Read(bytes, position, bytes.Length - position);
                // Font data stays alive for the process lifetime; no system installation.
                IntPtr memory = Marshal.AllocCoTaskMem(bytes.Length);
                Marshal.Copy(bytes, 0, memory, bytes.Length);
                uint count = 0; AddFontMemResourceEx(memory, (uint)bytes.Length, IntPtr.Zero, ref count);
                PrivateFontCollection one = new PrivateFontCollection();
                one.AddMemoryFont(memory, bytes.Length); collections.Add(one);
                return one.Families[0];
            }
        }
        internal static Font Create(float size, bool bold)
        { return new Font(bold ? semibold : regular, size, FontStyle.Regular, GraphicsUnit.Point); }
    }

    internal sealed class ToggleAudio : IDisposable
    {
        private readonly SoundPlayer start, stop;
        internal ToggleAudio()
        {
            start = Player("start.wav"); stop = Player("stop.wav");
        }
        private static SoundPlayer Player(string name)
        {
            SoundPlayer player = new SoundPlayer(Assembly.GetExecutingAssembly().GetManifestResourceStream(name));
            player.Load(); return player;
        }
        internal void Play(bool active) { try { (active ? start : stop).Play(); } catch (InvalidOperationException) { } }
        public void Dispose() { start.Stop(); stop.Stop(); start.Dispose(); stop.Dispose(); }
    }
}

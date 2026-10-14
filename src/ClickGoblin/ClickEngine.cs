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
    internal enum EndReason { Stopped, Limit, Duration, Blocked }

    // Wait handles and monotonic deadlines avoid spinning and keep input off the UI thread.
    internal sealed class ClickEngine : IDisposable
    {
        private readonly ManualResetEvent stop = new ManualResetEvent(false);
        private readonly AutoResetEvent changed = new AutoResetEvent(false);
        private readonly Func<bool, bool> send;
        private readonly Func<bool> gate;
        private readonly Action<EndReason> finished;
        private readonly int holdMs, delayMs, maxClicks, durationMs;
        private long intervalBits, count;
        private Thread worker;
        private volatile bool paused;
        internal long Count { get { return Interlocked.Read(ref count); } }
        internal bool Paused { get { return paused; } }
        internal double Interval { get { return BitConverter.Int64BitsToDouble(Interlocked.Read(ref intervalBits)); } }

        internal ClickEngine(double interval, int hold, int delay, int limit, int duration,
            Func<bool, bool> sender, Func<bool> allow, Action<EndReason> onFinished)
        {
            intervalBits = BitConverter.DoubleToInt64Bits(Math.Max(2, Math.Min(3600000, interval)));
            holdMs = hold; delayMs = delay; maxClicks = limit; durationMs = duration;
            send = sender; gate = allow; finished = onFinished;
        }
        internal void SetInterval(double milliseconds)
        {
            Interlocked.Exchange(ref intervalBits, BitConverter.DoubleToInt64Bits(Math.Max(2, Math.Min(3600000, milliseconds))));
            changed.Set();
        }
        internal void Start()
        {
            worker = new Thread(Run) { IsBackground = true, Name = "Click Goblin input" };
            worker.Start();
        }
        private void Run()
        {
            bool held = false, precision = false;
            EndReason reason = EndReason.Stopped;
            WaitHandle[] waits = { stop, changed };
            try
            {
                if (stop.WaitOne(delayMs)) return;
                Stopwatch clock = Stopwatch.StartNew();
                double next = Interval;
                while (!stop.WaitOne(0))
                {
                    double now = clock.Elapsed.TotalMilliseconds;
                    double remaining = durationMs > 0 ? durationMs - now : double.MaxValue;
                    if (remaining <= 0) { reason = EndReason.Duration; break; }
                    if (Interval <= 50 && !precision) precision = Native.timeBeginPeriod(1) == 0;
                    else if (Interval > 50 && precision) { Native.timeEndPeriod(1); precision = false; }
                    if (gate != null && !gate())
                    {
                        paused = true;
                        if (WaitHandle.WaitAny(waits, (int)Math.Ceiling(Math.Min(100, remaining))) == 0) break;
                        continue;
                    }
                    if (paused) { paused = false; next = now; }
                    double wait = Math.Min(next - now, remaining);
                    if (wait > 0)
                    {
                        int result = WaitHandle.WaitAny(waits, (int)Math.Ceiling(wait));
                        if (result == 0) break;
                        if (result == 1) next = clock.Elapsed.TotalMilliseconds + Interval;
                        continue;
                    }
                    // Foreground can change while waiting for a long interval.
                    if (gate != null && !gate()) { paused = true; continue; }
                    if (durationMs > 0 && clock.Elapsed.TotalMilliseconds >= durationMs) { reason = EndReason.Duration; break; }
                    if (!send(true)) { reason = EndReason.Blocked; break; }
                    held = true;
                    double pressedAt = clock.Elapsed.TotalMilliseconds;
                    int hold = Math.Max(1, Math.Min(holdMs, (int)(Interval / 2)));
                    if (durationMs > 0) hold = Math.Min(hold, Math.Max(1, (int)(durationMs - pressedAt)));
                    if (stop.WaitOne(hold)) break;
                    if (!send(false)) { reason = EndReason.Blocked; break; }
                    held = false;
                    long total = Interlocked.Increment(ref count);
                    if (maxClicks > 0 && total >= maxClicks) { reason = EndReason.Limit; break; }
                    next = pressedAt + Interval;
                    // Never emit catch-up bursts after the OS stalls.
                    if (next < clock.Elapsed.TotalMilliseconds) next = clock.Elapsed.TotalMilliseconds + 1;
                }
            }
            catch { reason = EndReason.Blocked; }
            finally
            {
                if (held) { try { send(false); } catch { } }
                if (precision) Native.timeEndPeriod(1);
                if (finished != null) finished(reason);
            }
        }
        public void Dispose()
        {
            stop.Set();
            if (worker != null) worker.Join();
            stop.Dispose(); changed.Dispose();
        }
    }

}

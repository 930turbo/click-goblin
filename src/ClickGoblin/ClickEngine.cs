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
        private readonly Func<int, bool, bool> staggerSend;
        private readonly int channels;
        private long intervalBits, count;
        private Thread worker;
        private volatile bool paused;
        internal long Count { get { return Interlocked.Read(ref count); } }
        internal bool Paused { get { return paused; } }
        internal double Interval { get { return BitConverter.Int64BitsToDouble(Interlocked.Read(ref intervalBits)); } }

        internal ClickEngine(double interval, int hold, int delay, int limit, int duration,
            Func<bool, bool> sender, Func<bool> allow, Action<EndReason> onFinished,
            int channelCount = 1, Func<int, bool, bool> staggeredSender = null)
        {
            intervalBits = BitConverter.DoubleToInt64Bits(Math.Max(2, Math.Min(3600000, interval)));
            holdMs = hold; delayMs = delay; maxClicks = limit; durationMs = duration;
            send = sender; gate = allow; finished = onFinished;
            channels = Math.Max(1, channelCount); staggerSend = staggeredSender;
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
            int heldChannel = 0;
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
                    if (Interval / channels <= 50 && !precision) precision = Native.timeBeginPeriod(1) == 0;
                    else if (Interval / channels > 50 && precision) { Native.timeEndPeriod(1); precision = false; }
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
                    double pressedAt = clock.Elapsed.TotalMilliseconds;
                    bool complete = true;
                    double cyclePeriod = Interval;
                    double nextChannel = pressedAt;
                    for (int channel = 0; channel < channels; channel++)
                    {
                        if (channel > 0)
                        {
                            while (true)
                            {
                                double gap = nextChannel - clock.Elapsed.TotalMilliseconds;
                                if (durationMs > 0) gap = Math.Min(gap, durationMs - clock.Elapsed.TotalMilliseconds);
                                if (gap <= 0) break;
                                int outcome = WaitHandle.WaitAny(waits, (int)Math.Ceiling(gap));
                                if (outcome == 0) { complete = false; break; }
                                if (outcome == 1) { cyclePeriod = Interval; nextChannel = clock.Elapsed.TotalMilliseconds + cyclePeriod / channels; }
                            }
                            if (!complete) break;
                        }
                        if (stop.WaitOne(0)) { complete = false; break; }
                        if (durationMs > 0 && clock.Elapsed.TotalMilliseconds >= durationMs) { reason = EndReason.Duration; complete = false; break; }
                        if (gate != null && !gate()) { paused = true; complete = false; break; }
                        heldChannel = channel;
                        if (!Send(channel, true)) { reason = EndReason.Blocked; complete = false; break; }
                        held = true;
                        nextChannel = clock.Elapsed.TotalMilliseconds + cyclePeriod / channels;
                        int hold = Math.Max(1, Math.Min(holdMs, (int)(cyclePeriod / (2 * channels))));
                        if (durationMs > 0) hold = Math.Min(hold, Math.Max(1, (int)(durationMs - clock.Elapsed.TotalMilliseconds)));
                        if (stop.WaitOne(hold)) { complete = false; break; }
                        if (!Send(channel, false)) { reason = EndReason.Blocked; complete = false; break; }
                        held = false;
                    }
                    if (!complete)
                    {
                        if (held || reason != EndReason.Stopped || stop.WaitOne(0)) break;
                        continue;
                    }
                    long total = Interlocked.Increment(ref count);
                    if (maxClicks > 0 && total >= maxClicks) { reason = EndReason.Limit; break; }
                    next = Math.Max(pressedAt + Interval, nextChannel);
                    // Never emit catch-up bursts after the OS stalls.
                    if (next < clock.Elapsed.TotalMilliseconds) next = clock.Elapsed.TotalMilliseconds + 1;
                }
            }
            catch { reason = EndReason.Blocked; }
            finally
            {
                if (held) { try { Send(heldChannel, false); } catch { } }
                if (precision) Native.timeEndPeriod(1);
                if (finished != null) finished(reason);
            }
        }
        private bool Send(int channel, bool pressed) { return staggerSend == null ? send(pressed) : staggerSend(channel, pressed); }
        public void Dispose()
        {
            stop.Set();
            if (worker != null) worker.Join();
            stop.Dispose(); changed.Dispose();
        }
    }

}

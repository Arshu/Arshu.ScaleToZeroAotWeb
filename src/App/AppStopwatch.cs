using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Arshu.App
{
    //Can be removed by directly using Stopwatch but GetElapsedTime not available in .NET Standard Libarary.
    public readonly struct AppStopwatch
    {
        private static readonly double s_timestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        public static long GetTimestamp() => Stopwatch.GetTimestamp();

        private static TimeSpan GetElapsedTime(long startTimestamp, long endTimestamp)
        {
            var timestampDelta = endTimestamp - startTimestamp;
            var ticks = (long)(s_timestampToTicks * timestampDelta);
            return new TimeSpan(ticks);
        }

        public static TimeSpan GetElapsedTime(long startTimestamp) => GetElapsedTime(startTimestamp, GetTimestamp());
    }
}

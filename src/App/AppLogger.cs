using System;

namespace Arshu.App
{
    public static class AppLogger
    {
        public static long AppStartTime = AppStopwatch.GetTimestamp();
        public static string FirstAddress = "";
        public static double lastLapsedTime = 0;

        public static void WriteLog(string message)
        {
            double lapsedTime = AppStopwatch.GetElapsedTime(AppStartTime).TotalMilliseconds;
            double lapsedDiffTime = lapsedTime - lastLapsedTime;
            lastLapsedTime = lapsedTime;
#if DEBUG
            Console.WriteLine("[" + DateTime.UtcNow.ToString("yyyy/MM/dd HH:mm:ss.fff tt") + "]LapsedTime[" + lapsedTime.ToString("00000.00") + "ms]DiffTime[" + lapsedDiffTime.ToString("00000.00") + "ms]" + message);
#else           
            if ((string.IsNullOrEmpty(FirstAddress) == true) || (AppBase.IsLocalHost(FirstAddress) ==true))
            {
                Console.WriteLine("[" + DateTime.UtcNow.ToString("yyyy/MM/dd HH:mm:ss.fff tt") + "]LapsedTime[" + lapsedTime.ToString("00000.00") + "ms]DiffTime[" + lapsedDiffTime.ToString("00000.00") + "ms]" + message);
            }
            else
            {
                Console.WriteLine("LapsedTime[" + lapsedTime.ToString("00000.00") + "ms]DiffTime[" + lapsedDiffTime.ToString("00000.00") + "ms]" + message);
            }
#endif
        }
    }

}

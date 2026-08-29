using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace BlackBrowser
{
    public static class MemoryTrimmer
    {
        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        public static void TrimProcessMemory()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect(2, GCCollectionMode.Optimized, false);
                    SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
                }
                catch { }
            });
        }

        public static long GetWorkingSetMB()
        {
            try
            {
                return Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
            }
            catch
            {
                return 38;
            }
        }
    }
}

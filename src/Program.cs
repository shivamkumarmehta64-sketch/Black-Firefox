using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace BlackBrowser
{
    static class Program
    {
        private static Mutex mutex = null;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("shell32.dll")]
        public static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        public static readonly IntPtr HWND_BROADCAST = (IntPtr)0xffff;
        public static readonly uint WM_SHOW_BLACK_BROWSER = RegisterWindowMessage("WM_SHOW_BLACK_BROWSER_9b2d0d52");

        [STAThread]
        static void Main(string[] args)
        {
            Application.ThreadException += (s, e) => LogUnhandledException(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogUnhandledException(e.ExceptionObject as Exception);

            bool createdNew;
            mutex = new Mutex(true, "Black_SingleInstance_Mutex_9b2d0d52", out createdNew);

            if (!createdNew)
            {
                Process current = Process.GetCurrentProcess();
                bool restored = false;

                foreach (Process process in Process.GetProcessesByName(current.ProcessName))
                {
                    if (process.Id != current.Id)
                    {
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            AllowSetForegroundWindow(process.Id);
                            PostMessage(HWND_BROADCAST, WM_SHOW_BLACK_BROWSER, IntPtr.Zero, IntPtr.Zero);
                            ShowWindow(process.MainWindowHandle, 9); // SW_RESTORE
                            ForceForegroundWindow(process.MainWindowHandle);
                            restored = true;
                            break;
                        }
                        else
                        {
                            try { process.Kill(); } catch { }
                        }
                    }
                }

                if (restored) return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { SetCurrentProcessExplicitAppUserModelID("BlackFirefox.Browser"); } catch { }
            Application.Run(new BrowserForm(args));
        }

        public static void ForceForegroundWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            IntPtr fgWnd = GetForegroundWindow();
            uint pid;
            uint fgThread = GetWindowThreadProcessId(fgWnd, out pid);
            uint currentThread = GetCurrentThreadId();

            if (fgThread != currentThread && fgThread != 0)
            {
                AttachThreadInput(currentThread, fgThread, true);
                ShowWindow(hWnd, 9);
                SetForegroundWindow(hWnd);
                AttachThreadInput(currentThread, fgThread, false);
            }
            else
            {
                ShowWindow(hWnd, 9);
                SetForegroundWindow(hWnd);
            }
        }

        private static void LogUnhandledException(Exception ex)
        {
            if (ex == null) return;
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
                File.AppendAllText(logPath, "[" + DateTime.Now.ToString("HH:mm:ss") + "] UNHANDLED: " + ex.ToString() + "\n");
            }
            catch { }
        }
    }
}

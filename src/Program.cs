using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BlackBrowser
{
    static class Program
    {
        private static Mutex mutex = null;
        private const string PipeName = "Black_SingleInstance_Pipe_9b2d0d52";

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

        [DllImport("user32.dll")]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        public static readonly IntPtr HWND_BROADCAST = (IntPtr)0xffff;
        public static readonly uint WM_SHOW_BLACK_BROWSER = RegisterWindowMessage("WM_SHOW_BLACK_BROWSER_9b2d0d52");

        public static string GetLogFilePath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "black-webview2");
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch { }
            return Path.Combine(dir, "debug.log");
        }

        public static void AppendLog(string line)
        {
            try
            {
                string logPath = GetLogFilePath();
                if (File.Exists(logPath) && new FileInfo(logPath).Length > 512 * 1024)
                {
                    string prev = logPath + ".1";
                    try { if (File.Exists(prev)) File.Delete(prev); } catch { }
                    try { File.Move(logPath, prev); } catch { }
                }
                File.AppendAllText(logPath, line + "\n", Encoding.UTF8);
            }
            catch { }
        }

        private static string ExtractFirstLaunchTarget(string[] args)
        {
            if (args == null) return "";
            foreach (string raw in args)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string a = raw.Trim();
                if (a.StartsWith("--") && !a.Equals("--youtube", StringComparison.OrdinalIgnoreCase) && !a.Equals("--ytmusic", StringComparison.OrdinalIgnoreCase))
                    continue;
                return a;
            }
            return "";
        }

        private static bool TrySendToExistingInstance(string payload)
        {
            try
            {
                using (NamedPipeClientStream client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    client.Connect(1200);
                    byte[] bytes = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(payload) ? "__SHOW__" : payload);
                    client.Write(bytes, 0, bytes.Length);
                    client.Flush();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void StartIpcServer(BrowserForm main)
        {
            Thread t = new Thread(() =>
            {
                while (main != null && !main.IsDisposed)
                {
                    try
                    {
                        using (NamedPipeServerStream server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None))
                        {
                            server.WaitForConnection();
                            using (StreamReader reader = new StreamReader(server, Encoding.UTF8))
                            {
                                string msg = reader.ReadToEnd();
                                if (!main.IsDisposed && main.IsHandleCreated)
                                {
                                    main.BeginInvoke((Action)(() =>
                                    {
                                        main.HandleExternalLaunchArg(msg);
                                    }));
                                }
                            }
                        }
                    }
                    catch
                    {
                        Thread.Sleep(250);
                    }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        [STAThread]
        static void Main(string[] args)
        {
            Application.ThreadException += (s, e) => LogUnhandledException(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogUnhandledException(e.ExceptionObject as Exception);

            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }

            bool createdNew;
            bool forceNewWindow = false;
            if (args != null)
            {
                foreach (string a in args)
                {
                    if (a == "--newwindow") { forceNewWindow = true; break; }
                }
            }
            if (!forceNewWindow)
            {
                try
                {
                    mutex = new Mutex(true, "Black_SingleInstance_Mutex_9b2d0d52", out createdNew);
                }
                catch
                {
                    createdNew = true;
                }
            }
            else
            {
                createdNew = true;
            }

            if (!createdNew)
            {
                string launchArg = ExtractFirstLaunchTarget(args);
                if (TrySendToExistingInstance(launchArg))
                {
                    return;
                }

                Process current = Process.GetCurrentProcess();
                bool restored = false;

                foreach (Process process in Process.GetProcessesByName(current.ProcessName))
                {
                    if (process.Id != current.Id && process.MainWindowHandle != IntPtr.Zero)
                    {
                        AllowSetForegroundWindow(process.Id);
                        PostMessage(HWND_BROADCAST, WM_SHOW_BLACK_BROWSER, IntPtr.Zero, IntPtr.Zero);
                        ShowWindow(process.MainWindowHandle, 9); // SW_RESTORE
                        ForceForegroundWindow(process.MainWindowHandle);
                        restored = true;
                        break;
                    }
                }

                if (restored) return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { SetCurrentProcessExplicitAppUserModelID("BlackFirefox.Browser"); } catch { }

            if (!EnsureWebView2RuntimeAvailable())
            {
                return;
            }

            SplashForm splash = null;
            try
            {
                splash = new SplashForm();
                splash.Show();
                splash.Update();
            }
            catch { }

            BrowserForm main = new BrowserForm(args);
            if (!forceNewWindow)
            {
                StartIpcServer(main);
            }
            main.Shown += (s, e) =>
            {
                try { if (splash != null && !splash.IsDisposed) splash.BeginClose(); } catch { }
            };

            Application.Run(main);
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

        public static string GetInstalledWebView2Version()
        {
            try
            {
                string ver = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString(null);
                return string.IsNullOrWhiteSpace(ver) ? null : ver.Trim();
            }
            catch
            {
                return null;
            }
        }

        public static bool EnsureWebView2RuntimeAvailable()
        {
            if (!string.IsNullOrEmpty(GetInstalledWebView2Version()))
                return true;

            AppendLog("[" + DateTime.Now.ToString("HH:mm:ss") + "] WebView2 Runtime not detected on startup.");

            DialogResult choice = MessageBox.Show(
                "Black Firefox requires the Microsoft Edge WebView2 Runtime to display websites, and it was not found on this PC.\n\n"
                + "Would you like to download and install the official WebView2 Runtime (~2 MB) now?",
                "Black Firefox — WebView2 Runtime Required",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (choice != DialogResult.Yes)
            {
                return false;
            }

            try
            {
                string setupDir = Path.Combine(Path.GetTempPath(), "BlackFirefoxSetup");
                if (!Directory.Exists(setupDir)) Directory.CreateDirectory(setupDir);
                string bootstrapperPath = Path.Combine(setupDir, "MicrosoftEdgeWebview2Setup.exe");

                System.Net.ServicePointManager.SecurityProtocol =
                    System.Net.ServicePointManager.SecurityProtocol | (System.Net.SecurityProtocolType)3072;

                using (System.Net.WebClient wc = new System.Net.WebClient())
                {
                    wc.Headers.Add("User-Agent", "Black-Firefox-Setup/9.0");
                    wc.DownloadFile("https://go.microsoft.com/fwlink/p/?LinkId=2124703", bootstrapperPath);
                }

                if (File.Exists(bootstrapperPath))
                {
                    using (Process p = Process.Start(new ProcessStartInfo(bootstrapperPath, "/install") { UseShellExecute = true }))
                    {
                        if (p != null) p.WaitForExit();
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog("[" + DateTime.Now.ToString("HH:mm:ss") + "] WebView2 bootstrapper failed: " + ex.Message);
            }

            if (!string.IsNullOrEmpty(GetInstalledWebView2Version()))
            {
                return true;
            }

            DialogResult openManual = MessageBox.Show(
                "WebView2 Runtime installation did not complete.\n\nOpen Microsoft's WebView2 download page in your default browser?",
                "Black Firefox — WebView2 Setup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Error);
            if (openManual == DialogResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://developer.microsoft.com/en-us/microsoft-edge/webview2/") { UseShellExecute = true });
                }
                catch { }
            }
            return false;
        }

        private static void LogUnhandledException(Exception ex)
        {
            if (ex == null) return;
            AppendLog("[" + DateTime.Now.ToString("HH:mm:ss") + "] UNHANDLED: " + ex.ToString());
        }
    }
}

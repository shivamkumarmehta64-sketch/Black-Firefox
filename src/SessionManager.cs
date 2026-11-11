using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace BlackBrowser
{
    public class SessionTabEntry
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public bool Pinned { get; set; }
    }

    public class SessionStateData
    {
        public int ActiveIndex { get; set; }
        public List<SessionTabEntry> Tabs { get; set; }
    }

    public static class SessionManager
    {
        private static readonly object fileLock = new object();
        private static readonly string SessionPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "black-webview2", "session.json");

        public static void SaveSession(List<SessionTabEntry> tabs, int activeIndex)
        {
            lock (fileLock)
            {
                try
                {
                    string dir = Path.GetDirectoryName(SessionPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    SessionStateData data = new SessionStateData
                    {
                        ActiveIndex = activeIndex,
                        Tabs = tabs ?? new List<SessionTabEntry>()
                    };
                    string json = new JavaScriptSerializer().Serialize(data);
                    File.WriteAllText(SessionPath, json, Encoding.UTF8);
                }
                catch { }
            }
        }

        public static SessionStateData LoadSession()
        {
            lock (fileLock)
            {
                try
                {
                    if (!File.Exists(SessionPath)) return null;
                    string json = File.ReadAllText(SessionPath, Encoding.UTF8);
                    return new JavaScriptSerializer().Deserialize<SessionStateData>(json);
                }
                catch
                {
                    return null;
                }
            }
        }

        public static void ClearSession()
        {
            lock (fileLock)
            {
                try { if (File.Exists(SessionPath)) File.Delete(SessionPath); } catch { }
            }
        }
    }
}


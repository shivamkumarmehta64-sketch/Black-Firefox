using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace BlackBrowser
{
    public static class DefaultBrowserManager
    {
        public static bool RegisterAsDefaultBrowser(out string error)
        {
            error = "";
            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    error = "Executable path could not be determined.";
                    return false;
                }

                string progId = "BlackBrowserHTML";
                string appName = "Black Browser";
                string iconPath = exePath + ",0";
                string openCommand = "\"" + exePath + "\" \"%1\"";

                // 1. Register ProgID under HKCU\Software\Classes
                using (RegistryKey classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + progId))
                {
                    if (classes != null)
                    {
                        classes.SetValue("", appName + " Document");
                        classes.SetValue("FriendlyTypeName", appName + " Document");

                        using (RegistryKey icon = classes.CreateSubKey("DefaultIcon"))
                        {
                            if (icon != null) icon.SetValue("", iconPath);
                        }

                        using (RegistryKey shell = classes.CreateSubKey(@"shell\open\command"))
                        {
                            if (shell != null) shell.SetValue("", openCommand);
                        }
                    }
                }

                // 2. Register StartMenuInternet client under HKCU
                using (RegistryKey client = Registry.CurrentUser.CreateSubKey(@"Software\Clients\StartMenuInternet\BlackBrowser"))
                {
                    if (client != null)
                    {
                        client.SetValue("", appName);

                        using (RegistryKey icon = client.CreateSubKey("DefaultIcon"))
                        {
                            if (icon != null) icon.SetValue("", iconPath);
                        }

                        using (RegistryKey shell = client.CreateSubKey(@"shell\open\command"))
                        {
                            if (shell != null) shell.SetValue("", "\"" + exePath + "\"");
                        }

                        using (RegistryKey capabilities = client.CreateSubKey("Capabilities"))
                        {
                            if (capabilities != null)
                            {
                                capabilities.SetValue("ApplicationDescription", "Black Browser - High performance, private web browser.");
                                capabilities.SetValue("ApplicationIcon", iconPath);
                                capabilities.SetValue("ApplicationName", appName);

                                using (RegistryKey fileAssoc = capabilities.CreateSubKey("FileAssociations"))
                                {
                                    if (fileAssoc != null)
                                    {
                                        fileAssoc.SetValue(".htm", progId);
                                        fileAssoc.SetValue(".html", progId);
                                        fileAssoc.SetValue(".pdf", progId);
                                        fileAssoc.SetValue(".svg", progId);
                                    }
                                }

                                using (RegistryKey urlAssoc = capabilities.CreateSubKey("URLAssociations"))
                                {
                                    if (urlAssoc != null)
                                    {
                                        urlAssoc.SetValue("http", progId);
                                        urlAssoc.SetValue("https", progId);
                                        urlAssoc.SetValue("ftp", progId);
                                    }
                                }
                            }
                        }
                    }
                }

                // 3. Register in RegisteredApplications
                using (RegistryKey regApps = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
                {
                    if (regApps != null)
                    {
                        regApps.SetValue("BlackBrowser", @"Software\Clients\StartMenuInternet\BlackBrowser\Capabilities");
                    }
                }

                // 4. Open Windows 10/11 Default Apps Settings
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "ms-settings:defaultapps",
                        UseShellExecute = true
                    });
                }
                catch { }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool IsRegistered()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Clients\StartMenuInternet\BlackBrowser"))
                {
                    return key != null;
                }
            }
            catch { return false; }
        }
    }
}

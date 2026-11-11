using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace BlackBrowser
{
    public class ExtensionItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Path { get; set; }
        public bool Enabled { get; set; }
        public string IconPath { get; set; }
        public int ManifestVersion { get; set; }
        public string PermissionsSummary { get; set; }
    }

    public static class ExtensionManager
    {
        private static readonly object fileLock = new object();
        private static string extensionsRoot;
        private static string configFilePath;
        private static List<ExtensionItem> extensionList = new List<ExtensionItem>();

        static ExtensionManager()
        {
            extensionsRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "black-webview2", "extensions");
            configFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "black-webview2", "extensions_config.json");

            try
            {
                if (!Directory.Exists(extensionsRoot)) Directory.CreateDirectory(extensionsRoot);
            }
            catch { }

            LoadExtensions();
        }

        public static string GetExtensionsDirectory()
        {
            return extensionsRoot;
        }

        private static string JsEscape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");
        }

        private static string AttributeEscape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("'", "&#39;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static void LoadExtensions()
        {
            lock (fileLock)
            {
                try
                {
                    if (File.Exists(configFilePath))
                    {
                        var serializer = new JavaScriptSerializer();
                        string json = File.ReadAllText(configFilePath, Encoding.UTF8);
                        extensionList = serializer.Deserialize<List<ExtensionItem>>(json) ?? new List<ExtensionItem>();
                    }
                    else
                    {
                        extensionList = new List<ExtensionItem>();
                    }
                }
                catch
                {
                    extensionList = new List<ExtensionItem>();
                }
            }
        }

        private static void SaveExtensions()
        {
            lock (fileLock)
            {
                try
                {
                    var serializer = new JavaScriptSerializer();
                    string json = serializer.Serialize(extensionList);
                    File.WriteAllText(configFilePath, json, Encoding.UTF8);
                }
                catch { }
            }
        }

        public static List<ExtensionItem> GetInstalledExtensions()
        {
            lock (fileLock)
            {
                return new List<ExtensionItem>(extensionList);
            }
        }

        public static string GetExtensionArguments()
        {
            lock (fileLock)
            {
                List<string> paths = new List<string>();
                foreach (var ext in extensionList)
                {
                    if (ext.Enabled && Directory.Exists(ext.Path))
                    {
                        paths.Add(ext.Path);
                    }
                }

                if (paths.Count == 0) return "--enable-features=msEdgeExtensionAPI,ExtensionsToolbarMenu";

                return "--enable-features=msEdgeExtensionAPI,ExtensionsToolbarMenu --load-extension=\"" + string.Join(",", paths.ToArray()) + "\"";
            }
        }

        public static bool AddUnpackedExtension(string folderPath, out string error)
        {
            error = "";
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                error = "Directory does not exist.";
                return false;
            }

            string manifestPath = Path.Combine(folderPath, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                error = "manifest.json not found in the selected folder.";
                return false;
            }

            try
            {
                string manifestContent = File.ReadAllText(manifestPath, Encoding.UTF8);
                var serializer = new JavaScriptSerializer();
                var dict = serializer.Deserialize<Dictionary<string, object>>(manifestContent);

                string name = dict.ContainsKey("name") ? dict["name"].ToString() : Path.GetFileName(folderPath);
                string version = dict.ContainsKey("version") ? dict["version"].ToString() : "1.0.0";
                string desc = dict.ContainsKey("description") ? dict["description"].ToString() : "Unpacked browser extension";
                int manifestVer = 2;
                if (dict.ContainsKey("manifest_version") && dict["manifest_version"] != null)
                {
                    int mv;
                    if (int.TryParse(dict["manifest_version"].ToString(), out mv)) manifestVer = mv;
                }

                List<string> perms = new List<string>();
                bool hasBroadAccess = false;
                string[] permKeys = new string[] { "permissions", "host_permissions" };
                foreach (string pk in permKeys)
                {
                    if (dict.ContainsKey(pk) && dict[pk] is System.Collections.IEnumerable && !(dict[pk] is string))
                    {
                        foreach (object po in (System.Collections.IEnumerable)dict[pk])
                        {
                            string ps = po != null ? po.ToString() : "";
                            if (string.IsNullOrWhiteSpace(ps)) continue;
                            perms.Add(ps);
                            if (ps == "<all_urls>" || ps.Contains("*://*/*") || ps == "cookies" ||
                                ps == "webRequest" || ps == "debugger" || ps == "nativeMessaging")
                            {
                                hasBroadAccess = true;
                            }
                        }
                    }
                }
                string permSummary = perms.Count == 0
                    ? ("MV" + manifestVer + " • No elevated permissions")
                    : ("MV" + manifestVer + (hasBroadAccess ? " • Elevated: " : " • ") + string.Join(", ", perms.ToArray()));
                if (permSummary.Length > 140) permSummary = permSummary.Substring(0, 137) + "...";

                string id = Guid.NewGuid().ToString("N");

                lock (fileLock)
                {
                    // Remove if already exists with same path
                    extensionList.RemoveAll(x => x.Path.Equals(folderPath, StringComparison.OrdinalIgnoreCase));

                    extensionList.Add(new ExtensionItem
                    {
                        Id = id,
                        Name = name,
                        Version = version,
                        Description = desc,
                        Path = folderPath,
                        Enabled = true,
                        IconPath = "",
                        ManifestVersion = manifestVer,
                        PermissionsSummary = permSummary
                    });

                    SaveExtensions();
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "Failed to parse manifest: " + ex.Message;
                return false;
            }
        }

        public static void ToggleExtension(string id, bool enabled)
        {
            lock (fileLock)
            {
                var ext = extensionList.Find(x => x.Id == id);
                if (ext != null)
                {
                    ext.Enabled = enabled;
                    SaveExtensions();
                }
            }
        }

        public static void RemoveExtension(string id)
        {
            lock (fileLock)
            {
                extensionList.RemoveAll(x => x.Id == id);
                SaveExtensions();
            }
        }

        public static string GetExtensionsHtml(bool isDark)
        {
            var list = GetInstalledExtensions();
            StringBuilder cards = new StringBuilder();

            if (list.Count == 0)
            {
                cards.Append(@"
                <div class='empty-state'>
                    <div class='empty-icon'>🧩</div>
                    <div class='empty-title'>No Extensions Installed Yet</div>
                    <div class='empty-desc'>Load unpacked Chrome / Edge extensions from your local disk to extend Black Firefox with ad blockers, developer tools, themes, and scripts.</div>
                    <a href='black://load-extension' class='btn-primary' style='display:inline-block;margin-top:16px;'>📂 Load Unpacked Extension</a>
                </div>");
            }
            else
            {
                foreach (var ext in list)
                {
                    string safeName = System.Web.HttpUtility.HtmlEncode(ext.Name ?? "Extension");
                    string safeVer = System.Web.HttpUtility.HtmlEncode(ext.Version ?? "1.0");
                    string safeDesc = System.Web.HttpUtility.HtmlEncode(ext.Description ?? "");
                    string safePath = System.Web.HttpUtility.HtmlEncode(ext.Path ?? "");
                    string safeId = System.Web.HttpUtility.HtmlEncode(ext.Id ?? "");
                    string safePerms = System.Web.HttpUtility.HtmlEncode(ext.PermissionsSummary ?? ("MV" + (ext.ManifestVersion > 0 ? ext.ManifestVersion : 2) + " • Unaudited"));
                    string attrPath = AttributeEscape(ext.Path ?? "");
                    string isChecked = ext.Enabled ? "checked" : "";
                    string statusBadge = ext.Enabled ? "<span class='badge active'>Active</span>" : "<span class='badge inactive'>Disabled</span>";

                    cards.Append(string.Format(@"
                    <div class='ext-card' data-id='{6}' data-name='{0}'>
                        <div class='ext-header'>
                            <div class='ext-icon'>🧩</div>
                            <div class='ext-info'>
                                <div class='ext-title-row'>
                                    <span class='ext-name'>{0}</span>
                                    <span class='ext-ver'>v{1}</span>
                                    {2}
                                </div>
                                <div class='ext-desc'>{3}</div>
                                <div class='ext-desc' style='margin-top:4px;font-size:11.5px;color:#cbd5e1;'>🛡️ {8}</div>
                                <div class='ext-path' title='{7}'>📁 {4}</div>
                            </div>
                        </div>
                        <div class='ext-actions'>
                            <label class='switch'>
                                <input type='checkbox' class='ext-toggle' {5}>
                                <span class='slider'></span>
                            </label>
                            <button type='button' class='btn-danger'>🗑️ Remove</button>
                        </div>
                    </div>", safeName, safeVer, statusBadge, safeDesc, safePath, isChecked, safeId, attrPath, safePerms));
                }
            }

            return string.Format(@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Extensions • Black Firefox</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; font-family: 'Segoe UI Variable Display', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }}
        body {{
            background: {0};
            color: {1};
            padding: 40px 20px;
            min-height: 100vh;
            display: flex;
            justify-content: center;
        }}
        .container {{
            max-width: 920px;
            width: 100%;
        }}
        .header {{
            display: flex;
            align-items: center;
            justify-content: space-between;
            margin-bottom: 30px;
            border-bottom: 1px solid {2};
            padding-bottom: 20px;
        }}
        .header-title {{
            display: flex;
            align-items: center;
            gap: 14px;
        }}
        .header-title h1 {{
            font-size: 26px;
            font-weight: 700;
            letter-spacing: -0.5px;
        }}
        .header-badge {{
            background: linear-gradient(135deg, #6366f1, #4f46e5);
            color: #fff;
            padding: 4px 10px;
            border-radius: 20px;
            font-size: 11px;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0.8px;
        }}
        .btn-primary {{
            background: linear-gradient(135deg, #6366f1, #4f46e5);
            color: #fff;
            border: none;
            padding: 10px 18px;
            border-radius: 8px;
            font-size: 13px;
            font-weight: 600;
            cursor: pointer;
            text-decoration: none;
            display: inline-flex;
            align-items: center;
            gap: 8px;
            transition: all 0.2s ease;
            box-shadow: 0 4px 12px rgba(99, 102, 241, 0.25);
        }}
        .btn-primary:hover {{
            transform: translateY(-1px);
            box-shadow: 0 6px 16px rgba(99, 102, 241, 0.4);
        }}
        .ext-grid {{
            display: flex;
            flex-direction: column;
            gap: 14px;
        }}
        .ext-card {{
            background: {3};
            border: 1px solid {2};
            border-radius: 12px;
            padding: 20px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 20px;
            transition: all 0.2s ease;
            backdrop-filter: blur(10px);
        }}
        .ext-card:hover {{
            border-color: rgba(99, 102, 241, 0.4);
            transform: translateY(-2px);
            box-shadow: 0 8px 24px rgba(0,0,0,0.3);
        }}
        .ext-header {{
            display: flex;
            align-items: flex-start;
            gap: 16px;
            flex: 1;
        }}
        .ext-icon {{
            font-size: 28px;
            background: rgba(99, 102, 241, 0.12);
            border: 1px solid rgba(99, 102, 241, 0.25);
            width: 48px;
            height: 48px;
            display: flex;
            align-items: center;
            justify-content: center;
            border-radius: 10px;
            flex-shrink: 0;
        }}
        .ext-info {{
            display: flex;
            flex-direction: column;
            gap: 4px;
            flex: 1;
        }}
        .ext-title-row {{
            display: flex;
            align-items: center;
            gap: 10px;
        }}
        .ext-name {{
            font-size: 16px;
            font-weight: 700;
            color: {4};
        }}
        .ext-ver {{
            font-size: 12px;
            color: #8890a6;
            background: rgba(255,255,255,0.06);
            padding: 2px 6px;
            border-radius: 4px;
        }}
        .badge {{
            font-size: 10px;
            padding: 2px 8px;
            border-radius: 10px;
            font-weight: 700;
            text-transform: uppercase;
        }}
        .badge.active {{
            background: rgba(16, 185, 129, 0.15);
            color: #34d399;
            border: 1px solid rgba(16, 185, 129, 0.3);
        }}
        .badge.inactive {{
            background: rgba(239, 68, 68, 0.15);
            color: #f87171;
            border: 1px solid rgba(239, 68, 68, 0.3);
        }}
        .ext-desc {{
            font-size: 13px;
            color: #949db8;
            line-height: 1.4;
        }}
        .ext-path {{
            font-size: 11px;
            color: #64748b;
            font-family: monospace;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
            max-width: 460px;
            margin-top: 4px;
        }}
        .ext-actions {{
            display: flex;
            align-items: center;
            gap: 14px;
        }}
        .btn-danger {{
            background: rgba(239, 68, 68, 0.12);
            color: #f87171;
            border: 1px solid rgba(239, 68, 68, 0.25);
            padding: 7px 12px;
            border-radius: 6px;
            font-size: 12px;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.2s ease;
        }}
        .btn-danger:hover {{
            background: rgba(239, 68, 68, 0.25);
            color: #fff;
        }}
        .switch {{
            position: relative;
            display: inline-block;
            width: 44px;
            height: 24px;
        }}
        .switch input {{ opacity: 0; width: 0; height: 0; }}
        .slider {{
            position: absolute;
            cursor: pointer;
            top: 0; left: 0; right: 0; bottom: 0;
            background-color: #33384a;
            transition: .3s;
            border-radius: 24px;
        }}
        .slider:before {{
            position: absolute;
            content: '';
            height: 18px;
            width: 18px;
            left: 3px;
            bottom: 3px;
            background-color: white;
            transition: .3s;
            border-radius: 50%;
        }}
        input:checked + .slider {{
            background-color: #6366f1;
        }}
        input:checked + .slider:before {{
            transform: translateX(20px);
        }}
        .empty-state {{
            text-align: center;
            padding: 60px 20px;
            background: {3};
            border: 1px dashed {2};
            border-radius: 14px;
        }}
        .empty-icon {{
            font-size: 48px;
            margin-bottom: 12px;
            opacity: 0.8;
        }}
        .empty-title {{
            font-size: 18px;
            font-weight: 700;
            margin-bottom: 8px;
        }}
        .empty-desc {{
            font-size: 13px;
            color: #8890a6;
            max-width: 480px;
            margin: 0 auto;
            line-height: 1.5;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='header-title'>
                <h1>Browser Extensions</h1>
                <span class='header-badge'>Chrome Engine Compatible</span>
            </div>
            <a href='black://load-extension' class='btn-primary'>📂 Load Unpacked Extension</a>
        </div>
        <div class='ext-grid' id='extGrid'>
            {5}
        </div>
    </div>
    <script>
        document.getElementById('extGrid').addEventListener('change', function(e) {{
            if (e.target && e.target.classList.contains('ext-toggle')) {{
                var card = e.target.closest('.ext-card');
                if (!card) return;
                var id = card.getAttribute('data-id') || '';
                location.href = 'black://toggle-extension?id=' + encodeURIComponent(id) + '&val=' + e.target.checked;
            }}
        }});
        document.getElementById('extGrid').addEventListener('click', function(e) {{
            var btn = e.target.closest('.btn-danger');
            if (!btn) return;
            var card = btn.closest('.ext-card');
            if (!card) return;
            var id = card.getAttribute('data-id') || '';
            var name = card.getAttribute('data-name') || 'this extension';
            if (confirm('Remove extension ' + name + '?')) {{
                location.href = 'black://remove-extension?id=' + encodeURIComponent(id);
            }}
        }});
    </script>
</body>
</html>",
            isDark ? "#0e0f14" : "#f4f5f9",
            isDark ? "#e2e8f0" : "#1e293b",
            isDark ? "rgba(255,255,255,0.08)" : "rgba(0,0,0,0.08)",
            isDark ? "rgba(22, 24, 34, 0.75)" : "rgba(255, 255, 255, 0.85)",
            isDark ? "#ffffff" : "#0f172a",
            cards.ToString());
        }
    }
}

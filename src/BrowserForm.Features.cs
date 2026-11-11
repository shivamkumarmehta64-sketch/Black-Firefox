using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BlackBrowser
{
    public partial class BrowserForm
    {
        private bool isAppWindowMode = false;
        private List<PasswordItem> pwAutofillMatches = new List<PasswordItem>();
        private ComboBox pwAccountCombo;

        private void RecordTabBlockedDomain(TabPage page, string blockedHost)
        {
            if (page == null || page.IsDisposed || string.IsNullOrEmpty(blockedHost)) return;
            try
            {
                TabMetadata meta = GetTabMetadata(page);
                if (meta != null && meta.BlockedDomains.Count < 100)
                {
                    meta.BlockedDomains.Add(blockedHost);
                }
            }
            catch { }
        }

        private static Color GetContainerAccentColor(string containerName)
        {
            if (string.IsNullOrEmpty(containerName)) return Color.FromArgb(12, 99, 232);
            switch (containerName.ToLowerInvariant())
            {
                case "work": return Color.FromArgb(59, 130, 246);
                case "personal": return Color.FromArgb(16, 185, 129);
                case "shopping": return Color.FromArgb(245, 158, 11);
                case "dev": return Color.FromArgb(139, 92, 246);
                default: return Color.FromArgb(12, 99, 232);
            }
        }

        private void UpdateAllTabPrivacyProfiles()
        {
            foreach (TabPage p in tabControl.TabPages)
            {
                WebView2 wv = GetWebView(p);
                if (wv != null && !wv.IsDisposed && wv.CoreWebView2 != null)
                {
                    try
                    {
                        wv.CoreWebView2.Profile.PreferredTrackingPreventionLevel = SettingsStore.StrictTrackingPrevention
                            ? CoreWebView2TrackingPreventionLevel.Strict
                            : CoreWebView2TrackingPreventionLevel.Balanced;
                    }
                    catch { }
                }
            }
        }

        private void ShowPerSiteShieldDialog()
        {
            WebView2 wv = GetCurrentWebView();
            TabPage page = tabControl.SelectedTab;
            TabMetadata meta = GetTabMetadata(page);
            string currentUri = (wv != null && wv.Source != null) ? wv.Source.ToString() : "";
            string currentHost = AdShieldState.GetHost(currentUri);
            bool isInternal = string.IsNullOrEmpty(currentHost) || InternalUrlRouter.IsInternalOrigin(currentUri);

            using (Form dlg = new Form())
            {
                dlg.Text = "Black AdShield & Privacy Controls";
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ClientSize = new Size(440, 440);
                dlg.BackColor = Color.FromArgb(24, 24, 32);
                dlg.ForeColor = Color.White;
                EnableDarkModeForWindow(dlg.Handle);

                Label header = new Label();
                header.Text = isInternal
                    ? ("🛡️ AdShield Session Total: " + totalBlockedAds + " blocked")
                    : ("🛡️ Site Shield: " + currentHost);
                header.Font = new Font("Segoe UI Variable Display", 12.5f, FontStyle.Bold);
                header.ForeColor = Color.FromArgb(235, 240, 250);
                header.Location = new Point(16, 14);
                header.AutoSize = true;
                dlg.Controls.Add(header);

                Label sub = new Label();
                sub.Text = "Session blocked: " + totalBlockedAds + " requests • Global tracker & script protection";
                sub.Font = new Font("Segoe UI", 9f);
                sub.ForeColor = Color.FromArgb(160, 168, 188);
                sub.Location = new Point(16, 42);
                sub.AutoSize = true;
                dlg.Controls.Add(sub);

                Button siteToggleBtn = new Button();
                siteToggleBtn.Location = new Point(16, 72);
                siteToggleBtn.Size = new Size(408, 36);
                siteToggleBtn.FlatStyle = FlatStyle.Flat;
                siteToggleBtn.FlatAppearance.BorderSize = 0;
                siteToggleBtn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                siteToggleBtn.Cursor = Cursors.Hand;
                if (isInternal)
                {
                    siteToggleBtn.Text = "🏠 Built-in Page (Protection Active)";
                    siteToggleBtn.Enabled = false;
                    siteToggleBtn.BackColor = Color.FromArgb(40, 44, 58);
                    siteToggleBtn.ForeColor = Color.FromArgb(160, 168, 188);
                }
                else
                {
                    bool siteEnabled = AdShieldState.IsEnabled(currentHost);
                    siteToggleBtn.Text = siteEnabled
                        ? ("🛡️ Shields UP on " + currentHost + " (Click to Disable for Site)")
                        : ("⚠️ Shields DOWN on " + currentHost + " (Click to Re-enable)");
                    siteToggleBtn.BackColor = siteEnabled ? Color.FromArgb(16, 145, 95) : Color.FromArgb(180, 90, 20);
                    siteToggleBtn.ForeColor = Color.White;
                    siteToggleBtn.Click += (s, e) =>
                    {
                        bool nowEnabled = AdShieldState.Toggle(currentHost);
                        ShowSoftCommunication(nowEnabled
                            ? ("🛡️ AdShield enabled for " + currentHost)
                            : ("⏸️ AdShield disabled for " + currentHost));
                        dlg.Close();
                        if (wv != null && !wv.IsDisposed && wv.CoreWebView2 != null) wv.Reload();
                    };
                }
                dlg.Controls.Add(siteToggleBtn);

                CheckBox chkGlobal = new CheckBox();
                chkGlobal.Text = "Global AdShield (BloomFilter + YouTube ad stripping)";
                chkGlobal.Checked = SettingsStore.AdShieldEnabled;
                chkGlobal.ForeColor = Color.FromArgb(220, 225, 240);
                chkGlobal.Location = new Point(16, 122);
                chkGlobal.AutoSize = true;
                chkGlobal.CheckedChanged += (s, e) => SettingsStore.AdShieldEnabled = chkGlobal.Checked;
                dlg.Controls.Add(chkGlobal);

                CheckBox chkStrip = new CheckBox();
                chkStrip.Text = "Strip URL tracking parameters (utm_*, fbclid, gclid, igshid)";
                chkStrip.Checked = SettingsStore.StripTrackingParams;
                chkStrip.ForeColor = Color.FromArgb(220, 225, 240);
                chkStrip.Location = new Point(16, 148);
                chkStrip.AutoSize = true;
                chkStrip.CheckedChanged += (s, e) => SettingsStore.StripTrackingParams = chkStrip.Checked;
                dlg.Controls.Add(chkStrip);

                CheckBox chkStrict = new CheckBox();
                chkStrict.Text = "Strict Tracking Prevention (Edge-Chromium Strict Level)";
                chkStrict.Checked = SettingsStore.StrictTrackingPrevention;
                chkStrict.ForeColor = Color.FromArgb(220, 225, 240);
                chkStrict.Location = new Point(16, 174);
                chkStrict.AutoSize = true;
                chkStrict.CheckedChanged += (s, e) =>
                {
                    SettingsStore.StrictTrackingPrevention = chkStrict.Checked;
                    UpdateAllTabPrivacyProfiles();
                };
                dlg.Controls.Add(chkStrict);

                CheckBox chkHttps = new CheckBox();
                chkHttps.Text = "HTTPS-First Auto-Upgrade on plain HTTP navigations";
                chkHttps.Checked = SettingsStore.HttpsFirstMode;
                chkHttps.ForeColor = Color.FromArgb(220, 225, 240);
                chkHttps.Location = new Point(16, 200);
                chkHttps.AutoSize = true;
                chkHttps.CheckedChanged += (s, e) => SettingsStore.HttpsFirstMode = chkHttps.Checked;
                dlg.Controls.Add(chkHttps);

                Label listLbl = new Label();
                listLbl.Text = "Trackers / Ad Requests Blocked on Current Tab:";
                listLbl.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                listLbl.ForeColor = Color.FromArgb(180, 188, 210);
                listLbl.Location = new Point(16, 232);
                listLbl.AutoSize = true;
                dlg.Controls.Add(listLbl);

                ListBox lb = new ListBox();
                lb.Location = new Point(16, 256);
                lb.Size = new Size(408, 124);
                lb.BackColor = Color.FromArgb(18, 19, 26);
                lb.ForeColor = Color.FromArgb(110, 231, 183);
                lb.BorderStyle = BorderStyle.FixedSingle;
                if (meta != null && meta.BlockedDomains.Count > 0)
                {
                    foreach (string d in meta.BlockedDomains) lb.Items.Add("Blocked: " + d);
                }
                else
                {
                    lb.Items.Add("No trackers intercepted on this tab yet.");
                }
                dlg.Controls.Add(lb);

                Button closeBtn = new Button();
                closeBtn.Text = "Done";
                closeBtn.Location = new Point(334, 392);
                closeBtn.Size = new Size(90, 32);
                closeBtn.FlatStyle = FlatStyle.Flat;
                closeBtn.FlatAppearance.BorderSize = 0;
                closeBtn.BackColor = Color.FromArgb(12, 99, 232);
                closeBtn.ForeColor = Color.White;
                closeBtn.DialogResult = DialogResult.OK;
                dlg.Controls.Add(closeBtn);

                dlg.ShowDialog(this);
            }
        }

        private void ShowMediaHubDialog()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "Audio & Media Hub — Black Browser";
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ClientSize = new Size(480, 360);
                dlg.BackColor = Color.FromArgb(24, 24, 32);
                dlg.ForeColor = Color.White;
                EnableDarkModeForWindow(dlg.Handle);

                Label hdr = new Label();
                hdr.Text = "🎵 Active Audio & Media Tabs";
                hdr.Font = new Font("Segoe UI Variable Display", 12.5f, FontStyle.Bold);
                hdr.Location = new Point(16, 14);
                hdr.AutoSize = true;
                dlg.Controls.Add(hdr);

                ListBox mediaList = new ListBox();
                mediaList.Location = new Point(16, 50);
                mediaList.Size = new Size(448, 220);
                mediaList.BackColor = Color.FromArgb(18, 19, 26);
                mediaList.ForeColor = Color.FromArgb(230, 234, 245);
                mediaList.Font = new Font("Segoe UI", 9.5f);
                List<TabPage> mediaTabs = new List<TabPage>();

                Action reloadMedia = () =>
                {
                    mediaList.Items.Clear();
                    mediaTabs.Clear();
                    for (int i = 0; i < tabControl.TabPages.Count; i++)
                    {
                        TabPage tp = tabControl.TabPages[i];
                        WebView2 wv = GetWebView(tp);
                        if (wv != null && !wv.IsDisposed && wv.CoreWebView2 != null)
                        {
                            bool playing = false;
                            bool muted = false;
                            try { playing = wv.CoreWebView2.IsDocumentPlayingAudio; } catch { }
                            try { muted = wv.CoreWebView2.IsMuted; } catch { }
                            if (playing || muted)
                            {
                                string tag = muted ? "[MUTED] " : "[PLAYING] ";
                                mediaList.Items.Add(tag + tp.Text);
                                mediaTabs.Add(tp);
                            }
                        }
                    }
                    if (mediaTabs.Count == 0)
                    {
                        mediaList.Items.Add("No tabs are currently playing audio.");
                    }
                    else
                    {
                        mediaList.SelectedIndex = 0;
                    }
                };
                reloadMedia();
                dlg.Controls.Add(mediaList);

                Button btnJump = new Button { Text = "Switch to Tab", Location = new Point(16, 286), Size = new Size(110, 34), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(12, 99, 232), ForeColor = Color.White };
                btnJump.FlatAppearance.BorderSize = 0;
                btnJump.Click += (s, e) =>
                {
                    int idx = mediaList.SelectedIndex;
                    if (idx >= 0 && idx < mediaTabs.Count)
                    {
                        tabControl.SelectedTab = mediaTabs[idx];
                        dlg.Close();
                    }
                };

                Button btnMute = new Button { Text = "Mute / Unmute", Location = new Point(134, 286), Size = new Size(115, 34), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(46, 48, 64), ForeColor = Color.White };
                btnMute.FlatAppearance.BorderSize = 0;
                btnMute.Click += (s, e) =>
                {
                    int idx = mediaList.SelectedIndex;
                    if (idx >= 0 && idx < mediaTabs.Count)
                    {
                        WebView2 wv = GetWebView(mediaTabs[idx]);
                        if (wv != null && wv.CoreWebView2 != null)
                        {
                            wv.CoreWebView2.IsMuted = !wv.CoreWebView2.IsMuted;
                            tabControl.Invalidate();
                            reloadMedia();
                        }
                    }
                };

                Button btnPip = new Button { Text = "📺 Video PiP", Location = new Point(257, 286), Size = new Size(105, 34), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(46, 48, 64), ForeColor = Color.White };
                btnPip.FlatAppearance.BorderSize = 0;
                btnPip.Click += (s, e) =>
                {
                    int idx = mediaList.SelectedIndex;
                    if (idx >= 0 && idx < mediaTabs.Count)
                    {
                        TriggerPictureInPictureOnTab(mediaTabs[idx]);
                    }
                };

                Button btnClose = new Button { Text = "Close", Location = new Point(370, 286), Size = new Size(94, 34), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(38, 40, 52), ForeColor = Color.White, DialogResult = DialogResult.OK };
                btnClose.FlatAppearance.BorderSize = 0;

                dlg.Controls.Add(btnJump);
                dlg.Controls.Add(btnMute);
                dlg.Controls.Add(btnPip);
                dlg.Controls.Add(btnClose);
                dlg.ShowDialog(this);
            }
        }

        private void TriggerPictureInPictureOnTab(TabPage targetTab)
        {
            WebView2 wv = GetWebView(targetTab ?? tabControl.SelectedTab);
            if (wv == null || wv.IsDisposed || wv.CoreWebView2 == null) return;
            try
            {
                wv.CoreWebView2.ExecuteScriptAsync(
                    "(function(){var vs=document.querySelectorAll('video');for(var i=0;i<vs.length;i++){var v=vs[i];if(!v.paused&&v.requestPictureInPicture){v.requestPictureInPicture().catch(function(){});return;}}if(vs[0]&&vs[0].requestPictureInPicture)vs[0].requestPictureInPicture().catch(function(){});})();");
                ShowSoftCommunication("📺 Requested Picture-in-Picture");
            }
            catch { }
        }

        private async void ShowSitePermissionsDialog()
        {
            WebView2 wv = GetCurrentWebView();
            if (wv == null || wv.IsDisposed || wv.CoreWebView2 == null)
            {
                ShowSoftCommunication("⚠️ Active tab WebView2 is not ready.");
                return;
            }

            string currentUri = wv.Source != null ? wv.Source.ToString() : "";
            string origin = "";
            try
            {
                Uri u;
                if (Uri.TryCreate(currentUri, UriKind.Absolute, out u))
                    origin = u.GetLeftPart(UriPartial.Authority);
            }
            catch { }

            IReadOnlyList<CoreWebView2PermissionSetting> settingsList = null;
            try
            {
                settingsList = await wv.CoreWebView2.Profile.GetNonDefaultPermissionSettingsAsync();
            }
            catch { }

            if (this.IsDisposed) return;

            using (Form dlg = new Form())
            {
                dlg.Text = "Site Info & Saved Permissions";
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ClientSize = new Size(500, 380);
                dlg.BackColor = Color.FromArgb(24, 24, 32);
                dlg.ForeColor = Color.White;
                EnableDarkModeForWindow(dlg.Handle);

                Label lblOrigin = new Label();
                bool isHttps = currentUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                lblOrigin.Text = (isHttps ? "🔒 Secure Connection: " : "🌐 Origin: ") + (string.IsNullOrEmpty(origin) ? "Internal / New Tab" : origin);
                lblOrigin.Font = new Font("Segoe UI Variable Display", 11f, FontStyle.Bold);
                lblOrigin.Location = new Point(16, 14);
                lblOrigin.AutoSize = true;
                dlg.Controls.Add(lblOrigin);

                Label lblSub = new Label();
                lblSub.Text = "Persistent permissions stored in your browser profile (select a row to reset):";
                lblSub.Font = new Font("Segoe UI", 9f);
                lblSub.ForeColor = Color.FromArgb(160, 168, 188);
                lblSub.Location = new Point(16, 44);
                lblSub.AutoSize = true;
                dlg.Controls.Add(lblSub);

                ListBox permBox = new ListBox();
                permBox.Location = new Point(16, 72);
                permBox.Size = new Size(468, 236);
                permBox.BackColor = Color.FromArgb(18, 19, 26);
                permBox.ForeColor = Color.FromArgb(228, 232, 244);
                permBox.Font = new Font("Segoe UI", 9.5f);

                List<CoreWebView2PermissionSetting> items = new List<CoreWebView2PermissionSetting>();
                if (settingsList != null && settingsList.Count > 0)
                {
                    foreach (var ps in settingsList)
                    {
                        items.Add(ps);
                        permBox.Items.Add(ps.PermissionState.ToString() + " — " + ps.PermissionKind.ToString() + " (" + ps.PermissionOrigin + ")");
                    }
                    permBox.SelectedIndex = 0;
                }
                else
                {
                    permBox.Items.Add("All site permissions are currently at safe Ask / Default.");
                }
                dlg.Controls.Add(permBox);

                Button btnReset = new Button { Text = "Reset to Ask (Default)", Location = new Point(16, 324), Size = new Size(165, 34), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(190, 55, 55), ForeColor = Color.White };
                btnReset.FlatAppearance.BorderSize = 0;
                btnReset.Enabled = items.Count > 0;
                btnReset.Click += async (s, e) =>
                {
                    int idx = permBox.SelectedIndex;
                    if (idx >= 0 && idx < items.Count)
                    {
                        var sel = items[idx];
                        try
                        {
                            await wv.CoreWebView2.Profile.SetPermissionStateAsync(sel.PermissionKind, sel.PermissionOrigin, CoreWebView2PermissionState.Default);
                            permissionDecisions.Remove(sel.PermissionOrigin + "|" + sel.PermissionKind.ToString());
                            items.RemoveAt(idx);
                            permBox.Items.RemoveAt(idx);
                            if (permBox.Items.Count == 0)
                            {
                                permBox.Items.Add("All site permissions are currently at safe Ask / Default.");
                                btnReset.Enabled = false;
                            }
                            ShowSoftCommunication("🔒 Permission reset to Ask (Default)");
                        }
                        catch { }
                    }
                };
                dlg.Controls.Add(btnReset);

                Button btnDone = new Button { Text = "Close", Location = new Point(394, 324), Size = new Size(90, 34), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(46, 48, 64), ForeColor = Color.White, DialogResult = DialogResult.OK };
                btnDone.FlatAppearance.BorderSize = 0;
                dlg.Controls.Add(btnDone);

                dlg.ShowDialog(this);
            }
        }

        private void InstallCurrentSiteAsDesktopApp()
        {
            WebView2 wv = GetCurrentWebView();
            string rawUrl = (wv != null && wv.Source != null) ? wv.Source.ToString() : "";
            Uri parsed;
            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) ||
                InternalUrlRouter.IsInternalOrigin(rawUrl))
            {
                ShowSoftCommunication("⚠️ Navigate to an external HTTP/HTTPS website before installing as an app.");
                return;
            }

            TabPage cur = tabControl.SelectedTab;
            string appName = (cur != null && !string.IsNullOrWhiteSpace(cur.Text)) ? cur.Text : parsed.Host;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                appName = appName.Replace(invalid, ' ');
            appName = appName.Trim();
            if (appName.Length == 0) appName = parsed.Host;
            if (appName.Length > 40) appName = appName.Substring(0, 40).Trim();

            try
            {
                string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutPath = Path.Combine(desktopDir, appName + ".lnk");
                string exePath = Application.ExecutablePath;
                string iconPath = Path.Combine(Application.StartupPath, "icon.ico");
                string argStr = "--newwindow --app=" + parsed.AbsoluteUri;

                string psScript =
                    "$ws = New-Object -ComObject WScript.Shell; " +
                    "$sc = $ws.CreateShortcut('" + shortcutPath.Replace("'", "''") + "'); " +
                    "$sc.TargetPath = '" + exePath.Replace("'", "''") + "'; " +
                    "$sc.Arguments = '" + argStr.Replace("'", "''") + "'; " +
                    "$sc.WorkingDirectory = '" + Application.StartupPath.Replace("'", "''") + "'; " +
                    (File.Exists(iconPath) ? ("$sc.IconLocation = '" + iconPath.Replace("'", "''") + ",0'; ") : "") +
                    "$sc.Save()";

                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + psScript + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit(5000);
                }
                ShowSoftCommunication("📲 Installed \"" + appName + "\" Desktop App shortcut on Desktop!");
            }
            catch
            {
                ShowSoftCommunication("⚠️ Failed to create Desktop App shortcut.");
            }
        }

        public static string GenerateStrongPassword(int length = 16)
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string digits = "23456789";
            const string symbols = "!@#$%^&*-_+=";
            string all = upper + lower + digits + symbols;
            if (length < 8) length = 8;

            char[] chars = new char[length];
            byte[] rngBytes = new byte[length * 4];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(rngBytes);
            }
            chars[0] = upper[rngBytes[0] % upper.Length];
            chars[1] = lower[rngBytes[1] % lower.Length];
            chars[2] = digits[rngBytes[2] % digits.Length];
            chars[3] = symbols[rngBytes[3] % symbols.Length];
            for (int i = 4; i < length; i++)
            {
                uint val = BitConverter.ToUInt32(rngBytes, i * 4);
                chars[i] = all[(int)(val % (uint)all.Length)];
            }
            for (int i = length - 1; i > 0; i--)
            {
                int j = rngBytes[i] % (i + 1);
                char tmp = chars[i];
                chars[i] = chars[j];
                chars[j] = tmp;
            }
            return new string(chars);
        }

        private void PromptGenerateStrongPassword()
        {
            string pw = GenerateStrongPassword(16);
            try { Clipboard.SetText(pw); } catch { }
            ShowSoftCommunication("🔑 16-char strong password copied to clipboard!");
        }

        private async Task<bool> TryHotLoadExtensionAsync(WebView2 wv, string folderPath)
        {
            if (wv == null || wv.IsDisposed || wv.CoreWebView2 == null) return false;
            try
            {
                await wv.CoreWebView2.Profile.AddBrowserExtensionAsync(folderPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task SyncRuntimeExtensionAsync(WebView2 wv, ExtensionItem item, bool removeInsteadOfToggle)
        {
            if (wv == null || wv.IsDisposed || wv.CoreWebView2 == null || item == null) return;
            try
            {
                if (!removeInsteadOfToggle && item.Enabled)
                {
                    await wv.CoreWebView2.Profile.AddBrowserExtensionAsync(item.Path);
                }
                IReadOnlyList<CoreWebView2BrowserExtension> loaded = await wv.CoreWebView2.Profile.GetBrowserExtensionsAsync();
                if (loaded == null) return;
                foreach (var ext in loaded)
                {
                    if (string.Equals(ext.Name, item.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (removeInsteadOfToggle) await ext.RemoveAsync();
                        else await ext.EnableAsync(item.Enabled);
                    }
                }
            }
            catch { }
        }

        public static bool TryEvaluateQuickMath(string query, out string formattedResult)
        {
            formattedResult = null;
            if (string.IsNullOrWhiteSpace(query)) return false;
            string trimmed = query.Trim();
            if (!trimmed.StartsWith("=", StringComparison.Ordinal)) return false;
            string expr = trimmed.Substring(1).Trim();
            if (expr.Length == 0 || expr.Length > 80) return false;
            for (int i = 0; i < expr.Length; i++)
            {
                char c = expr[i];
                if (!((c >= '0' && c <= '9') || c == '.' || c == '+' || c == '-' || c == '*' || c == '/' || c == '%' || c == '(' || c == ')' || c == ' '))
                    return false;
            }
            try
            {
                int pos = 0;
                double val = ParseMathExpr(expr, ref pos);
                while (pos < expr.Length && expr[pos] == ' ') pos++;
                if (pos != expr.Length || double.IsNaN(val) || double.IsInfinity(val)) return false;
                formattedResult = val.ToString("G12", System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static double ParseMathExpr(string s, ref int pos)
        {
            double x = ParseMathTerm(s, ref pos);
            while (true)
            {
                SkipWs(s, ref pos);
                if (pos < s.Length && s[pos] == '+') { pos++; x += ParseMathTerm(s, ref pos); }
                else if (pos < s.Length && s[pos] == '-') { pos++; x -= ParseMathTerm(s, ref pos); }
                else return x;
            }
        }

        private static double ParseMathTerm(string s, ref int pos)
        {
            double x = ParseMathFactor(s, ref pos);
            while (true)
            {
                SkipWs(s, ref pos);
                if (pos < s.Length && s[pos] == '*') { pos++; x *= ParseMathFactor(s, ref pos); }
                else if (pos < s.Length && s[pos] == '/')
                {
                    pos++;
                    double d = ParseMathFactor(s, ref pos);
                    if (Math.Abs(d) < 1e-15) throw new DivideByZeroException();
                    x /= d;
                }
                else if (pos < s.Length && s[pos] == '%')
                {
                    pos++;
                    double d = ParseMathFactor(s, ref pos);
                    if (Math.Abs(d) < 1e-15) throw new DivideByZeroException();
                    x %= d;
                }
                else return x;
            }
        }

        private static double ParseMathFactor(string s, ref int pos)
        {
            SkipWs(s, ref pos);
            if (pos < s.Length && s[pos] == '+') { pos++; return ParseMathFactor(s, ref pos); }
            if (pos < s.Length && s[pos] == '-') { pos++; return -ParseMathFactor(s, ref pos); }
            if (pos < s.Length && s[pos] == '(')
            {
                pos++;
                double inner = ParseMathExpr(s, ref pos);
                SkipWs(s, ref pos);
                if (pos >= s.Length || s[pos] != ')') throw new FormatException();
                pos++;
                return inner;
            }
            int start = pos;
            bool seenDot = false;
            while (pos < s.Length && ((s[pos] >= '0' && s[pos] <= '9') || (!seenDot && s[pos] == '.')))
            {
                if (s[pos] == '.') seenDot = true;
                pos++;
            }
            if (pos == start) throw new FormatException();
            return double.Parse(s.Substring(start, pos - start), System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void SkipWs(string s, ref int pos)
        {
            while (pos < s.Length && s[pos] == ' ') pos++;
        }
    }
}

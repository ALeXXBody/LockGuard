using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

namespace LockGuardSetup
{
    static class Program
    {
        public const string AppTitle = "LockGuard";
        public const string Version = "3.1";
        public const string ResName = "LockGuard.exe";
        public static string InstallDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LockGuard");
        public static string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LockGuard");
        public static string ExeName = "LockGuard.exe";

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (!IsAdmin())
            {
                MessageBox.Show("LockGuard installer must run as Administrator.", AppTitle + " Installer",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Application.Run(new InstallForm());
        }

        public static bool IsAdmin()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    var p = new WindowsPrincipal(id);
                    return p.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }
    }

    public class InstallForm : Form
    {
        TextBox logBox;
        Label lblStatus;
        CheckBox chkAutoStart;
        CheckBox chkShortcut;

        public InstallForm()
        {
            Text = "LockGuard " + Program.Version + " - Setup";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 480);
            BackColor = UI.Bg;
            ForeColor = UI.Text;
            Font = UI.F(9);

            var title = new Label();
            title.Text = "LockGuard v" + Program.Version + " Installer";
            title.Font = UI.FB(16);
            title.ForeColor = UI.Accent;
            title.Location = new Point(24, 18); title.AutoSize = true; Controls.Add(title);

            var sub = new Label();
            sub.Text = "Password-protected night internet guard & intrusion monitor.\nInstalls to: " + Program.InstallDir;
            sub.ForeColor = UI.Dim;
            sub.Location = new Point(24, 52); sub.AutoSize = true; Controls.Add(sub);

            chkAutoStart = new CheckBox();
            chkAutoStart.Text = "Auto-start LockGuard at Windows login";
            chkAutoStart.Checked = true;
            chkAutoStart.ForeColor = UI.Text;
            chkAutoStart.Location = new Point(26, 110); chkAutoStart.AutoSize = true; Controls.Add(chkAutoStart);

            chkShortcut = new CheckBox();
            chkShortcut.Text = "Create desktop & start menu shortcut";
            chkShortcut.Checked = true;
            chkShortcut.ForeColor = UI.Text;
            chkShortcut.Location = new Point(26, 136); chkShortcut.AutoSize = true; Controls.Add(chkShortcut);

            lblStatus = new Label();
            lblStatus.Text = "Ready.";
            lblStatus.Location = new Point(26, 168); lblStatus.AutoSize = true;
            lblStatus.ForeColor = UI.Green; Controls.Add(lblStatus);

            logBox = new TextBox();
            logBox.Multiline = true; logBox.ReadOnly = true;
            logBox.BackColor = UI.Bg2;
            logBox.ForeColor = Color.FromArgb(120, 220, 140);
            logBox.Font = UI.FCode(9);
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Location = new Point(24, 200); logBox.Size = new Size(512, 190);
            Controls.Add(logBox);

            var btnInstall = new FlatBtn();
            btnInstall.Text = "Install";
            btnInstall.BaseColor = UI.Accent;
            btnInstall.HoverColor = Color.FromArgb(90, 200, 255);
            btnInstall.ForeColor = Color.FromArgb(6, 12, 20);
            btnInstall.Accent = true;
            btnInstall.Font = UI.FB(10);
            btnInstall.Location = new Point(24, 406); btnInstall.Size = new Size(160, 44);
            Controls.Add(btnInstall);
            btnInstall.Click += (s, e) => Install();

            var btnUninstall = new FlatBtn();
            btnUninstall.Text = "Uninstall";
            btnUninstall.BaseColor = UI.Red;
            btnUninstall.HoverColor = Color.FromArgb(255, 150, 150);
            btnUninstall.ForeColor = Color.White;
            btnUninstall.Accent = true;
            btnUninstall.Font = UI.FB(10);
            btnUninstall.Location = new Point(196, 406); btnUninstall.Size = new Size(160, 44);
            Controls.Add(btnUninstall);
            btnUninstall.Click += (s, e) => Uninstall();

            var btnClose = new FlatBtn();
            btnClose.Text = "Close";
            btnClose.Location = new Point(368, 406); btnClose.Size = new Size(160, 44);
            Controls.Add(btnClose);
            btnClose.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
        }

        void Log(string msg)
        {
            if (InvokeRequired) { Invoke(new Action<string>(Log), msg); return; }
            logBox.AppendText("> " + msg + Environment.NewLine);
            lblStatus.Text = msg;
        }

        bool RunCmd(string file, string args, out string output)
        {
            output = "";
            try
            {
                var psi = new ProcessStartInfo(file, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex) { output = ex.Message; return false; }
        }

        void Install()
        {
            DialogResult confirm = MessageBox.Show(
                "Install LockGuard v" + Program.Version + "?\n\nLocation: " + Program.InstallDir,
                "LockGuard Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            try
            {
                Log("Installing LockGuard...");

                // 1. Extract embedded exe
                string appExe = Path.Combine(Program.InstallDir, Program.ExeName);
                try
                {
                    using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(Program.ResName))
                    {
                        if (s == null)
                        {
                            // fallback: scan resource names
                            foreach (string name in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                            {
                                if (name.EndsWith(Program.ResName))
                                {
                                    using (Stream s2 = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                                    {
                                        Directory.CreateDirectory(Program.InstallDir);
                                        using (FileStream fs = File.Create(appExe)) s2.CopyTo(fs);
                                    }
                                    break;
                                }
                            }
                        }
                        else
                        {
                            Directory.CreateDirectory(Program.InstallDir);
                            using (FileStream fs = File.Create(appExe)) s.CopyTo(fs);
                        }
                    }
                }
                catch (Exception ex) { Log("ERROR extracting app: " + ex.Message); return; }
                Log("Extracted: " + appExe);

                // 2. Data dir
                Directory.CreateDirectory(Program.DataDir);
                Directory.CreateDirectory(Path.Combine(Program.DataDir, "Logs"));
                Log("Created data directory");

                // 3. Scheduled task (auto start)
                if (chkAutoStart.Checked)
                {
                    string outp;
                    bool ok = RunCmd("schtasks.exe",
                        "/create /tn LockGuard_NightInternetMonitor /tr \"" + appExe + "\" /sc onlogon /rl highest /f", out outp);
                    Log(ok ? "Scheduled task created: auto-start at login" : "Auto-start task failed: " + outp.Trim());
                }

                // 4. Audit policy
                string o1, o2, o3;
                RunCmd("auditpol.exe", "/set /subcategory:\"Logon\" /success:enable /failure:enable", out o1);
                RunCmd("auditpol.exe", "/set /subcategory:\"Logoff\" /success:enable /failure:enable", out o2);
                RunCmd("auditpol.exe", "/set /subcategory:\"Special Logon\" /success:enable /failure:enable", out o3);
                Log("Audit policies configured (lock/unlock tracking)");

                // 5. Shortcuts
                if (chkShortcut.Checked)
                {
                    CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "LockGuard.lnk"), appExe);
                    CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "LockGuard.lnk"), appExe);
                    Log("Shortcuts created");
                }

                // 6. Uninstall registry entry
                RegisterUninstall(appExe);
                Log("Install complete!");

                // Launch
                if (MessageBox.Show("Start LockGuard now?", "LockGuard Setup",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Process.Start(appExe);
                }
            }
            catch (Exception ex)
            {
                Log("FATAL: " + ex.Message);
            }
        }

        void CreateShortcut(string lnkPath, string target)
        {
            try
            {
                string ps = "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('" + lnkPath +
                    "');$s.TargetPath='" + target + "';$s.WorkingDirectory='" + Path.GetDirectoryName(target) +
                    "';$s.IconLocation='" + target + "',0;$s.Save()";
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(ps));
                var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process p = Process.Start(psi)) { p.WaitForExit(15000); }
            }
            catch { }
        }

        void RegisterUninstall(string appExe)
        {
            try
            {
                string unsub = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\LockGuard";
                using (var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(unsub))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "LockGuard");
                        key.SetValue("DisplayVersion", Program.Version);
                        key.SetValue("Publisher", "LockGuard");
                        key.SetValue("InstallLocation", Program.InstallDir);
                        key.SetValue("DisplayIcon", appExe);
                        key.SetValue("UninstallString", "\"" + Assembly.GetExecutingAssembly().Location + "\" -u");
                    }
                }
            }
            catch { }
        }

        void Uninstall()
        {
            if (MessageBox.Show("Uninstall LockGuard?\n\nThis removes: app files, auto-start task, logs.\nInternet adapters will be re-enabled.", "LockGuard Uninstall",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                Log("Uninstalling LockGuard...");

                // Kill running app
                foreach (Process p in Process.GetProcessesByName("LockGuard"))
                {
                    try { p.Kill(); p.WaitForExit(3000); } catch { }
                }

                // Remove task
                string taskOut;
                RunCmd("schtasks.exe", "/delete /tn LockGuard_NightInternetMonitor /f", out taskOut);
                Log("Removed auto-start task");

                // Remove shortcuts
                try
                {
                    File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "LockGuard.lnk"));
                    File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "LockGuard.lnk"));
                }
                catch { }
                Log("Removed shortcuts");

                // Remove uninstall entry
                try { Microsoft.Win32.Registry.LocalMachine.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\LockGuard"); } catch { }

                // Delete install dir
                try
                {
                    if (Directory.Exists(Program.InstallDir)) Directory.Delete(Program.InstallDir, true);
                }
                catch (Exception ex) { Log("Could not delete app files: " + ex.Message); }
                Log("Removed app files");

                if (MessageBox.Show("Delete user data (password, config, logs) as well?", "LockGuard Uninstall",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        if (Directory.Exists(Program.DataDir)) Directory.Delete(Program.DataDir, true);
                        Log("Removed user data");
                    }
                    catch { }
                }

                Log("Uninstall complete.");
            }
            catch (Exception ex) { Log("FATAL: " + ex.Message); }
        }
    }

    public static class UI
    {
        public static Color Bg       = Color.FromArgb(10, 14, 20);
        public static Color Bg2      = Color.FromArgb(13, 18, 26);
        public static Color Card     = Color.FromArgb(17, 23, 34);
        public static Color CardAlt  = Color.FromArgb(22, 32, 46);
        public static Color Text     = Color.FromArgb(230, 238, 246);
        public static Color Dim      = Color.FromArgb(138, 154, 176);
        public static Color Accent   = Color.FromArgb(56, 189, 248);
        public static Color AccentDark = Color.FromArgb(14, 116, 144);
        public static Color Violet   = Color.FromArgb(167, 139, 250);
        public static Color Green    = Color.FromArgb(52, 211, 153);
        public static Color Red      = Color.FromArgb(248, 113, 113);
        public static Color Orange   = Color.FromArgb(251, 191, 36);
        public static Color Border   = Color.FromArgb(31, 43, 61);

        public static Font F(float size) { return new Font("Segoe UI", size); }
        public static Font FB(float size) { return new Font("Segoe UI", size, FontStyle.Bold); }
        public static Font FCode(float size) { return new Font("Consolas", size); }

        public static GraphicsPath Rounded(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2f;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Blend(Color a, Color b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            return Color.FromArgb(a.A,
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static void Glow(Graphics g, Rectangle r, int rad, Color c, int layers)
        {
            for (int i = layers; i >= 1; i--)
            {
                Rectangle gr = new Rectangle(r.X - i * 2, r.Y - i * 2, r.Width + i * 4, r.Height + i * 4);
                using (GraphicsPath p = Rounded(gr, rad + i * 3))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(4 + (layers - i + 1) * 5, c)))
                    g.FillPath(b, p);
            }
        }
    }

    public class FlatBtn : Control
    {
        Color _base, _hover, _press;
        bool _hovering;
        bool _accent;
        public Color BaseColor { get { return _base; } set { _base = value; Invalidate(); } }
        public Color HoverColor { get { return _hover; } set { _hover = value; Invalidate(); } }
        public bool Accent { get { return _accent; } set { _accent = value; Invalidate(); } }

        public FlatBtn()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(13, 18, 26);
            Cursor = Cursors.Hand;
            _base = UI.CardAlt; _hover = Color.FromArgb(34, 48, 70); _press = Color.FromArgb(12, 17, 26);
            ForeColor = UI.Text;
            Font = UI.F(9);
            TabStop = false;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            Rectangle r = ClientRectangle;
            Color bg = (Parent != null) ? Parent.BackColor : BackColor;
            using (SolidBrush b = new SolidBrush(bg)) pevent.Graphics.FillRectangle(b, r);
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovering = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovering = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs mevent) { base.OnMouseDown(mevent); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs mevent) { base.OnMouseUp(mevent); Invalidate(); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rc = ClientRectangle;
            rc.Width--; rc.Height--;

            Color top, bot;
            Color glow = Color.Transparent;
            if (!Enabled)
            {
                top = Color.FromArgb(22, 28, 38); bot = Color.FromArgb(14, 19, 28);
            }
            else if (Capture)
            {
                top = _press; bot = _press;
                glow = _accent ? UI.AccentDark : _hover;
            }
            else if (_hovering)
            {
                top = _hover; bot = _base;
                glow = _accent ? UI.Accent : _hover;
            }
            else
            {
                top = _base; bot = UI.Blend(_base, Color.Black, 0.18f);
                glow = _accent ? Color.FromArgb(140, UI.Accent) : Color.FromArgb(70, UI.Accent);
            }

            if (glow != Color.Transparent && Enabled)
            {
                UI.Glow(g, rc, 8, glow, 3);
            }

            using (GraphicsPath path = UI.Rounded(rc, 8))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(rc, top, bot, LinearGradientMode.Vertical))
                    g.FillPath(b, path);
                using (Pen pen = new Pen(Color.FromArgb(70, 255, 255, 255), 1))
                    g.DrawPath(pen, path);
            }

            if (!_accent && Enabled)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(150, UI.Accent)))
                    g.FillRectangle(b, rc.X + 8, rc.Bottom - 2, rc.Width - 16, 2);
            }

            using (SolidBrush t = new SolidBrush(Enabled ? ForeColor : Color.FromArgb(110, 120, 135)))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(Text, Font, t, ClientRectangle, sf);
                sf.Dispose();
            }
        }
    }
}
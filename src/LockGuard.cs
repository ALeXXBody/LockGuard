using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Diagnostics.Eventing.Reader;

namespace LockGuard
{
    static class Program
    {
        public const string AppTitle = "LockGuard";
        public const string Version = "3.0";
        public const string SingleInstanceId = "LockGuard_SingleInstance_v3";
        public const string ShowEventId = "LockGuard_ShowEvent_v3";
        public static Icon AppIcon;
        public static string AppPath;

        [STAThread]
        static void Main()
        {
            AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            try { AppIcon = Icon.ExtractAssociatedIcon(AppPath); }
            catch { AppIcon = SystemIcons.Shield; }

            bool createdNew;
            Mutex mtx = new Mutex(true, SingleInstanceId, out createdNew);
            if (!createdNew)
            {
                string[] cmdArgs = Environment.GetCommandLineArgs();
                bool elevatedRelaunch = cmdArgs.Length > 1 && cmdArgs[1] == "-elevated";
                if (elevatedRelaunch)
                {
                    // A -elevated instance was spawned by an existing (non-elevated) one that is
                    // about to exit. Wait for it to release the singleton, then take over.
                    try { mtx.ReleaseMutex(); } catch { }
                    createdNew = false;
                    var deadline = DateTime.UtcNow.AddSeconds(15);
                    while (!createdNew)
                    {
                        if (DateTime.UtcNow > deadline) return;
                        try
                        {
                            mtx = new Mutex(false, SingleInstanceId);
                            createdNew = mtx.WaitOne(0);
                        }
                        catch (AbandonedMutexException) { createdNew = true; }
                        catch { }
                        if (!createdNew) Thread.Sleep(200);
                    }
                }
                else
                {
                    try
                    {
                        EventWaitHandle ev = EventWaitHandle.OpenExisting(ShowEventId);
                        ev.Set();
                        ev.Dispose();
                    }
                    catch { }
                    return;
                }
            }

            EventWaitHandle showEvent = null;
            try { showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventId); }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ---- Global exception safety: a tray app with no visible window MUST never die silently ----
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                try { Logger.Error("UI thread exception: " + e.Exception); } catch { }
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { Logger.Error("Unhandled exception: " + e.ExceptionObject); } catch { }
            };

            var cfg = ConfigManager.Load();
            if (!cfg.Configured)
            {
                var setup = new PasswordSetupForm();
                if (setup.ShowDialog() == DialogResult.OK && setup.Password.Length >= 4)
                {
                    cfg.SetPassword(setup.Password);
                    cfg.Save();
                }
                else
                {
                    MessageBox.Show("A password is required to use LockGuard.\n\nPlease run LockGuard again and set a password.",
                        "LockGuard - Setup Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            var main = new MainForm(cfg, showEvent);
            Application.Run(main);
            mtx.ReleaseMutex();
        }
    }

    // ---------- Color/font/glow helpers ----------
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
        public static Color GreenDark = Color.FromArgb(16, 110, 80);
        public static Color Red      = Color.FromArgb(248, 113, 113);
        public static Color RedDark  = Color.FromArgb(150, 45, 55);
        public static Color Orange   = Color.FromArgb(251, 191, 36);
        public static Color Border   = Color.FromArgb(31, 43, 61);

        public static Font F(float size) { return new Font("Segoe UI", size); }
        public static Font FB(float size) { return new Font("Segoe UI", size, FontStyle.Bold); }
        public static Font FL(float size) { return new Font("Segoe UI Light", size); }
        public static Font FCode(float size) { return new Font("Consolas", size); }

        public static GraphicsPath Rounded(RectangleF r, int rad)
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
        public static GraphicsPath Rounded(Rectangle r, int rad) { return Rounded(new RectangleF(r.X, r.Y, r.Width, r.Height), rad); }

        public static Color Blend(Color a, Color b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            return Color.FromArgb(a.A,
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        // Soft multi-layer neon halo around a rounded rectangle
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

    // ---------- Config ----------
    public class ConfigManager
    {
        public string DataDir;
        public string ConfigFile;
        public string PasswordHash;
        public string PasswordSalt;
        public int StartHour = 18;
        public int EndHour = 9;
        public bool AutoStart = true;
        public bool Configured = false;

        public ConfigManager() { }

        public string ResolveDataDir()
        {
            string common = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LockGuard");
            try
            {
                if (!Directory.Exists(common)) Directory.CreateDirectory(common);
                string probe = Path.Combine(common, ".write_test");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return common;
            }
            catch
            {
                string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LockGuard");
                if (!Directory.Exists(local)) Directory.CreateDirectory(local);
                return local;
            }
        }

        public string LogDir { get { return Path.Combine(DataDir, "Logs"); } }

        public void InitPaths()
        {
            DataDir = ResolveDataDir();
            ConfigFile = Path.Combine(DataDir, "config.json");
            if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);
        }

        public static ConfigManager Load()
        {
            var c = new ConfigManager();
            c.InitPaths();
            if (File.Exists(c.ConfigFile))
            {
                try
                {
                    var ser = new JavaScriptSerializer();
                    var d = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(c.ConfigFile));
                    if (d != null)
                    {
                        if (d.ContainsKey("password_hash")) c.PasswordHash = d["password_hash"] as string;
                        if (d.ContainsKey("password_salt")) c.PasswordSalt = d["password_salt"] as string;
                        if (d.ContainsKey("start_hour")) { int v; if (int.TryParse(Convert.ToString(d["start_hour"]), out v)) c.StartHour = v; }
                        if (d.ContainsKey("end_hour")) { int v; if (int.TryParse(Convert.ToString(d["end_hour"]), out v)) c.EndHour = v; }
                        if (d.ContainsKey("auto_start")) { bool v; if (bool.TryParse(Convert.ToString(d["auto_start"]), out v)) c.AutoStart = v; }
                        if (!string.IsNullOrEmpty(c.PasswordHash)) c.Configured = true;
                    }
                }
                catch { }
            }
            return c;
        }

        public void Save()
        {
            try
            {
                InitPaths();
                var d = new Dictionary<string, object>();
                d["password_hash"] = PasswordHash;
                d["password_salt"] = PasswordSalt;
                d["start_hour"] = StartHour;
                d["end_hour"] = EndHour;
                d["auto_start"] = AutoStart;
                var ser = new JavaScriptSerializer();
                File.WriteAllText(ConfigFile, ser.Serialize(d), Encoding.UTF8);
            }
            catch { }
        }

        public string HashPassword(string pw)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(pw + PasswordSalt));
                return Convert.ToBase64String(h);
            }
        }

        public void SetPassword(string pw)
        {
            byte[] bytes = new byte[32];
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(bytes);
            PasswordSalt = Convert.ToBase64String(bytes);
            PasswordHash = HashPassword(pw);
            Configured = true;
        }

        public bool TestPassword(string pw)
        {
            if (string.IsNullOrEmpty(PasswordHash) || string.IsNullOrEmpty(PasswordSalt)) return false;
            return HashPassword(pw) == PasswordHash;
        }

        public bool IsNight(int hour)
        {
            if (StartHour < EndHour) return hour >= StartHour && hour < EndHour;
            return hour >= StartHour || hour < EndHour;
        }
    }

    // ---------- Logger ----------
    public class Logger
    {
        public static string DataDir = "";
        public static void Init(string dir) { DataDir = dir; }
        static string LogFile()
        {
            return Path.Combine(DataDir, "Logs", "LockGuard_" + DateTime.Now.ToString("yyyy-MM") + ".log");
        }
        public static void Info(string msg) { Write(msg, "INFO"); }
        public static void Warn(string msg) { Write(msg, "WARN"); }
        public static void Error(string msg) { Write(msg, "ERROR"); }
        public static void Alert(string msg) { Write(msg, "ALERT"); }
        public static void Write(string msg, string level)
        {
            try
            {
                string line = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] [" + level + "] " + msg;
                Directory.CreateDirectory(Path.GetDirectoryName(LogFile()));
                using (StreamWriter sw = File.AppendText(LogFile())) sw.WriteLine(line);
            }
            catch { }
        }
        public static void AlertFile(string msg)
        {
            try
            {
                string p = Path.Combine(DataDir, "Logs", "UnlockAttempts.log");
                string line = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + msg;
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                using (StreamWriter sw = File.AppendText(p)) sw.WriteLine(line);
            }
            catch { }
        }
    }

    // ---------- Network ----------
    public class AdapterInfo
    {
        public string Name;
        public string Desc;
        public bool Enabled;
        public string Status;
        public bool Physical;
    }

    public class NetworkOps
    {
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

        static bool LooksPhysical(string desc)
        {
            string l = desc == null ? "" : desc.ToLowerInvariant();
            if (l.Contains("loopback")) return false;
            if (l.Contains("virtual")) return false;
            if (l.Contains("vmware")) return false;
            if (l.Contains("virtualbox")) return false;
            if (l.Contains("hyper-v")) return false;
            if (l.Contains("hyperv")) return false;
            if (l.Contains("tap-")) return false;
            if (l.Contains("tap ")) return false;
            if (l.Contains("nordvpn")) return false;
            if (l.Contains("openvpn")) return false;
            if (l.Contains("tun")) return false;
            if (l.Contains("wsl")) return false;
            if (l.Contains("bluetooth")) return false;
            if (l.Contains("ras async")) return false;
            if (l.Contains("wwan")) return false;
            return true;
        }

        static string SafeGet(ManagementObject m, string prop)
        {
            try { return Convert.ToString(m[prop]); }
            catch { return ""; }
        }

        public static List<AdapterInfo> List()
        {
            var result = new List<AdapterInfo>();
            try
            {
                using (var s = new ManagementObjectSearcher("root\\CIMV2",
                    "SELECT * FROM Win32_NetworkAdapter"))
                using (var ms = s.Get())
                {
                    foreach (ManagementObject m in ms)
                    {
                        string id = SafeGet(m, "NetConnectionID");
                        string desc = SafeGet(m, "InterfaceDescription");
                        if (string.IsNullOrEmpty(id)) continue;
                        if (string.IsNullOrEmpty(desc)) desc = "Network Adapter";
                        var a = new AdapterInfo();
                        a.Name = id;
                        a.Desc = desc;
                        try { a.Enabled = Convert.ToBoolean(m["NetEnabled"]); }
                        catch { a.Enabled = false; }
                        a.Physical = LooksPhysical(desc);
                        int st = 0; int.TryParse(SafeGet(m, "NetConnectionStatus"), out st);
                        switch (st)
                        {
                            case 2: a.Status = "Connected"; break;
                            case 7: a.Status = "Media disconnected"; break;
                            default: a.Status = "Disabled"; break;
                        }
                        result.Add(a);
                    }
                }
            }
            catch { }
            return result;
        }

        public static List<AdapterInfo> EnabledPhysical()
        {
            var r = new List<AdapterInfo>();
            foreach (var a in List()) if (a.Enabled && a.Physical) r.Add(a);
            return r;
        }

        static int RunNetsh(string args)
        {
            try
            {
                Process p = Process.Start(new ProcessStartInfo("netsh", args)
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
                p.WaitForExit(20000);
                return p.ExitCode;
            }
            catch { return -1; }
        }

        public static string[] TargetNames()
        {
            var list = new List<string>();
            foreach (var a in List()) if (a.Physical) list.Add(a.Name);
            return list.ToArray();
        }

        // Parse `netsh interface show interface`. It lists EVERY interface even when
        // WMI filters hide them (the "disappeared adapter" case), so enable always wins.
        public static void NetInterfaces(out string[] all, out string[] disabled)
        {
            var ali = new List<string>();
            var dis = new List<string>();
            try
            {
                Process p = Process.Start(new ProcessStartInfo("netsh", "interface show interface")
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true });
                string out2 = p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                foreach (string raw in out2.Split('\n'))
                {
                    string line = raw.TrimEnd('\r');
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.StartsWith("Admin State") || line.StartsWith("----")) continue;
                    string[] t = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (t.Length < 4) continue;
                    bool adminEnabled = string.Equals(t[0], "Enabled", StringComparison.OrdinalIgnoreCase);
                    string name = string.Join(" ", t, 3, t.Length - 3);
                    if (string.IsNullOrEmpty(name)) continue;
                    ali.Add(name);
                    if (!adminEnabled) dis.Add(name);
                }
            }
            catch { }
            all = ali.ToArray();
            disabled = dis.ToArray();
        }

        public static bool DisableAll(string[] names, out string detail)
        {
            detail = "";
            if (!IsAdmin())
            {
                detail = "not_admin";
                return false;
            }
            int done = 0;
            foreach (string n in names)
            {
                if (RunNetsh("interface set interface name=\"" + n + "\" admin=disable") == 0) done++;
            }
            return done > 0;
        }

        public static bool EnableAll(string[] names, out string detail)
        {
            detail = "";
            if (!IsAdmin()) { detail = "not_admin"; return false; }
            int done = 0;
            foreach (string n in names)
            {
                if (RunNetsh("interface set interface name=\"" + n + "\" admin=enable") == 0) done++;
            }
            return done > 0;
        }

        // Restore anything still flagged Disabled at the netsh level (always safe to call).
        public static bool EnableDisabled(out string detail)
        {
            detail = "";
            if (!IsAdmin()) { detail = "not_admin"; return false; }
            string[] all, disabled;
            NetInterfaces(out all, out disabled);
            return EnableAll(disabled, out detail);
        }
    }

    // ---------- Scheduled task (autostart) ----------
    public class AutoStartManager
    {
        public const string TaskName = "LockGuard_NightInternetMonitor";
        public static string ExePath;

        public static bool Exists()
        {
            try
            {
                Process p = Process.Start(new ProcessStartInfo("schtasks", "/query /tn " + TaskName + " /nh")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
                string out2 = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);
                return !string.IsNullOrEmpty(out2.Trim());
            }
            catch { return false; }
        }

        public static void Enable()
        {
            string tr = "\"" + ExePath + "\"";
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe",
                    "/create /tn " + TaskName + " /tr " + tr + " /sc onlogon /rl highest /f");
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                psi.CreateNoWindow = true;
                Process.Start(psi);
            }
            catch { }
        }

        public static void Disable()
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", "/delete /tn " + TaskName + " /f");
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                psi.CreateNoWindow = true;
                Process.Start(psi);
            }
            catch { }
        }

        public static void EnableSilent()
        {
            string tr = "\"" + ExePath + "\"";
            try
            {
                Process p = Process.Start(new ProcessStartInfo("schtasks.exe",
                    "/create /tn " + TaskName + " /tr " + tr + " /sc onlogon /rl highest /f")
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true });
                p.WaitForExit(10000);
            }
            catch { }
        }
    }

    // ---------- Toggle switch ----------
    public class ToggleSwitch : Control
    {
        bool _checked;
        bool _hover;
        public bool Checked { get { return _checked; } set { _checked = value; Invalidate(); } }
        public event EventHandler CheckedChanged;
        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Size = new Size(48, 26);
            Cursor = Cursors.Hand;
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            _checked = !_checked;
            Invalidate();
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rc = ClientRectangle;
            rc.Inflate(-3, -3);
            Color track = _checked ? UI.Accent : (_hover ? Color.FromArgb(80, 84, 96) : Color.FromArgb(60, 64, 76));
            using (GraphicsPath path = UI.Rounded(rc, rc.Height))
            using (SolidBrush b = new SolidBrush(track))
            {
                g.FillPath(b, path);
            }
            float pad = 3f;
            float d = rc.Height - pad * 2;
            float x = _checked ? rc.Right - d - pad : rc.Left + pad;
            using (SolidBrush thumb = new SolidBrush(Color.White))
            {
                g.FillEllipse(thumb, x, rc.Y + pad, d, d);
            }
            if (Focus() && ShowFocusCues)
            {
                using (Pen p = new Pen(UI.Accent)) g.DrawRectangle(p, 0, 0, ClientRectangle.Width - 1, ClientRectangle.Height - 1);
            }
        }
    }

    // ---------- Flat button ----------
    public class FlatBtn : Control, IButtonControl
    {
        Color _base, _hover, _press;
        int _rad = 10;
        bool _hovering;
        DialogResult dr = DialogResult.None;
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
            Font = UI.F(10);
            ForeColor = UI.Text;
            Cursor = Cursors.Hand;
            _base = UI.CardAlt; _hover = Color.FromArgb(34, 48, 70); _press = Color.FromArgb(12, 17, 26);
            TabStop = false;
        }
        public DialogResult DialogResult { get { return dr; } set { dr = value; } }
        public void NotifyDefault(bool value) { }
        public void PerformClick()
        {
            OnClick(EventArgs.Empty);
        }
        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (dr != DialogResult.None && FindForm() != null) FindForm().DialogResult = dr;
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
                UI.Glow(g, rc, _rad, glow, 3);
            }

            using (GraphicsPath path = UI.Rounded(rc, _rad))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(rc, top, bot, LinearGradientMode.Vertical))
                    g.FillPath(b, path);
                using (Pen pen = new Pen(Color.FromArgb(70, 255, 255, 255), 1))
                    g.DrawPath(pen, path);
            }

            // faint accent underline for neutral buttons
            if (!_accent && Enabled)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(150, UI.Accent)))
                    g.FillRectangle(b, rc.X + _rad, rc.Bottom - 2, rc.Width - _rad * 2, 2);
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

    // ===================================================================
    //  MODERN DIALOG CHROME + INPUT
    // ===================================================================
    public class GlassForm : Form
    {
        protected Panel TitleStrip;
        protected string CaptionText = "";
        Panel dlgClose;

        public GlassForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = UI.Bg;
            ForeColor = UI.Text;
            Font = UI.F(9);
            DoubleBuffered = true;
        }

        protected void BuildGlassChrome(string title)
        {
            CaptionText = title;
            TitleStrip = new Panel();
            TitleStrip.Dock = DockStyle.Top;
            TitleStrip.Height = 42;
            TitleStrip.BackColor = Color.FromArgb(12, 16, 24);
            TitleStrip.Paint += (s, e) =>
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(12, 16, 24))) e.Graphics.FillRectangle(b, 0, 0, TitleStrip.Width, TitleStrip.Height);
                using (SolidBrush b = new SolidBrush(UI.Border)) e.Graphics.FillRectangle(b, 0, TitleStrip.Height - 1, TitleStrip.Width, 1);
                using (LinearGradientBrush b = new LinearGradientBrush(new Point(0, 0), new Point(TitleStrip.Width, 0), UI.Accent, UI.Violet))
                    e.Graphics.FillRectangle(b, 0, TitleStrip.Height - 2, TitleStrip.Width, 2);
                using (SolidBrush b = new SolidBrush(UI.Text))
                    e.Graphics.DrawString(title, UI.FB(10), b, 14, 13);
            };
            TitleStrip.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
                }
            };
            Controls.Add(TitleStrip);

            dlgClose = new Panel();
            dlgClose.Size = new Size(46, 42); dlgClose.Dock = DockStyle.Right;
            dlgClose.BackColor = Color.Transparent;
            dlgClose.Paint += (s, e) =>
            {
                if (dlgClose.Tag != null)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 50, 60))) e.Graphics.FillRectangle(b, 0, 0, dlgClose.Width, dlgClose.Height);
                using (Pen p = new Pen(Color.FromArgb(200, 208, 220), 1.6f))
                {
                    e.Graphics.DrawLine(p, 16, 15, 30, 29);
                    e.Graphics.DrawLine(p, 30, 15, 16, 29);
                }
            };
            dlgClose.MouseEnter += (s, e) => { dlgClose.Tag = 1; dlgClose.Invalidate(); };
            dlgClose.MouseLeave += (s, e) => { dlgClose.Tag = null; dlgClose.Invalidate(); };
            dlgClose.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            TitleStrip.Controls.Add(dlgClose);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(UI.Border, 1))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    // Rounded dark input with accent focus ring
    public class GlassInput : Panel
    {
        TextBox box;
        bool focus;
        public TextBox Box { get { return box; } }
        public override string Text { get { return box.Text; } set { box.Text = value; } }
        public bool UseSystemPasswordChar { get { return box.UseSystemPasswordChar; } set { box.UseSystemPasswordChar = value; } }
        public GlassInput()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = UI.Bg;
            Size = new Size(200, 40);
            box = new TextBox();
            box.BorderStyle = BorderStyle.None;
            box.BackColor = Color.FromArgb(20, 27, 39);
            box.ForeColor = UI.Text;
            box.Font = UI.F(11);
            box.Location = new Point(12, 11);
            box.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            box.GotFocus += (s, e) => { focus = true; Invalidate(); };
            box.LostFocus += (s, e) => { focus = false; Invalidate(); };
            Controls.Add(box);
            Resize += (s, e) => { box.Width = Width - 24; };
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rc = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = UI.Rounded(rc, 8))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(20, 27, 39))) g.FillPath(b, path);
                Color border = focus ? UI.Accent : UI.Border;
                if (focus) UI.Glow(g, rc, 8, UI.Accent, 2);
                using (Pen pen = new Pen(border, focus ? 1.6f : 1f)) g.DrawPath(pen, path);
            }
        }
    }

    // ===================================================================
    //  PASSWORD SETUP
    // ===================================================================
    public class PasswordSetupForm : GlassForm
    {
        public string Password = "";
        GlassInput txtPass, txtConfirm;
        Label lblErr;

        public PasswordSetupForm()
        {
            ClientSize = new Size(460, 420);
            BuildGlassChrome("Set Your Password");

            var info = new Label();
            info.Text = "This password is required to exit or disable LockGuard.\nKeep it safe - write it down somewhere.";
            info.ForeColor = UI.Dim; info.Location = new Point(28, 66); info.AutoSize = true; Controls.Add(info);

            var l1 = new Label(); l1.Text = "PASSWORD"; l1.Font = UI.FB(8); l1.ForeColor = UI.Dim; l1.Location = new Point(28, 108); l1.AutoSize = true; Controls.Add(l1);
            txtPass = new GlassInput(); txtPass.Location = new Point(28, 128); txtPass.Size = new Size(404, 42); txtPass.UseSystemPasswordChar = true; Controls.Add(txtPass);

            var l2 = new Label(); l2.Text = "CONFIRM PASSWORD"; l2.Font = UI.FB(8); l2.ForeColor = UI.Dim; l2.Location = new Point(28, 186); l2.AutoSize = true; Controls.Add(l2);
            txtConfirm = new GlassInput(); txtConfirm.Location = new Point(28, 206); txtConfirm.Size = new Size(404, 42); txtConfirm.UseSystemPasswordChar = true; Controls.Add(txtConfirm);

            lblErr = new Label(); lblErr.ForeColor = UI.Red; lblErr.Font = UI.FB(9); lblErr.Location = new Point(28, 256); lblErr.Size = new Size(404, 22); Controls.Add(lblErr);

            var btnOk = new FlatBtn(); btnOk.Text = "Create Password"; btnOk.BaseColor = UI.Accent; btnOk.HoverColor = Color.FromArgb(90, 200, 255); btnOk.ForeColor = Color.FromArgb(6, 12, 20); btnOk.Accent = true;
            btnOk.Location = new Point(28, 296); btnOk.Size = new Size(404, 48); btnOk.Font = UI.FB(11); Controls.Add(btnOk);
            btnOk.Click += BtnOk_Click;

            AcceptButton = btnOk;
            Shown += (s, e) => txtPass.Box.Focus();
        }

        void BtnOk_Click(object sender, EventArgs e)
        {
            if (txtPass.Text.Length < 4) { lblErr.Text = "Password must be at least 4 characters."; return; }
            if (txtPass.Text != txtConfirm.Text) { lblErr.Text = "Passwords do not match."; return; }
            Password = txtPass.Text;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // ===================================================================
    //  PASSWORD PROMPT
    // ===================================================================
    public class PasswordForm : GlassForm
    {
        public string Password = "";
        GlassInput txtPass;

        public PasswordForm(string title, string prompt)
        {
            ClientSize = new Size(420, 250);
            BuildGlassChrome(title);

            var l = new Label(); l.Text = prompt; l.Font = UI.F(10); l.ForeColor = UI.Text; l.Location = new Point(26, 62); l.Size = new Size(368, 26); Controls.Add(l);
            txtPass = new GlassInput(); txtPass.Location = new Point(26, 92); txtPass.Size = new Size(368, 42); txtPass.UseSystemPasswordChar = true; Controls.Add(txtPass);

            var btnOk = new FlatBtn(); btnOk.Text = "Unlock"; btnOk.BaseColor = UI.Accent; btnOk.HoverColor = Color.FromArgb(90, 200, 255); btnOk.ForeColor = Color.FromArgb(6, 12, 20); btnOk.Accent = true;
            btnOk.Location = new Point(26, 152); btnOk.Size = new Size(178, 46); Controls.Add(btnOk);
            btnOk.Click += (s, e) => { Password = txtPass.Text; DialogResult = DialogResult.OK; Close(); };

            var btnCancel = new FlatBtn(); btnCancel.Text = "Cancel"; btnCancel.Location = new Point(216, 152); btnCancel.Size = new Size(178, 46); Controls.Add(btnCancel);
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            AcceptButton = btnOk; CancelButton = btnCancel;
            Shown += (s, e) => txtPass.Box.Focus();
        }
    }

    // ===================================================================
    //  SETTINGS
    // ===================================================================
    public class SettingsForm : GlassForm
    {
        ConfigManager cfg;
        NumericUpDown numStart, numEnd;
        ToggleSwitch tglAuto;
        public bool changed = false;

        public SettingsForm(ConfigManager config)
        {
            cfg = config;
            ClientSize = new Size(500, 410);
            BuildGlassChrome("Settings");

            var lStart = new Label(); lStart.Text = "START HOUR  (night begins)"; lStart.Font = UI.FB(8); lStart.ForeColor = UI.Dim; lStart.Location = new Point(28, 66); lStart.AutoSize = true; Controls.Add(lStart);
            numStart = new NumericUpDown(); numStart.Location = new Point(340, 60); numStart.Size = new Size(132, 30);
            numStart.Minimum = 0; numStart.Maximum = 23; numStart.Value = cfg.StartHour;
            numStart.Font = UI.F(11); numStart.BorderStyle = BorderStyle.FixedSingle;
            numStart.BackColor = Color.FromArgb(20, 27, 39); numStart.ForeColor = UI.Text; Controls.Add(numStart);

            var lEnd = new Label(); lEnd.Text = "END HOUR  (night ends)"; lEnd.Font = UI.FB(8); lEnd.ForeColor = UI.Dim; lEnd.Location = new Point(28, 112); lEnd.AutoSize = true; Controls.Add(lEnd);
            numEnd = new NumericUpDown(); numEnd.Location = new Point(340, 106); numEnd.Size = new Size(132, 30);
            numEnd.Minimum = 0; numEnd.Maximum = 23; numEnd.Value = cfg.EndHour;
            numEnd.Font = UI.F(11); numEnd.BorderStyle = BorderStyle.FixedSingle;
            numEnd.BackColor = Color.FromArgb(20, 27, 39); numEnd.ForeColor = UI.Text; Controls.Add(numEnd);

            var hint = new Label(); hint.Text = "Example: 18 -> 9 means night is 18:00 to 09:00.\nInternet is cut when the PC is locked during night hours.";
            hint.Font = UI.F(8.5f); hint.ForeColor = UI.Dim; hint.Location = new Point(28, 156); hint.AutoSize = true; Controls.Add(hint);

            var sep = new Panel(); sep.Bounds = new Rectangle(28, 210, 444, 1); sep.BackColor = UI.Border; Controls.Add(sep);

            var lAuto = new Label(); lAuto.Text = "Auto-start at Windows login"; lAuto.Font = UI.F(10); lAuto.Location = new Point(28, 232); lAuto.AutoSize = true; Controls.Add(lAuto);
            tglAuto = new ToggleSwitch(); tglAuto.Location = new Point(420, 226); tglAuto.Checked = cfg.AutoStart; Controls.Add(tglAuto);

            var btnSave = new FlatBtn(); btnSave.Text = "Save"; btnSave.BaseColor = UI.Accent; btnSave.HoverColor = Color.FromArgb(90, 200, 255); btnSave.ForeColor = Color.FromArgb(6, 12, 20); btnSave.Accent = true;
            btnSave.Location = new Point(28, 322); btnSave.Size = new Size(216, 48); btnSave.Font = UI.FB(11); Controls.Add(btnSave);
            btnSave.Click += BtnSave_Click;

            var btnCancel = new FlatBtn(); btnCancel.Text = "Cancel"; btnCancel.Location = new Point(256, 322); btnCancel.Size = new Size(216, 48); Controls.Add(btnCancel);
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        }

        void BtnSave_Click(object sender, EventArgs e)
        {
            int s = (int)numStart.Value, en = (int)numEnd.Value;
            if (s == en) { MessageBox.Show("Start and End hours must be different.", "LockGuard", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            cfg.StartHour = s;
            cfg.EndHour = en;
            bool newAuto = tglAuto.Checked;
            if (newAuto != cfg.AutoStart) changed = true;
            cfg.AutoStart = newAuto;
            cfg.Save();
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // ===================================================================
    //  MAIN FORM
    // ===================================================================
    public class MainForm : Form
    {
        // ---------- Shell-restart robustness (the REAL "disappeared tray icon" fix) ----------
        // When Explorer/DWM restarts (LiveKernelEvent, BEX, crash), Windows broadcasts
        // RegisterWindowMessage("TaskbarCreated") to EVERY top-level window. Only apps that
        // listen and re-register their NotifyIcon keep the tray icon alive. This app did NOT
        // listen (that's why the icon vanished while pid stayed alive at 09:11 = "disappeared").
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern uint RegisterWindowMessage(string lpStringTake);
        static int WmTaskbarCreated2 = (int)RegisterWindowMessage("TaskbarCreated");
        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if (m.Msg == WmTaskbarCreated2)
            {
                try { EnsureTrayVisible(); } catch { }
                try { tray.Visible = false; tray.Visible = true; } catch { }
                return;
            }
            base.WndProc(ref m);
        }
        ConfigManager cfg;
        AdapterView adList;
        TextBox logBox;
        FlatBtn btnDisable, btnEnable, btnRefresh, btnSettings, btnClear;
        NotifyIcon tray;
        ContextMenuStrip trayMenu;
        ToolStripMenuItem mShow, mDisable, mEnable, mStatus, mExit, mSettings;
        System.Windows.Forms.Timer timer;
        EventWaitHandle showEvent;
        bool internetOff = false;
        bool promptedElevate = false;
        string[] disabledTargets = null;
        ulong lastRecordId = 0;
        Panel titleBar, btnClose, btnMin;
        Label lblPasswordBadge;

        public MainForm(ConfigManager config, EventWaitHandle ev)
        {
            cfg = config;
            showEvent = ev;
            Logger.Init(config.DataDir);

            Text = "LockGuard v" + Program.Version;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(940, 700);
            MinimumSize = new Size(940, 700);
            BackColor = UI.Bg;
            ForeColor = UI.Text;
            Font = UI.F(9);
            Icon = Program.AppIcon;
            FormBorderStyle = FormBorderStyle.None;

            BuildTitleBar();
            BuildStatusCards();
            BuildAdapterCard();
            BuildControls();
            BuildLog();
            BuildTray();
            BuildMonitor();

            FormClosing += MainForm_FormClosing;
            Shown += (s, e) => Loaded();
            ResizeRedraw = true;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        void BuildTitleBar()
        {
            titleBar = new Panel();
            titleBar.Dock = DockStyle.Top;
            titleBar.Height = 40;
            titleBar.BackColor = Color.FromArgb(12, 16, 24);
            titleBar.Paint += (s, e) =>
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(12, 16, 24))) e.Graphics.FillRectangle(b, 0, 0, titleBar.Width, titleBar.Height);
                using (SolidBrush b = new SolidBrush(UI.Border)) e.Graphics.FillRectangle(b, 0, titleBar.Height - 1, titleBar.Width, 1);
            };
            titleBar.MouseDown += TitleBar_MouseDown;
            Controls.Add(titleBar);

            // Logo
            var logo = new PictureBox();
            logo.Size = new Size(24, 24); logo.Location = new Point(14, 8);
            logo.BackColor = Color.Transparent;
            logo.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath c = new GraphicsPath())
                {
                    c.AddEllipse(2, 2, 20, 20);
                    using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(2, 2, 20, 20), UI.Accent, UI.Violet, 45f))
                        e.Graphics.FillPath(b, c);
                }
                using (SolidBrush b = new SolidBrush(Color.White))
                {
                    e.Graphics.FillEllipse(b, 7, 7, 5, 4);
                    e.Graphics.FillRectangle(b, 9, 11, 2, 5);
                }
            };
            titleBar.Controls.Add(logo);

            var titleLbl = new Label();
            titleLbl.Text = "LOCKGUARD";
            titleLbl.Font = UI.FB(10);
            titleLbl.ForeColor = UI.Text;
            titleLbl.AutoSize = true;
            titleLbl.BackColor = Color.Transparent;
            titleLbl.Location = new Point(46, 6);
            titleBar.Controls.Add(titleLbl);

            var verLbl = new Label();
            verLbl.Text = "v" + Program.Version;
            verLbl.Font = UI.F(8);
            verLbl.ForeColor = UI.Dim;
            verLbl.AutoSize = true;
            verLbl.BackColor = Color.Transparent;
            verLbl.Location = new Point(46, 22);
            titleBar.Controls.Add(verLbl);

            lblPasswordBadge = new Label();
            lblPasswordBadge.Text = "  PASSWORD PROTECTED  ";
            lblPasswordBadge.Font = UI.FB(7.5f);
            lblPasswordBadge.ForeColor = Color.FromArgb(255, 180, 120);
            lblPasswordBadge.AutoSize = true;
            lblPasswordBadge.BackColor = Color.FromArgb(40, 20, 10);
            lblPasswordBadge.Location = new Point(120, 12);
            lblPasswordBadge.Padding = new Padding(3, 1, 3, 1);
            titleBar.Controls.Add(lblPasswordBadge);

            // Close button
            btnClose = new Panel();
            btnClose.Size = new Size(46, 40); btnClose.Dock = DockStyle.Right;
            btnClose.BackColor = Color.Transparent;
            btnClose.Paint += (s, e) =>
            {
                if (btnClose.Tag != null)
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 50, 60))) e.Graphics.FillRectangle(b, 0, 0, btnClose.Width, btnClose.Height);
                }
                using (Pen p = new Pen(Color.FromArgb(200, 208, 220), 1.6f))
                {
                    e.Graphics.DrawLine(p, 15, 13, 31, 29);
                    e.Graphics.DrawLine(p, 31, 13, 15, 29);
                }
            };
            btnClose.MouseEnter += (s, e) => { btnClose.Tag = 1; btnClose.Invalidate(); };
            btnClose.MouseLeave += (s, e) => { btnClose.Tag = null; btnClose.Invalidate(); };
            btnClose.Click += (s, e) => { Hide(); };
            titleBar.Controls.Add(btnClose);

            // Minimize button
            btnMin = new Panel();
            btnMin.Size = new Size(46, 40); btnMin.Dock = DockStyle.Right;
            btnMin.BackColor = Color.Transparent;
            btnMin.Paint += (s, e) =>
            {
                if (btnMin.Tag != null)
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(50, 140, 200))) e.Graphics.FillRectangle(b, 0, 0, btnMin.Width, btnMin.Height);
                }
                using (Pen p = new Pen(Color.FromArgb(200, 208, 220), 1.6f))
                {
                    e.Graphics.DrawLine(p, 15, 22, 31, 22);
                }
            };
            btnMin.MouseEnter += (s, e) => { btnMin.Tag = 1; btnMin.Invalidate(); };
            btnMin.MouseLeave += (s, e) => { btnMin.Tag = null; btnMin.Invalidate(); };
            btnMin.Click += (s, e) => { WindowState = FormWindowState.Minimized; };
            titleBar.Controls.Add(btnMin);
        }

        void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(this.Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
        }

        Panel Card(Rectangle rc, string title)
        {
            var p = new Panel();
            p.Bounds = rc;
            p.BackColor = UI.Card;
            p.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rr = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
                using (GraphicsPath path = UI.Rounded(rr, 12))
                {
                    using (SolidBrush b = new SolidBrush(UI.Card)) e.Graphics.FillPath(b, path);
                    using (Pen pen = new Pen(UI.Border, 1)) e.Graphics.DrawPath(pen, path);
                }
                using (SolidBrush b = new SolidBrush(UI.Accent)) e.Graphics.FillRectangle(b, 4, 0, 3, p.Height);
                e.Graphics.DrawString(title, UI.FB(9), new SolidBrush(UI.Dim), 18, 12);
            };
            return p;
        }

        // Small status tile with icon, glow accent + value
        class StatTile : Control
        {
            Color accent;
            Color valueColor;
            int kind; // 0 moon, 1 globe, 2 shield, 3 clock
            public Label Value;
            public StatTile(string caption, int iconKind, Color accentColor, int w, int h)
            {
                accent = accentColor;
                kind = iconKind;
                valueColor = UI.Text;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                Size = new Size(w, h);
                BackColor = UI.Card;
                var cap = new Label();
                cap.Text = caption.ToUpperInvariant();
                cap.Font = UI.FB(8f);
                cap.ForeColor = UI.Dim;
                cap.Location = new Point(16, 12);
                cap.AutoSize = true;
                Controls.Add(cap);
                Value = new Label();
                Value.Text = "--";
                Value.Font = UI.FB(13);
                Value.ForeColor = valueColor;
                Value.Location = new Point(16, 36);
                Value.AutoSize = true;
                Controls.Add(Value);
            }
            public void SetValue(string v, Color c)
            {
                Value.Text = v;
                Value.ForeColor = c;
            }
            void DrawIcon(Graphics g)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int cx = ClientSize.Width - 38;
                int cy = ClientSize.Height / 2;
                using (Pen p = new Pen(Color.FromArgb(120, accent), 2f))
                {
                    switch (kind)
                    {
                        case 0: // moon
                            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, accent)))
                            {
                                g.FillEllipse(b, cx - 12, cy - 12, 24, 24);
                                using (SolidBrush cut = new SolidBrush(UI.Bg2))
                                    g.FillEllipse(cut, cx - 6, cy - 15, 24, 24);
                            }
                            break;
                        case 1: // globe
                            g.DrawEllipse(p, cx - 11, cy - 11, 22, 22);
                            g.DrawEllipse(p, cx - 11, cy - 5, 22, 10);
                            g.DrawLine(p, cx - 11, cy, cx + 11, cy);
                            g.DrawLine(p, cx, cy - 11, cx, cy + 11);
                            break;
                        case 2: // shield
                            g.DrawArc(p, cx - 11, cy - 10, 8, 8, 180, 180);
                            g.DrawArc(p, cx + 3, cy - 10, 8, 8, 180, 180);
                            g.DrawLine(p, cx - 11, cy - 10, cx - 15, cy - 4);
                            g.DrawLine(p, cx + 11, cy - 10, cx + 15, cy - 4);
                            g.DrawLine(p, cx - 15, cy - 4, cx + 15, cy - 4);
                            g.DrawArc(p, cx - 17, cy - 4, 34, 16, 0, 180);
                            break;
                        case 3: // clock
                            g.DrawEllipse(p, cx - 11, cy - 11, 22, 22);
                            g.DrawLine(p, cx, cy - 6, cx, cy);
                            g.DrawLine(p, cx, cy, cx + 6, cy + 4);
                            break;
                    }
                }
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rc = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                using (GraphicsPath path = UI.Rounded(rc, 10))
                {
                    using (SolidBrush b = new SolidBrush(UI.Bg2)) g.FillPath(b, path);
                    using (Pen pen = new Pen(UI.Border, 1)) g.DrawPath(pen, path);
                }
                // left accent bar with glow
                UI.Glow(g, new Rectangle(3, 6, 6, ClientSize.Height - 12), 8, accent, 2);
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new Rectangle(4, 8, 4, ClientSize.Height - 16), accent, Color.Transparent, 90f))
                    g.FillRectangle(b, 4, 8, 4, ClientSize.Height - 16);
                DrawIcon(g);
            }
        }

        List<StatTile> statTiles = new List<StatTile>();
        void BuildStatusCards()
        {
            int x = 18, y = 60, w = 214, h = 80, gap = 14;
            StatTile tile;

            tile = new StatTile("Mode", 0, UI.Accent, w, h);
            tile.Location = new Point(x, y); Controls.Add(tile); statTiles.Add(tile);
            x += w + gap;

            tile = new StatTile("Internet", 1, UI.Green, w, h);
            tile.Location = new Point(x, y); Controls.Add(tile); statTiles.Add(tile);
            x += w + gap;

            tile = new StatTile("Protection", 2, UI.Orange, w, h);
            tile.Location = new Point(x, y); Controls.Add(tile); statTiles.Add(tile);
            x += w + gap;

            tile = new StatTile("Schedule", 3, Color.FromArgb(180, 130, 220), w, h);
            tile.Location = new Point(x, y); Controls.Add(tile); statTiles.Add(tile);
        }

        void BuildAdapterCard()
        {
            Panel card = Card(new Rectangle(18, 150, 904, 112), "NETWORK ADAPTERS");
            Controls.Add(card);

            adList = new AdapterView();
            adList.Bounds = new Rectangle(16, 32, 872, 72);
            adList.BackColor = Color.FromArgb(14, 19, 28);
            card.Controls.Add(adList);
        }

        // Futuristic adapter strip - custom drawn rows with LED dots
        class AdapterView : Control
        {
            List<AdapterInfo> items = new List<AdapterInfo>();
            Font fName = UI.F(9);
            Font fDetail = UI.F(8);
            public AdapterView()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }
            public void SetItems(List<AdapterInfo> list)
            {
                items = list;
                Invalidate();
            }
            protected override void OnPaint(PaintEventArgs pevent)
            {
                Graphics g = pevent.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(BackColor);
                int y = 4;
                int rowH = 30;
                using (SolidBrush sep = new SolidBrush(Color.FromArgb(28, 36, 50)))
                {
                    for (int i = 0; i < items.Count; i++, y += rowH)
                    {
                        AdapterInfo a = items[i];
                        Color dot = a.Enabled ? UI.Green : UI.Red;
                        using (SolidBrush b = new SolidBrush(dot))
                            g.FillEllipse(b, 8, y + 8, 8, 8);
                        using (Pen p = new Pen(Color.FromArgb(90, dot), 1f))
                            g.DrawEllipse(p, 4, y + 4, 16, 16);
                        using (SolidBrush b = new SolidBrush(UI.Text))
                            g.DrawString(a.Name, fName, b, 28, y + 1);
                        using (SolidBrush b = new SolidBrush(UI.Dim))
                            g.DrawString(a.Desc, fDetail, b, 28, y + 17);
                        // status text right
                        string st = a.Enabled ? "UP" : "DOWN";
                        if (!a.Enabled && a.Status.Contains("disconnect")) st = a.Status;
                        using (SolidBrush b = new SolidBrush(a.Enabled ? UI.Green : UI.Orange))
                        {
                            StringFormat sf = new StringFormat();
                            sf.Alignment = StringAlignment.Far;
                            g.DrawString(st, UI.FB(8), b, new RectangleF(ClientSize.Width - 140, y + 4, 128, 20), sf);
                            sf.Dispose();
                        }
                        if (i < items.Count - 1)
                            g.FillRectangle(sep, 12, y + rowH - 1, ClientSize.Width - 24, 1);
                    }
                }
            }
        }

        void BuildControls()
        {
            btnDisable = new FlatBtn(); btnDisable.Text = "Disable Internet"; btnDisable.BaseColor = Color.FromArgb(150, 45, 55); btnDisable.HoverColor = Color.FromArgb(190, 60, 72); btnDisable.ForeColor = Color.White; btnDisable.Accent = true;
            btnDisable.Location = new Point(18, 282); btnDisable.Size = new Size(200, 46); btnDisable.Font = UI.FB(11); Controls.Add(btnDisable);
            btnDisable.Click += (s, e) => DisableAction();

            btnEnable = new FlatBtn(); btnEnable.Text = "Enable Internet"; btnEnable.BaseColor = Color.FromArgb(45, 140, 80); btnEnable.HoverColor = Color.FromArgb(60, 175, 105); btnEnable.ForeColor = Color.White; btnEnable.Accent = true;
            btnEnable.Location = new Point(226, 282); btnEnable.Size = new Size(200, 46); btnEnable.Font = UI.FB(11); Controls.Add(btnEnable);
            btnEnable.Click += (s, e) => EnableAction();

            btnSettings = new FlatBtn(); btnSettings.Text = "Settings"; btnSettings.Location = new Point(434, 282); btnSettings.Size = new Size(140, 46); Controls.Add(btnSettings);
            btnSettings.Click += (s, e) => SettingsAction();

            btnRefresh = new FlatBtn(); btnRefresh.Text = "Refresh"; btnRefresh.Location = new Point(582, 282); btnRefresh.Size = new Size(100, 46); Controls.Add(btnRefresh);
            btnRefresh.Click += (s, e) => { RefreshAll(); AddLog("Refreshed"); };

            btnClear = new FlatBtn(); btnClear.Text = "Clear Log"; btnClear.Location = new Point(690, 282); btnClear.Size = new Size(120, 46); Controls.Add(btnClear);
            btnClear.Click += (s, e) => logBox.Clear();
        }

        void BuildLog()
        {
            var lbl = new Label(); lbl.Text = "ACTIVITY LOG"; lbl.Font = UI.FB(9); lbl.ForeColor = UI.Dim; lbl.Location = new Point(20, 342); lbl.AutoSize = true; Controls.Add(lbl);
            logBox = new TextBox();
            logBox.Multiline = true; logBox.ReadOnly = true;
            logBox.BackColor = Color.FromArgb(13, 18, 26);
            logBox.ForeColor = Color.FromArgb(120, 220, 140);
            logBox.Font = UI.FCode(9);
            logBox.Location = new Point(20, 366);
            logBox.Size = new Size(900, 318);
            logBox.BorderStyle = BorderStyle.None;
            logBox.ScrollBars = ScrollBars.Vertical;
            Controls.Add(logBox);
        }

        void BuildTray()
        {
            tray = new NotifyIcon();
            tray.Text = "LockGuard";
            tray.Icon = Program.AppIcon;
            tray.Visible = true;

            trayMenu = new ContextMenuStrip();
            trayMenu.BackColor = Color.FromArgb(30, 32, 40);
            trayMenu.ForeColor = UI.Text;
            trayMenu.Renderer = new ToolStripProfessionalRenderer(new TrayColorTable());
            trayMenu.ShowImageMargin = false;

            mShow = new ToolStripMenuItem("Show Window"); mShow.ForeColor = UI.Text; mShow.Font = UI.FB(9);
            mShow.Click += (s, e) => ShowWindow();
            trayMenu.Items.Add(mShow);

            trayMenu.Items.Add(new ToolStripSeparator());
            mDisable = new ToolStripMenuItem("Disable Internet"); mDisable.ForeColor = UI.Text;
            mDisable.Click += (s, e) => DisableAction();
            mEnable = new ToolStripMenuItem("Enable Internet"); mEnable.ForeColor = UI.Text;
            mEnable.Click += (s, e) => EnableAction();
            trayMenu.Items.Add(mDisable);
            trayMenu.Items.Add(mEnable);

            trayMenu.Items.Add(new ToolStripSeparator());
            mSettings = new ToolStripMenuItem("Settings..."); mSettings.ForeColor = UI.Text;
            mSettings.Click += (s, e) => SettingsAction();
            trayMenu.Items.Add(mSettings);

            mStatus = new ToolStripMenuItem("Status: --"); mStatus.ForeColor = UI.Dim; mStatus.Enabled = false;
            trayMenu.Items.Add(mStatus);

            trayMenu.Items.Add(new ToolStripSeparator());
            mExit = new ToolStripMenuItem("Exit  (password required)"); mExit.ForeColor = Color.FromArgb(235, 120, 120);
            mExit.Click += (s, e) => ExitAction();
            trayMenu.Items.Add(mExit);

            tray.ContextMenuStrip = trayMenu;
            tray.DoubleClick += (s, e) => ShowWindow();
            tray.BalloonTipClicked += (s, e) => ShowWindow();

            // ---------- Persist tray icon across Explorer/shell restarts ----------
            // Windows broadcasts TaskbarCreated to every top-level window when the shell
            // restarts (Explorer crash, DWM restart, taskbar recreation). Apps that only
            // register their NotifyIcon once permanently lose the icon while the process
            // stays alive. Re-register on the broadcast so the tray icon always comes back.
        }

        bool trayKnown;

        void RegisterTaskbarCreated()
        {
            if (trayKnown) return;
            tray.Visible = false;
            tray.Visible = true;
            trayKnown = true;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { RegisterTaskbarCreated(); } catch { }
        }


        void EnsureTrayVisible()
        {
            try
            {
                if (tray == null) return;
                if (tray.Icon == null && Program.AppIcon != null) tray.Icon = Program.AppIcon;
                if (!tray.Visible) tray.Visible = true;
            }
            catch { }
        }


        void BuildMonitor()
        {
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 5000;
            timer.Tick += Tick;
        }

        void Loaded()
        {
            Logger.Info("LockGuard v" + Program.Version + " started");
            Logger.Info("User: " + Environment.UserName + " | PC: " + Environment.MachineName);
            Logger.Info("Night: " + cfg.StartHour + ":00 - " + cfg.EndHour + ":00");
            RefreshAll();
            AddLog("LockGuard started - password protection ACTIVE");
            AutoStartManager.ExePath = Program.AppPath;
            if (NetworkOps.IsAdmin()) { AddLog("Running as Administrator - full control"); statTiles[2].SetValue("ACTIVE", UI.Green); }
            else { AddLog("WARNING: Not admin. Use Settings or elevated launch for adapter control."); statTiles[2].SetValue("LIMITED", UI.Orange); }
            timer.Start();
            tray.ShowBalloonTip(2500, "LockGuard", "Running. Password required to exit.", ToolTipIcon.Info);
            if (showEvent != null)
            {
                var t = new Thread(() =>
                {
                    for (; ; )
                    {
                        try { showEvent.WaitOne(); }
                        catch { return; }
                        try { Invoke(new Action(ShowWindow)); }
                        catch { return; }
                    }
                });
                t.IsBackground = true; t.Start();
            }
        }

        void ShowWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
            if (Program.AppIcon != null) tray.Icon = NetworkOps.IsAdmin() ? Program.AppIcon : SystemIcons.Shield;
        }

        void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
            tray.ShowBalloonTip(1500, "LockGuard", "Running in tray. Right-click to manage.", ToolTipIcon.Info);
        }

        void DisableAction()
        {
            if (!RequirePassword("disable internet")) return;
            var targets = TargetAdapters();
            if (targets == null || targets.Length == 0) { AddLog("No eligible adapters found."); return; }
            AddLog("Disabling internet...");
            string detail;
            bool ok = NetworkOps.DisableAll(targets, out detail);
            if (detail == "not_admin") { ElevatePrompt("Disabling the internet requires administrator rights. Restart LockGuard as Administrator?"); return; }
            if (ok) { internetOff = true; disabledTargets = targets; AddLog("Internet disabled"); Logger.Info("Internet DISABLED (manual)"); }
            else AddLog("Disable FAILED");
            RefreshAll();
        }

        void EnableAction()
        {
            if (!RequirePassword("enable internet")) return;
            string[] targets = disabledTargets != null && disabledTargets.Length > 0 ? disabledTargets : TargetAdapters();
            if (targets == null || targets.Length == 0) { AddLog("No eligible adapters found."); return; }
            AddLog("Enabling internet...");
            string detail;
            bool ok = NetworkOps.EnableAll(targets, out detail);
            if (detail == "not_admin") { ElevatePrompt("Enabling the internet requires administrator rights. Restart LockGuard as Administrator?"); return; }
            if (!ok && targets != disabledTargets) { ok = NetworkOps.EnableAll(TargetAdapters(), out detail); }
            if (!ok && detail != "not_admin") { ok = NetworkOps.EnableDisabled(out detail); }
            if (ok) { internetOff = false; disabledTargets = null; AddLog("Internet enabled"); Logger.Info("Internet RE-ENABLED"); }
            else if (detail == "not_admin") { ElevatePrompt("Enabling the internet requires administrator rights. Restart LockGuard as Administrator?"); }
            else AddLog("Nothing to enable");
            RefreshAll();
        }

        string[] TargetAdapters()
        {
            var list = new List<string>();
            foreach (var a in NetworkOps.List())
            {
                if (!a.Physical) continue;
                list.Add(a.Name);
            }
            return list.ToArray();
        }

        void ElevatePrompt(string msg)
        {
            var r = MessageBox.Show(msg + "\n\nThe taskbar/syray app will restart elevated.", "Administrator Required",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r == DialogResult.Yes)
            {
                try
                {
                    var psi = new ProcessStartInfo(Program.AppPath);
                    psi.Arguments = "-elevated";
                    psi.Verb = "runas";
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                    Application.Exit();
                }
                catch { MessageBox.Show("Elevation was cancelled.", "LockGuard"); }
            }
        }

        void SettingsAction()
        {
            var f = new SettingsForm(cfg);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                if (f.changed)
                {
                    if (cfg.AutoStart) { AutoStartManager.Enable(); AddLog("Auto-start at login: ON"); }
                    else { AutoStartManager.Disable(); AddLog("Auto-start at login: OFF"); }
                }
                AddLog("Settings saved: night " + cfg.StartHour + ":00 - " + cfg.EndHour + ":00");
                RefreshAll();
            }
        }

        void ExitAction()
        {
            if (!RequirePassword("exit LockGuard")) return;
            Logger.Info("LockGuard exited (password verified)");
            tray.Visible = false;
            tray.Dispose();
            Application.Exit();
        }

        bool RequirePassword(string action)
        {
            var f = new PasswordForm("LockGuard - Authentication", "Password to " + action + ":");
            if (f.ShowDialog(this) != DialogResult.OK) return false;
            if (cfg.TestPassword(f.Password)) return true;
            tray.ShowBalloonTip(4000, "LockGuard ALERT", "Wrong password. Attempt logged.", ToolTipIcon.Error);
            Logger.Alert("WRONG PASSWORD for: " + action);
            return false;
        }

        void RefreshAll()
        {
            bool night = cfg.IsNight(DateTime.Now.Hour);
            statTiles[0].SetValue(night ? "NIGHT" : "DAY", night ? UI.Red : UI.Green);

            var ups = NetworkOps.EnabledPhysical();
            if (internetOff || ups.Count == 0) statTiles[1].SetValue("OFF", UI.Red);
            else statTiles[1].SetValue("ON  (" + ups.Count + ")", UI.Green);

            statTiles[3].SetValue(cfg.StartHour.ToString("00") + ":00 - " + cfg.EndHour.ToString("00") + ":00", UI.Text);

            btnDisable.Enabled = !internetOff;
            btnEnable.Enabled = internetOff;
            mDisable.Enabled = !internetOff;
            mEnable.Enabled = internetOff;

            adList.SetItems(NetworkOps.List());

            string m = night ? "NIGHT" : "DAY";
            string n = (internetOff || ups.Count == 0) ? "OFF" : "ON";
            mStatus.Text = "Mode: " + m + "   Net: " + n;
            tray.Text = "LockGuard [" + m + "|" + n + "]";
        }

        void Tick(object sender, EventArgs e)
        {
            try
            {
                bool wasNight = cfg.IsNight(DateTime.Now.AddSeconds(-5).Hour);
                bool nowNight = cfg.IsNight(DateTime.Now.Hour);
                if (nowNight && !wasNight)
                {
                    AddLog(">>> Night started");
                    tray.ShowBalloonTip(4000, "LockGuard", "Night mode ON - internet will be cut on lock.", ToolTipIcon.Warning);
                }
                if (!nowNight && wasNight)
                {
                    AddLog(">>> Night ended");
                    if (internetOff) { ForceEnable(); AddLog("Internet auto re-enabled (morning)"); }
                }
                PollEvents();
                RefreshAll();
            }
            catch { }
        }

        void PollEvents()
        {
            try
            {
                string query = "*[System[(EventID=4800 or EventID=4801 or EventID=4802 or EventID=4803)]]";
                var logQuery = new EventLogQuery("Security", PathType.LogName, query);
                using (EventLogReader reader = new EventLogReader(logQuery))
                {
                    EventRecord ev;
                    bool night = cfg.IsNight(DateTime.Now.Hour);
                    while ((ev = reader.ReadEvent()) != null)
                    {
                        long rec = ev.RecordId.GetValueOrDefault();
                        if (lastRecordId > 0 && (ulong)rec <= lastRecordId) continue;
                        lastRecordId = (ulong)rec;
                        string user = "unknown";
                        try { if (ev.Properties != null && ev.Properties.Count > 1) user = Convert.ToString(ev.Properties[1].Value); } catch { }
                        HandleEvent((int)ev.Id, user, night);
                    }
                }
            }
            catch { }
        }

        void HandleEvent(int id, string user, bool night)
        {
            switch (id)
            {
                case 4800:
                    AddLog("LOCKED by " + user);
                    Logger.Info("LOCKED by " + user);
                    if (night)
                    {
                        AddLog("NIGHT LOCK - cutting internet");
                        string detail;
                        var targets = TargetAdapters();
                        if (targets != null && targetLen(targets) > 0 && NetworkOps.DisableAll(targets, out detail))
                        {
                            if (detail == "not_admin") { AddLog("Cannot cut internet: not admin"); }
                            else { internetOff = true; disabledTargets = targets; AddLog("Internet cut"); tray.ShowBalloonTip(5000, "ALERT", "Locked at NIGHT - internet cut", ToolTipIcon.Warning); }
                        }
                    }
                    break;
                case 4801:
                    if (night)
                    {
                        string alert = "!!! UNLOCK ATTEMPT by " + user + " at NIGHT !!!";
                        AddLog(alert);
                        Logger.AlertFile(alert);
                        Logger.Alert(alert);
                        tray.ShowBalloonTip(8000, "INTRUSION ALERT", "Unlock at NIGHT by " + user + "!", ToolTipIcon.Error);
                        ForceEnable();
                        AddLog("Internet re-enabled for user");
                    }
                    else
                    {
                        AddLog("UNLOCKED by " + user + " (day)");
                        if (internetOff) { ForceEnable(); }
                    }
                    break;
                case 4802:
                    AddLog("Screensaver started by " + user);
                    break;
                case 4803:
                    if (night) { AddLog("Screensaver dismissed at NIGHT by " + user); Logger.Warn("Screensaver off at night by " + user); }
                    break;
            }
        }

        int targetLen(string[] t) { return t == null ? 0 : t.Length; }

        void ForceEnable()
        {
            string detail = "";
            string[] targets = disabledTargets != null && disabledTargets.Length > 0 ? disabledTargets : TargetAdapters();
            bool ok = targets != null && targets.Length > 0 && NetworkOps.EnableAll(targets, out detail);
            if (!ok && targets != disabledTargets) { ok = NetworkOps.EnableAll(TargetAdapters(), out detail); }
            if (!ok && detail != "not_admin") { ok = NetworkOps.EnableDisabled(out detail); }
            if (ok)
            {
                internetOff = false;
                disabledTargets = null;
                AddLog("Internet enabled");
            }
        }

        void AddLog(string text)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text;
            logBox.AppendText(line + Environment.NewLine);
        }
    }

    public class TrayColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(30, 32, 40); } }
        public override Color MenuBorder { get { return Color.FromArgb(60, 64, 76); } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return Color.FromArgb(55, 60, 75); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(55, 60, 75); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(55, 60, 75); } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(65, 70, 88); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(65, 70, 88); } }
        public override Color SeparatorDark { get { return Color.FromArgb(55, 58, 70); } }
        public override Color SeparatorLight { get { return Color.FromArgb(40, 42, 52); } }
    }
}
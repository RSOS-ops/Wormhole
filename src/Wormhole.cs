using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text; 
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.ServiceProcess; 
using System.Runtime.InteropServices; 
using Microsoft.Win32; 
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

namespace WormholeConsole
{
    public class WormholeForm : Form
    {
        // --- NATIVE GDI FOR ROUNDED WINDOW CORNERS ---
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect,
            int nWidthEllipse, int nHeightEllipse
        );

        // --- NATIVE DRAGGING IMPORTS ---
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        // Configuration
        private string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        private string pythonScript;
        private WebView2 webView;
        
        // ENGINE & STATE
        private Process audioEngine;
        private bool isMuted = false;
        private NeuButton btnMute; 
        private NotifyIcon trayIcon;

        // MONITORING
        private System.Windows.Forms.Timer serviceMonitorTimer;
        private bool lastKnownRunningState = false; 
        private bool isRunningSequence = false; 

        // COLORS (Mica Theme)
        private Color micaBackground = ColorTranslator.FromHtml("#2e3238");
        private Color micaText = ColorTranslator.FromHtml("#e0e5ec");
        // About Link Color
        private Color aboutLinkColor = ColorTranslator.FromHtml("#f50a1c");

        // FONT MANAGEMENT
        private PrivateFontCollection pfc = new PrivateFontCollection();
        private FontFamily moonerFont;

        public WormholeForm()
        {
            // 1. PATH SETUP
            string assetsDir = Path.Combine(baseDir, "assets"); 
            string fontPath = Path.Combine(assetsDir, "fonts", "mooner-rounded.otf"); 
            string htmlPath = Path.Combine(assetsDir, "wormhole.html");
            string iconPath = Path.Combine(assetsDir, "wormhole.ico");
            pythonScript = Path.Combine(baseDir, "src", "vst_engine.py");
            string cacheDir = Path.Combine(baseDir, "WebView2Cache");

            // 2. LOAD CUSTOM FONT
            LoadCustomFont(fontPath);

            // 3. FORM SETUP
            this.Text = "Wormhole Console";
            this.Size = new Size(500, 800);
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = micaBackground; 
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = false; 
            
            // Apply 8px Rounded Corners
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 8, 8));

            // 4. SYSTEM TRAY SETUP
            trayIcon = new NotifyIcon();
            trayIcon.Text = "Wormhole Console";
            if (File.Exists(iconPath)) trayIcon.Icon = new Icon(iconPath);
            trayIcon.Visible = false;
            trayIcon.DoubleClick += (s, e) => RestoreFromTray();

            // 5. START ENGINES
            EnforceManualStartup(); 
            StartAudioEngine();
            InitServiceMonitor();

            // 6. LAYOUT PANELS
            Panel gripPanel = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = micaBackground };
            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 420, BackColor = micaBackground }; 
            Panel botPanel = new Panel { Dock = DockStyle.Fill, BackColor = micaBackground };
            
            this.Controls.Add(botPanel);
            this.Controls.Add(topPanel);
            this.Controls.Add(gripPanel);

            // --- TITLE BAR BUTTONS ---
            // Minimize
            Button btnMin = new Button();
            btnMin.Size = new Size(45, 30);
            btnMin.Dock = DockStyle.Right;
            btnMin.FlatStyle = FlatStyle.Flat;
            btnMin.FlatAppearance.BorderSize = 0;
            btnMin.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);
            btnMin.BackColor = Color.Transparent;
            btnMin.Click += (s, e) => MinimizeToTray();
            btnMin.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.None; 
                using (Pen p = new Pen(micaText, 2))
                {
                    int cx = btnMin.Width / 2;
                    int y = btnMin.Height - 10;
                    e.Graphics.DrawLine(p, cx - 6, y, cx + 6, y);
                }
            };
            gripPanel.Controls.Add(btnMin);

            // Close
            Button btnClose = new Button();
            btnClose.Size = new Size(45, 30);
            btnClose.Dock = DockStyle.Right; 
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.Crimson;
            btnClose.BackColor = Color.Transparent;
            btnClose.Click += (s, e) => ExitApp();
            btnClose.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(micaText, 2))
                {
                    int cx = btnClose.Width / 2;
                    int cy = btnClose.Height / 2;
                    int r = 5; 
                    e.Graphics.DrawLine(p, cx - r, cy - r, cx + r, cy + r);
                    e.Graphics.DrawLine(p, cx + r, cy - r, cx - r, cy + r);
                }
            };
            gripPanel.Controls.Add(btnClose);

            // 7. WEBVIEW2
            if (!File.Exists(htmlPath))
            {
                Label err = new Label { Text = "MISSING FILE:\n" + htmlPath, ForeColor = Color.Red, Dock = DockStyle.Fill };
                topPanel.Controls.Add(err);
            }
            else
            {
                try 
                {
                    webView = new WebView2();
                    webView.Dock = DockStyle.Fill;
                    webView.DefaultBackgroundColor = micaBackground;
                    
                    webView.NavigationCompleted += async (s, e) => { 
                        for (int i = 0; i < 10; i++) 
                        {
                            bool running = AreServicesRunning();
                            lastKnownRunningState = running;
                            UpdateUiState(running);
                            await Task.Delay(250); 
                        }
                    };
                    
                    topPanel.Controls.Add(webView);
                    InitializeWebView(webView, cacheDir, htmlPath);
                }
                catch (Exception ex) { MessageBox.Show("Critical Init Error: " + ex.Message); }
            }

            // 8. NEUMORPHIC UI BUTTONS
            CreateNeuButton(botPanel, "OPEN WORMHOLE", 50, (s, e) => RunSequence(true));
            CreateNeuButton(botPanel, "COLLAPSE WORMHOLE", 130, (s, e) => RunSequence(false));
            btnMute = CreateNeuButton(botPanel, "MUTE SFX", 210, (s, e) => ToggleMute());

            // 9. ABOUT LINK
            Label lblAbout = new Label();
            lblAbout.Text = "About";
            lblAbout.ForeColor = aboutLinkColor; 
            lblAbout.BackColor = Color.Transparent;
            lblAbout.AutoSize = true;
            lblAbout.Cursor = Cursors.Hand;
            if (moonerFont != null) lblAbout.Font = new Font(moonerFont, 10, FontStyle.Regular);
            else lblAbout.Font = new Font("Segoe UI", 9, FontStyle.Regular);
            
            lblAbout.Location = new Point(botPanel.Width - 70, botPanel.Height - 30);
            lblAbout.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            
            lblAbout.MouseEnter += (s, e) => lblAbout.ForeColor = Color.White;
            lblAbout.MouseLeave += (s, e) => lblAbout.ForeColor = aboutLinkColor; 
            
            lblAbout.Click += (s, e) => {
                using (var about = new AboutPopup(moonerFont)) {
                    about.ShowDialog(this);
                }
            };
            botPanel.Controls.Add(lblAbout);

            // 10. DRAG LOGIC
            AddNativeDrag(this);
            AddNativeDrag(gripPanel);
            AddNativeDrag(topPanel);
            AddNativeDrag(botPanel);
        }

        // --- CUSTOM FONT LOADER ---
        private void LoadCustomFont(string path)
        {
            if (File.Exists(path))
            {
                try 
                {
                    pfc.AddFontFile(path);
                    moonerFont = pfc.Families[0];
                }
                catch (Exception ex) { 
                    Debug.WriteLine("Font Load Error: " + ex.Message);
                    moonerFont = new FontFamily("Segoe UI"); 
                }
            }
            else
            {
                moonerFont = new FontFamily("Segoe UI");
            }
        }

        private NeuButton CreateNeuButton(Panel p, string text, int y, EventHandler action)
        {
            NeuButton btn = new NeuButton();
            btn.Text = text;
            if (moonerFont != null) btn.CustomFontFamily = moonerFont; 
            btn.Location = new Point(100, y);
            btn.Size = new Size(300, 60);
            btn.Click += action;
            p.Controls.Add(btn);
            return btn;
        }

        // --- WINDOW STATE LOGIC ---
        private void MinimizeToTray() { this.Hide(); trayIcon.Visible = true; }
        private void RestoreFromTray() { this.Show(); this.WindowState = FormWindowState.Normal; trayIcon.Visible = false; this.Activate(); }
        private void EnforceManualStartup() { CheckAndSetManual("Tailscale"); CheckAndSetManual("SunshineService"); }
        private void CheckAndSetManual(string serviceName) { try { using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceName, false)) { if (key != null) { object val = key.GetValue("Start"); if (val != null && (int)val != 3) { RunCommand("sc", "config \"" + serviceName + "\" start= demand", true); }}}} catch {} }
        private void InitServiceMonitor() { serviceMonitorTimer = new System.Windows.Forms.Timer(); serviceMonitorTimer.Interval = 2000; serviceMonitorTimer.Tick += MonitorTick; serviceMonitorTimer.Start(); }
        private bool AreServicesRunning() { try { bool tailscale = false; bool sunshine = false; ServiceController[] services = ServiceController.GetServices(); foreach (ServiceController service in services) { if (service.ServiceName == "Tailscale" && service.Status == ServiceControllerStatus.Running) tailscale = true; if (service.ServiceName == "SunshineService" && service.Status == ServiceControllerStatus.Running) sunshine = true; } return tailscale && sunshine; } catch { return false; } }
        private void MonitorTick(object sender, EventArgs e) { if (isRunningSequence || webView == null || webView.CoreWebView2 == null) return; bool currentlyRunning = AreServicesRunning(); if (currentlyRunning != lastKnownRunningState) { lastKnownRunningState = currentlyRunning; UpdateUiState(currentlyRunning); } }
        private void UpdateUiState(bool running) { if (running) { webView.ExecuteScriptAsync("setWormholeState(true)"); ShowHud("SYSTEMS ONLINE", "cyan", true); } else { webView.ExecuteScriptAsync("setWormholeState(false)"); ShowHud("SYSTEMS OFFLINE", "crimson", true); } }
        private async void RunSequence(bool open) { isRunningSequence = true; if (open) TriggerSound("PLAY_OPEN"); else TriggerSound("PLAY_CLOSE"); string colorHex = open ? "cyan" : "crimson"; if (open) { if (webView != null && webView.CoreWebView2 != null) webView.ExecuteScriptAsync("setWormholeState(true)"); ShowHud("STARTING TAILSCALE...", colorHex); RunCommand("net", "start tailscale", true); await Task.Delay(1000); ShowHud("STARTING SUNSHINE...", colorHex); RunCommand("net", "start SunshineService", true); await Task.Delay(2000); } else { ShowHud("STOPPING SUNSHINE...", colorHex); RunCommand("net", "stop SunshineService", true); await Task.Delay(1000); ShowHud("STOPPING TAILSCALE...", colorHex); RunCommand("net", "stop tailscale", true); await Task.Delay(1000); } isRunningSequence = false; lastKnownRunningState = !AreServicesRunning(); MonitorTick(null, null); }
        private void StartAudioEngine() { Task.Run(() => { try { string pythonExe = "python"; string localPython = Path.Combine(baseDir, "python", "python.exe"); if (File.Exists(localPython)) pythonExe = localPython; ProcessStartInfo psi = new ProcessStartInfo(pythonExe, "\"" + pythonScript + "\""); psi.UseShellExecute = false; psi.RedirectStandardInput = true; psi.RedirectStandardOutput = true; psi.CreateNoWindow = true; audioEngine = new Process(); audioEngine.StartInfo = psi; audioEngine.Start(); } catch (Exception ex) { MessageBox.Show("Audio Engine Failed: " + ex.Message); } }); }
        private void TriggerSound(string cmd) { if (isMuted) return; if (audioEngine != null && !audioEngine.HasExited) audioEngine.StandardInput.WriteLine(cmd); }
        private void ToggleMute() { isMuted = !isMuted; btnMute.Text = isMuted ? "UNMUTE SFX" : "MUTE SFX"; if (isMuted) TriggerSound("MUTE_TOGGLE"); }
        private void ExitApp() { if (audioEngine != null && !audioEngine.HasExited) try { audioEngine.Kill(); } catch {} if (trayIcon != null) trayIcon.Dispose(); Application.Exit(); }
        private async void InitializeWebView(WebView2 wv, string cache, string html) { var env = await CoreWebView2Environment.CreateAsync(null, cache, null); await wv.EnsureCoreWebView2Async(env); wv.Source = new Uri(new Uri(html).AbsoluteUri); }
        private void ShowHud(string text, string color, bool persist = false) { if (webView != null && webView.CoreWebView2 != null) { string safeText = text.Replace("'", ""); string jsPersist = persist ? "true" : "false"; string js = "updateHud('" + safeText + "', '" + color + "', " + jsPersist + ")"; try { webView.ExecuteScriptAsync(js); } catch {} } }
        private void RunCommand(string cmd, string args, bool hidden = false) { ProcessStartInfo psi = new ProcessStartInfo(cmd, args); psi.UseShellExecute = false; psi.CreateNoWindow = true; psi.Verb = "runas"; try { Process.Start(psi); } catch {} }
        private void AddNativeDrag(Control c) { c.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); } }; }

        [STAThread]
        static void Main() { Application.EnableVisualStyles(); Application.Run(new WormholeForm()); }
    }

    // --- POPUP: ABOUT & DONATION ---
    public class AboutPopup : Form
    {
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        public AboutPopup(FontFamily customFont)
        {
            this.Size = new Size(400, 500);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            
            // Solid Dark Background
            this.BackColor = ColorTranslator.FromHtml("#1c1e21");

            // Matched Region (20) with Paint (20)
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));

            // Dragging
            this.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(this.Handle, 0xA1, 0x2, 0); }};

            // Close Button
            Label btnClose = new Label { Text = "X", ForeColor = Color.Gray, AutoSize = true, Location = new Point(370, 10), Cursor = Cursors.Hand };
            btnClose.Click += (s, e) => this.Close();
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.White;
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.Gray;
            this.Controls.Add(btnClose);

            // Title
            Label lblTitle = new Label { Text = "ABOUT WORMHOLE", AutoSize = false, Width = 400, TextAlign = ContentAlignment.MiddleCenter, Height = 40, ForeColor = Color.Cyan, Location = new Point(0, 20) };
            if (customFont != null) lblTitle.Font = new Font(customFont, 16);
            this.Controls.Add(lblTitle);

            // Copyright
            string copyText = "Chip Johnson\n Los Angeles, CA\n 2025\nThe Royal Society of Summoners";
            Label lblCopy = new Label { Text = copyText, AutoSize = false, Width = 380, Height = 50, TextAlign = ContentAlignment.TopCenter, ForeColor = Color.FromArgb(200, 200, 200), Location = new Point(10, 70), Font = new Font("Segoe UI", 9) };
            this.Controls.Add(lblCopy);

            // Divider
            Label div1 = new Label { AutoSize = false, Height = 1, Width = 300, BackColor = Color.FromArgb(60,60,60), Location = new Point(50, 130) };
            this.Controls.Add(div1);

            // Donation Text
            Label lblDonate = new Label { Text = "If you're finding this tool useful,\nconsider buying me a coffee!", AutoSize = false, Width = 380, Height = 40, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, Location = new Point(10, 145), Font = new Font("Segoe UI", 10, FontStyle.Italic) };
            this.Controls.Add(lblDonate);

            // Links (Cashapp, Venmo, Paypal)
            AddLink("Cashapp ($tmjiii)", "https://cash.me/$tmjiii", 195);
            AddLink("Venmo (@tmjiii)", "https://venmo.com/@tmjiii", 225);
            AddLink("PayPal (tmjiii)", "https://paypal.me/tmjiii", 255);

            // Divider 2
            Label div2 = new Label { AutoSize = false, Height = 1, Width = 300, BackColor = Color.FromArgb(60,60,60), Location = new Point(50, 295) };
            this.Controls.Add(div2);

            // Crypto Header (Red + Larger)
            Label lblCrypto = new Label { Text = "OR - If you're on the right side of the new world order:", AutoSize = false, Width = 380, Height = 20, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(10, 310) };
            lblCrypto.ForeColor = ColorTranslator.FromHtml("#f50a1c"); 
            lblCrypto.Font = new Font("Segoe UI", 10, FontStyle.Bold); 
            this.Controls.Add(lblCrypto);

            // Crypto Addresses
            AddCryptoBox("Ξ ETH", "0xA6921320f87Ba53f40570edfbc8584b11F43613D", 340);
            AddCryptoBox("₿ BTC", "bc1q3f6kfh7nmyuh4wwl09a7sseha2avqjngprp3h8", 390);
        }

        // White Border Implementation
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen borderPen = new Pen(Color.White, 2)) 
            {
                Rectangle rect = this.ClientRectangle;
                // Inflate by -2 to pull border completely inside the jagged Region clip
                rect.Inflate(-2, -2); 
                
                int d = 20; 
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                    e.Graphics.DrawPath(borderPen, path);
                }
            }
        }

        private void AddLink(string text, string url, int y)
        {
            LinkLabel ll = new LinkLabel();
            ll.Text = text;
            ll.AutoSize = false;
            ll.Width = 400;
            ll.TextAlign = ContentAlignment.MiddleCenter;
            ll.Location = new Point(0, y);
            ll.LinkColor = Color.FromArgb(100, 200, 255);
            ll.ActiveLinkColor = Color.White;
            ll.Font = new Font("Segoe UI", 11);
            ll.LinkClicked += (s, e) => {
                try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch {}
            };
            this.Controls.Add(ll);
        }

        private void AddCryptoBox(string coin, string addr, int y)
        {
            // Increased Font to 10 for the Label (ETH/BTC)
            Label lbl = new Label { Text = coin + ":", ForeColor = Color.Gray, AutoSize = true, Location = new Point(20, y), Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            this.Controls.Add(lbl);

            TextBox tb = new TextBox();
            tb.Text = addr;
            tb.ReadOnly = true;
            tb.BorderStyle = BorderStyle.None;
            tb.BackColor = Color.FromArgb(40, 44, 50); 
            tb.ForeColor = Color.FromArgb(200, 200, 200);
            
            // Increased Width to 360 to accommodate larger text
            tb.Width = 360; 
            tb.Location = new Point(20, y + 20);
            
            // Increased Font to 11 for the Address
            tb.Font = new Font("Consolas", 11); 
            this.Controls.Add(tb);
        }
    }

    // --- NEU BUTTON (With Centering Fix) ---
    public class NeuButton : Control
    {
        private bool isPressed = false;
        private int borderRadius = 20;
        private int shadowOffset = 4;
        private int textYOffset = 6; // 6px offset for Mooner font
        private Color baseColor = ColorTranslator.FromHtml("#2e3238"); 
        private Color textColor = ColorTranslator.FromHtml("#e0e5ec");
        public FontFamily CustomFontFamily { get; set; }

        public NeuButton()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent; 
            this.ForeColor = textColor;
            this.Font = new Font("Segoe UI", 12, FontStyle.Bold); 
            this.Cursor = Cursors.Hand;
            this.Size = new Size(200, 50);
        }
        protected override void OnMouseDown(MouseEventArgs e) { isPressed = true; this.Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { isPressed = false; this.Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; 
            Rectangle rect = new Rectangle(5, 5, this.Width - 10, this.Height - 10);
            if (isPressed) rect.Offset(2, 2);
            using (GraphicsPath path = GetRoundedPath(rect, borderRadius))
            {
                if (!isPressed)
                {
                    for (int i = 0; i < 4; i++) { using (Pen p = new Pen(Color.FromArgb(40 - (i * 10), 0, 0, 0), i + 2)) g.DrawPath(p, GetShiftedPath(rect, shadowOffset, shadowOffset, borderRadius)); }
                    for (int i = 0; i < 4; i++) { using (Pen p = new Pen(Color.FromArgb(30 - (i * 5), 255, 255, 255), i + 2)) g.DrawPath(p, GetShiftedPath(rect, -shadowOffset / 2, -shadowOffset / 2, borderRadius)); }
                }
                using (LinearGradientBrush brush = new LinearGradientBrush(rect, isPressed ? Color.FromArgb(20, 23, 27) : ControlPaint.Light(baseColor, 0.05f), isPressed ? Color.FromArgb(20, 23, 27) : baseColor, 45f)) { g.FillPath(brush, path); }
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                    Font useFont = this.Font;
                    if (CustomFontFamily != null) { useFont = new Font(CustomFontFamily, 14, FontStyle.Regular); }
                    Rectangle textRect = rect; textRect.Offset(0, textYOffset);
                    g.DrawString(this.Text, useFont, new SolidBrush(this.ForeColor), textRect, sf);
                }
            }
        }
        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath(); int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }
        private GraphicsPath GetShiftedPath(Rectangle rect, int offX, int offY, int radius) { Rectangle newRect = rect; newRect.Offset(offX, offY); return GetRoundedPath(newRect, radius); }
    }
}
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private NeuButton btnMute; // Using new component
        private NotifyIcon trayIcon;

        // MONITORING
        private System.Windows.Forms.Timer serviceMonitorTimer;
        private bool lastKnownRunningState = false; 
        private bool isRunningSequence = false; 

        // COLORS (Mica Theme)
        private Color micaBackground = ColorTranslator.FromHtml("#2e3238");
        private Color micaText = ColorTranslator.FromHtml("#e0e5ec");

        public WormholeForm()
        {
            // 1. PATH SETUP
            string assetsDir = Path.Combine(baseDir, "assets"); 
            string htmlPath = Path.Combine(assetsDir, "wormhole.html");
            string iconPath = Path.Combine(assetsDir, "wormhole.ico");
            pythonScript = Path.Combine(baseDir, "src", "vst_engine.py");
            string cacheDir = Path.Combine(baseDir, "WebView2Cache");

            // 2. FORM SETUP
            this.Text = "Wormhole Console";
            this.Size = new Size(500, 800);
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = micaBackground; // Mica Color
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = false; 
            
            // Apply 8px Rounded Corners to the App Window
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 8, 8));

            // 3. SYSTEM TRAY SETUP
            trayIcon = new NotifyIcon();
            trayIcon.Text = "Wormhole Console";
            if (File.Exists(iconPath)) trayIcon.Icon = new Icon(iconPath);
            trayIcon.Visible = false;
            trayIcon.DoubleClick += (s, e) => RestoreFromTray();

            // 4. START ENGINES
            EnforceManualStartup(); 
            StartAudioEngine();
            InitServiceMonitor();

            // 5. LAYOUT PANELS
            Panel gripPanel = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = micaBackground };
            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 420, BackColor = micaBackground }; 
            Panel botPanel = new Panel { Dock = DockStyle.Fill, BackColor = micaBackground };
            
            this.Controls.Add(botPanel);
            this.Controls.Add(topPanel);
            this.Controls.Add(gripPanel);

            // --- TITLE BAR BUTTONS (Custom Paint) ---
            
            // 1. MINIMIZE BUTTON (Far Right)
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

            // 2. CLOSE BUTTON (Left of Min)
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


            // 6. WEBVIEW2
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
                    webView.DefaultBackgroundColor = micaBackground; // Blend with Mica
                    
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

            // 7. NEUMORPHIC UI BUTTONS
            // Increased spacing (Y axis) to accommodate shadows
            CreateNeuButton(botPanel, "OPEN WORMHOLE", 50, (s, e) => RunSequence(true));
            CreateNeuButton(botPanel, "COLLAPSE WORMHOLE", 130, (s, e) => RunSequence(false));
            btnMute = CreateNeuButton(botPanel, "MUTE SFX", 210, (s, e) => ToggleMute());

            // 8. DRAG LOGIC
            AddNativeDrag(this);
            AddNativeDrag(gripPanel);
            AddNativeDrag(topPanel);
            AddNativeDrag(botPanel);
        }

        // --- WINDOW STATE LOGIC ---

        private void MinimizeToTray()
        {
            this.Hide();
            trayIcon.Visible = true;
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            trayIcon.Visible = false;
            this.Activate(); 
        }

        // --- SERVICE CONFIGURATION ---

        private void EnforceManualStartup()
        {
            CheckAndSetManual("Tailscale");
            CheckAndSetManual("SunshineService");
        }

        private void CheckAndSetManual(string serviceName)
        {
            try 
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceName, false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("Start");
                        if (val != null && (int)val != 3) 
                        {
                            RunCommand("sc", "config \"" + serviceName + "\" start= demand", true);
                        }
                    }
                }
            }
            catch {}
        }


        // --- SERVICE MONITORING ---

        private void InitServiceMonitor()
        {
            serviceMonitorTimer = new System.Windows.Forms.Timer();
            serviceMonitorTimer.Interval = 2000; 
            serviceMonitorTimer.Tick += MonitorTick;
            serviceMonitorTimer.Start();
        }

        private bool AreServicesRunning()
        {
            try
            {
                bool tailscale = false;
                bool sunshine = false;
                ServiceController[] services = ServiceController.GetServices();
                foreach (ServiceController service in services)
                {
                    if (service.ServiceName == "Tailscale" && service.Status == ServiceControllerStatus.Running) tailscale = true;
                    if (service.ServiceName == "SunshineService" && service.Status == ServiceControllerStatus.Running) sunshine = true;
                }
                return tailscale && sunshine;
            }
            catch { return false; }
        }

        private void MonitorTick(object sender, EventArgs e)
        {
            if (isRunningSequence || webView == null || webView.CoreWebView2 == null) return;

            bool currentlyRunning = AreServicesRunning();

            if (currentlyRunning != lastKnownRunningState)
            {
                lastKnownRunningState = currentlyRunning;
                UpdateUiState(currentlyRunning);
            }
        }

        private void UpdateUiState(bool running)
        {
            if (running) {
                webView.ExecuteScriptAsync("setWormholeState(true)");
                ShowHud("SYSTEMS ONLINE", "cyan", true);
            } else {
                webView.ExecuteScriptAsync("setWormholeState(false)");
                ShowHud("SYSTEMS OFFLINE", "crimson", true);
            }
        }

        // --- AUDIO & SEQUENCES ---

        private async void RunSequence(bool open)
        {
            isRunningSequence = true; 
            
            if (open) TriggerSound("PLAY_OPEN");
            else TriggerSound("PLAY_CLOSE");

            string colorHex = open ? "cyan" : "crimson"; 

            if (open) {
                if (webView != null && webView.CoreWebView2 != null) webView.ExecuteScriptAsync("setWormholeState(true)");
                ShowHud("STARTING TAILSCALE...", colorHex);
                RunCommand("net", "start tailscale", true);
                await Task.Delay(1000); 
                
                ShowHud("STARTING SUNSHINE...", colorHex);
                RunCommand("net", "start SunshineService", true);
                await Task.Delay(2000); 
            } else {
                ShowHud("STOPPING SUNSHINE...", colorHex);
                RunCommand("net", "stop SunshineService", true);
                await Task.Delay(1000);
                
                ShowHud("STOPPING TAILSCALE...", colorHex);
                RunCommand("net", "stop tailscale", true);
                await Task.Delay(1000);
            }

            isRunningSequence = false; 
            lastKnownRunningState = !AreServicesRunning(); 
            MonitorTick(null, null); 
        }

        private void StartAudioEngine()
        {
            Task.Run(() => 
            {
                try {
                    string pythonExe = "python";
                    string localPython = Path.Combine(baseDir, "python", "python.exe");
                    if (File.Exists(localPython)) pythonExe = localPython;

                    ProcessStartInfo psi = new ProcessStartInfo(pythonExe, "\"" + pythonScript + "\"");
                    psi.UseShellExecute = false;
                    psi.RedirectStandardInput = true;
                    psi.RedirectStandardOutput = true; 
                    psi.CreateNoWindow = true;

                    audioEngine = new Process();
                    audioEngine.StartInfo = psi;
                    audioEngine.Start();
                }
                catch (Exception ex) { MessageBox.Show("Audio Engine Failed: " + ex.Message); }
            });
        }

        private void TriggerSound(string cmd)
        {
            if (isMuted) return;
            if (audioEngine != null && !audioEngine.HasExited) audioEngine.StandardInput.WriteLine(cmd);
        }
        
        private void ToggleMute()
        {
            isMuted = !isMuted;
            btnMute.Text = isMuted ? "UNMUTE SFX" : "MUTE SFX";
            if (isMuted) TriggerSound("MUTE_TOGGLE"); 
        }

        private void ExitApp()
        {
            if (audioEngine != null && !audioEngine.HasExited) try { audioEngine.Kill(); } catch {}
            if (trayIcon != null) trayIcon.Dispose(); 
            Application.Exit();
        }

        // --- UTILITIES ---

        private async void InitializeWebView(WebView2 wv, string cache, string html)
        {
            var env = await CoreWebView2Environment.CreateAsync(null, cache, null);
            await wv.EnsureCoreWebView2Async(env);
            wv.Source = new Uri(new Uri(html).AbsoluteUri);
        }

        // UPDATED: Now creates NeuButtons
        private NeuButton CreateNeuButton(Panel p, string text, int y, EventHandler action)
        {
            NeuButton btn = new NeuButton();
            btn.Text = text;
            btn.Location = new Point(100, y); // Centered X roughly (500 width / 2 - 150 (half btn width))
            // Note: NeuButton class hardcodes width to 200/300, adjusting here if needed or relying on class default
            btn.Size = new Size(300, 60); // Slightly taller for better shadow rendering
            btn.Click += action;
            p.Controls.Add(btn);
            return btn;
        }

        private void ShowHud(string text, string color, bool persist = false)
        {
            if (webView != null && webView.CoreWebView2 != null)
            {
                string safeText = text.Replace("'", "");
                string jsPersist = persist ? "true" : "false"; 
                string js = "updateHud('" + safeText + "', '" + color + "', " + jsPersist + ")";
                try { webView.ExecuteScriptAsync(js); } catch {}
            }
        }

        private void RunCommand(string cmd, string args, bool hidden = false)
        {
            ProcessStartInfo psi = new ProcessStartInfo(cmd, args);
            psi.UseShellExecute = false; 
            psi.CreateNoWindow = true; 
            psi.Verb = "runas"; 
            try { Process.Start(psi); } catch {}
        }

        private void AddNativeDrag(Control c)
        {
            c.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };
        }

        [STAThread]
        static void Main() { Application.EnableVisualStyles(); Application.Run(new WormholeForm()); }
    }

    // --- CUSTOM NEUMORPHIC BUTTON CLASS ---
    public class NeuButton : Control
    {
        private bool isPressed = false;
        
        // Configuration
        private int borderRadius = 20;
        private int shadowOffset = 4; 
        
        // Colors - Tuned for Mica Dark (#2e3238)
        private Color baseColor = ColorTranslator.FromHtml("#2e3238"); 
        private Color textColor = ColorTranslator.FromHtml("#e0e5ec");
        
        // We use slightly lighter/darker variations for the shadows
        private Color shadowDark = Color.FromArgb(20, 23, 27); // Darker than base
        private Color shadowLight = Color.FromArgb(60, 65, 75); // Lighter than base

        public NeuButton()
        {
            // Enable double buffering to stop flickering
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = Color.Transparent; // Important for rounded corners
            this.ForeColor = textColor;
            this.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            this.Cursor = Cursors.Hand;
            this.Size = new Size(200, 50);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            isPressed = true;
            this.Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            isPressed = false;
            this.Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Define the main button shape
            // We shrink the rectangle slightly so shadows don't clip at the edges
            Rectangle rect = new Rectangle(5, 5, this.Width - 10, this.Height - 10);
            
            // If pressed, we physically shift the button down-right by 2px
            if (isPressed)
            {
                rect.Offset(2, 2);
            }

            using (GraphicsPath path = GetRoundedPath(rect, borderRadius))
            {
                // 1. DRAW SHADOWS (Only if NOT pressed)
                if (!isPressed)
                {
                    // Dark Shadow (Bottom-Right) - "Fake Blur" loop
                    for (int i = 0; i < 4; i++)
                    {
                        using (Pen p = new Pen(Color.FromArgb(40 - (i * 10), 0, 0, 0), i + 2))
                        {
                            g.DrawPath(p, GetShiftedPath(rect, shadowOffset, shadowOffset, borderRadius));
                        }
                    }

                    // Light Highlight (Top-Left)
                    for (int i = 0; i < 4; i++)
                    {
                        using (Pen p = new Pen(Color.FromArgb(30 - (i * 5), 255, 255, 255), i + 2))
                        {
                            g.DrawPath(p, GetShiftedPath(rect, -shadowOffset / 2, -shadowOffset / 2, borderRadius));
                        }
                    }
                }

                // 2. FILL BUTTON SURFACE
                // Subtle gradient to enhance 3D feel
                using (LinearGradientBrush brush = new LinearGradientBrush(rect, 
                    isPressed ? shadowDark : ControlPaint.Light(baseColor, 0.05f), 
                    isPressed ? shadowDark : baseColor, 
                    45f))
                {
                    g.FillPath(brush, path);
                }

                // 3. DRAW TEXT
                // Centered locked text
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(this.Text, this.Font, new SolidBrush(this.ForeColor), rect, sf);
                }
            }
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private GraphicsPath GetShiftedPath(Rectangle rect, int offX, int offY, int radius)
        {
            Rectangle newRect = rect;
            newRect.Offset(offX, offY);
            return GetRoundedPath(newRect, radius);
        }
    }
}
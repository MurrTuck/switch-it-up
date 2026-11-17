// The "Switch it up" window: light/dark theme that can follow Windows, gradient switch button.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MonitorSwitch
{
    // Current colors. Apply() swaps between the light and dark sets.
    static class Theme
    {
        public static bool IsDark = true;
        public static Color Back, Field, Button, Border, Text, Muted, Good, Bad;
        public static readonly Color AccentA = Color.FromArgb(124, 92, 255);  // violet
        public static readonly Color AccentB = Color.FromArgb(0, 194, 224);   // cyan

        static Theme() { Apply(true); }

        public static void Apply(bool dark)
        {
            IsDark = dark;
            if (dark)
            {
                Back = Color.FromArgb(22, 24, 29);
                Field = Color.FromArgb(38, 42, 51);
                Button = Color.FromArgb(38, 42, 51);
                Border = Color.FromArgb(62, 67, 80);
                Text = Color.FromArgb(240, 242, 247);
                Muted = Color.FromArgb(139, 146, 160);
                Good = Color.FromArgb(74, 222, 128);
                Bad = Color.FromArgb(255, 107, 107);
            }
            else
            {
                Back = Color.FromArgb(244, 245, 249);
                Field = Color.White;
                Button = Color.FromArgb(226, 229, 237);
                Border = Color.FromArgb(205, 210, 221);
                Text = Color.FromArgb(24, 27, 33);
                Muted = Color.FromArgb(98, 106, 120);
                Good = Color.FromArgb(22, 163, 74);
                Bad = Color.FromArgb(214, 40, 40);
            }
        }

        // True when Windows is set to dark mode for apps (Settings > Personalization > Colors).
        public static bool WindowsUsesDark()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = key == null ? null : key.GetValue("AppsUseLightTheme");
                    if (v is int) return (int)v == 0;
                }
            }
            catch { }
            return false; // Windows' own default is light
        }
    }

    // A flat button with rounded corners and a left-to-right gradient fill.
    class RoundedButton : Button
    {
        public Color FillA = Theme.Field;
        public Color FillB = Theme.Field;
        public int Radius = 10;
        bool hover, down;

        public RoundedButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        Color Shade(Color c)
        {
            if (down) return Blend(c, Color.Black, 0.15f);
            if (hover) return Theme.IsDark ? Blend(c, Color.White, 0.12f) : Blend(c, Color.Black, 0.07f);
            return c;
        }

        static Color Blend(Color c, Color with, float amount)
        {
            return Color.FromArgb(
                (int)(c.R + (with.R - c.R) * amount),
                (int)(c.G + (with.G - c.G) * amount),
                (int)(c.B + (with.B - c.B) * amount));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Back);
            if (Width < 4 || Height < 4) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            int d = Math.Min((int)(Radius * 2 * g.DpiX / 96f), Math.Min(r.Width, r.Height));

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                using (LinearGradientBrush fill = new LinearGradientBrush(new Rectangle(0, 0, Width, Height), Shade(FillA), Shade(FillB), 0f))
                    g.FillPath(fill, path);
            }

            using (StringFormat sf = new StringFormat())
            using (SolidBrush text = new SolidBrush(ForeColor))
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(Text, Font, text, new RectangleF(0, 0, Width, Height), sf);
            }
        }
    }

    // A combo box whose border and drop-down arrow are repainted to match the current theme.
    class DarkComboBox : ComboBox
    {
        public DarkComboBox()
        {
            FlatStyle = FlatStyle.Flat;
            BackColor = Theme.Field;
            ForeColor = Theme.Text;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x000F) return; // WM_PAINT
            using (Graphics g = Graphics.FromHwnd(Handle))
            {
                int bw = SystemInformation.VerticalScrollBarWidth;
                Rectangle button = new Rectangle(Width - bw - 1, 1, bw, Height - 2);
                using (SolidBrush fill = new SolidBrush(Theme.Field)) g.FillRectangle(fill, button);
                using (Pen border = new Pen(Theme.Border)) g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

                g.SmoothingMode = SmoothingMode.AntiAlias;
                float cx = button.X + button.Width / 2f, cy = button.Y + button.Height / 2f;
                float s = button.Width / 4.5f;
                using (Pen chevron = new Pen(Theme.Muted, Math.Max(1.5f, s / 2.5f)))
                    g.DrawLines(chevron, new PointF[] { new PointF(cx - s, cy - s / 2), new PointF(cx, cy + s / 2), new PointF(cx + s, cy - s / 2) });
            }
        }
    }

    static class Hotkey
    {
        public const int ModAlt = 1, ModControl = 2, ModShift = 4;

        public static string Describe(int mods, int key)
        {
            if (key == 0) return "None";
            string s = "";
            if ((mods & ModControl) != 0) s += "Ctrl + ";
            if ((mods & ModAlt) != 0) s += "Alt + ";
            if ((mods & ModShift) != 0) s += "Shift + ";
            string name = ((Keys)key).ToString();
            if (name.Length == 2 && name[0] == 'D' && char.IsDigit(name[1])) name = name.Substring(1);
            return s + name;
        }
    }

    // Click it, then press a key combination to record it. Backspace or Delete clears it.
    class HotkeyBox : TextBox
    {
        public int Mods, Key;
        public event EventHandler HotkeyChanged;

        public HotkeyBox()
        {
            ReadOnly = true;
            BorderStyle = BorderStyle.FixedSingle;
            BackColor = Theme.Field;
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
            ShortcutsEnabled = false;
        }

        public void SetHotkey(int mods, int key)
        {
            Mods = mods;
            Key = key;
            Text = Hotkey.Describe(mods, key);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Text = "Press keys..."; }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Text = Hotkey.Describe(Mods, Key); }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys code = keyData & Keys.KeyCode;
            Keys mods = keyData & Keys.Modifiers;

            if (code == Keys.Tab && mods == Keys.None) return base.ProcessCmdKey(ref msg, keyData);
            if (code == Keys.ControlKey || code == Keys.Menu || code == Keys.ShiftKey ||
                code == Keys.LWin || code == Keys.RWin) return true; // wait for the real key

            if (mods == Keys.None && (code == Keys.Back || code == Keys.Delete))
            {
                Mods = 0; Key = 0;
            }
            else if ((mods & (Keys.Control | Keys.Alt)) == 0)
            {
                Text = "Use Ctrl or Alt with a key";
                return true;
            }
            else
            {
                Mods = ((mods & Keys.Control) != 0 ? Hotkey.ModControl : 0) |
                       ((mods & Keys.Alt) != 0 ? Hotkey.ModAlt : 0) |
                       ((mods & Keys.Shift) != 0 ? Hotkey.ModShift : 0);
                Key = (int)code;
            }
            Text = Hotkey.Describe(Mods, Key);
            if (HotkeyChanged != null) HotkeyChanged(this, EventArgs.Empty);
            if (Parent != null) Parent.SelectNextControl(this, true, true, true, true);
            return true;
        }
    }

    class MainForm : Form
    {
        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint key);
        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        const int HotkeyId = 1;
        const int WM_HOTKEY = 0x0312;
        HotkeyBox hotkeyBox;
        bool hotkeyRegistered;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        Settings settings;
        ComboBox monitorBox, inputABox, inputBBox;
        RoundedButton switchButton, refreshButton;
        Label statusDot, statusLabel;
        CheckBox topMostBox;
        ComboBox appearanceBox;
        int statusKind; // 0 good, 1 problem, 2 neutral (kept so colors can be redone on theme change)
        bool loading;
        static readonly string[] AppearanceValues = { "system", "light", "dark" };
        int lastKnownInput = -2;

        public MainForm()
        {
            settings = Settings.Load();

            Text = "Switch it up";
            Font = new Font("Segoe UI", 10f);
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            SuspendLayout();

            Label title = new Label();
            title.Text = "Switch it up";
            title.Font = new Font("Segoe UI Semibold", 20f);
            title.AutoSize = true;
            title.Location = new Point(18, 16);

            Label subtitle = new Label();
            subtitle.Text = "Flip a monitor between two inputs with one click.";
            subtitle.Tag = "muted";
            subtitle.AutoSize = true;
            subtitle.Location = new Point(24, 58);

            Label monitorLabel = MakeCaption("MONITOR", 24, 98);
            monitorBox = new DarkComboBox();
            monitorBox.DropDownStyle = ComboBoxStyle.DropDownList;
            monitorBox.SetBounds(24, 118, 298, 26);
            monitorBox.SelectedIndexChanged += delegate { if (!loading) { SaveFromUi(); ShowCurrent(); } };

            refreshButton = new RoundedButton();
            refreshButton.Text = "Refresh";
            refreshButton.Radius = 6;
            refreshButton.SetBounds(332, 117, 84, 28);
            refreshButton.Click += delegate { LoadMonitors(); ShowCurrent(); };

            Label aLabel = MakeCaption("INPUT A", 24, 162);
            inputABox = MakeInputBox(24, 182);
            Label bLabel = MakeCaption("INPUT B", 228, 162);
            inputBBox = MakeInputBox(228, 182);

            switchButton = new RoundedButton();
            switchButton.Text = "Switch Input";
            switchButton.Font = new Font("Segoe UI Semibold", 18f);
            switchButton.ForeColor = Color.White;
            switchButton.FillA = Theme.AccentA;
            switchButton.FillB = Theme.AccentB;
            switchButton.Radius = 14;
            switchButton.SetBounds(24, 292, 392, 84);
            switchButton.Click += delegate { DoSwitch(); };

            Label hotkeyLabel = MakeCaption("KEYBOARD SHORTCUT", 24, 226);
            hotkeyBox = new HotkeyBox();
            hotkeyBox.SetBounds(24, 246, 188, 26);
            hotkeyBox.SetHotkey(settings.HotkeyMods, settings.HotkeyKey);
            hotkeyBox.GotFocus += delegate { UnregisterShortcut(); };
            hotkeyBox.LostFocus += delegate { RegisterShortcut(true); };
            hotkeyBox.HotkeyChanged += delegate
            {
                settings.HotkeyMods = hotkeyBox.Mods;
                settings.HotkeyKey = hotkeyBox.Key;
                settings.Save();
            };

            Label hotkeyHint = new Label();
            hotkeyHint.Text = "Click the box, then press new keys.\nWorks while the app is open.";
            hotkeyHint.Tag = "muted";
            hotkeyHint.Font = new Font("Segoe UI", 8.5f);
            hotkeyHint.SetBounds(226, 242, 196, 36);

            statusDot = new Label();
            statusDot.Text = "●";
            statusDot.SetBounds(24, 390, 18, 40);

            statusLabel = new Label();
            statusLabel.SetBounds(44, 390, 372, 40);

            topMostBox = new CheckBox();
            topMostBox.Text = "Keep this window on top";
            topMostBox.Tag = "muted";
            topMostBox.SetBounds(26, 436, 220, 24);
            topMostBox.CheckedChanged += delegate { TopMost = topMostBox.Checked; if (!loading) SaveFromUi(); };

            Label appearanceLabel = new Label();
            appearanceLabel.Text = "Theme";
            appearanceLabel.Tag = "muted";
            appearanceLabel.TextAlign = ContentAlignment.MiddleRight;
            appearanceLabel.SetBounds(236, 434, 56, 26);
            appearanceBox = new DarkComboBox();
            appearanceBox.DropDownStyle = ComboBoxStyle.DropDownList;
            appearanceBox.Items.AddRange(new object[] { "Match Windows", "Light", "Dark" });
            appearanceBox.SetBounds(296, 434, 120, 26);
            appearanceBox.SelectedIndexChanged += delegate
            {
                if (loading || appearanceBox.SelectedIndex < 0) return;
                settings.Appearance = AppearanceValues[appearanceBox.SelectedIndex];
                settings.Save();
                ApplyTheme();
            };

            Controls.AddRange(new Control[] { title, subtitle, monitorLabel, monitorBox, refreshButton, aLabel, inputABox,
                                              bLabel, inputBBox, hotkeyLabel, hotkeyBox, hotkeyHint, switchButton,
                                              statusDot, statusLabel, topMostBox, appearanceLabel, appearanceBox });
            AcceptButton = switchButton;

            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(440, 472);
            ResumeLayout(false);

            loading = true;
            inputABox.Text = Inputs.Label(settings.InputA);
            inputBBox.Text = Inputs.Label(settings.InputB);
            topMostBox.Checked = settings.TopMost;
            int ai = Array.IndexOf(AppearanceValues, settings.Appearance);
            appearanceBox.SelectedIndex = ai < 0 ? 0 : ai;
            loading = false;

            ApplyTheme();
            LoadMonitors();
            ShowCurrent();
            ActiveControl = switchButton;

            Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnWindowsSettingChanged;
        }

        // Windows raises this when the light/dark setting (among others) changes.
        void OnWindowsSettingChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if (e.Category != Microsoft.Win32.UserPreferenceCategory.General) return;
            if (settings.Appearance != "system" || IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new MethodInvoker(ApplyTheme));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnWindowsSettingChanged;
            base.OnFormClosed(e);
        }

        void ApplyTheme()
        {
            bool dark = settings.Appearance == "dark" ||
                        (settings.Appearance != "light" && Theme.WindowsUsesDark());
            Theme.Apply(dark);

            SuspendLayout();
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            foreach (Control c in Controls)
            {
                if (c == switchButton) continue; // the gradient button looks the same in both
                if (c is RoundedButton)
                {
                    RoundedButton b = (RoundedButton)c;
                    b.FillA = b.FillB = Theme.Button;
                    b.ForeColor = Theme.Text;
                }
                else if (c is ComboBox || c is TextBox)
                {
                    c.BackColor = Theme.Field;
                    c.ForeColor = Theme.Text;
                }
                else if ("muted".Equals(c.Tag))
                {
                    c.ForeColor = Theme.Muted;
                }
                else if (c != statusDot && c != statusLabel)
                {
                    c.ForeColor = Theme.Text;
                }
            }
            ColorStatus();
            ResumeLayout();
            ApplyTitleBar();
            Invalidate(true);
        }

        void ApplyTitleBar()
        {
            if (!IsHandleCreated) return;
            try
            {
                int on = Theme.IsDark ? 1 : 0;
                DwmSetWindowAttribute(Handle, 20, ref on, 4); // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE
                // Nudge Windows to redraw the title bar now rather than on the next focus change.
                SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020);
            }
            catch { }
        }

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyTitleBar();
            RegisterShortcut(false);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterShortcut();
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && (int)m.WParam == HotkeyId) DoSwitch();
            base.WndProc(ref m);
        }

        void RegisterShortcut(bool report)
        {
            UnregisterShortcut();
            if (settings.HotkeyKey == 0)
            {
                if (report) SetStatus("Keyboard shortcut turned off.", Theme.Muted);
                return;
            }
            // 0x4000 = MOD_NOREPEAT: holding the keys down switches only once
            hotkeyRegistered = RegisterHotKey(Handle, HotkeyId, (uint)settings.HotkeyMods | 0x4000, (uint)settings.HotkeyKey);
            string name = Hotkey.Describe(settings.HotkeyMods, settings.HotkeyKey);
            if (!hotkeyRegistered)
                SetStatus(name + " is already used by another program. Pick a different shortcut.", Theme.Bad);
            else if (report)
                SetStatus("Press " + name + " to switch.", Theme.Good);
        }

        void UnregisterShortcut()
        {
            if (hotkeyRegistered && IsHandleCreated) UnregisterHotKey(Handle, HotkeyId);
            hotkeyRegistered = false;
        }

        // If this window is on the monitor about to be switched, move it to the nearest other
        // screen so the button stays visible.
        void MoveOffSwitchedMonitor()
        {
            MonitorInfo target = monitorBox.SelectedItem as MonitorInfo;
            if (target == null || WindowState != FormWindowState.Normal) return;
            if (Screen.FromControl(this).Bounds != target.Bounds) return;

            Point here = new Point(Left + Width / 2, Top + Height / 2);
            Screen best = null;
            double bestDist = double.MaxValue;
            foreach (Screen s in Screen.AllScreens)
            {
                if (s.Bounds == target.Bounds) continue;
                Rectangle b = s.Bounds;
                double dx = b.Left + b.Width / 2 - here.X, dy = b.Top + b.Height / 2 - here.Y;
                double dist = dx * dx + dy * dy;
                if (dist < bestDist) { bestDist = dist; best = s; }
            }
            if (best == null) return; // no other screen to go to

            Rectangle wa = best.WorkingArea;
            Location = new Point(wa.Left + Math.Max(0, (wa.Width - Width) / 2),
                                 wa.Top + Math.Max(0, (wa.Height - Height) / 2));
        }

        Label MakeCaption(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            l.Tag = "muted";
            l.AutoSize = true;
            l.Location = new Point(x, y);
            return l;
        }

        ComboBox MakeInputBox(int x, int y)
        {
            ComboBox box = new DarkComboBox();
            box.DropDownStyle = ComboBoxStyle.DropDown; // editable, for monitors with non-standard values
            box.SetBounds(x, y, 188, 26);
            foreach (int v in Inputs.Values) box.Items.Add(Inputs.Label(v));
            box.SelectedIndexChanged += delegate { if (!loading) { SaveFromUi(); UpdateButton(); } };
            box.Leave += delegate { if (!loading) { SaveFromUi(); UpdateButton(); } };
            return box;
        }

        void LoadMonitors()
        {
            loading = true;
            monitorBox.Items.Clear();
            List<MonitorInfo> monitors = Ddc.List();
            int select = -1;
            for (int i = 0; i < monitors.Count; i++)
            {
                monitorBox.Items.Add(monitors[i]);
                if (string.Equals(monitors[i].Key, settings.MonitorKey, StringComparison.OrdinalIgnoreCase)) select = i;
            }
            // Only auto-pick when there's nothing remembered, so a monitor that is
            // temporarily missing doesn't get replaced by a different one.
            if (select < 0 && monitors.Count > 0 && string.IsNullOrEmpty(settings.MonitorKey))
            {
                select = 0;
                settings.MonitorKey = monitors[0].Key;
                settings.Save();
            }
            monitorBox.SelectedIndex = select;
            loading = false;
        }

        void SaveFromUi()
        {
            MonitorInfo m = monitorBox.SelectedItem as MonitorInfo;
            if (m != null) settings.MonitorKey = m.Key;
            int v;
            if (Inputs.TryParse(inputABox.Text, out v)) settings.InputA = v;
            if (Inputs.TryParse(inputBBox.Text, out v)) settings.InputB = v;
            settings.TopMost = topMostBox.Checked;
            settings.Save();
        }

        // Labels the big button with where the next press will go.
        void UpdateButton()
        {
            int a = settings.InputA, b = settings.InputB;
            if (lastKnownInput == -2 || a == b) { switchButton.Text = "Switch Input"; return; }
            int next;
            if (lastKnownInput == -1) next = (settings.LastSet == b) ? a : b;
            else next = (lastKnownInput == a) ? b : a;
            switchButton.Text = "Switch to " + Inputs.ShortName(next);
        }

        void ShowCurrent()
        {
            if (monitorBox.SelectedItem == null)
            {
                lastKnownInput = -2;
                SetStatus(monitorBox.Items.Count == 0
                    ? "No monitors found."
                    : "Your saved monitor isn't visible right now. Pick one or press Refresh.", Theme.Bad);
            }
            else
            {
                lastKnownInput = Ddc.GetInput(settings.MonitorKey);
                if (lastKnownInput >= 0) SetStatus("Currently on " + Inputs.ShortName(lastKnownInput), Theme.Good);
                else if (lastKnownInput == -1) SetStatus("This monitor isn't answering. Check that DDC/CI is turned on in its menu.", Theme.Bad);
                else SetStatus("That monitor isn't visible to Windows right now.", Theme.Bad);
            }
            UpdateButton();
        }

        void DoSwitch()
        {
            SaveFromUi();
            if (settings.InputA == settings.InputB)
            {
                SetStatus("Input A and Input B are the same.", Theme.Bad);
                return;
            }
            MoveOffSwitchedMonitor();
            Cursor = Cursors.WaitCursor;
            bool ok;
            string msg = Switcher.Toggle(settings, out ok);
            Cursor = Cursors.Default;
            if (ok) lastKnownInput = settings.LastSet;
            SetStatus(msg, ok ? Theme.Good : Theme.Bad);
            UpdateButton();
        }

        void SetStatus(string text, Color color)
        {
            statusKind = color == Theme.Bad ? 1 : color == Theme.Good ? 0 : 2;
            statusLabel.Text = text;
            ColorStatus();
        }

        void ColorStatus()
        {
            Color c = statusKind == 0 ? Theme.Good : statusKind == 1 ? Theme.Bad : Theme.Muted;
            statusDot.ForeColor = c;
            statusLabel.ForeColor = statusKind == 1 ? Theme.Bad : Theme.Text;
        }
    }
}

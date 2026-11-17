// The "Switch it up" window: dark theme, gradient switch button.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MonitorSwitch
{
    static class Theme
    {
        public static readonly Color Back = Color.FromArgb(22, 24, 29);
        public static readonly Color Field = Color.FromArgb(38, 42, 51);
        public static readonly Color Border = Color.FromArgb(62, 67, 80);
        public static readonly Color Text =Color.FromArgb(240, 242, 247);
        public static readonly Color Muted = Color.FromArgb(139, 146, 160);
        public static readonly Color AccentA = Color.FromArgb(124, 92, 255);  // violet
        public static readonly Color AccentB = Color.FromArgb(0, 194, 224);   // cyan
        public static readonly Color Good = Color.FromArgb(74, 222, 128);
        public static readonly Color Bad = Color.FromArgb(255, 107, 107);
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
            if (hover) return Blend(c, Color.White, 0.12f);
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

    // A combo box whose border and drop-down arrow are repainted to match the dark theme.
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

    class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        Settings settings;
        ComboBox monitorBox, inputABox, inputBBox;
        RoundedButton switchButton, refreshButton;
        Label statusDot, statusLabel;
        CheckBox topMostBox;
        bool loading;
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
            subtitle.ForeColor = Theme.Muted;
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
            switchButton.SetBounds(24, 232, 392, 84);
            switchButton.Click += delegate { DoSwitch(); };

            statusDot = new Label();
            statusDot.Text = "●";
            statusDot.SetBounds(24, 330, 18, 40);

            statusLabel = new Label();
            statusLabel.SetBounds(44, 330, 372, 40);

            topMostBox = new CheckBox();
            topMostBox.Text = "Keep this window on top";
            topMostBox.ForeColor = Theme.Muted;
            topMostBox.SetBounds(26, 374, 300, 24);
            topMostBox.CheckedChanged += delegate { TopMost = topMostBox.Checked; if (!loading) SaveFromUi(); };

            Controls.AddRange(new Control[] { title, subtitle, monitorLabel, monitorBox, refreshButton, aLabel, inputABox,
                                              bLabel, inputBBox, switchButton, statusDot, statusLabel, topMostBox });
            AcceptButton = switchButton;

            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(440, 412);
            ResumeLayout(false);

            loading = true;
            inputABox.Text = Inputs.Label(settings.InputA);
            inputBBox.Text = Inputs.Label(settings.InputB);
            topMostBox.Checked = settings.TopMost;
            loading = false;

            LoadMonitors();
            ShowCurrent();
            ActiveControl = switchButton;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int on = 1;
                DwmSetWindowAttribute(Handle, 20, ref on, 4); // dark title bar
            }
            catch { }
        }

        Label MakeCaption(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            l.ForeColor = Theme.Muted;
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
            statusDot.ForeColor = color;
            statusLabel.ForeColor = color == Theme.Bad ? Theme.Bad : Theme.Text;
            statusLabel.Text = text;
        }
    }
}

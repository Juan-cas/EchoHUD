// SPDX-License-Identifier: GPL-3.0-only
//
// Theme.cs - colours, fonts, icons and the small custom controls of the UI.
//
// The look is a compact dark panel. Every size in the UI code is written in
// "design pixels" (as if the screen were 96 DPI) and converted with
// Theme.Px(), so the panel stays the same physical size on high-DPI screens.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EchoHud
{
    internal static class Theme
    {
        internal static readonly Color Background = Color.FromArgb(20, 24, 31);
        internal static readonly Color Surface = Color.FromArgb(29, 35, 45);
        internal static readonly Color Raised = Color.FromArgb(41, 49, 62);
        internal static readonly Color Hover = Color.FromArgb(54, 64, 80);
        internal static readonly Color TextColor = Color.FromArgb(232, 237, 242);
        internal static readonly Color Muted = Color.FromArgb(147, 160, 176);
        internal static readonly Color Accent = Color.FromArgb(45, 212, 191);
        internal static readonly Color OnAccent = Color.FromArgb(6, 35, 31);
        internal static readonly Color Warning = Color.FromArgb(255, 198, 100);
        internal static readonly Color Highlight = Color.FromArgb(255, 176, 32);

        /// <summary>Colours a timer can be drawn in; the index is saved as CooldownTimer.Colour.</summary>
        internal static readonly Color[] TimerColours =
        {
            Accent,
            Color.FromArgb(255, 176, 32),
            Color.FromArgb(248, 113, 113),
            Color.FromArgb(96, 165, 250),
            Color.FromArgb(192, 132, 252)
        };

        static float scale = 1f;
        static bool iconFontAvailable;

        internal static Font Body { get; private set; }
        internal static Font Bold { get; private set; }
        internal static Font Small { get; private set; }
        internal static Font Icons { get; private set; }

        /// <summary>Call once at startup, after DPI awareness has been set.</summary>
        internal static void Initialize()
        {
            using (Graphics desktop = Graphics.FromHwnd(IntPtr.Zero)) scale = desktop.DpiX / 96f;
            Body = new Font("Segoe UI", 9.5f);
            Bold = new Font("Segoe UI Semibold", 10f);
            Small = new Font("Segoe UI", 8.25f);
            // Icon font shipped with Windows 10 and 11. If it is missing,
            // Windows substitutes another font and we fall back to letters.
            Icons = new Font("Segoe MDL2 Assets", 10.5f);
            iconFontAvailable = Icons.Name == "Segoe MDL2 Assets";
            if (!iconFontAvailable) Icons = Bold;
        }

        /// <summary>Converts design pixels (96 DPI) to real pixels on this machine.</summary>
        internal static int Px(int designPixels)
        {
            return (int)Math.Round(designPixels * scale);
        }

        /// <summary>Returns the icon-font glyph, or a plain-text stand-in when the font is missing.</summary>
        internal static string Icon(string glyph, string fallback)
        {
            return iconFontAvailable ? glyph : fallback;
        }

        // Glyph codes from "Segoe MDL2 Assets".
        internal static string IconAdd { get { return Icon("", "+"); } }
        internal static string IconShow { get { return Icon("", "o"); } }
        internal static string IconHide { get { return Icon("", "-"); } }
        internal static string IconLock { get { return Icon("", "L"); } }
        internal static string IconUnlock { get { return Icon("", "U"); } }
        internal static string IconDelete { get { return Icon("", "x"); } }
        internal static string IconRename { get { return Icon("", "ab"); } }
        internal static string IconCrop { get { return Icon("", "[]"); } }
        internal static string IconPlay { get { return Icon("\uE768", ">"); } }
        internal static string IconClock { get { return Icon("\uE823", "t"); } }
        internal static string IconKeyboard { get { return Icon("\uE765", "k"); } }
        internal static string IconSettings { get { return Icon("\uE713", "="); } }
        internal static string IconTimer { get { return Icon("\uE916", "T"); } }
    }

    /// <summary>A small square flat button showing one icon. Always give it a tooltip.</summary>
    internal sealed class IconButton : Button
    {
        bool active;

        internal IconButton(string glyph, string accessibleName, EventHandler click)
        {
            Text = glyph;
            Font = Theme.Icons;
            AccessibleName = accessibleName;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Theme.Hover;
            FlatAppearance.MouseDownBackColor = Theme.Hover;
            BackColor = Theme.Raised;
            ForeColor = Theme.TextColor;
            Size = new Size(Theme.Px(30), Theme.Px(30));
            TabStop = true;
            if (click != null) Click += click;
        }

        /// <summary>Highlighted state, e.g. "locked" or "hidden".</summary>
        internal bool Active
        {
            get { return active; }
            set
            {
                active = value;
                ForeColor = value ? Theme.Accent : Theme.TextColor;
            }
        }
    }

    /// <summary>A minimal horizontal slider that matches the dark theme.</summary>
    internal sealed class Slider : Control
    {
        int minimum, maximum = 100, current, step = 5;

        internal event EventHandler ValueChanged;

        internal Slider(int minimum, int maximum, int step, string accessibleName)
        {
            this.minimum = minimum;
            this.maximum = maximum;
            this.step = step;
            current = minimum;
            AccessibleName = accessibleName;
            AccessibleRole = AccessibleRole.Slider;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Height = Theme.Px(22);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        internal int Value
        {
            get { return current; }
            set
            {
                int next = Geometry.Clamp(value, minimum, maximum);
                if (next == current) return;
                current = next;
                Invalidate();
            }
        }

        /// <summary>Changes the value because the USER asked, and tells listeners.</summary>
        void SetFromUser(int value)
        {
            int before = current;
            Value = value;
            if (current != before && ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        int Knob { get { return Theme.Px(12); } }

        void SetFromMouse(int x)
        {
            int track = Math.Max(1, Width - Knob);
            double fraction = Math.Max(0, Math.Min(1, (x - Knob / 2.0) / track));
            int raw = minimum + (int)Math.Round(fraction * (maximum - minimum));
            SetFromUser(minimum + (int)Math.Round((raw - minimum) / (double)step) * step);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            SetFromMouse(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Button == MouseButtons.Left) SetFromMouse(e.X);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left) SetFromUser(current - step);
            else if (e.KeyCode == Keys.Right) SetFromUser(current + step);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent == null ? Theme.Surface : Parent.BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int knob = Knob, middle = Height / 2, thickness = Theme.Px(4);
            int track = Math.Max(1, Width - knob);
            int position = knob / 2 + (int)((long)(current - minimum) * track / Math.Max(1, maximum - minimum));
            using (Brush rail = new SolidBrush(Theme.Raised))
                g.FillRectangle(rail, knob / 2, middle - thickness / 2, track, thickness);
            using (Brush filled = new SolidBrush(Theme.Accent))
            {
                g.FillRectangle(filled, knob / 2, middle - thickness / 2, position - knob / 2, thickness);
                g.FillEllipse(filled, position - knob / 2, middle - knob / 2, knob, knob);
            }
            if (Focused)
                using (Pen ring = new Pen(Theme.TextColor, 1f))
                    g.DrawEllipse(ring, position - knob / 2 - 2, middle - knob / 2 - 2, knob + 3, knob + 3);
        }
    }

    /// <summary>Dark colours for right-click menus.</summary>
    internal sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Surface; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Surface; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Surface; } }
        public override Color MenuBorder { get { return Theme.Raised; } }
        public override Color MenuItemBorder { get { return Theme.Hover; } }
        public override Color MenuItemSelected { get { return Theme.Hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Hover; } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.Hover; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.Hover; } }
        public override Color SeparatorDark { get { return Theme.Raised; } }
        public override Color SeparatorLight { get { return Theme.Raised; } }
        public override Color CheckBackground { get { return Theme.Accent; } }
        public override Color CheckSelectedBackground { get { return Theme.Accent; } }
        public override Color CheckPressedBackground { get { return Theme.Accent; } }
        public override Color ButtonSelectedBorder { get { return Theme.Accent; } }
    }

    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        internal DarkMenuRenderer() : base(new DarkMenuColors()) { }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.TextColor;
            base.OnRenderArrow(e);
        }
    }

    internal static class Dialogs
    {
        internal static ContextMenuStrip DarkMenu()
        {
            return new ContextMenuStrip
            {
                Renderer = new DarkMenuRenderer(),
                ForeColor = Theme.TextColor,
                BackColor = Theme.Surface,
                Font = Theme.Body
            };
        }

        /// <summary>Gives a sub-menu the same dark look as the menu it belongs to.</summary>
        internal static ToolStripMenuItem Submenu(ContextMenuStrip strip, string text)
        {
            ToolStripMenuItem parent = new ToolStripMenuItem(text);
            parent.DropDown.BackColor = Theme.Surface;
            parent.DropDown.ForeColor = Theme.TextColor;
            parent.DropDown.Renderer = strip.Renderer;
            return parent;
        }

        /// <summary>Asks the user for one short line of text. Returns null if cancelled.</summary>
        internal static string AskText(IWin32Window owner, string title, string label, string initial)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = title;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.BackColor = Theme.Background;
                dialog.ForeColor = Theme.TextColor;
                dialog.Font = Theme.Body;
                dialog.ClientSize = new Size(Theme.Px(320), Theme.Px(126));

                Label caption = new Label { Text = label, AutoSize = false, Bounds = new Rectangle(Theme.Px(14), Theme.Px(12), Theme.Px(292), Theme.Px(22)) };
                TextBox input = new TextBox
                {
                    Text = initial ?? "",
                    MaxLength = 40,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Theme.Raised,
                    ForeColor = Theme.TextColor,
                    Bounds = new Rectangle(Theme.Px(14), Theme.Px(40), Theme.Px(292), Theme.Px(26)),
                    AccessibleName = label
                };
                Button accept = TextButton(Strings.Get("common.ok"), true);
                accept.Location = new Point(Theme.Px(118), Theme.Px(82));
                accept.DialogResult = DialogResult.OK;
                Button cancel = TextButton(Strings.Get("common.cancel"), false);
                cancel.Location = new Point(Theme.Px(214), Theme.Px(82));
                cancel.DialogResult = DialogResult.Cancel;

                dialog.Controls.AddRange(new Control[] { caption, input, accept, cancel });
                dialog.AcceptButton = accept;
                dialog.CancelButton = cancel;
                return dialog.ShowDialog(owner) == DialogResult.OK ? input.Text.Trim() : null;
            }
        }

        /// <summary>A flat text button; "primary" paints it in the accent colour.</summary>
        internal static Button TextButton(string text, bool primary)
        {
            Button button = new Button
            {
                Text = text,
                Font = primary ? Theme.Bold : Theme.Body,
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Theme.Accent : Theme.Raised,
                ForeColor = primary ? Theme.OnAccent : Theme.TextColor,
                Size = new Size(Theme.Px(92), Theme.Px(30)),
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(94, 234, 212) : Theme.Hover;
            return button;
        }
    }
}

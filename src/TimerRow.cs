// SPDX-License-Identifier: GPL-3.0-only
//
// TimerRow.cs - one line in the main panel's list, representing one timer.
//
//   +----------------------------------------------------------+
//   | Potion                  [play][clock][keys][rename][x]   |
//   | 10:00 - Ctrl+F5                                          |
//   +----------------------------------------------------------+
//
// Like RegionRow, it only raises events; the main window does the work. The
// row matters most when timers are locked, because a locked timer lets
// clicks pass through and so cannot be right-clicked.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace EchoHud
{
    internal sealed class TimerRow : Panel
    {
        internal readonly CooldownTimer Spec;

        readonly Label name = new Label(), details = new Label();
        readonly TextBox editor = new TextBox();

        /// <summary>The user renamed the timer through this row.</summary>
        internal event EventHandler Changed;
        internal event EventHandler StartRequested;
        internal event EventHandler DurationRequested;
        internal event EventHandler HotkeyRequested;
        internal event EventHandler DeleteRequested;

        internal TimerRow(CooldownTimer spec, ToolTip tips, int width)
        {
            Spec = spec;
            BackColor = Theme.Surface;
            Size = new Size(width, Theme.Px(54));

            int side = Theme.Px(28), gap = Theme.Px(4), right = width - Theme.Px(8);
            string[] glyphs = { Theme.IconDelete, Theme.IconRename, Theme.IconKeyboard, Theme.IconClock, Theme.IconPlay };
            string[] keys = { "row.delete", "row.rename", "timer.hotkeyTip", "timer.durationTip", "timer.startTip" };
            EventHandler[] actions =
            {
                delegate { Raise(DeleteRequested); },
                delegate { BeginRename(); },
                delegate { Raise(HotkeyRequested); },
                delegate { Raise(DurationRequested); },
                delegate { Raise(StartRequested); }
            };
            for (int i = 0; i < glyphs.Length; i++)
            {
                IconButton button = new IconButton(glyphs[i], Strings.Get(keys[i]), actions[i]);
                button.Size = new Size(side, side);
                button.BackColor = Theme.Surface;
                right -= side;
                button.Location = new Point(right, Theme.Px(13));
                right -= gap;
                tips.SetToolTip(button, Strings.Get(keys[i]));
                Controls.Add(button);
            }

            Rectangle nameArea = new Rectangle(Theme.Px(12), Theme.Px(7), right - Theme.Px(16), Theme.Px(22));
            name.Bounds = nameArea;
            name.Font = Theme.Bold;
            name.ForeColor = Theme.TextColor;
            name.AutoEllipsis = true;
            name.TextAlign = ContentAlignment.MiddleLeft;
            name.DoubleClick += delegate { BeginRename(); };

            details.Bounds = new Rectangle(Theme.Px(12), Theme.Px(29), right - Theme.Px(16), Theme.Px(18));
            details.Font = Theme.Small;
            details.ForeColor = Theme.Muted;
            details.AutoEllipsis = true;

            editor.Bounds = nameArea;
            editor.Font = Theme.Body;
            editor.MaxLength = 60;
            editor.BorderStyle = BorderStyle.FixedSingle;
            editor.BackColor = Theme.Raised;
            editor.ForeColor = Theme.TextColor;
            editor.Visible = false;
            editor.AccessibleName = Strings.Get("row.rename");
            editor.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { EndRename(true); e.Handled = e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Escape) { EndRename(false); e.Handled = e.SuppressKeyPress = true; }
            };
            editor.LostFocus += delegate { EndRename(true); };

            Controls.AddRange(new Control[] { name, details, editor });
            Sync();
        }

        /// <summary>Refreshes the texts from the timer (call after anything else changed it).</summary>
        internal void Sync()
        {
            name.Text = Spec.Name;
            name.ForeColor = Spec.Visible ? Theme.TextColor : Theme.Muted;
            string hotkey = Hotkeys.Describe(Spec.Hotkey);
            details.Text = Durations.Format(Spec.Seconds) + "  ·  " + (hotkey.Length > 0 ? hotkey : Strings.Get("hotkey.none"));
        }

        internal void BeginRename()
        {
            editor.Text = Spec.Name;
            editor.Visible = true;
            editor.BringToFront();
            editor.Focus();
            editor.SelectAll();
        }

        void EndRename(bool keep)
        {
            if (!editor.Visible) return;
            editor.Visible = false;
            string text = editor.Text.Trim();
            if (!keep || text.Length == 0 || text == Spec.Name) return;
            Spec.Name = text;
            Sync();
            Raise(Changed);
        }

        void Raise(EventHandler handler)
        {
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}

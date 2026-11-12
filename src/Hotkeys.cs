// SPDX-License-Identifier: GPL-3.0-only
//
// Hotkeys.cs - key combinations that start a timer, lock, or show/hide.
//
// A hotkey is stored as one number: a System.Windows.Forms.Keys value, which
// is a key code plus optional Ctrl/Alt/Shift bits. This file converts that
// number to what Windows needs (RegisterHotKey) and to readable text, and
// contains the small dialog that asks the user to press a combination.
//
// IMPORTANT BEHAVIOUR TO UNDERSTAND
//   Windows hands a registered combination to this program INSTEAD of the
//   program that has focus. So the hotkey of a timer cannot be the same key
//   that uses the item in the game: the game would never receive it. Making
//   one key do both would require either listening to every keystroke
//   (a keyboard hook) or re-sending the key to the game - and this program
//   does neither, on purpose. See docs/SAFETY.md.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace EchoHud
{
    internal static class Hotkeys
    {
        /// <summary>True for keys that only make sense held together with another key.</summary>
        internal static bool IsModifierOnly(Keys key)
        {
            Keys code = key & Keys.KeyCode;
            return code == Keys.ControlKey || code == Keys.ShiftKey || code == Keys.Menu
                || code == Keys.LControlKey || code == Keys.RControlKey || code == Keys.LShiftKey || code == Keys.RShiftKey
                || code == Keys.LMenu || code == Keys.RMenu || code == Keys.LWin || code == Keys.RWin || code == Keys.None;
        }

        /// <summary>Splits a stored hotkey into the two values RegisterHotKey wants. False if there is no usable hotkey.</summary>
        internal static bool Split(int stored, out uint modifiers, out uint virtualKey)
        {
            Keys keys = (Keys)stored;
            modifiers = 0;
            virtualKey = (uint)(keys & Keys.KeyCode);
            if (stored <= 0 || IsModifierOnly(keys)) return false;
            if ((keys & Keys.Control) != 0) modifiers |= Native.ModControl;
            if ((keys & Keys.Alt) != 0) modifiers |= Native.ModAlt;
            if ((keys & Keys.Shift) != 0) modifiers |= Native.ModShift;
            return true;
        }

        /// <summary>"Ctrl+Shift+F5" style text; empty when there is no hotkey.</summary>
        internal static string Describe(int stored)
        {
            uint modifiers, virtualKey;
            if (!Split(stored, out modifiers, out virtualKey)) return "";
            Keys code = (Keys)virtualKey;
            string name = code.ToString();
            if (code >= Keys.D0 && code <= Keys.D9) name = ((int)(code - Keys.D0)).ToString();
            else if (code >= Keys.NumPad0 && code <= Keys.NumPad9) name = "Num " + (int)(code - Keys.NumPad0);
            return ((modifiers & Native.ModControl) != 0 ? "Ctrl+" : "")
                + ((modifiers & Native.ModAlt) != 0 ? "Alt+" : "")
                + ((modifiers & Native.ModShift) != 0 ? "Shift+" : "")
                + name;
        }

        internal const int Cancelled = -1, Cleared = 0;

        /// <summary>
        /// Asks the user to press a combination. Returns the stored value,
        /// <see cref="Cleared"/> if they removed the hotkey, or
        /// <see cref="Cancelled"/>. Keys are read only while this dialog,
        /// one of our own windows, has focus.
        /// </summary>
        internal static int Ask(IWin32Window owner, int current)
        {
            return Ask(owner, current, Strings.Get("hotkey.title"), Strings.Get("hotkey.explain"));
        }

        /// <summary>The same dialog with its own title and explanation.</summary>
        internal static int Ask(IWin32Window owner, int current, string title, string explanation)
        {
            using (Form dialog = new Form())
            {
                int result = Cancelled;
                dialog.Text = title;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.BackColor = Theme.Background;
                dialog.ForeColor = Theme.TextColor;
                dialog.Font = Theme.Body;
                dialog.ClientSize = new Size(Theme.Px(360), Theme.Px(200));
                dialog.KeyPreview = true;

                Label explain = new Label
                {
                    Text = explanation,
                    ForeColor = Theme.Muted,
                    Bounds = new Rectangle(Theme.Px(14), Theme.Px(10), Theme.Px(332), Theme.Px(92))
                };
                Label shown = new Label
                {
                    Text = current > 0 ? Describe(current) : Strings.Get("hotkey.none"),
                    Font = Theme.Bold,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Theme.Raised,
                    Bounds = new Rectangle(Theme.Px(14), Theme.Px(106), Theme.Px(332), Theme.Px(34))
                };
                Button clear = Dialogs.TextButton(Strings.Get("hotkey.clear"), false);
                clear.Location = new Point(Theme.Px(14), Theme.Px(156));
                clear.TabStop = false;
                clear.Click += delegate { result = Cleared; dialog.Close(); };
                Button cancel = Dialogs.TextButton(Strings.Get("common.cancel"), false);
                cancel.Location = new Point(Theme.Px(254), Theme.Px(156));
                cancel.TabStop = false;
                cancel.Click += delegate { dialog.Close(); };

                dialog.KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    e.Handled = e.SuppressKeyPress = true;
                    if (e.KeyCode == Keys.Escape) { dialog.Close(); return; }
                    if (IsModifierOnly(e.KeyData)) return; // wait for the real key
                    result = (int)e.KeyData;
                    dialog.Close();
                };
                dialog.Controls.AddRange(new Control[] { explain, shown, clear, cancel });
                dialog.ShowDialog(owner);
                return result;
            }
        }
    }
}

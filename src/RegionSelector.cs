// SPDX-License-Identifier: GPL-3.0-only
//
// RegionSelector.cs - "draw a box over what you want to mirror".
//
// When the user clicks Add region, this borderless window appears exactly
// over the source window and shows a dimmed live copy of it. The user drags
// a rectangle; the part inside the rectangle lights up; releasing the mouse
// accepts it. Esc or right-click cancels.
//
// HOW IT IS DRAWN (and why no pixels are read)
//   The window holds TWO DWM thumbnails of the same source:
//     1. the whole source at low opacity over a black background (the dimmed
//        look), and
//     2. only the selected rectangle at full opacity, placed exactly on top
//        (the lit-up selection).
//   Moving the mouse just changes thumbnail 2's rectangle. The compositor
//   does all the drawing, so the preview stays live while selecting.
//
// The mouse clicks land on THIS window, not on the game: nothing is sent to
// the source. Because the window is a copy, it also works when the source is
// partly covered by other windows.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace EchoHud
{
    internal sealed class RegionSelector : Form
    {
        const int MinimumSelection = 4; // pixels; smaller drags count as a stray click

        readonly SourceWindow source;
        readonly Size sourceSize;
        readonly HintWindow hint = new HintWindow();
        IntPtr dimmed, bright;
        Point start;
        bool dragging;
        Rectangle selection;

        /// <summary>
        /// Lets the user pick a rectangle of the source. Returns false if they
        /// cancelled or the source went away. Coordinates are pixels inside
        /// the source's client area, and <paramref name="size"/> is that
        /// client area's size at the moment of selection.
        /// </summary>
        internal static bool Pick(IWin32Window owner, SourceWindow source, out Rectangle crop, out Size size)
        {
            crop = Rectangle.Empty;
            size = Size.Empty;
            if (!Sources.IsUsable(source)) return false;
            Rectangle bounds = Native.ClientScreenBounds(source.Handle);
            if (bounds.Width < 16 || bounds.Height < 16) return false;

            using (RegionSelector selector = new RegionSelector(source, bounds))
            {
                if (selector.ShowDialog(owner) != DialogResult.OK) return false;
                crop = selector.selection;
                size = selector.sourceSize;
                return true;
            }
        }

        RegionSelector(SourceWindow source, Rectangle bounds)
        {
            this.source = source;
            sourceSize = bounds.Size;
            Text = "Echo HUD - " + Strings.Get("selector.title");
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Black;
            Cursor = Cursors.Cross;
            KeyPreview = true;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Registered first = drawn first, so the bright selection ends up on top.
            Native.DwmRegisterThumbnail(Handle, source.Handle, out dimmed);
            Native.DwmRegisterThumbnail(Handle, source.Handle, out bright);
            Place(dimmed, new Rectangle(Point.Empty, sourceSize), 105, true);
            Place(bright, Rectangle.Empty, 255, false);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (dimmed != IntPtr.Zero) Native.DwmUnregisterThumbnail(dimmed);
            if (bright != IntPtr.Zero) Native.DwmUnregisterThumbnail(bright);
            dimmed = bright = IntPtr.Zero;
            base.OnHandleDestroyed(e);
        }

        /// <summary>Draws one part of the source 1:1 at the same place inside this window.</summary>
        static void Place(IntPtr thumbnail, Rectangle part, byte opacity, bool visible)
        {
            if (thumbnail == IntPtr.Zero) return;
            Native.ThumbnailProperties properties = new Native.ThumbnailProperties();
            properties.Flags = Native.TnpAll;
            properties.SourceClientAreaOnly = true;
            properties.Opacity = opacity;
            properties.Visible = visible && part.Width > 0 && part.Height > 0;
            properties.Source = new Native.RECT(part);
            properties.Destination = new Native.RECT(part);
            Native.DwmUpdateThumbnailProperties(thumbnail, ref properties);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            hint.ShowAbove(this, Strings.Get("selector.hint"));
            Activate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right) { Cancel(); return; }
            if (e.Button != MouseButtons.Left) return;
            start = e.Location;
            dragging = true;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            selection = Geometry.FromDrag(start, e.Location, sourceSize);
            Place(bright, selection, 255, true);
            hint.SetText(selection.Width + " × " + selection.Height);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging || e.Button != MouseButtons.Left) return;
            dragging = false;
            Capture = false;
            selection = Geometry.FromDrag(start, e.Location, sourceSize);
            if (selection.Width < MinimumSelection || selection.Height < MinimumSelection)
            {
                // A click without a drag: stay open and let the user try again.
                selection = Rectangle.Empty;
                Place(bright, selection, 255, false);
                hint.SetText(Strings.Get("selector.hint"));
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Cancel();
        }

        // Deliberately NOT cancelled when the window loses focus: Windows reports
        // short focus changes while a dialog opens, which closed the selector
        // before the user could draw anything. Esc and right-click are the exits.
        void Cancel()
        {
            if (IsDisposed || DialogResult != DialogResult.None) return;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) hint.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A small text bubble floating over the selector. It has to be a separate
    /// window because DWM thumbnails are drawn on top of everything a window
    /// paints itself. It is click-through and never takes focus.
    /// </summary>
    internal sealed class HintWindow : Form
    {
        readonly Label label = new Label();
        Form anchor;

        internal HintWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Theme.Surface;
            label.ForeColor = Theme.TextColor;
            label.Font = Theme.Bold;
            label.AutoSize = true;
            label.Location = new Point(Theme.Px(12), Theme.Px(7));
            Controls.Add(label);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= Native.WsExToolWindow | Native.WsExLayered | Native.WsExTransparent | Native.WsExNoActivate;
                return parameters;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.SetLayeredWindowAttributes(Handle, 0, 235, Native.LwaAlpha);
        }

        internal void ShowAbove(Form target, string text)
        {
            anchor = target;
            SetText(text);
            Show(target);
        }

        /// <summary>Updates the text and keeps the bubble centred near the top of the selector.</summary>
        internal void SetText(string text)
        {
            if (label.Text == text) return;
            label.Text = text;
            Size size = new Size(label.PreferredWidth + Theme.Px(24), label.PreferredHeight + Theme.Px(14));
            if (anchor == null) { Size = size; return; }
            Rectangle screen = Screen.FromRectangle(anchor.Bounds).WorkingArea;
            int x = anchor.Left + (anchor.Width - size.Width) / 2;
            int y = Math.Max(anchor.Top, screen.Top) + Theme.Px(14);
            Bounds = new Rectangle(Geometry.Clamp(x, screen.Left, screen.Right - size.Width), y, size.Width, size.Height);
        }
    }
}

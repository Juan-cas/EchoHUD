// SPDX-License-Identifier: GPL-3.0-only
//
// RegionSelector.cs - "draw a box over what you want to mirror".
//
// When the user clicks Add region, this borderless window appears exactly
// over the source window and shows a dimmed live copy of it. The user drags
// a rectangle; the part inside the rectangle lights up; releasing the mouse
// accepts it. Esc or right-click cancels. Holding Shift while releasing keeps
// the selector open, so several small areas (for example each health bar of a
// party list, without the background between them) can be picked in one go.
// With areas picked that way, pressing R adds one more at the same spacing -
// draw two rows of a list and press R for each further row.
//
// A magnifier follows the mouse so that thin things such as health bars can
// be boxed precisely (see LoupeWindow at the end of this file). Afterwards
// the arrow keys on the finished mirror adjust the area pixel by pixel.
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
using System.Collections.Generic;
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
        readonly LoupeWindow loupe;
        readonly int capacity;                                   // how many areas may be picked
        readonly List<Rectangle> accepted = new List<Rectangle>(); // areas confirmed so far
        readonly List<IntPtr> lit = new List<IntPtr>();           // one bright thumbnail per confirmed area
        IntPtr dimmed, bright;
        Point start;
        bool dragging;
        Rectangle selection;

        /// <summary>
        /// Lets the user pick one or more rectangles of the source. Returns
        /// false if they cancelled or the source went away. Coordinates are
        /// pixels inside the source's client area, and <paramref name="size"/>
        /// is that client area's size at the moment of selection.
        ///
        /// Releasing the mouse normally finishes with that one area. Holding
        /// Shift while releasing keeps the selector open for another area, up
        /// to <paramref name="maximum"/>; Enter then finishes.
        /// </summary>
        internal static bool Pick(IWin32Window owner, SourceWindow source, int maximum, out List<Rectangle> crops, out Size size)
        {
            crops = new List<Rectangle>();
            size = Size.Empty;
            if (!Sources.IsUsable(source) || maximum < 1) return false;
            Rectangle bounds = Native.ClientScreenBounds(source.Handle);
            if (bounds.Width < 16 || bounds.Height < 16) return false;

            using (RegionSelector selector = new RegionSelector(source, bounds, maximum))
            {
                if (selector.ShowDialog(owner) != DialogResult.OK || selector.accepted.Count == 0) return false;
                crops.AddRange(selector.accepted);
                size = selector.sourceSize;
                return true;
            }
        }

        RegionSelector(SourceWindow source, Rectangle bounds, int maximum)
        {
            this.source = source;
            capacity = maximum;
            sourceSize = bounds.Size;
            loupe = new LoupeWindow(source);
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
            foreach (IntPtr thumbnail in lit) Native.DwmUnregisterThumbnail(thumbnail);
            lit.Clear();
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
            hint.ShowAbove(this, Strings.Get(capacity > 1 ? "selector.hintMany" : "selector.hint"));
            loupe.Show(this);
            loupe.Follow(PointToClient(Cursor.Position), sourceSize, Bounds);
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
            loupe.Follow(e.Location, sourceSize, Bounds);
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
                ShowProgress();
                return;
            }

            accepted.Add(selection);
            bool wantsMore = (ModifierKeys & Keys.Shift) != 0 && accepted.Count < capacity;
            if (!wantsMore)
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            KeepLit();
            ShowProgress();
        }

        void ShowProgress()
        {
            hint.SetText(accepted.Count == 0
                ? Strings.Get(capacity > 1 ? "selector.hintMany" : "selector.hint")
                : Strings.Format("selector.progress", accepted.Count));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Cancel();
            else if (e.KeyCode == Keys.Enter && accepted.Count > 0 && !dragging)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else if (e.KeyCode == Keys.R && !dragging) RepeatSpacing();
        }

        /// <summary>
        /// Adds one more area continuing the series of the last two (or, with
        /// only one so far, directly below it). Nothing is detected in the
        /// picture: it is plain arithmetic on the rectangles the user drew.
        /// </summary>
        void RepeatSpacing()
        {
            if (accepted.Count == 0 || accepted.Count >= capacity) return;
            Rectangle last = accepted[accepted.Count - 1];
            Rectangle? previous = accepted.Count > 1 ? accepted[accepted.Count - 2] : (Rectangle?)null;
            Rectangle next = Geometry.NextInSeries(previous, last, sourceSize);
            if (next.IsEmpty)
            {
                hint.SetText(Strings.Get("selector.noRoom"));
                return;
            }
            accepted.Add(next);
            Place(bright, next, 255, true);
            KeepLit();
            ShowProgress();
        }

        /// <summary>Leaves the current bright thumbnail in place and prepares a fresh one for the next area.</summary>
        void KeepLit()
        {
            lit.Add(bright);
            bright = IntPtr.Zero;
            Native.DwmRegisterThumbnail(Handle, source.Handle, out bright);
            Place(bright, Rectangle.Empty, 255, false);
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
            if (disposing)
            {
                hint.Dispose();
                loupe.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A magnifying glass for the selector: a small window near the mouse
    /// showing the few pixels around it, enlarged. Like everything else here
    /// it is a DWM thumbnail - Windows draws the enlarged copy, and the
    /// program still never sees a pixel. Click-through; never takes focus.
    /// </summary>
    internal sealed class LoupeWindow : Form
    {
        const int Radius = 16; // source pixels shown on each side of the mouse
        const int Zoom = 5;

        readonly SourceWindow source;
        IntPtr thumbnail;

        internal LoupeWindow(SourceWindow source)
        {
            this.source = source;
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Theme.Accent; // shows as a thin frame around the picture
            int side = (2 * Radius + 1) * Zoom + 4;
            Bounds = new Rectangle(-10000, -10000, side, side);
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
            Native.SetLayeredWindowAttributes(Handle, 0, 255, Native.LwaAlpha);
            if (Native.DwmRegisterThumbnail(Handle, source.Handle, out thumbnail) != 0) thumbnail = IntPtr.Zero;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (thumbnail != IntPtr.Zero) Native.DwmUnregisterThumbnail(thumbnail);
            thumbnail = IntPtr.Zero;
            base.OnHandleDestroyed(e);
        }

        /// <summary>
        /// Shows the surroundings of <paramref name="mouse"/> (source pixels)
        /// and moves the loupe beside the mouse, flipping to the other side
        /// near the edges of the selector so it never covers the spot itself.
        /// </summary>
        internal void Follow(Point mouse, Size sourceSize, Rectangle selectorOnScreen)
        {
            if (thumbnail == IntPtr.Zero) return;
            int span = 2 * Radius + 1;
            int left = Geometry.Clamp(mouse.X - Radius, 0, sourceSize.Width - span);
            int top = Geometry.Clamp(mouse.Y - Radius, 0, sourceSize.Height - span);
            Rectangle part = new Rectangle(left, top, Math.Min(span, sourceSize.Width), Math.Min(span, sourceSize.Height));

            Native.ThumbnailProperties properties = new Native.ThumbnailProperties();
            properties.Flags = Native.TnpAll;
            properties.SourceClientAreaOnly = true;
            properties.Opacity = 255;
            properties.Visible = true;
            properties.Source = new Native.RECT(part);
            properties.Destination = new Native.RECT(new Rectangle(2, 2, part.Width * Zoom, part.Height * Zoom));
            Native.DwmUpdateThumbnailProperties(thumbnail, ref properties);

            int gap = Theme.Px(28);
            int x = selectorOnScreen.Left + mouse.X + gap, y = selectorOnScreen.Top + mouse.Y + gap;
            if (x + Width > selectorOnScreen.Right) x = selectorOnScreen.Left + mouse.X - gap - Width;
            if (y + Height > selectorOnScreen.Bottom) y = selectorOnScreen.Top + mouse.Y - gap - Height;
            Location = new Point(x, y);
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

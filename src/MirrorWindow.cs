// SPDX-License-Identifier: GPL-3.0-only
//
// MirrorWindow.cs - one floating window that shows one region of the source.
//
// HOW THE PICTURE GETS HERE
//   We call DwmRegisterThumbnail(ourWindow, sourceWindow). From then on the
//   Windows compositor draws the chosen part of the source into this window
//   on every frame, on the GPU, with no work from us. We never receive the
//   pixels, so there is no capture loop, no timer-driven redraw and no lag
//   beyond the compositor's own.
//
// TWO MODES
//   Unlocked  The window has a coloured frame, can be dragged anywhere
//             (clicking into place beside other mirrors), resized from its
//             bottom-right corner, and right-clicked for options. The mirror
//             clicked last has a white frame and listens to the arrow keys:
//               arrows        move the mirrored AREA one pixel
//               Shift+arrows  make the area one pixel larger or smaller
//               Ctrl+arrows   move the mirror WINDOW one pixel
//   Locked    The frame disappears and the window becomes "click-through":
//             Windows delivers mouse clicks to whatever is underneath, as if
//             the mirror were not there. It also never takes keyboard focus.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EchoHud
{
    internal sealed class MirrorWindow : Form
    {
        internal readonly MirrorRegion Spec;

        SourceWindow source;
        IntPtr thumbnail;       // DWM handle; Zero when nothing is registered
        Size lastSourceSize;
        bool locked;
        bool applying;          // true while WE move/resize the window, to ignore our own events
        bool active;            // this mirror has keyboard focus (unlocked only)
        Point grab;             // where inside the window the mouse took hold of it, for snapping
        ToolStripMenuItem groupItem;
        readonly ContextMenuStrip menu;
        ToolStripMenuItem borderItem;

        /// <summary>The user moved, resized or restyled this mirror; the profile needs saving.</summary>
        internal event EventHandler Edited;

        /// <summary>The user dragged this mirror by (dx, dy) pixels. Raised before Edited.</summary>
        internal event Action<int, int> Dragged;
        internal event EventHandler RecropRequested;
        internal event EventHandler RenameRequested;
        internal event EventHandler DeleteRequested;

        /// <summary>Raised as the right-click menu opens, so the owner can list the groups.</summary>
        internal event Action<ToolStripMenuItem> GroupMenuOpening;

        /// <summary>
        /// Supplied by the owner: the rectangles this mirror may snap to while
        /// dragged, or null when snapping is switched off.
        /// </summary>
        internal Func<List<Rectangle>> SnapTargets;

        /// <summary>Where the mirrored picture (without the editing frame) sits on the desktop.</summary>
        internal Rectangle PictureBounds
        {
            get
            {
                Size picture = Geometry.Output(Crop().Size, Spec.ScalePercent);
                return new Rectangle(Spec.X, Spec.Y, picture.Width, picture.Height);
            }
        }

        internal MirrorWindow(MirrorRegion region)
        {
            Spec = region;
            Text = "Echo HUD mirror";
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            MinimumSize = new Size(Geometry.MinSide, Geometry.MinSide);
            BackColor = Theme.Accent;
            menu = BuildMenu();
        }

        // ------------------------------------------------------------------
        // Window styles
        // ------------------------------------------------------------------

        /// <summary>Showing a mirror must never pull focus away from the game.</summary>
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= Native.WsExToolWindow | Native.WsExLayered;
                if (locked) parameters.ExStyle |= Native.WsExTransparent | Native.WsExNoActivate;
                return parameters;
            }
        }

        /// <summary>Frame thickness: always visible while editing, optional once locked.</summary>
        int Frame
        {
            get { return !locked || Spec.Border ? Theme.Px(2) : 0; }
        }

        internal void SetLocked(bool value)
        {
            if (locked == value) return;
            locked = value;
            // Click-through is an extended window style, which Windows only
            // applies reliably when the window is created - so recreate it.
            if (IsHandleCreated) RecreateHandle();
            ApplyRegion();
        }

        // ------------------------------------------------------------------
        // Thumbnail lifetime
        // ------------------------------------------------------------------

        /// <summary>Points this mirror at a source window (or at nothing, with null).</summary>
        internal void Bind(SourceWindow next)
        {
            IntPtr before = source == null ? IntPtr.Zero : source.Handle;
            IntPtr after = next == null ? IntPtr.Zero : next.Handle;
            source = next;
            if (before == after && (thumbnail != IntPtr.Zero || after == IntPtr.Zero)) return;
            Unregister();
            Register();
            ApplyRegion();
        }

        void Register()
        {
            if (!IsHandleCreated || source == null || !Native.IsWindow(source.Handle)) return;
            if (Native.DwmRegisterThumbnail(Handle, source.Handle, out thumbnail) != 0) thumbnail = IntPtr.Zero;
        }

        void Unregister()
        {
            if (thumbnail == IntPtr.Zero) return;
            Native.DwmUnregisterThumbnail(thumbnail);
            thumbnail = IntPtr.Zero;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyOpacity();
            Register();
            UpdateThumbnail();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            Unregister(); // A thumbnail belongs to the window handle it was registered on.
            base.OnHandleDestroyed(e);
        }

        // ------------------------------------------------------------------
        // Applying the region to the window
        // ------------------------------------------------------------------

        Size SourceSize()
        {
            Size live = Sources.IsUsable(source) ? Native.ClientSize(source.Handle) : Size.Empty;
            return live.Width > 0 && live.Height > 0 ? live : new Size(Spec.SourceWidth, Spec.SourceHeight);
        }

        Rectangle Crop()
        {
            // The game re-flows its interface when resized; projectors and the demo stretch.
            bool stretches = source == null || source.Kind != SourceKind.Tibia;
            return Geometry.CurrentCrop(Spec, SourceSize(), stretches);
        }

        /// <summary>Re-reads every field of <see cref="Spec"/> and updates the window to match.</summary>
        internal void ApplyRegion()
        {
            applying = true;
            try
            {
                lastSourceSize = SourceSize();
                Size picture = Geometry.Output(Crop().Size, Spec.ScalePercent);
                int frame = Frame;
                BackColor = FrameColour;
                // Spec.X/Y is the corner of the PICTURE, so it does not jump
                // when the frame appears or disappears.
                Bounds = new Rectangle(Spec.X - frame, Spec.Y - frame, picture.Width + 2 * frame, picture.Height + 2 * frame);
            }
            finally
            {
                applying = false;
            }
            ApplyOpacity();
            UpdateThumbnail();
        }

        Color FrameColour
        {
            get { return locked ? Theme.Highlight : active ? Theme.TextColor : Theme.Accent; }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            active = true;
            BackColor = FrameColour;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            active = false;
            BackColor = FrameColour;
        }

        void ApplyOpacity()
        {
            if (!IsHandleCreated) return;
            Native.SetLayeredWindowAttributes(Handle, 0, (byte)(Spec.OpacityPercent * 255 / 100), Native.LwaAlpha);
        }

        /// <summary>Tells the compositor which part of the source to draw, and where.</summary>
        void UpdateThumbnail()
        {
            if (thumbnail == IntPtr.Zero) return;
            Rectangle crop = Crop();
            int frame = Frame;
            Rectangle inside = new Rectangle(frame, frame, ClientSize.Width - 2 * frame, ClientSize.Height - 2 * frame);

            Native.ThumbnailProperties properties = new Native.ThumbnailProperties();
            properties.Flags = Native.TnpAll;
            properties.SourceClientAreaOnly = true; // crop coordinates exclude the title bar
            properties.Opacity = 255;               // whole-window opacity is handled by ApplyOpacity
            properties.Visible = Sources.IsUsable(source) && crop.Width > 0 && inside.Width > 0 && inside.Height > 0;
            properties.Source = new Native.RECT(crop);
            properties.Destination = new Native.RECT(Geometry.Fit(crop.Size, inside));
            Native.DwmUpdateThumbnailProperties(thumbnail, ref properties);
        }

        /// <summary>Called a few times per second by the main window to follow source resizes.</summary>
        internal void FollowSource()
        {
            if (Visible && SourceSize() != lastSourceSize) ApplyRegion();
        }

        // ------------------------------------------------------------------
        // Moving and resizing with the mouse (unlocked only)
        // ------------------------------------------------------------------

        protected override void WndProc(ref Message m)
        {
            if (!locked)
            {
                if (m.Msg == Native.WmNcHitTest)
                {
                    // Tell Windows the whole window is a "title bar" (drag to
                    // move) except the bottom-right corner (drag to resize).
                    // Windows then runs the move/resize itself.
                    long packed = m.LParam.ToInt64();
                    Point cursor = PointToClient(new Point(unchecked((short)(packed & 0xFFFF)), unchecked((short)((packed >> 16) & 0xFFFF))));
                    int grip = Theme.Px(16);
                    bool corner = cursor.X >= ClientSize.Width - grip && cursor.Y >= ClientSize.Height - grip;
                    m.Result = new IntPtr(corner ? Native.HtBottomRight : Native.HtCaption);
                    return;
                }
                if (m.Msg == Native.WmNcRButtonUp)
                {
                    menu.Show(Cursor.Position);
                    return;
                }
                if (m.Msg == Native.WmNcLButtonDblClk) return; // would otherwise maximise the window
                if (m.Msg == Native.WmEnterSizeMove)
                {
                    Point cursor = Cursor.Position;
                    grab = new Point(cursor.X - Left, cursor.Y - Top);
                }
                if (m.Msg == Native.WmMoving && SnapWhileMoving(m.LParam))
                {
                    m.Result = new IntPtr(1); // "we changed the rectangle"
                    return;
                }
            }
            base.WndProc(ref m);
        }

        /// <summary>
        /// While the user drags the window, Windows asks where it should go
        /// next. We answer with the nearest position that lines the PICTURE
        /// (not the editing frame) up with a neighbour, so that mirrors sit
        /// flush against each other once locked.
        ///
        /// The position is worked out from the mouse each time rather than
        /// from Windows' proposal; otherwise a snapped window would stay
        /// stuck to its neighbour.
        /// </summary>
        bool SnapWhileMoving(IntPtr proposed)
        {
            List<Rectangle> targets = SnapTargets == null ? null : SnapTargets();
            if (targets == null) return false;
            Native.RECT rect = (Native.RECT)Marshal.PtrToStructure(proposed, typeof(Native.RECT));
            int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top, frame = Frame;
            Point cursor = Cursor.Position;
            Rectangle picture = new Rectangle(cursor.X - grab.X + frame, cursor.Y - grab.Y + frame, width - 2 * frame, height - 2 * frame);
            Point snapped = Geometry.Snap(picture, targets, Theme.Px(8));
            rect.Left = snapped.X - frame;
            rect.Top = snapped.Y - frame;
            rect.Right = rect.Left + width;
            rect.Bottom = rect.Top + height;
            Marshal.StructureToPtr(rect, proposed, false);
            return true;
        }

        /// <summary>Arrow keys fine-tune the mirror that was clicked last. See the top of this file.</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys code = keyData & Keys.KeyCode;
            int dx = code == Keys.Left ? -1 : code == Keys.Right ? 1 : 0;
            int dy = code == Keys.Up ? -1 : code == Keys.Down ? 1 : 0;
            if (locked || (dx == 0 && dy == 0)) return base.ProcessCmdKey(ref msg, keyData);

            if ((keyData & Keys.Control) != 0)
            {
                Spec.X += dx;
                Spec.Y += dy;
            }
            else Geometry.Nudge(Spec, dx, dy, (keyData & Keys.Shift) != 0);
            ApplyRegion();
            NotifyChanged();
            return true;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateThumbnail(); // keep the picture fitted while the user drags the corner
        }

        /// <summary>Fires when the user lets go after dragging or resizing.</summary>
        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            if (applying || locked) return;
            int frame = Frame;
            Spec.ScalePercent = Geometry.ScaleForWidth(ClientSize.Width - 2 * frame, Crop().Width);
            int dx = Left + frame - Spec.X, dy = Top + frame - Spec.Y;
            Spec.X = Left + frame;
            Spec.Y = Top + frame;
            ApplyRegion(); // snap the window to the exact proportions of the crop
            if ((dx != 0 || dy != 0) && Dragged != null) Dragged(dx, dy);
            NotifyChanged();
        }

        // ------------------------------------------------------------------
        // Right-click menu
        // ------------------------------------------------------------------

        ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip strip = Dialogs.DarkMenu();

            ToolStripMenuItem size = new ToolStripMenuItem(Strings.Get("menu.size"));
            foreach (int percent in new[] { 50, 100, 150, 200, 300, 400 })
            {
                int value = percent;
                size.DropDownItems.Add(value + " %", null, delegate { Spec.ScalePercent = value; ApplyRegion(); NotifyChanged(); });
            }
            ToolStripMenuItem opacity = new ToolStripMenuItem(Strings.Get("menu.opacity"));
            foreach (int percent in new[] { 100, 80, 60, 40 })
            {
                int value = percent;
                opacity.DropDownItems.Add(value + " %", null, delegate { Spec.OpacityPercent = value; ApplyRegion(); NotifyChanged(); });
            }
            foreach (ToolStripMenuItem parent in new[] { size, opacity })
            {
                parent.DropDown.BackColor = Theme.Surface;
                parent.DropDown.ForeColor = Theme.TextColor;
                parent.DropDown.Renderer = strip.Renderer;
            }

            borderItem = new ToolStripMenuItem(Strings.Get("menu.border"), null, delegate
            {
                Spec.Border = !Spec.Border;
                ApplyRegion();
                NotifyChanged();
            });

            strip.Items.Add(size);
            strip.Items.Add(opacity);
            groupItem = Dialogs.Submenu(strip, Strings.Get("menu.group"));
            groupItem.DropDownItems.Add("-"); // placeholder so the arrow shows; filled when the menu opens

            strip.Items.Add(borderItem);
            strip.Items.Add(groupItem);
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(Strings.Get("menu.recrop"), null, delegate { Raise(RecropRequested); });
            strip.Items.Add(Strings.Get("menu.rename"), null, delegate { Raise(RenameRequested); });
            strip.Items.Add(Strings.Get("menu.hide"), null, delegate { Spec.Visible = false; NotifyChanged(); });
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(Strings.Get("menu.delete"), null, delegate { Raise(DeleteRequested); });
            strip.Opening += delegate
            {
                borderItem.Checked = Spec.Border;
                if (GroupMenuOpening != null) GroupMenuOpening(groupItem);
            };
            return strip;
        }

        void NotifyChanged()
        {
            Raise(Edited);
        }

        void Raise(EventHandler handler)
        {
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) menu.Dispose();
            base.Dispose(disposing);
        }
    }
}

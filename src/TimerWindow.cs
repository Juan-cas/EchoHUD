// SPDX-License-Identifier: GPL-3.0-only
//
// TimerWindow.cs - a floating countdown, drawn as a circle or as a bar.
//
// WHAT IT IS
//   A kitchen timer. As a circle, a ring empties as time runs out with the
//   remaining time in the middle; as a bar, a line shrinks under the name
//   and the time. At zero it turns solid, (optionally) plays a sound, and
//   (optionally) starts counting up - "+0:42" means it has been ready for 42
//   seconds.
//
// WHAT IT IS NOT
//   It does not know when you used an item or cast a spell. Nothing in this
//   program watches the game or your keyboard. YOU start the timer: with the
//   hotkey you assigned to it, by double-clicking it, or with the play
//   button in the panel. See docs/SAFETY.md for why it works this way.
//
// HOW IT IS DRAWN
//   Unlike mirrors, this window shows a picture we draw ourselves, so it can
//   have smooth round edges: the picture is drawn into a bitmap with
//   transparency and handed to Windows with Native.PresentOwnBitmap.
//
// Locked/unlocked behaves like MirrorWindow: unlocked can be dragged,
// double-clicked and right-clicked; locked lets clicks pass through.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Windows.Forms;

namespace EchoHud
{
    internal sealed class TimerWindow : Form
    {
        internal readonly CooldownTimer Spec;

        DateTime endsAt;   // when the countdown reaches (or reached) zero
        bool running;
        bool finished;     // reached zero and not restarted or reset yet
        bool locked;
        string lastDrawn = "";
        SoundPlayer player; // kept alive while a custom sound plays
        readonly Timer clock = new Timer();
        readonly ContextMenuStrip menu;
        ToolStripMenuItem groupItem;

        /// <summary>The user moved or restyled this timer; the profile needs saving.</summary>
        internal event EventHandler Edited;

        /// <summary>The user dragged this timer by (dx, dy) pixels. Raised before Edited.</summary>
        internal event Action<int, int> Dragged;
        internal event EventHandler DurationRequested;
        internal event EventHandler HotkeyRequested;
        internal event EventHandler RenameRequested;
        internal event EventHandler DeleteRequested;

        /// <summary>Raised as the right-click menu opens, so the owner can list the groups.</summary>
        internal event Action<ToolStripMenuItem> GroupMenuOpening;

        internal TimerWindow(CooldownTimer spec)
        {
            Spec = spec;
            Text = "Echo HUD timer";
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            MinimumSize = new Size(8, 8);
            clock.Interval = 200;
            clock.Tick += delegate { Advance(); };
            menu = BuildMenu();
            ApplySpec();
        }

        // ------------------------------------------------------------------
        // Starting and stopping
        // ------------------------------------------------------------------

        internal bool IsRunning { get { return running; } }

        /// <summary>Starts the countdown from the full duration, even if it was already running.</summary>
        internal void StartOrRestart()
        {
            endsAt = DateTime.UtcNow.AddSeconds(Spec.Seconds);
            running = true;
            finished = false;
            clock.Start();
            Render(true);
        }

        /// <summary>Back to the idle state, showing the full duration.</summary>
        internal void Reset()
        {
            running = finished = false;
            clock.Stop();
            Render(true);
        }

        double Remaining
        {
            get { return running ? Math.Max(0, (endsAt - DateTime.UtcNow).TotalSeconds) : finished ? 0 : Spec.Seconds; }
        }

        void Advance()
        {
            if (running && Remaining <= 0)
            {
                running = false;
                finished = true;
                PlaySound();
                Render(true);
                return;
            }
            // Once finished the clock only keeps ticking for the "+0:42" display.
            if (finished && !Spec.CountUp) clock.Stop();
            Render(false);
        }

        /// <summary>
        /// Plays the chosen .wav file, or the standard Windows sound if none
        /// was chosen or it cannot be played. Only local files are accepted;
        /// see CooldownTimer.IsPlayableSoundPath.
        /// </summary>
        void PlaySound()
        {
            if (!Spec.Sound) return;
            try
            {
                if (CooldownTimer.IsPlayableSoundPath(Spec.SoundFile) && File.Exists(Spec.SoundFile))
                {
                    if (player != null) player.Dispose();
                    player = new SoundPlayer(Spec.SoundFile);
                    player.Play();
                    return;
                }
            }
            catch (Exception) { /* a broken sound file must not break the timer */ }
            SystemSounds.Exclamation.Play();
        }

        // ------------------------------------------------------------------
        // Window styles and placement
        // ------------------------------------------------------------------

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

        internal void SetLocked(bool value)
        {
            if (locked == value) return;
            locked = value;
            if (IsHandleCreated) RecreateHandle(); // same reason as MirrorWindow.SetLocked
            Render(true);
        }

        /// <summary>Window size for a timer: a square for the circle, a wide strip for the bar.</summary>
        internal static Size SizeFor(CooldownTimer spec)
        {
            int side = Theme.Px(spec.Size);
            return spec.Shape == TimerShape.Bar ? new Size(side * 3, Math.Max(Theme.Px(22), side * 2 / 5)) : new Size(side, side);
        }

        /// <summary>Re-reads <see cref="Spec"/> and updates size, position and picture.</summary>
        internal void ApplySpec()
        {
            Bounds = new Rectangle(new Point(Spec.X, Spec.Y), SizeFor(Spec));
            Render(true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Render(true);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) Render(true);
        }

        // ------------------------------------------------------------------
        // Drawing
        // ------------------------------------------------------------------

        /// <summary>
        /// Draws the timer and hands the picture to Windows. Skipped when
        /// nothing visible would change, unless <paramref name="force"/> is set.
        /// </summary>
        void Render(bool force)
        {
            if (!IsHandleCreated || Width < 8 || Height < 8) return;
            double remaining = Remaining;
            string text = !finished ? Durations.Format((int)Math.Ceiling(remaining))
                : Spec.CountUp ? "+" + Durations.Format((int)(DateTime.UtcNow - endsAt).TotalSeconds)
                : Strings.Get("timer.ready");
            // The ring moves slowly; redrawing when the text changes (once a
            // second) plus a coarse ring step is plenty.
            string signature = text + "|" + (int)(remaining * 360 / Math.Max(1, Spec.Seconds));
            if (!force && signature == lastDrawn) return;
            lastDrawn = signature;

            using (Bitmap picture = new Bitmap(Width, Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(picture))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    if (Spec.Shape == TimerShape.Bar) DrawBar(g, Width, Height, remaining, text);
                    else DrawCircle(g, Width, remaining, text);
                }
                Native.PresentOwnBitmap(Handle, picture, Location, (byte)(Spec.OpacityPercent * 255 / 100));
            }
        }

        /// <summary>The timer's own colour; amber (or white, for an amber timer) during the last ten seconds.</summary>
        Color Colour(double remaining)
        {
            Color own = Theme.TimerColours[Geometry.Clamp(Spec.Colour, 0, Theme.TimerColours.Length - 1)];
            if (!running || remaining > 10) return own;
            return own == Theme.Highlight ? Theme.TextColor : Theme.Highlight;
        }

        void DrawCircle(Graphics g, int side, double remaining, string text)
        {
            float thickness = Math.Max(4f, side / 9f);
            RectangleF disc = new RectangleF(1, 1, side - 2, side - 2);
            RectangleF ring = RectangleF.Inflate(disc, -thickness / 2 - 2, -thickness / 2 - 2);

            using (Brush back = new SolidBrush(Color.FromArgb(205, Theme.Background)))
                g.FillEllipse(back, disc);
            using (Pen track = new Pen(Theme.Raised, thickness))
                g.DrawEllipse(track, ring);

            Color colour = Colour(remaining);
            if (finished)
            {
                using (Pen full = new Pen(colour, thickness)) g.DrawEllipse(full, ring);
            }
            else if (running)
            {
                // The lit part is what is LEFT, starting at 12 o'clock.
                float sweep = (float)(360.0 * remaining / Math.Max(1, Spec.Seconds));
                using (Pen arc = new Pen(colour, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    if (sweep > 0.5f) g.DrawArc(arc, ring, -90, sweep);
            }
            if (!locked)
            {
                // Dashed outline = "you can drag me".
                using (Pen edit = new Pen(Theme.Accent, 1.5f) { DashStyle = DashStyle.Dash })
                    g.DrawEllipse(edit, disc);
            }

            // Under the time: the timer's name, or "Ready" while counting up.
            string caption = finished && Spec.CountUp ? Strings.Get("timer.ready") : Spec.Name;
            bool showCaption = side >= Theme.Px(64) && caption.Length > 0;
            float textSize = side * (text.Length <= 4 ? 0.27f : text.Length <= 5 ? 0.22f : 0.17f);
            using (StringFormat centred = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            using (Font big = new Font("Segoe UI", textSize, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush ink = new SolidBrush(running || finished ? Theme.TextColor : Theme.Muted))
            {
                RectangleF timeArea = new RectangleF(0, showCaption ? -side * 0.06f : 0, side, side);
                g.DrawString(text, big, ink, timeArea, centred);
                if (showCaption)
                {
                    using (Font small = new Font("Segoe UI", side * 0.115f, FontStyle.Regular, GraphicsUnit.Pixel))
                    using (Brush faint = new SolidBrush(Theme.Muted))
                        g.DrawString(caption, small, faint, new RectangleF(side * 0.2f, side * 0.6f, side * 0.6f, side * 0.18f), centred);
                }
            }
        }

        void DrawBar(Graphics g, int width, int height, double remaining, string text)
        {
            RectangleF body = new RectangleF(1, 1, width - 2, height - 2);
            float radius = Math.Min(height / 3f, Theme.Px(8));
            using (GraphicsPath outline = Rounded(body, radius))
            {
                using (Brush back = new SolidBrush(Color.FromArgb(215, Theme.Background)))
                    g.FillPath(back, outline);

                // A line along the bottom shows what is LEFT, shrinking to the left.
                float line = Math.Max(3f, height / 7f), inset = radius;
                RectangleF track = new RectangleF(body.Left + inset, body.Bottom - line - 3, body.Width - 2 * inset, line);
                using (Brush rail = new SolidBrush(Theme.Raised)) g.FillRectangle(rail, track);
                double fraction = finished ? 1 : running ? remaining / Math.Max(1, Spec.Seconds) : 0;
                using (Brush fill = new SolidBrush(Colour(remaining)))
                    g.FillRectangle(fill, track.Left, track.Top, (float)(track.Width * fraction), track.Height);

                if (!locked)
                    using (Pen edit = new Pen(Theme.Accent, 1.5f) { DashStyle = DashStyle.Dash })
                        g.DrawPath(edit, outline);
            }

            float textSize = Math.Max(9f, (height - height / 7f - 6) * 0.62f);
            RectangleF area = new RectangleF(radius, 0, width - 2 * radius, height - height / 7f - 4);
            string caption = finished && Spec.CountUp ? Strings.Get("timer.ready") : Spec.Name;
            using (Font time = new Font("Segoe UI", textSize, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font label = new Font("Segoe UI", textSize * 0.82f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Brush ink = new SolidBrush(running || finished ? Theme.TextColor : Theme.Muted))
            using (Brush faint = new SolidBrush(Theme.Muted))
            using (StringFormat right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            using (StringFormat left = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                float timeWidth = g.MeasureString(text, time).Width + 2;
                g.DrawString(text, time, ink, area, right);
                g.DrawString(caption, label, faint, new RectangleF(area.Left, area.Top, Math.Max(0, area.Width - timeWidth), area.Height), left);
            }
        }

        static GraphicsPath Rounded(RectangleF box, float radius)
        {
            float d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(box.Left, box.Top, d, d, 180, 90);
            path.AddArc(box.Right - d, box.Top, d, d, 270, 90);
            path.AddArc(box.Right - d, box.Bottom - d, d, d, 0, 90);
            path.AddArc(box.Left, box.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ------------------------------------------------------------------
        // Mouse (unlocked only)
        // ------------------------------------------------------------------

        protected override void WndProc(ref Message m)
        {
            if (!locked)
            {
                if (m.Msg == Native.WmNcHitTest)
                {
                    m.Result = new IntPtr(Native.HtCaption); // the whole timer drags the window
                    return;
                }
                if (m.Msg == Native.WmNcRButtonUp)
                {
                    menu.Show(Cursor.Position);
                    return;
                }
                if (m.Msg == Native.WmNcLButtonDblClk)
                {
                    StartOrRestart();
                    return;
                }
            }
            base.WndProc(ref m);
        }

        /// <summary>Fires when the user lets go after dragging.</summary>
        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            if (locked || (Spec.X == Left && Spec.Y == Top)) return;
            int dx = Left - Spec.X, dy = Top - Spec.Y;
            Spec.X = Left;
            Spec.Y = Top;
            if (Dragged != null) Dragged(dx, dy);
            Raise(Edited);
        }

        // ------------------------------------------------------------------
        // Right-click menu
        // ------------------------------------------------------------------

        ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip strip = Dialogs.DarkMenu();
            strip.Items.Add(Strings.Get("timer.start"), null, delegate { StartOrRestart(); });
            strip.Items.Add(Strings.Get("timer.reset"), null, delegate { Reset(); });
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(Strings.Get("timer.duration"), null, delegate { Raise(DurationRequested); });
            strip.Items.Add(Strings.Get("timer.hotkey"), null, delegate { Raise(HotkeyRequested); });

            ToolStripMenuItem shape = Dialogs.Submenu(strip, Strings.Get("timer.shape"));
            ToolStripMenuItem circle = new ToolStripMenuItem(Strings.Get("timer.shapeCircle"), null, delegate { Spec.Shape = TimerShape.Circle; Restyle(); });
            ToolStripMenuItem bar = new ToolStripMenuItem(Strings.Get("timer.shapeBar"), null, delegate { Spec.Shape = TimerShape.Bar; Restyle(); });
            shape.DropDownItems.AddRange(new ToolStripItem[] { circle, bar });

            ToolStripMenuItem colour = Dialogs.Submenu(strip, Strings.Get("timer.colour"));
            for (int i = 0; i < Theme.TimerColours.Length; i++)
            {
                int index = i;
                Bitmap swatch = new Bitmap(16, 16);
                using (Graphics g = Graphics.FromImage(swatch))
                using (Brush paint = new SolidBrush(Theme.TimerColours[i]))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillEllipse(paint, 2, 2, 12, 12);
                }
                colour.DropDownItems.Add((i + 1).ToString(), swatch, delegate { Spec.Colour = index; Restyle(); });
            }

            ToolStripMenuItem size = Dialogs.Submenu(strip, Strings.Get("menu.size"));
            foreach (int pixels in new[] { 48, 64, 72, 96, 128, 160 })
            {
                int value = pixels;
                size.DropDownItems.Add(value + " px", null, delegate { Spec.Size = value; Restyle(); });
            }
            ToolStripMenuItem opacity = Dialogs.Submenu(strip, Strings.Get("menu.opacity"));
            foreach (int percent in new[] { 100, 80, 60, 40 })
            {
                int value = percent;
                opacity.DropDownItems.Add(value + " %", null, delegate { Spec.OpacityPercent = value; Restyle(); });
            }

            ToolStripMenuItem sound = Dialogs.Submenu(strip, Strings.Get("timer.sound"));
            ToolStripMenuItem soundOff = new ToolStripMenuItem(Strings.Get("timer.soundOff"), null, delegate { Spec.Sound = false; Raise(Edited); });
            ToolStripMenuItem soundStandard = new ToolStripMenuItem(Strings.Get("timer.soundStandard"), null, delegate { Spec.Sound = true; Spec.SoundFile = ""; Raise(Edited); PlaySound(); });
            ToolStripMenuItem soundFile = new ToolStripMenuItem(Strings.Get("timer.soundFile"), null, delegate { ChooseSoundFile(); });
            sound.DropDownItems.AddRange(new ToolStripItem[] { soundOff, soundStandard, soundFile });

            ToolStripMenuItem countUp = new ToolStripMenuItem(Strings.Get("timer.countUp"), null, delegate
            {
                Spec.CountUp = !Spec.CountUp;
                if (finished && Spec.CountUp) clock.Start();
                Restyle();
            });
            groupItem = Dialogs.Submenu(strip, Strings.Get("menu.group"));
            groupItem.DropDownItems.Add("-"); // placeholder so the arrow shows; filled when the menu opens

            strip.Items.AddRange(new ToolStripItem[] { shape, colour, size, opacity, sound, countUp, groupItem });
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(Strings.Get("menu.rename"), null, delegate { Raise(RenameRequested); });
            strip.Items.Add(Strings.Get("menu.hide"), null, delegate { Spec.Visible = false; Raise(Edited); });
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(Strings.Get("menu.delete"), null, delegate { Raise(DeleteRequested); });
            strip.Opening += delegate
            {
                circle.Checked = Spec.Shape == TimerShape.Circle;
                bar.Checked = Spec.Shape == TimerShape.Bar;
                bool custom = Spec.Sound && CooldownTimer.IsPlayableSoundPath(Spec.SoundFile);
                soundOff.Checked = !Spec.Sound;
                soundStandard.Checked = Spec.Sound && !custom;
                soundFile.Checked = custom;
                countUp.Checked = Spec.CountUp;
                if (GroupMenuOpening != null) GroupMenuOpening(groupItem);
            };
            return strip;
        }

        void Restyle()
        {
            ApplySpec();
            Raise(Edited);
        }

        void ChooseSoundFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "WAV (*.wav)|*.wav";
                dialog.Title = Strings.Get("timer.soundFile");
                if (dialog.ShowDialog() != DialogResult.OK) return;
                if (!CooldownTimer.IsPlayableSoundPath(dialog.FileName)) return; // e.g. a file on a network share
                Spec.Sound = true;
                Spec.SoundFile = dialog.FileName;
                Raise(Edited);
                PlaySound(); // let the user hear what they picked
            }
        }

        void Raise(EventHandler handler)
        {
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                clock.Dispose();
                menu.Dispose();
                if (player != null) player.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

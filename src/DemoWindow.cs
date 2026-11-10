// SPDX-License-Identifier: GPL-3.0-only
//
// DemoWindow.cs - a pretend game screen for trying the app without the game.
//
// Everything in it is drawn with plain shapes and system fonts: there is no
// game artwork in this project. It animates a little (bars, cooldown squares)
// so you can see that mirrors are live.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EchoHud
{
    internal sealed class DemoWindow : Form
    {
        internal static DemoWindow Current { get; private set; }
        internal static bool IsOpen { get { return Current != null && !Current.IsDisposed && Current.IsHandleCreated; } }

        readonly Timer animation = new Timer();
        readonly DateTime started = DateTime.UtcNow;

        internal static void Open()
        {
            if (IsOpen) { Current.Activate(); return; }
            Current = new DemoWindow();
            Current.Show();
        }

        DemoWindow()
        {
            Text = "Echo HUD - " + Strings.Get("source.demo");
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(Theme.Px(900), Theme.Px(540));
            MinimumSize = new Size(Theme.Px(480), Theme.Px(320));
            StartPosition = FormStartPosition.CenterScreen;
            ShowIcon = false;
            BackColor = Color.FromArgb(38, 44, 40);
            DoubleBuffered = true;
            ResizeRedraw = true;
            animation.Interval = 50;
            animation.Tick += delegate { Invalidate(); };
            animation.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            animation.Stop();
            animation.Dispose();
            if (Current == this) Current = null;
            base.OnFormClosed(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            double t = (DateTime.UtcNow - started).TotalSeconds;
            int width = ClientSize.Width, height = ClientSize.Height;
            int side = Theme.Px(190), pad = Theme.Px(10);

            // "Game view": a checkerboard floor with a marker walking in a circle.
            Rectangle view = new Rectangle(pad, pad, width - side - 3 * pad, height - Theme.Px(90));
            using (Brush floor = new SolidBrush(Color.FromArgb(58, 84, 62))) g.FillRectangle(floor, view);
            int tile = Theme.Px(36);
            using (Brush dark = new SolidBrush(Color.FromArgb(50, 74, 55)))
                for (int y = 0; y * tile < view.Height; y++)
                    for (int x = y % 2; x * tile < view.Width; x += 2)
                        g.FillRectangle(dark, Rectangle.Intersect(view, new Rectangle(view.X + x * tile, view.Y + y * tile, tile, tile)));
            int cx = view.X + view.Width / 2 + (int)(Math.Cos(t) * view.Width / 4);
            int cy = view.Y + view.Height / 2 + (int)(Math.Sin(t) * view.Height / 4);
            g.FillEllipse(Brushes.Gold, cx - tile / 3, cy - tile / 3, tile * 2 / 3, tile * 2 / 3);

            // "Action bar": eight squares with a cooldown sweep and a number.
            int slot = Theme.Px(44), barY = view.Bottom + pad + Theme.Px(8);
            Color[] colours = { Color.IndianRed, Color.RoyalBlue, Color.SeaGreen, Color.Orchid, Color.Orange, Color.Teal, Color.Goldenrod, Color.SlateBlue };
            for (int i = 0; i < 8; i++)
            {
                Rectangle box = new Rectangle(view.X + i * (slot + Theme.Px(6)), barY, slot, slot);
                using (Brush fill = new SolidBrush(colours[i])) g.FillRectangle(fill, box);
                double period = 3 + i, left = period - (t % period);
                using (Brush shade = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
                    g.FillPie(shade, box, -90, (float)(360 * left / period));
                g.DrawRectangle(Pens.Black, box);
                TextRenderer.DrawText(g, Math.Ceiling(left).ToString(), Theme.Bold, box, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // "Side panel": party list with health and mana bars.
            int x0 = width - side - pad, y0 = pad;
            using (Brush panel = new SolidBrush(Color.FromArgb(52, 52, 56))) g.FillRectangle(panel, x0, y0, side, height - 2 * pad);
            string[] names = { "Knight", "Paladin", "Druid", "Sorcerer", "Monk" };
            for (int i = 0; i < names.Length; i++)
            {
                int rowY = y0 + Theme.Px(10) + i * Theme.Px(46);
                TextRenderer.DrawText(g, names[i], Theme.Body, new Point(x0 + Theme.Px(8), rowY), Color.White);
                DrawBar(g, x0 + Theme.Px(8), rowY + Theme.Px(20), side - Theme.Px(16), 0.5 + 0.5 * Math.Sin(t * 0.7 + i), Color.FromArgb(200, 60, 60));
                DrawBar(g, x0 + Theme.Px(8), rowY + Theme.Px(30), side - Theme.Px(16), 0.5 + 0.5 * Math.Cos(t * 0.5 + i * 2), Color.FromArgb(70, 110, 220));
            }
        }

        static void DrawBar(Graphics g, int x, int y, int width, double fraction, Color colour)
        {
            int height = Theme.Px(7);
            g.FillRectangle(Brushes.Black, x, y, width, height);
            using (Brush fill = new SolidBrush(colour))
                g.FillRectangle(fill, x + 1, y + 1, (int)((width - 2) * Math.Max(0, Math.Min(1, fraction))), height - 2);
        }
    }
}

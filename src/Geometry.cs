// SPDX-License-Identifier: GPL-3.0-only
//
// Geometry.cs - all rectangle arithmetic, with no Windows calls.
//
// Everything here is a pure function so it can be unit tested (tests/Checks.cs)
// and reasoned about without running the app.

using System;
using System.Collections.Generic;
using System.Drawing;

namespace EchoHud
{
    internal static class Geometry
    {
        /// <summary>Smallest and largest side of a mirror window, in pixels.</summary>
        internal const int MinSide = 8, MaxSide = 4000;

        /// <summary>The rectangle between two drag points, clipped to the source.</summary>
        internal static Rectangle FromDrag(Point start, Point end, Size bounds)
        {
            int left = Clamp(Math.Min(start.X, end.X), 0, bounds.Width);
            int top = Clamp(Math.Min(start.Y, end.Y), 0, bounds.Height);
            int right = Clamp(Math.Max(start.X, end.X), 0, bounds.Width);
            int bottom = Clamp(Math.Max(start.Y, end.Y), 0, bounds.Height);
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        /// <summary>
        /// Where a region's crop lies inside the source as it is sized NOW.
        ///
        /// A region remembers the source size it was drawn on. If the source
        /// has since been resized there are two sensible answers:
        ///   proportional = true  : the picture is stretched (OBS projector,
        ///                          demo), so the crop stretches with it.
        ///   proportional = false : the layout is re-flowed at the same pixel
        ///                          size (the game client), so the crop keeps
        ///                          its pixel position and is only pushed
        ///                          back inside if it would fall off the edge.
        /// </summary>
        internal static Rectangle CurrentCrop(MirrorRegion region, Size current, bool proportional)
        {
            if (current.Width < 1 || current.Height < 1) return Rectangle.Empty;

            if (proportional && (current.Width != region.SourceWidth || current.Height != region.SourceHeight))
            {
                int left = (int)((long)region.CropX * current.Width / region.SourceWidth);
                int top = (int)((long)region.CropY * current.Height / region.SourceHeight);
                int right = (int)((long)(region.CropX + region.CropWidth) * current.Width / region.SourceWidth);
                int bottom = (int)((long)(region.CropY + region.CropHeight) * current.Height / region.SourceHeight);
                return Rectangle.FromLTRB(left, top, Math.Max(left + 1, right), Math.Max(top + 1, bottom));
            }

            int width = Math.Min(region.CropWidth, current.Width);
            int height = Math.Min(region.CropHeight, current.Height);
            return new Rectangle(
                Clamp(region.CropX, 0, current.Width - width),
                Clamp(region.CropY, 0, current.Height - height),
                width, height);
        }

        /// <summary>
        /// Moves a region's crop by (dx, dy) source pixels, or - with
        /// <paramref name="resize"/> - grows or shrinks it by that much from
        /// its bottom-right corner. The crop always stays inside the source.
        /// This is what the arrow keys do on an unlocked mirror.
        /// </summary>
        internal static void Nudge(MirrorRegion region, int dx, int dy, bool resize)
        {
            if (resize)
            {
                region.CropWidth = Clamp(region.CropWidth + dx, 1, region.SourceWidth - region.CropX);
                region.CropHeight = Clamp(region.CropHeight + dy, 1, region.SourceHeight - region.CropY);
            }
            else
            {
                region.CropX = Clamp(region.CropX + dx, 0, region.SourceWidth - region.CropWidth);
                region.CropY = Clamp(region.CropY + dy, 0, region.SourceHeight - region.CropHeight);
            }
        }

        /// <summary>
        /// The next rectangle of an evenly spaced series: the same size as
        /// <paramref name="last"/>, as far from it as it is from
        /// <paramref name="previous"/>. With no previous rectangle the series
        /// continues straight down, touching. Returns an empty rectangle if
        /// the result would not fit inside the source.
        /// </summary>
        internal static Rectangle NextInSeries(Rectangle? previous, Rectangle last, Size bounds)
        {
            int dx = previous.HasValue ? last.X - previous.Value.X : 0;
            int dy = previous.HasValue ? last.Y - previous.Value.Y : last.Height;
            if (dx == 0 && dy == 0) return Rectangle.Empty;
            Rectangle next = new Rectangle(last.X + dx, last.Y + dy, last.Width, last.Height);
            bool fits = next.Left >= 0 && next.Top >= 0 && next.Right <= bounds.Width && next.Bottom <= bounds.Height;
            return fits ? next : Rectangle.Empty;
        }

        /// <summary>
        /// Where a dragged rectangle should land so that its edges line up
        /// with nearby rectangles. Each axis snaps on its own, to the closest
        /// edge within <paramref name="threshold"/> pixels, and only against
        /// rectangles that are close on the other axis too - a mirror far away
        /// must not pull this one around.
        /// </summary>
        internal static Point Snap(Rectangle moving, IList<Rectangle> others, int threshold)
        {
            int bestX = threshold + 1, bestY = threshold + 1, shiftX = 0, shiftY = 0;
            foreach (Rectangle other in others)
            {
                bool nearVertically = moving.Top <= other.Bottom + threshold && moving.Bottom >= other.Top - threshold;
                bool nearHorizontally = moving.Left <= other.Right + threshold && moving.Right >= other.Left - threshold;
                if (nearVertically)
                    foreach (int delta in new[] { other.Left - moving.Left, other.Right - moving.Left, other.Left - moving.Right, other.Right - moving.Right })
                        if (Math.Abs(delta) < bestX) { bestX = Math.Abs(delta); shiftX = delta; }
                if (nearHorizontally)
                    foreach (int delta in new[] { other.Top - moving.Top, other.Bottom - moving.Top, other.Top - moving.Bottom, other.Bottom - moving.Bottom })
                        if (Math.Abs(delta) < bestY) { bestY = Math.Abs(delta); shiftY = delta; }
            }
            return new Point(moving.X + (bestX <= threshold ? shiftX : 0), moving.Y + (bestY <= threshold ? shiftY : 0));
        }

        /// <summary>Size of the mirrored picture for a crop shown at a zoom percentage.</summary>
        internal static Size Output(Size crop, int scalePercent)
        {
            return new Size(
                Clamp((int)((long)crop.Width * scalePercent / 100), MinSide, MaxSide),
                Clamp((int)((long)crop.Height * scalePercent / 100), MinSide, MaxSide));
        }

        /// <summary>The zoom percentage that makes a crop this many pixels wide.</summary>
        internal static int ScaleForWidth(int pictureWidth, int cropWidth)
        {
            if (cropWidth < 1) return 100;
            return Clamp((int)Math.Round(pictureWidth * 100.0 / cropWidth), MirrorRegion.MinScale, MirrorRegion.MaxScale);
        }

        /// <summary>Largest rectangle with the content's proportions that fits centred in the box.</summary>
        internal static Rectangle Fit(Size content, Rectangle box)
        {
            if (content.Width < 1 || content.Height < 1 || box.Width < 1 || box.Height < 1) return Rectangle.Empty;
            double factor = Math.Min((double)box.Width / content.Width, (double)box.Height / content.Height);
            int width = Math.Max(1, (int)(content.Width * factor));
            int height = Math.Max(1, (int)(content.Height * factor));
            return new Rectangle(box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2, width, height);
        }

        internal static int Clamp(int value, int minimum, int maximum)
        {
            if (maximum < minimum) return minimum;
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }
}

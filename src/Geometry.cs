// SPDX-License-Identifier: GPL-3.0-only
//
// Geometry.cs - all rectangle arithmetic, with no Windows calls.
//
// Everything here is a pure function so it can be unit tested (tests/Checks.cs)
// and reasoned about without running the app.

using System;
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

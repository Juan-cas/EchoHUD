// SPDX-License-Identifier: GPL-3.0-only
//
// Model.cs - the data a user's layout is made of, and nothing else.
//
// These two classes are what gets written to disk (see Store.cs). They are
// public with public fields only because XmlSerializer requires it.
//
// Two coordinate spaces appear here - keep them apart:
//   * Crop*  : pixels inside the SOURCE window's client area.
//   * X, Y   : pixels on the DESKTOP, where the mirror window is placed.

using System;
using System.Collections.Generic;
using System.IO;

namespace EchoHud
{
    /// <summary>One mirrored rectangle: what to copy, and where and how to show it.</summary>
    public class MirrorRegion
    {
        public const int MinScale = 25, MaxScale = 800;
        public const int MinOpacity = 20, MaxOpacity = 100;

        public string Name = "";

        // What to show: a rectangle inside the source window...
        public int CropX, CropY, CropWidth = 64, CropHeight = 64;

        // ...and how big that source window was when the rectangle was drawn.
        // Needed to keep the crop meaningful if the source is later resized.
        public int SourceWidth = 1920, SourceHeight = 1080;

        // Where and how to show it.
        public int X = 100, Y = 100;
        public int ScalePercent = 100;
        public int OpacityPercent = 100;
        public bool Visible = true;

        /// <summary>Draw a thin highlight frame around the mirror even when locked.</summary>
        public bool Border;

        /// <summary>Throws if a value is outside what the program can safely display.</summary>
        public void Validate()
        {
            if (Name == null || Name.Length > 60) throw new InvalidDataException("Region name is missing or too long.");
            if (SourceWidth < 16 || SourceHeight < 16 || SourceWidth > 20000 || SourceHeight > 20000)
                throw new InvalidDataException("Region source size is out of range.");
            if (CropWidth < 1 || CropHeight < 1 || CropX < 0 || CropY < 0
                || (long)CropX + CropWidth > SourceWidth || (long)CropY + CropHeight > SourceHeight)
                throw new InvalidDataException("Region crop does not fit inside its source.");
            if (ScalePercent < MinScale || ScalePercent > MaxScale) throw new InvalidDataException("Region scale is out of range.");
            if (OpacityPercent < MinOpacity || OpacityPercent > MaxOpacity) throw new InvalidDataException("Region opacity is out of range.");
            if (Math.Abs((long)X) > 100000 || Math.Abs((long)Y) > 100000) throw new InvalidDataException("Region position is out of range.");
        }

        public MirrorRegion Clone()
        {
            return (MirrorRegion)MemberwiseClone();
        }
    }

    /// <summary>A named layout: which source to mirror and the regions taken from it.</summary>
    public class Profile
    {
        public const int MaxRegions = 32;

        public int Version = 1;
        public string Name = "Default";
        public SourceKind Source = SourceKind.Tibia;

        /// <summary>
        /// Remembered list label, used to find the same OBS projector again.
        /// For Tibia this is always the generic "Tibia" label, never a character name.
        /// </summary>
        public string SourceLabel = "";

        /// <summary>Locked mirrors cannot be moved and let mouse clicks pass through.</summary>
        public bool Locked;

        /// <summary>Master switch for showing or hiding every mirror at once.</summary>
        public bool MirrorsVisible = true;

        public List<MirrorRegion> Regions = new List<MirrorRegion>();

        public void Validate()
        {
            if (!Store.IsValidName(Name)) throw new InvalidDataException("Profile name is not valid.");
            if (SourceLabel == null || SourceLabel.Length > 300) throw new InvalidDataException("Profile source label is not valid.");
            if (!Enum.IsDefined(typeof(SourceKind), Source)) throw new InvalidDataException("Profile source kind is not valid.");
            if (Regions == null || Regions.Count > MaxRegions) throw new InvalidDataException("Profile has too many regions.");
            foreach (MirrorRegion region in Regions)
            {
                if (region == null) throw new InvalidDataException("Profile contains an empty region.");
                region.Validate();
            }
        }
    }
}

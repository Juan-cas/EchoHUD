// SPDX-License-Identifier: GPL-3.0-only
//
// RegionRow.cs - one line in the main panel's list, representing one mirror.
//
//   +----------------------------------------------------------+
//   | Cooldowns                          [rename][eye][crop][x] |
//   | Size  ----o-----  150 %    Opacity  --------o  100 %      |
//   +----------------------------------------------------------+
//
// The row edits the MirrorRegion directly and raises Changed; the main window
// then updates the mirror and saves. The row never talks to the mirror itself.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace EchoHud
{
    internal sealed class RegionRow : Panel
    {
        internal readonly MirrorRegion Spec;

        readonly Label name = new Label();
        readonly TextBox editor = new TextBox();
        readonly IconButton visibility;
        readonly Slider size, opacity;
        readonly Label sizeValue = new Label(), opacityValue = new Label();
        readonly ToolTip tips;

        /// <summary>The user changed something about the region through this row.</summary>
        internal event EventHandler Changed;
        internal event EventHandler RecropRequested;
        internal event EventHandler DeleteRequested;

        internal RegionRow(MirrorRegion region, ToolTip tips, int width)
        {
            Spec = region;
            this.tips = tips;
            BackColor = Theme.Surface;
            Size = new Size(width, Theme.Px(74));

            // ---- first line: name and action buttons ----
            int button = Theme.Px(28), gap = Theme.Px(4), right = width - Theme.Px(8);
            IconButton delete = MakeButton(Theme.IconDelete, "row.delete", delegate { Raise(DeleteRequested); }, ref right, button, gap);
            IconButton recrop = MakeButton(Theme.IconCrop, "row.recrop", delegate { Raise(RecropRequested); }, ref right, button, gap);
            visibility = MakeButton(Theme.IconShow, "row.hide", delegate
            {
                Spec.Visible = !Spec.Visible;
                Sync();
                Raise(Changed);
            }, ref right, button, gap);
            IconButton rename = MakeButton(Theme.IconRename, "row.rename", delegate { BeginRename(); }, ref right, button, gap);

            Rectangle nameArea = new Rectangle(Theme.Px(12), Theme.Px(9), right - Theme.Px(16), Theme.Px(24));
            name.Bounds = nameArea;
            name.Font = Theme.Bold;
            name.ForeColor = Theme.TextColor;
            name.AutoEllipsis = true;
            name.TextAlign = ContentAlignment.MiddleLeft;
            name.DoubleClick += delegate { BeginRename(); };

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

            // ---- second line: size and opacity ----
            int y = Theme.Px(44);
            size = new Slider(MirrorRegion.MinScale, 400, 5, Strings.Get("row.size"));
            opacity = new Slider(MirrorRegion.MinOpacity, MirrorRegion.MaxOpacity, 5, Strings.Get("row.opacity"));
            PlaceSetting(Strings.Get("row.size"), size, sizeValue, Theme.Px(12), y, Theme.Px(52), Theme.Px(74));
            PlaceSetting(Strings.Get("row.opacity"), opacity, opacityValue, Theme.Px(190), y, Theme.Px(58), Theme.Px(66));
            size.ValueChanged += delegate { Spec.ScalePercent = size.Value; Sync(); Raise(Changed); };
            opacity.ValueChanged += delegate { Spec.OpacityPercent = opacity.Value; Sync(); Raise(Changed); };

            Controls.AddRange(new Control[] { name, editor, rename, visibility, recrop, delete });
            Sync();
        }

        IconButton MakeButton(string glyph, string textKey, EventHandler click, ref int right, int side, int gap)
        {
            IconButton button = new IconButton(glyph, Strings.Get(textKey), click);
            button.Size = new Size(side, side);
            button.BackColor = Theme.Surface;
            right -= side;
            button.Location = new Point(right, Theme.Px(7));
            right -= gap;
            tips.SetToolTip(button, Strings.Get(textKey));
            return button;
        }

        void PlaceSetting(string caption, Slider slider, Label value, int x, int y, int captionWidth, int sliderWidth)
        {
            Label label = new Label
            {
                Text = caption,
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(x, y, captionWidth, Theme.Px(22))
            };
            slider.Bounds = new Rectangle(x + captionWidth, y, sliderWidth, Theme.Px(22));
            value.Font = Theme.Small;
            value.ForeColor = Theme.TextColor;
            value.TextAlign = ContentAlignment.MiddleLeft;
            value.Bounds = new Rectangle(x + captionWidth + sliderWidth + Theme.Px(4), y, Theme.Px(44), Theme.Px(22));
            Controls.AddRange(new Control[] { label, slider, value });
        }

        /// <summary>Refreshes the controls from the region (call after anything else changed it).</summary>
        internal void Sync()
        {
            name.Text = Spec.Name;
            name.ForeColor = Spec.Visible ? Theme.TextColor : Theme.Muted;
            size.Value = Spec.ScalePercent;
            opacity.Value = Spec.OpacityPercent;
            sizeValue.Text = Spec.ScalePercent + " %";
            opacityValue.Text = Spec.OpacityPercent + " %";
            visibility.Text = Spec.Visible ? Theme.IconShow : Theme.IconHide;
            visibility.Active = !Spec.Visible;
            string tip = Strings.Get(Spec.Visible ? "row.hide" : "row.show");
            visibility.AccessibleName = tip;
            tips.SetToolTip(visibility, tip);
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

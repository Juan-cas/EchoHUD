// SPDX-License-Identifier: GPL-3.0-only
//
// MainForm.cs - the control panel, and the conductor of everything else.
//
//   +------------------------------------------+
//   | (o) Source  [ Tibia                  v ] |   which window to mirror
//   | Tibia connected. Mirrors are live.       |   one-line status / hints
//   | [ + Add region ]   [eye] [lock]   2 / 32 |   the three main actions
//   | +--------------------------------------+ |
//   | | Cooldowns            [..][..][..][..] | |   one RegionRow per mirror
//   | | Size --o--  Opacity ------o           | |
//   | +--------------------------------------+ |
//   | Profile [ Default  v ] [+] [x]     About |   saved layouts
//   +------------------------------------------+
//
// RESPONSIBILITIES
//   * owns the current Profile and one MirrorWindow per region in it
//   * every 250 ms: checks which source windows exist, re-attaches mirrors if
//     the source restarted, shows/hides mirrors, and saves pending changes
//   * turns button presses into changes on the Profile
//
// There is no Save button: every change is written to disk within a moment.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace EchoHud
{
    internal sealed class MainForm : Form
    {
        internal const string Version = "1.0.0";

        /// <summary>One entry of the source drop-down: a live window, or a "not running" placeholder.</summary>
        sealed class SourceOption
        {
            internal SourceKind Kind;
            internal SourceWindow Window; // null for a placeholder
            internal string Label;
            public override string ToString() { return Label; }
        }

        // ---- state ----
        Profile profile = new Profile();
        readonly List<MirrorWindow> mirrors = new List<MirrorWindow>();
        readonly List<RegionRow> rows = new List<RegionRow>();
        SourceWindow current;        // the window mirrors are attached to, or null
        string sourceSignature = ""; // fingerprint of the drop-down contents, to avoid needless rebuilds
        bool dirty;                  // profile has unsaved changes
        bool selecting;              // the region selector is open
        bool rescan = true;          // look for sources on the next tick instead of waiting
        int tick;
        string flashText;            // temporary status message...
        DateTime flashUntil;         // ...and when it expires

        // ---- controls ----
        readonly Timer timer = new Timer();
        readonly ToolTip tips = new ToolTip();
        readonly Label statusDot = new Label(), status = new Label(), count = new Label(), empty = new Label();
        readonly ComboBox sourceList = new ComboBox(), profileList = new ComboBox();
        readonly Panel list = new Panel();
        Button add;
        IconButton showAll, lockAll;

        internal MainForm()
        {
            BuildWindow();
            OpenProfile(LoadStartupProfile());
            timer.Interval = 250;
            timer.Tick += delegate { Tick(); };
            timer.Start();
        }

        // ------------------------------------------------------------------
        // Building the window
        // ------------------------------------------------------------------

        void BuildWindow()
        {
            Text = "Echo HUD";
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Background;
            ForeColor = Theme.TextColor;
            Font = Theme.Body;
            ClientSize = new Size(Theme.Px(400), Theme.Px(560));
            Rectangle desk = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(desk.Left + Theme.Px(40), desk.Top + Theme.Px(40));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (Exception) { /* a missing icon is not worth failing over */ }

            int margin = Theme.Px(14), width = ClientSize.Width - 2 * margin;

            // ---- source row ----
            statusDot.Text = "●"; // filled circle
            statusDot.Bounds = new Rectangle(margin - Theme.Px(2), Theme.Px(14), Theme.Px(18), Theme.Px(24));
            statusDot.TextAlign = ContentAlignment.MiddleCenter;
            Label sourceCaption = Caption(Strings.Get("source.label"), margin + Theme.Px(20), Theme.Px(14), Theme.Px(58));
            StyleCombo(sourceList, Strings.Get("source.label"));
            sourceList.Bounds = new Rectangle(margin + Theme.Px(80), Theme.Px(13), width - Theme.Px(80), Theme.Px(26));
            sourceList.SelectionChangeCommitted += delegate { SourceChosen(); };

            status.Bounds = new Rectangle(margin, Theme.Px(46), width, Theme.Px(34));
            status.Font = Theme.Small;
            status.ForeColor = Theme.Muted;

            // ---- toolbar ----
            int toolbarY = Theme.Px(86);
            add = Dialogs.TextButton("+  " + Strings.Get("action.add"), true);
            add.Bounds = new Rectangle(margin, toolbarY, Theme.Px(170), Theme.Px(34));
            add.Click += delegate { AddRegion(); };
            tips.SetToolTip(add, Strings.Get("action.addTip"));

            showAll = new IconButton(Theme.IconShow, Strings.Get("action.hideAll"), delegate { ToggleAllVisible(); });
            showAll.Location = new Point(margin + Theme.Px(182), toolbarY + Theme.Px(2));
            lockAll = new IconButton(Theme.IconUnlock, Strings.Get("action.lock"), delegate { SetLocked(!profile.Locked); });
            lockAll.Location = new Point(margin + Theme.Px(218), toolbarY + Theme.Px(2));

            count.Bounds = new Rectangle(ClientSize.Width - margin - Theme.Px(80), toolbarY, Theme.Px(80), Theme.Px(34));
            count.TextAlign = ContentAlignment.MiddleRight;
            count.ForeColor = Theme.Muted;

            // ---- list of regions ----
            int listTop = toolbarY + Theme.Px(46), footer = Theme.Px(52);
            list.Bounds = new Rectangle(margin, listTop, width, ClientSize.Height - listTop - footer);
            list.AutoScroll = true;
            empty.Text = Strings.Get("list.empty");
            empty.ForeColor = Theme.Muted;
            empty.TextAlign = ContentAlignment.MiddleCenter;
            empty.Dock = DockStyle.Fill;
            list.Controls.Add(empty);

            // ---- profile row ----
            int footerY = ClientSize.Height - Theme.Px(40);
            Label profileCaption = Caption(Strings.Get("profile.label"), margin, footerY, Theme.Px(50));
            StyleCombo(profileList, Strings.Get("profile.label"));
            profileList.Bounds = new Rectangle(margin + Theme.Px(52), footerY - Theme.Px(1), Theme.Px(150), Theme.Px(26));
            profileList.SelectionChangeCommitted += delegate { SwitchProfile(profileList.SelectedItem as string); };
            IconButton newProfile = new IconButton(Theme.IconAdd, Strings.Get("profile.new"), delegate { NewProfile(); });
            newProfile.Location = new Point(margin + Theme.Px(208), footerY - Theme.Px(3));
            IconButton deleteProfile = new IconButton(Theme.IconDelete, Strings.Get("profile.delete"), delegate { DeleteProfile(); });
            deleteProfile.Location = new Point(margin + Theme.Px(242), footerY - Theme.Px(3));
            tips.SetToolTip(newProfile, Strings.Get("profile.new"));
            tips.SetToolTip(deleteProfile, Strings.Get("profile.delete"));

            LinkLabel about = new LinkLabel
            {
                Text = Strings.Get("action.about"),
                Font = Theme.Small,
                LinkColor = Theme.Muted,
                ActiveLinkColor = Theme.Accent,
                LinkBehavior = LinkBehavior.HoverUnderline,
                TextAlign = ContentAlignment.MiddleRight,
                Bounds = new Rectangle(ClientSize.Width - margin - Theme.Px(112), footerY, Theme.Px(112), Theme.Px(24))
            };
            about.LinkClicked += delegate
            {
                MessageBox.Show(this, Strings.Format("about.text", Version), "Echo HUD", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            Controls.AddRange(new Control[]
            {
                statusDot, sourceCaption, sourceList, status, add, showAll, lockAll, count, list,
                profileCaption, profileList, newProfile, deleteProfile, about
            });
            ActiveControl = add; // so Enter/Space on a fresh window starts the main action
        }

        static Label Caption(string text, int x, int y, int width)
        {
            return new Label
            {
                Text = text,
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(x, y, width, Theme.Px(24))
            };
        }

        static void StyleCombo(ComboBox combo, string accessibleName)
        {
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = Theme.Raised;
            combo.ForeColor = Theme.TextColor;
            combo.AccessibleName = accessibleName;
        }

        // ------------------------------------------------------------------
        // The heartbeat
        // ------------------------------------------------------------------

        void Tick()
        {
            tick++;
            if (rescan || tick % 4 == 0) RefreshSources(); // once a second, or right away when asked
            UpdateMirrors();
            if (dirty) Save();
            UpdateStatus();
        }

        /// <summary>Finds the source windows that exist now and attaches the mirrors to the right one.</summary>
        void RefreshSources()
        {
            rescan = false;
            List<SourceWindow> found = Sources.Discover();
            current = SourcePolicy.Choose(found, profile.Source, profile.SourceLabel, current == null ? IntPtr.Zero : current.Handle);
            if (current != null && profile.SourceLabel != current.Label)
            {
                profile.SourceLabel = current.Label;
                dirty = true;
            }
            foreach (MirrorWindow mirror in mirrors) mirror.Bind(current);
            if (!sourceList.DroppedDown) RebuildSourceList(found);
        }

        /// <summary>
        /// Fills the drop-down. Each kind of source is always listed, as live
        /// windows when they exist or as one "not running" entry otherwise,
        /// so the user can choose what to wait for.
        /// </summary>
        void RebuildSourceList(List<SourceWindow> found)
        {
            List<SourceOption> options = new List<SourceOption>();
            AddOptions(options, found, SourceKind.Tibia, Strings.Get("source.tibiaMissing"));
            AddOptions(options, found, SourceKind.ObsProjector, Strings.Get("source.obsMissing"));
            AddOptions(options, found, SourceKind.Demo, Strings.Get("source.demo"));

            StringBuilder signature = new StringBuilder();
            SourceOption selected = null;
            foreach (SourceOption option in options)
            {
                signature.Append(option.Label).Append('|').Append(option.Window == null ? IntPtr.Zero : option.Window.Handle).Append(';');
                bool isCurrent = current != null && option.Window != null && option.Window.Handle == current.Handle;
                if (isCurrent || (selected == null && current == null && option.Kind == profile.Source)) selected = option;
            }
            signature.Append(selected == null ? "" : selected.Label);
            if (signature.ToString() == sourceSignature) return;
            sourceSignature = signature.ToString();

            sourceList.BeginUpdate();
            sourceList.Items.Clear();
            foreach (SourceOption option in options) sourceList.Items.Add(option);
            sourceList.SelectedItem = selected;
            sourceList.EndUpdate();
        }

        static void AddOptions(List<SourceOption> options, List<SourceWindow> found, SourceKind kind, string missingLabel)
        {
            int before = options.Count;
            foreach (SourceWindow window in found)
                if (window.Kind == kind) options.Add(new SourceOption { Kind = kind, Window = window, Label = window.Label });
            if (options.Count == before) options.Add(new SourceOption { Kind = kind, Label = missingLabel });
        }

        void SourceChosen()
        {
            SourceOption option = sourceList.SelectedItem as SourceOption;
            if (option == null) return;
            profile.Source = option.Kind;
            profile.SourceLabel = option.Window == null ? "" : option.Window.Label;
            current = option.Window;
            if (option.Kind == SourceKind.Demo && !DemoWindow.IsOpen) DemoWindow.Open();
            dirty = rescan = true;
            flashText = null;
        }

        /// <summary>Shows each mirror only when it has something to show and the user wants it shown.</summary>
        void UpdateMirrors()
        {
            bool sourceReady = Sources.IsUsable(current);
            foreach (MirrorWindow mirror in mirrors)
            {
                bool wanted = sourceReady && !selecting && profile.MirrorsVisible && mirror.Spec.Visible;
                if (wanted && !mirror.Visible) mirror.Show();
                else if (!wanted && mirror.Visible) mirror.Hide();
                mirror.FollowSource();
            }
        }

        void UpdateStatus()
        {
            bool ready = Sources.IsUsable(current);
            statusDot.ForeColor = ready ? Theme.Accent : Theme.Warning;
            if (flashText != null && DateTime.UtcNow < flashUntil) { status.Text = flashText; return; }
            flashText = null;

            if (ready) status.Text = Strings.Format("status.live", current.Label);
            else if (current != null) status.Text = Strings.Get("status.minimized");
            else if (profile.Source == SourceKind.Tibia) status.Text = Strings.Get("status.waitTibia");
            else if (profile.Source == SourceKind.ObsProjector) status.Text = Strings.Get("status.waitObs");
            else status.Text = Strings.Get("status.waitDemo");
        }

        /// <summary>Shows a message in the status line for a few seconds.</summary>
        void Flash(string text)
        {
            flashText = text;
            flashUntil = DateTime.UtcNow.AddSeconds(6);
            status.Text = text;
        }

        // ------------------------------------------------------------------
        // Regions
        // ------------------------------------------------------------------

        void AddRegion()
        {
            if (profile.Regions.Count >= Profile.MaxRegions)
            {
                Flash(Strings.Format("status.limit", Profile.MaxRegions));
                return;
            }
            Rectangle crop;
            Size sourceSize;
            if (!PickArea(out crop, out sourceSize)) return;

            MirrorRegion region = new MirrorRegion
            {
                Name = Strings.Format("row.defaultName", profile.Regions.Count + 1),
                CropX = crop.X,
                CropY = crop.Y,
                CropWidth = crop.Width,
                CropHeight = crop.Height,
                SourceWidth = sourceSize.Width,
                SourceHeight = sourceSize.Height
            };
            // Start the mirror just beside the thing it mirrors, so it is easy to spot.
            Rectangle origin = Native.ClientScreenBounds(current.Handle);
            Rectangle screen = Screen.FromRectangle(origin).WorkingArea;
            region.X = Geometry.Clamp(origin.X + crop.X + Theme.Px(28), screen.Left, screen.Right - crop.Width);
            region.Y = Geometry.Clamp(origin.Y + crop.Y + Theme.Px(28), screen.Top, screen.Bottom - crop.Height);

            profile.Regions.Add(region);
            if (profile.Locked) SetLocked(false); // a new mirror must be movable
            AttachRegion(region);
            LayoutRows();
            MarkChanged();
            Flash(Strings.Get("status.placed"));
        }

        void RecropRegion(MirrorRegion region)
        {
            Rectangle crop;
            Size sourceSize;
            if (!PickArea(out crop, out sourceSize)) return;
            region.CropX = crop.X;
            region.CropY = crop.Y;
            region.CropWidth = crop.Width;
            region.CropHeight = crop.Height;
            region.SourceWidth = sourceSize.Width;
            region.SourceHeight = sourceSize.Height;
            MirrorFor(region).ApplyRegion();
            MarkChanged();
        }

        /// <summary>Opens the selector over the current source. Explains what is wrong if there is none.</summary>
        bool PickArea(out Rectangle crop, out Size sourceSize)
        {
            crop = Rectangle.Empty;
            sourceSize = Size.Empty;
            if (profile.Source == SourceKind.Demo && !DemoWindow.IsOpen) DemoWindow.Open();
            RefreshSources();
            if (!Sources.IsUsable(current))
            {
                string key = current != null ? "help.noSource.minimized"
                    : profile.Source == SourceKind.ObsProjector ? "help.noSource.obs" : "help.noSource.tibia";
                MessageBox.Show(this, Strings.Get(key), "Echo HUD", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            selecting = true;
            UpdateMirrors(); // hide mirrors so they are not in the way
            try
            {
                return RegionSelector.Pick(this, current, out crop, out sourceSize);
            }
            finally
            {
                selecting = false;
                UpdateMirrors();
            }
        }

        void DeleteRegion(MirrorRegion region)
        {
            if (MessageBox.Show(this, Strings.Format("region.deleteConfirm", region.Name), "Echo HUD",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            MirrorWindow mirror = MirrorFor(region);
            RegionRow row = RowFor(region);
            mirrors.Remove(mirror);
            rows.Remove(row);
            list.Controls.Remove(row);
            mirror.Close();
            row.Dispose();
            profile.Regions.Remove(region);
            LayoutRows();
            MarkChanged();
        }

        /// <summary>Creates the mirror window and the list row for a region and wires them together.</summary>
        void AttachRegion(MirrorRegion region)
        {
            MirrorWindow mirror = new MirrorWindow(region);
            mirror.SetLocked(profile.Locked);
            mirror.Bind(current);
            mirror.ApplyRegion();
            int rowWidth = list.Width - SystemInformation.VerticalScrollBarWidth - Theme.Px(2);
            RegionRow row = new RegionRow(region, tips, rowWidth);

            // Changes made on the mirror (drag, resize, right-click menu) update the row...
            mirror.Edited += delegate { row.Sync(); MarkChanged(); };
            mirror.RecropRequested += delegate { RecropRegion(region); };
            mirror.DeleteRequested += delegate { DeleteRegion(region); };
            mirror.RenameRequested += delegate
            {
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                Activate();
                list.ScrollControlIntoView(row);
                row.BeginRename();
            };
            // ...and changes made in the row update the mirror.
            row.Changed += delegate { mirror.ApplyRegion(); MarkChanged(); };
            row.RecropRequested += delegate { RecropRegion(region); };
            row.DeleteRequested += delegate { DeleteRegion(region); };

            mirrors.Add(mirror);
            rows.Add(row);
            list.Controls.Add(row);
        }

        MirrorWindow MirrorFor(MirrorRegion region)
        {
            return mirrors.Find(delegate(MirrorWindow m) { return m.Spec == region; });
        }

        RegionRow RowFor(MirrorRegion region)
        {
            return rows.Find(delegate(RegionRow r) { return r.Spec == region; });
        }

        /// <summary>Stacks the rows top to bottom and refreshes the toolbar around them.</summary>
        void LayoutRows()
        {
            list.SuspendLayout();
            list.AutoScrollPosition = Point.Empty;
            int y = 0;
            foreach (RegionRow row in rows)
            {
                row.Location = new Point(0, y);
                y += row.Height + Theme.Px(6);
            }
            empty.Visible = rows.Count == 0;
            list.ResumeLayout();
            UpdateToolbar();
        }

        void UpdateToolbar()
        {
            count.Text = Strings.Format("list.count", profile.Regions.Count, Profile.MaxRegions);

            lockAll.Text = profile.Locked ? Theme.IconLock : Theme.IconUnlock;
            lockAll.Active = profile.Locked;
            string lockTip = Strings.Get(profile.Locked ? "action.unlock" : "action.lock");
            lockAll.AccessibleName = lockTip;
            tips.SetToolTip(lockAll, lockTip);

            showAll.Text = profile.MirrorsVisible ? Theme.IconShow : Theme.IconHide;
            showAll.Active = !profile.MirrorsVisible;
            string showTip = Strings.Get(profile.MirrorsVisible ? "action.hideAll" : "action.showAll");
            showAll.AccessibleName = showTip;
            tips.SetToolTip(showAll, showTip);
        }

        void SetLocked(bool locked)
        {
            profile.Locked = locked;
            foreach (MirrorWindow mirror in mirrors) mirror.SetLocked(locked);
            UpdateToolbar();
            MarkChanged();
            Flash(Strings.Get(locked ? "status.locked" : "status.unlocked"));
        }

        void ToggleAllVisible()
        {
            profile.MirrorsVisible = !profile.MirrorsVisible;
            UpdateToolbar();
            MarkChanged();
        }

        /// <summary>Records that the profile changed; the next tick shows/hides mirrors and saves.</summary>
        void MarkChanged()
        {
            dirty = true;
            UpdateMirrors();
        }

        // ------------------------------------------------------------------
        // Profiles
        // ------------------------------------------------------------------

        Profile LoadStartupProfile()
        {
            string name = Store.LastProfile ?? "Default";
            if (Store.Exists(name))
            {
                try { return Store.Load(name); }
                catch (Exception error) { Flash(Strings.Format("status.loadFailed", error.Message)); }
            }
            // First run (or unreadable file): start empty, under a name that does not overwrite anything.
            string fresh = "Default";
            for (int i = 2; Store.Exists(fresh); i++) fresh = "Default " + i;
            return new Profile { Name = fresh };
        }

        /// <summary>Replaces everything on screen with the given profile.</summary>
        void OpenProfile(Profile next)
        {
            foreach (MirrorWindow mirror in mirrors) mirror.Close();
            foreach (RegionRow row in rows) { list.Controls.Remove(row); row.Dispose(); }
            mirrors.Clear();
            rows.Clear();

            profile = next;
            current = null;
            if (profile.Source == SourceKind.Demo && !DemoWindow.IsOpen) DemoWindow.Open();
            RefreshSources();
            foreach (MirrorRegion region in profile.Regions) AttachRegion(region);
            LayoutRows();
            RefreshProfileList();
            Store.LastProfile = profile.Name;
            UpdateMirrors();
        }

        void RefreshProfileList()
        {
            List<string> names = Store.ListProfiles();
            if (!names.Contains(profile.Name)) names.Insert(0, profile.Name);
            profileList.BeginUpdate();
            profileList.Items.Clear();
            foreach (string name in names) profileList.Items.Add(name);
            profileList.SelectedItem = profile.Name;
            profileList.EndUpdate();
        }

        void SwitchProfile(string name)
        {
            if (name == null || name == profile.Name) return;
            Save();
            try
            {
                OpenProfile(Store.Load(name));
            }
            catch (Exception error)
            {
                Flash(Strings.Format("status.loadFailed", error.Message));
                RefreshProfileList();
            }
        }

        void NewProfile()
        {
            string name = Dialogs.AskText(this, Strings.Get("profile.new"), Strings.Get("profile.newPrompt"), "");
            if (name == null) return;
            if (!Store.IsValidName(name) || Store.Exists(name) || name == profile.Name)
            {
                Flash(Strings.Get("status.badName"));
                return;
            }
            Save();
            Profile fresh = new Profile { Name = name, Source = profile.Source, SourceLabel = profile.SourceLabel };
            OpenProfile(fresh);
            dirty = true;
        }

        void DeleteProfile()
        {
            if (MessageBox.Show(this, Strings.Format("profile.deleteConfirm", profile.Name), "Echo HUD",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                Store.Delete(profile.Name);
            }
            catch (Exception error)
            {
                Flash(Strings.Format("status.saveFailed", error.Message));
                return;
            }
            dirty = false;
            List<string> remaining = Store.ListProfiles();
            Profile next = null;
            foreach (string name in remaining)
            {
                try { next = Store.Load(name); break; }
                catch (Exception) { /* skip unreadable profiles */ }
            }
            OpenProfile(next ?? new Profile { Name = "Default" });
        }

        void Save()
        {
            dirty = false;
            try
            {
                Store.Save(profile);
                Store.LastProfile = profile.Name;
            }
            catch (Exception error)
            {
                Flash(Strings.Format("status.saveFailed", error.Message));
            }
        }

        // ------------------------------------------------------------------
        // Shutting down
        // ------------------------------------------------------------------

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timer.Stop();
            if (dirty) Save();
            foreach (MirrorWindow mirror in mirrors.ToArray()) mirror.Close();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                tips.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

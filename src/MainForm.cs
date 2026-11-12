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
//   | | Potion     [play][time][key][..][x]  | |   one TimerRow per timer
//   | +--------------------------------------+ |
//   | Profile [ Default  v ] [+] [x]    [gear] |   saved layouts, settings menu
//   +------------------------------------------+
//
// The panel also lives in the notification area (the "tray", next to the
// clock): minimising hides it there, and the icon's menu can lock, show/hide
// and exit without opening the panel.
//
// RESPONSIBILITIES
//   * owns the current Profile, one MirrorWindow per region and one
//     TimerWindow per cooldown timer in it
//   * owns the Settings of this computer (see Model.cs) and the settings menu
//   * registers the hotkeys (timers, lock, show/hide) and acts when Windows
//     reports one
//   * every 250 ms: checks which source windows exist, re-attaches mirrors if
//     the source restarted, shows/hides mirrors, and saves pending changes
//   * turns button presses into changes on the Profile
//
// There is no Save button: every change is written to disk within a moment.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace EchoHud
{
    internal sealed class MainForm : Form
    {
        internal const string Version = "1.2.0";
        const string ReleasesPage = "https://github.com/Juan-cas/EchoHUD/releases";

        // Hotkey ids. Timers use 1..MaxTimers (their list position plus one).
        const int LockHotkeyId = 100, ShowHotkeyId = 101;

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
        readonly List<TimerWindow> timers = new List<TimerWindow>();
        readonly List<TimerRow> timerRows = new List<TimerRow>();
        SourceWindow current;        // the window mirrors are attached to, or null
        string sourceSignature = ""; // fingerprint of the drop-down contents, to avoid needless rebuilds
        bool dirty;                  // profile has unsaved changes
        bool selecting;              // the region selector is open
        bool rescan = true;          // look for sources on the next tick instead of waiting
        int tick;
        string flashText;            // temporary status message...
        DateTime flashUntil;         // ...and when it expires
        Settings settings = Store.LoadSettings();
        bool startHidden;            // launched with Windows: do not show the panel
        bool trayHintShown;          // the "still running here" bubble was shown once
        bool lastInFront = true;     // last answer of GameInFront, kept while Windows reports no window
        readonly int ownProcess = Process.GetCurrentProcess().Id;
        NotifyIcon tray;
        System.Threading.RegisteredWaitHandle wakeWait;

        // ---- controls ----
        readonly Timer timer = new Timer();
        readonly ToolTip tips = new ToolTip();
        readonly Label statusDot = new Label(), status = new Label(), count = new Label(), empty = new Label();
        readonly ComboBox sourceList = new ComboBox(), profileList = new ComboBox();
        readonly Panel list = new Panel();
        Button add;
        IconButton showAll, lockAll, addTimer, settingsButton;

        /// <param name="startHidden">Start in the notification area without showing the panel.</param>
        /// <param name="wake">Signalled when the program is started a second time; we then show the panel.</param>
        internal MainForm(bool startHidden, System.Threading.WaitHandle wake)
        {
            BuildWindow();
            BuildTray();
            if (startHidden && !settings.MinimizeToTray) WindowState = FormWindowState.Minimized;
            else this.startHidden = startHidden;
            OpenProfile(LoadStartupProfile());
            timer.Interval = 250;
            timer.Tick += delegate { Tick(); };
            timer.Start();
            if (wake != null)
                wakeWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(wake, delegate
                {
                    // Runs on a background thread: hand over to the window's own thread.
                    try { BeginInvoke((MethodInvoker)RestorePanel); }
                    catch (InvalidOperationException) { /* the window is closing */ }
                }, null, -1, false);
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
            addTimer = new IconButton(Theme.IconTimer, Strings.Get("action.addTimer"), delegate { AddTimer(); });
            addTimer.Location = new Point(margin + Theme.Px(254), toolbarY + Theme.Px(2));
            tips.SetToolTip(addTimer, Strings.Get("action.addTimer"));

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

            settingsButton = new IconButton(Theme.IconSettings, Strings.Get("settings.title"), delegate { ShowSettingsMenu(); });
            settingsButton.Location = new Point(ClientSize.Width - margin - settingsButton.Width, footerY - Theme.Px(3));
            tips.SetToolTip(settingsButton, Strings.Get("settings.title"));

            Controls.AddRange(new Control[]
            {
                statusDot, sourceCaption, sourceList, status, add, showAll, lockAll, addTimer, count, list,
                profileCaption, profileList, newProfile, deleteProfile, settingsButton
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
            bool sourceReady = Sources.IsUsable(current), inFront = GameInFront();
            foreach (MirrorWindow mirror in mirrors)
            {
                bool wanted = sourceReady && inFront && !selecting && profile.MirrorsVisible && mirror.Spec.Visible;
                if (wanted && !mirror.Visible) mirror.Show();
                else if (!wanted && mirror.Visible) mirror.Hide();
                mirror.FollowSource();
            }
            // Timers do not depend on a source window at all.
            foreach (TimerWindow window in timers)
            {
                bool wanted = inFront && profile.MirrorsVisible && window.Spec.Visible;
                if (wanted && !window.Visible) window.Show();
                else if (!wanted && window.Visible) window.Hide();
            }
        }

        /// <summary>
        /// With the "only while the game is in front" setting: is the window
        /// the user is working in the game, the mirrored source, or one of
        /// our own? This asks Windows which window is in front and looks at
        /// its title and class - the same information used to find the game.
        /// </summary>
        bool GameInFront()
        {
            if (!settings.OnlyWithGame) return true;
            IntPtr front = Native.GetForegroundWindow();
            if (front == IntPtr.Zero) return lastInFront; // briefly the case while switching windows
            uint processId;
            Native.GetWindowThreadProcessId(front, out processId);
            lastInFront = processId == ownProcess
                || (current != null && front == current.Handle)
                || SourcePolicy.IsTibiaWindow(Native.Title(front), Native.ClassName(front));
            return lastInFront;
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
            int room = Profile.MaxRegions - profile.Regions.Count;
            if (room <= 0)
            {
                Flash(Strings.Format("status.limit", Profile.MaxRegions));
                return;
            }
            List<Rectangle> crops;
            Size sourceSize;
            if (!PickAreas(room, out crops, out sourceSize)) return;

            // Start each mirror just beside the thing it mirrors, so it is easy
            // to spot. Using the same offset for all of them keeps areas picked
            // together (say, five health bars) in their original arrangement.
            Rectangle origin = Native.ClientScreenBounds(current.Handle);
            Rectangle screen = Screen.FromRectangle(origin).WorkingArea;
            if (profile.Locked) SetLocked(false); // new mirrors must be movable
            // Areas picked together form a group, so Shift+drag moves them as one.
            string group = "";
            if (crops.Count > 1)
            {
                List<string> taken = profile.GroupNames();
                for (int i = 1; group.Length == 0 || taken.Contains(group); i++) group = Strings.Format("group.defaultName", i);
            }
            foreach (Rectangle crop in crops)
            {
                MirrorRegion region = new MirrorRegion
                {
                    Name = Strings.Format("row.defaultName", profile.Regions.Count + 1),
                    CropX = crop.X,
                    CropY = crop.Y,
                    CropWidth = crop.Width,
                    CropHeight = crop.Height,
                    SourceWidth = sourceSize.Width,
                    SourceHeight = sourceSize.Height,
                    Group = group,
                    X = Geometry.Clamp(origin.X + crop.X + Theme.Px(28), screen.Left, screen.Right - crop.Width),
                    Y = Geometry.Clamp(origin.Y + crop.Y + Theme.Px(28), screen.Top, screen.Bottom - crop.Height)
                };
                profile.Regions.Add(region);
                AttachRegion(region);
            }
            LayoutRows();
            MarkChanged();
            Flash(Strings.Get(crops.Count > 1 ? "status.placedMany" : "status.placed"));
        }

        void RecropRegion(MirrorRegion region)
        {
            List<Rectangle> crops;
            Size sourceSize;
            if (!PickAreas(1, out crops, out sourceSize)) return;
            region.CropX = crops[0].X;
            region.CropY = crops[0].Y;
            region.CropWidth = crops[0].Width;
            region.CropHeight = crops[0].Height;
            region.SourceWidth = sourceSize.Width;
            region.SourceHeight = sourceSize.Height;
            MirrorFor(region).ApplyRegion();
            MarkChanged();
        }

        /// <summary>
        /// Opens the selector over the current source and returns up to
        /// <paramref name="maximum"/> areas. Explains what is wrong if there
        /// is no usable source.
        /// </summary>
        bool PickAreas(int maximum, out List<Rectangle> crops, out Size sourceSize)
        {
            crops = new List<Rectangle>();
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
                return RegionSelector.Pick(this, current, maximum, out crops, out sourceSize);
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
            mirror.Dragged += delegate(int dx, int dy) { MoveCompanions(mirror, region.Group, dx, dy); };
            mirror.SnapTargets = delegate { return SnapTargetsFor(mirror); };
            mirror.GroupMenuOpening += delegate(ToolStripMenuItem parent)
            {
                FillGroupMenu(parent, region.Group, delegate(string name) { region.Group = name; MarkChanged(); });
            };
            mirror.RecropRequested += delegate { RecropRegion(region); };
            mirror.DeleteRequested += delegate { DeleteRegion(region); };
            mirror.RenameRequested += delegate
            {
                RestorePanel();
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
            foreach (TimerRow row in timerRows)
            {
                row.Location = new Point(0, y);
                y += row.Height + Theme.Px(6);
            }
            empty.Visible = rows.Count == 0 && timerRows.Count == 0;
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
            foreach (TimerWindow window in timers) window.SetLocked(locked);
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

        /// <summary>
        /// Called when the user finished dragging a mirror or timer.
        ///   Shift held: the other members of its group move by the same amount.
        ///   Ctrl held:  every other mirror and timer moves, so a finished
        ///               layout can be relocated as one block.
        /// </summary>
        void MoveCompanions(Form moved, string group, int dx, int dy)
        {
            bool everything = (ModifierKeys & Keys.Control) != 0;
            bool groupOnly = !everything && (ModifierKeys & Keys.Shift) != 0 && group.Length > 0;
            if (!everything && !groupOnly) return;
            foreach (MirrorWindow other in mirrors)
            {
                if (other == moved || (groupOnly && other.Spec.Group != group)) continue;
                other.Spec.X += dx;
                other.Spec.Y += dy;
                other.ApplyRegion();
            }
            foreach (TimerWindow other in timers)
            {
                if (other == moved || (groupOnly && other.Spec.Group != group)) continue;
                other.Spec.X += dx;
                other.Spec.Y += dy;
                other.ApplySpec();
            }
        }

        /// <summary>
        /// What a dragged mirror may click into place against: the other
        /// visible mirrors and timers, and the edges of its screen. Null when
        /// snapping is switched off.
        /// </summary>
        List<Rectangle> SnapTargetsFor(MirrorWindow moving)
        {
            if (!settings.Snap) return null;
            List<Rectangle> targets = new List<Rectangle>();
            foreach (MirrorWindow other in mirrors)
                if (other != moving && other.Visible) targets.Add(other.PictureBounds);
            foreach (TimerWindow other in timers)
                if (other.Visible) targets.Add(other.Bounds);
            targets.Add(Screen.FromControl(moving).WorkingArea);
            return targets;
        }

        /// <summary>
        /// Fills the "Group" sub-menu of a mirror or timer: no group, each
        /// existing group, and a way to start a new one.
        /// </summary>
        void FillGroupMenu(ToolStripMenuItem parent, string now, Action<string> choose)
        {
            parent.DropDownItems.Clear();
            parent.DropDownItems.Add(new ToolStripMenuItem(Strings.Get("group.none"), null, delegate { choose(""); }) { Checked = now.Length == 0 });
            foreach (string name in profile.GroupNames())
            {
                string captured = name;
                parent.DropDownItems.Add(new ToolStripMenuItem(name.Replace("&", "&&"), null, delegate { choose(captured); }) { Checked = name == now });
            }
            parent.DropDownItems.Add(new ToolStripSeparator());
            parent.DropDownItems.Add(Strings.Get("group.new"), null, delegate
            {
                string typed = Dialogs.AskText(this, Strings.Get("group.new"), Strings.Get("group.newPrompt"), "");
                if (typed == null) return;
                if (typed.Length == 0 || !Groups.IsValidName(typed)) { Flash(Strings.Get("status.badGroup")); return; }
                choose(typed);
            });
        }

        // ------------------------------------------------------------------
        // Cooldown timers
        // ------------------------------------------------------------------

        void AddTimer()
        {
            if (profile.Timers.Count >= Profile.MaxTimers)
            {
                Flash(Strings.Format("status.timerLimit", Profile.MaxTimers));
                return;
            }
            CooldownTimer spec = new CooldownTimer { Name = Strings.Format("timer.defaultName", profile.Timers.Count + 1) };
            // Place it next to the panel, stacking new ones downwards.
            Rectangle screen = Screen.FromControl(this).WorkingArea;
            Size size = TimerWindow.SizeFor(spec);
            spec.X = Geometry.Clamp(Right + Theme.Px(16), screen.Left, screen.Right - size.Width);
            spec.Y = Geometry.Clamp(Top + Theme.Px(40) + profile.Timers.Count * (size.Height + Theme.Px(12)), screen.Top, screen.Bottom - size.Height);

            profile.Timers.Add(spec);
            if (profile.Locked) SetLocked(false); // a new timer must be movable
            AttachTimer(spec);
            LayoutRows();
            MarkChanged();
            Flash(Strings.Get("status.timerAdded"));
        }

        /// <summary>Creates the timer window and its list row and wires them together.</summary>
        void AttachTimer(CooldownTimer spec)
        {
            TimerWindow window = new TimerWindow(spec);
            window.SetLocked(profile.Locked);
            int rowWidth = list.Width - SystemInformation.VerticalScrollBarWidth - Theme.Px(2);
            TimerRow row = new TimerRow(spec, tips, rowWidth);

            window.Edited += delegate { row.Sync(); MarkChanged(); };
            window.Dragged += delegate(int dx, int dy) { MoveCompanions(window, spec.Group, dx, dy); };
            window.GroupMenuOpening += delegate(ToolStripMenuItem parent)
            {
                FillGroupMenu(parent, spec.Group, delegate(string name) { spec.Group = name; MarkChanged(); });
            };
            window.DurationRequested += delegate { AskDuration(window, row); };
            window.HotkeyRequested += delegate { AskHotkey(window, row); };
            window.DeleteRequested += delegate { DeleteTimer(window, row); };
            window.RenameRequested += delegate
            {
                RestorePanel();
                list.ScrollControlIntoView(row);
                row.BeginRename();
            };
            row.Changed += delegate { window.ApplySpec(); MarkChanged(); };
            row.StartRequested += delegate { window.StartOrRestart(); };
            row.DurationRequested += delegate { AskDuration(window, row); };
            row.HotkeyRequested += delegate { AskHotkey(window, row); };
            row.DeleteRequested += delegate { DeleteTimer(window, row); };

            timers.Add(window);
            timerRows.Add(row);
            list.Controls.Add(row);
        }

        /// <summary>Offers common durations in a small menu at the mouse, plus "Other..." to type one.</summary>
        void AskDuration(TimerWindow window, TimerRow row)
        {
            ContextMenuStrip menu = Dialogs.DarkMenu();
            foreach (int minutes in new[] { 1, 2, 5, 10, 15, 20, 30, 60 })
            {
                int seconds = minutes * 60;
                menu.Items.Add(new ToolStripMenuItem(Strings.Format("timer.minutes", minutes), null, delegate { SetDuration(window, row, seconds); })
                {
                    Checked = window.Spec.Seconds == seconds
                });
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Strings.Get("timer.durationOther"), null, delegate
            {
                string typed = Dialogs.AskText(this, window.Spec.Name, Strings.Get("timer.durationPrompt"), Durations.Format(window.Spec.Seconds));
                if (typed == null) return;
                int seconds;
                if (Durations.TryParse(typed, out seconds)) SetDuration(window, row, seconds);
                else Flash(Strings.Get("status.badDuration"));
            });
            ShowThrowawayMenu(menu, Cursor.Position);
        }

        void SetDuration(TimerWindow window, TimerRow row, int seconds)
        {
            window.Spec.Seconds = seconds;
            window.Reset();
            row.Sync();
            MarkChanged();
        }

        /// <summary>Shows a menu built for one use and disposes it after it closed and its click was handled.</summary>
        void ShowThrowawayMenu(ContextMenuStrip menu, Point screenPosition)
        {
            menu.Closed += delegate { BeginInvoke((MethodInvoker)delegate { menu.Dispose(); }); };
            menu.Show(screenPosition);
        }

        void AskHotkey(TimerWindow window, TimerRow row)
        {
            // Release our hotkeys first, or pressing the current one would
            // start a timer instead of reaching the dialog.
            UnregisterHotkeys();
            int chosen = Hotkeys.Ask(this, window.Spec.Hotkey);
            if (chosen != Hotkeys.Cancelled)
            {
                window.Spec.Hotkey = chosen;
                row.Sync();
                MarkChanged();
            }
            RegisterHotkeys();
        }

        void DeleteTimer(TimerWindow window, TimerRow row)
        {
            if (MessageBox.Show(this, Strings.Format("region.deleteConfirm", window.Spec.Name), "Echo HUD",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            UnregisterHotkeys(); // ids are list positions, which are about to shift
            timers.Remove(window);
            timerRows.Remove(row);
            list.Controls.Remove(row);
            profile.Timers.Remove(window.Spec);
            window.Close();
            row.Dispose();
            RegisterHotkeys();
            LayoutRows();
            MarkChanged();
        }

        /// <summary>
        /// Asks Windows to tell us when one of the user's hotkeys is pressed.
        /// A timer's registration id is its position in the list plus one;
        /// lock and show/hide have fixed ids.
        /// </summary>
        void RegisterHotkeys()
        {
            UnregisterHotkeys();
            List<string> refused = new List<string>();
            for (int i = 0; i < timers.Count; i++) RegisterOne(i + 1, timers[i].Spec.Hotkey, refused);
            RegisterOne(LockHotkeyId, settings.LockHotkey, refused);
            RegisterOne(ShowHotkeyId, settings.ShowHotkey, refused);
            if (refused.Count > 0) Flash(Strings.Format("status.hotkeyTaken", String.Join(", ", refused.ToArray())));
        }

        void RegisterOne(int id, int stored, List<string> refused)
        {
            uint modifiers, virtualKey;
            if (!Hotkeys.Split(stored, out modifiers, out virtualKey)) return;
            if (!Native.RegisterHotKey(Handle, id, modifiers | Native.ModNoRepeat, virtualKey)) refused.Add(Hotkeys.Describe(stored));
        }

        void UnregisterHotkeys()
        {
            if (!IsHandleCreated) return;
            for (int id = 1; id <= Profile.MaxTimers; id++) Native.UnregisterHotKey(Handle, id);
            Native.UnregisterHotKey(Handle, LockHotkeyId);
            Native.UnregisterHotKey(Handle, ShowHotkeyId);
        }

        /// <summary>Windows reports a pressed hotkey here; nothing else about the keyboard reaches us.</summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WmHotKey)
            {
                int id = m.WParam.ToInt32();
                if (id == LockHotkeyId) SetLocked(!profile.Locked);
                else if (id == ShowHotkeyId) ToggleAllVisible();
                else if (id >= 1 && id <= timers.Count) timers[id - 1].StartOrRestart();
                return;
            }
            base.WndProc(ref m);
        }

        // ------------------------------------------------------------------
        // Settings menu
        // ------------------------------------------------------------------

        void ShowSettingsMenu()
        {
            ContextMenuStrip menu = Dialogs.DarkMenu();
            AddToggle(menu, "settings.onlyWithGame", settings.OnlyWithGame, delegate(bool on) { settings.OnlyWithGame = on; });
            AddToggle(menu, "settings.snap", settings.Snap, delegate(bool on) { settings.Snap = on; });
            AddToggle(menu, "settings.tray", settings.MinimizeToTray, delegate(bool on) { settings.MinimizeToTray = on; });
            AddToggle(menu, "settings.autostart", Autostart.IsEnabled, delegate(bool on) { Autostart.Set(on); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Strings.Format("settings.lockHotkey", HotkeyText(settings.LockHotkey)), null, delegate { AskGlobalHotkey(true); });
            menu.Items.Add(Strings.Format("settings.showHotkey", HotkeyText(settings.ShowHotkey)), null, delegate { AskGlobalHotkey(false); });

            ToolStripMenuItem language = Dialogs.Submenu(menu, Strings.Get("settings.language"));
            string[] names = { Strings.Get("settings.languageAuto"), "English", "Español", "Português", "Polski" };
            for (int i = 0; i < Settings.Languages.Length; i++)
            {
                string code = Settings.Languages[i];
                language.DropDownItems.Add(new ToolStripMenuItem(names[i], null, delegate
                {
                    settings.Language = code;
                    SaveSettings();
                    Flash(Strings.Get("status.languageNext"));
                }) { Checked = settings.Language == code });
            }
            menu.Items.Add(language);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Strings.Get("profile.import"), null, delegate { ImportProfile(); });
            menu.Items.Add(Strings.Get("profile.export"), null, delegate { ExportProfile(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Strings.Get("action.releases"), null, delegate { OpenReleasesPage(); });
            menu.Items.Add(Strings.Get("action.about"), null, delegate
            {
                MessageBox.Show(this, Strings.Format("about.text", Version), "Echo HUD", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            menu.Closed += delegate { BeginInvoke((MethodInvoker)delegate { menu.Dispose(); }); };
            menu.Show(settingsButton, new Point(settingsButton.Width, 0), ToolStripDropDownDirection.AboveLeft);
        }

        void AddToggle(ContextMenuStrip menu, string key, bool value, Action<bool> change)
        {
            menu.Items.Add(new ToolStripMenuItem(Strings.Get(key), null, delegate
            {
                try { change(!value); }
                catch (Exception error) { Flash(Strings.Format("status.saveFailed", error.Message)); return; }
                SaveSettings();
            }) { Checked = value });
        }

        static string HotkeyText(int stored)
        {
            string text = Hotkeys.Describe(stored);
            return text.Length > 0 ? text : Strings.Get("hotkey.none");
        }

        void AskGlobalHotkey(bool forLock)
        {
            UnregisterHotkeys(); // so the current combination can be typed into the dialog
            string title = Strings.Get(forLock ? "settings.lockHotkeyTitle" : "settings.showHotkeyTitle");
            int chosen = Hotkeys.Ask(this, forLock ? settings.LockHotkey : settings.ShowHotkey, title, Strings.Get("hotkey.explainGlobal"));
            if (chosen != Hotkeys.Cancelled)
            {
                if (forLock) settings.LockHotkey = chosen;
                else settings.ShowHotkey = chosen;
                SaveSettings();
            }
            RegisterHotkeys();
        }

        void SaveSettings()
        {
            try { Store.SaveSettings(settings); }
            catch (Exception error) { Flash(Strings.Format("status.saveFailed", error.Message)); }
            UpdateMirrors();
        }

        /// <summary>
        /// Opens the download page in the user's web browser. Echo HUD itself
        /// still makes no network connection: it only asks Windows to open a
        /// web address, exactly like clicking a link.
        /// </summary>
        void OpenReleasesPage()
        {
            try { Process.Start(ReleasesPage); }
            catch (Exception) { Flash(ReleasesPage); }
        }

        // ------------------------------------------------------------------
        // Notification area ("tray") icon
        // ------------------------------------------------------------------

        void BuildTray()
        {
            ContextMenuStrip menu = Dialogs.DarkMenu();
            menu.Items.Add(Strings.Get("tray.open"), null, delegate { RestorePanel(); });
            ToolStripItem lockItem = menu.Items.Add("", null, delegate { SetLocked(!profile.Locked); });
            ToolStripItem showItem = menu.Items.Add("", null, delegate { ToggleAllVisible(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Strings.Get("tray.exit"), null, delegate { Close(); });
            menu.Opening += delegate
            {
                lockItem.Text = Strings.Get(profile.Locked ? "tray.unlock" : "tray.lock");
                showItem.Text = Strings.Get(profile.MirrorsVisible ? "action.hideAll" : "action.showAll");
            };
            tray = new NotifyIcon { Icon = Icon, Text = "Echo HUD", ContextMenuStrip = menu, Visible = true };
            tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) RestorePanel(); };
        }

        /// <summary>Brings the panel back from the notification area or the taskbar.</summary>
        void RestorePanel()
        {
            startHidden = false;
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        /// <summary>Minimising hides the panel in the notification area, if that setting is on.</summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (tray == null || WindowState != FormWindowState.Minimized || !settings.MinimizeToTray) return;
            Hide();
            if (trayHintShown) return;
            trayHintShown = true;
            tray.ShowBalloonTip(4000, "Echo HUD", Strings.Get("tray.hint"), ToolTipIcon.Info);
        }

        /// <summary>When started with Windows, the very first "show the window" is skipped.</summary>
        protected override void SetVisibleCore(bool value)
        {
            if (value && startHidden)
            {
                startHidden = false;
                if (!IsHandleCreated) CreateHandle();
                value = false;
            }
            base.SetVisibleCore(value);
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
            foreach (TimerWindow window in timers) window.Close();
            foreach (TimerRow row in timerRows) { list.Controls.Remove(row); row.Dispose(); }
            mirrors.Clear();
            rows.Clear();
            timers.Clear();
            timerRows.Clear();

            profile = next;
            current = null;
            if (profile.Source == SourceKind.Demo && !DemoWindow.IsOpen) DemoWindow.Open();
            RefreshSources();
            foreach (MirrorRegion region in profile.Regions) AttachRegion(region);
            foreach (CooldownTimer spec in profile.Timers) AttachTimer(spec);
            LayoutRows();
            RegisterHotkeys();
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

        void ExportProfile()
        {
            Save();
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = Strings.Get("profile.fileType") + " (*.xml)|*.xml";
                dialog.FileName = profile.Name + ".xml";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Store.Export(profile, dialog.FileName);
                    Flash(Strings.Get("status.exported"));
                }
                catch (Exception error) { Flash(Strings.Format("status.saveFailed", error.Message)); }
            }
        }

        void ImportProfile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = Strings.Get("profile.fileType") + " (*.xml)|*.xml";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Profile imported = Store.Import(dialog.FileName);
                    Save();
                    Store.Save(imported);
                    OpenProfile(imported);
                    Flash(Strings.Format("status.imported", imported.Name));
                }
                catch (Exception error) { Flash(Strings.Format("status.loadFailed", error.Message)); }
            }
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
            foreach (TimerWindow window in timers.ToArray()) window.Close();
            UnregisterHotkeys();
            if (wakeWait != null) wakeWait.Unregister(null);
            if (tray != null) tray.Visible = false; // otherwise the icon lingers until the mouse passes over it
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                tips.Dispose();
                if (tray != null)
                {
                    if (tray.ContextMenuStrip != null) tray.ContextMenuStrip.Dispose();
                    tray.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}

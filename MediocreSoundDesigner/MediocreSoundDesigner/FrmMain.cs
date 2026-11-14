using MediocreSoundDesigner.Properties;
using System.Text.Json;

namespace MediocreSoundDesigner;

public partial class FrmMain : Form
{
    // fields
    private TrackManager trackManager;
    private readonly UndoManager undoManager = new();
    private UndoManager.Snapshot volumeSnapshot;

    // Colors handed out to tracks in order (chosen to look good on a dark background)
    private static readonly Color[] TrackColors =
    {
        Color.FromArgb(79, 195, 247),   // blue
        Color.FromArgb(129, 199, 132),  // green
        Color.FromArgb(255, 183, 77),   // orange
        Color.FromArgb(186, 104, 200),  // purple
        Color.FromArgb(240, 98, 146),   // pink
        Color.FromArgb(77, 208, 225),   // cyan
    };
    private int nextColorIndex = 0;

    // Play/pause images, loaded once (Resources.play creates a new image every time it's read)
    private static readonly Image PlayImage = Resources.play;
    private static readonly Image PauseImage = Resources.pause;

    // Toolbar (built in code in BuildToolbar)
    private ToolStrip toolbar;
    private ToolStripButton tbUndo, tbRedo, tbPlay, tbStop, tbLoop;
    private ToolStripLabel tbTime;

    // Playback state used for Stop and Loop
    private bool userStopped = false;
    private NAudio.Wave.PlaybackState lastState = NAudio.Wave.PlaybackState.Stopped;

    // ctor
    public FrmMain()
    {
        InitializeComponent();
        Setup();
    }

    // setup
    private void Setup()
    {
        DoubleBuffered = true;
        trackManager = new(panTracks);
        btnPlay.BackgroundImage = PlayImage;
        trackManager.DrawTimescale = trkDrawTimescale.Value;
        BuildToolbar();
        UpdateSizes();
        ApplySettings();
        SyncVolumeSlider();
        UpdateUndoMenu();
        trkVolume.MouseDown += (s, e) => volumeSnapshot = undoManager.Capture(trackManager.SelectedTrack);
        trkVolume.MouseUp += (s, e) =>
        {
            if (volumeSnapshot is not null && volumeSnapshot.Volume != trackManager.SelectedTrack?.Volume)
            {
                undoManager.Push(volumeSnapshot);
                UpdateUndoMenu();
            }
            volumeSnapshot = null;
        };
        lblPlayhead.Width = 2;       // thin playhead line
        tmrUpdate.Interval = 15;     // update about 60 times a second
    }
    private void ApplySettings()
    {
        BackColor = Settings.Color.WinBg;
        panTracks.BackColor = Settings.Color.TrackBg_Unselected;
        lblPlayhead.BackColor = Settings.Color.Playline;
        trackManager.ApplySettings();
    }

    // GUI controls
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // keyboard shortcuts
        //open file
        if (keyData == (Keys.Control | Keys.O))
        {
            menuFileOpen_Click(this, EventArgs.Empty);
            return true;
        }
        //save file
        else if (keyData == (Keys.Control | Keys.S))
        {
            menuFileSave_Click(this, EventArgs.Empty);
            return true;
        }
        //play/pause
        else if (keyData == Keys.Space)
        {
            btnPlay_Click(this, EventArgs.Empty);
            return true;
        }
        //reverse track
        else if (keyData == (Keys.Control | Keys.R))
        {
            menuEditReverse_Click(this, EventArgs.Empty);
            return true;
        }
        //flip track
        else if (keyData == (Keys.Control | Keys.F))
        {
            menuEditFlip_Click(this, EventArgs.Empty);
            return true;
        }

        //Exsisting keyboard controls
        if (trackManager.SelectedTrack is not null)
        {
            if (keyData == Keys.Left)
            {
                RunUndoable(t => t.ShiftSampleOver(-5));
                return true;
            }
            else if (keyData == Keys.Right)
            {
                RunUndoable(t => t.ShiftSampleOver(+5));
                return true;
            }
            else if (keyData == Keys.Delete)
            {
                if (trackManager.SelectedTrack is not null)
                {
                    if (MessageBox.Show("This will delete the selected track. This is NOT undoable!", "Are You Sure?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Exclamation) == DialogResult.Yes)
                    {
                        panTracks.Controls.Remove(trackManager.SelectedTrack.PanDrawSamples);
                        undoManager.Forget(trackManager.SelectedTrack);
                        UpdateUndoMenu();
                        trackManager.SelectedTrack.PanDrawSamples.Dispose();
                        trackManager.SelectedTrack.Dispose();
                        trackManager.RemTrack(trackManager.SelectedTrack);
                        SyncVolumeSlider();
                        UpdateSizes();
                        return true;
                    }
                }
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void FrmMain_SizeChanged(object sender, EventArgs e)
    {
        UpdateSizes();
    }

    // Play / Pause / Resume (the big button, the toolbar button and Space all use this)
    private void btnPlay_Click(object sender, EventArgs e)
    {
        if (trackManager.Tracks.Count == 0) return;

        switch (trackManager.State)
        {
            case NAudio.Wave.PlaybackState.Playing:
                trackManager.PauseAll();
                break;
            case NAudio.Wave.PlaybackState.Paused:
                trackManager.ResumeAll();     // continue where it was paused
                break;
            default:
                trackManager.PlayAll();       // start from the beginning
                break;
        }
        userStopped = false;
    }

    // Stop and rewind to the start
    private void StopPlayback()
    {
        userStopped = true;
        trackManager.StopAll();
        lblPlayhead.Left = 0;
    }

    private void menuFileOpen_Click(object sender, EventArgs e)
    {
        ofdLoadFile.DefaultExt = "wav";
        if (ofdLoadFile.ShowDialog() == DialogResult.OK)
        {
            Track newTrack = new(ofdLoadFile.FileName);
            CreateTrackPanel(newTrack);
            trackManager.AddTrack(newTrack);
            UpdateSizes();
            newTrack.PanDrawSamples.Invalidate();
        }
    }
    private void menuFileSave_Click(object sender, EventArgs e)
    {
        sfdSaveFile.DefaultExt = "wav";
        if (sfdSaveFile.ShowDialog() == DialogResult.OK)
        {
            trackManager.SaveToFile(sfdSaveFile.FileName);
        }
    }
    private void menuFileSettings_Click(object sender, EventArgs e)
    {
        FrmEditSettings frm = new();
        frm.SetSound(trackManager.SelectedTrack);
        if (frm.ShowDialog() == DialogResult.OK)
        {
            ApplySettings();
        }
    }

    private void menuEditFlip_Click(object sender, EventArgs e)
    {
        RunUndoable(t => t.Flip());
    }
    private void menuEditReverse_Click(object sender, EventArgs e)
    {
        RunUndoable(t => t.Reverse());
    }
    private void menuEditVolume_Click(object sender, EventArgs e)
    {
        ShowEditDialog(new FrmEditVolume());
    }
    private void menuEditEliminateSilence_Click(object sender, EventArgs e)
    {
        ShowEditDialog(new FrmEditEliminateSilence());
    }

    private void menuGenRecord_Click(object sender, EventArgs e)
    {
        Track newTrack = new(new float[0]);
        newTrack.Name = "Recording";
        CreateTrackPanel(newTrack);
        trackManager.AddTrack(newTrack);
        newTrack.PanDrawSamples.Invalidate();

        FrmGenRecord frm = new();
        frm.SetSound(trackManager.SelectedTrack);
        if (frm.ShowDialog() == DialogResult.Cancel)
        {
            trackManager.RemTrack(newTrack);
            SyncVolumeSlider();
        }
        UpdateSizes();
    }
    private void menuGenNoise_Click(object sender, EventArgs e)
    {
        const int NUM_SAMPLES = 100_000;
        float[] samples = new float[NUM_SAMPLES];
        Random rand = new();
        for (int i = 0; i < NUM_SAMPLES; i++)
        {
            samples[i] = rand.NextSingle() * 2.0f - 1.0f;
        }
        Track newTrack = new(samples);
        newTrack.Name = "Noise";
        CreateTrackPanel(newTrack);
        trackManager.AddTrack(newTrack);
        UpdateSizes();
        newTrack.PanDrawSamples.Invalidate();
    }
    private void menuGenGradientNoise_Click(object sender, EventArgs e)
    {
        const int NUM_SAMPLES = 250_000;
        float[] samples = new float[NUM_SAMPLES];
        Random rand = new();
        float cur = 0;
        float inc = 0.005f;
        int dir = +1;
        for (int i = 0; i < NUM_SAMPLES; i++)
        {
            cur += (inc * dir);
            if (rand.NextSingle() < 0.5f)
            {
                dir *= -1;
            }
            if (cur > 0.9f)
            {
                dir = -1;
            }
            if (cur < -0.9f)
            {
                dir = +1;
            }
            samples[i] = cur;
        }
        Track newTrack = new(samples);
        newTrack.Name = "Gradient Noise";
        CreateTrackPanel(newTrack);
        trackManager.AddTrack(newTrack);
        UpdateSizes();
        newTrack.PanDrawSamples.Invalidate();
    }

    private void tmrUpdatePlaybackState_Tick(object sender, EventArgs e)
    {
        var state = trackManager.State;
        bool hasTracks = trackManager.Tracks.Count > 0;

        // Loop: if playback just reached the end by itself, start again
        if (tbLoop.Checked && hasTracks && !userStopped
            && lastState == NAudio.Wave.PlaybackState.Playing
            && state == NAudio.Wave.PlaybackState.Stopped)
        {
            trackManager.PlayAll();
            state = trackManager.State;
        }
        lastState = state;

        bool playing = state == NAudio.Wave.PlaybackState.Playing;
        if (hasTracks)
        {
            Image want = playing ? PauseImage : PlayImage;
            if (btnPlay.BackgroundImage != want) btnPlay.BackgroundImage = want;
            btnPlay.Visible = true;
            lblPlayhead.Left = trackManager.GetPlayheadPos();
            lblPlayhead.Visible = true;
        }
        else
        {
            btnPlay.BackgroundImage = null;
            btnPlay.Visible = false;
            lblPlayhead.Visible = false;
        }

        // Toolbar: only change text when it actually changes (avoids flicker)
        string playText = playing ? "|| Pause" : "\u25B6 Play";
        if (tbPlay.Text != playText) tbPlay.Text = playText;
        tbPlay.Enabled = hasTracks;
        tbStop.Enabled = hasTracks;

        string time = $"{FormatTime(trackManager.GetPlayheadSeconds())} / {FormatTime(trackManager.TotalSeconds)}";
        if (tbTime.Text != time) tbTime.Text = time;
    }

    // 75.3 seconds -> "1:15.3"
    private static string FormatTime(double seconds)
    {
        int minutes = (int)(seconds / 60);
        return $"{minutes}:{seconds - minutes * 60:00.0}";
    }

    private void trkDrawTimescale_Scroll(object sender, EventArgs e)
    {
        // Max + Min - Value so the zoom never reaches 0 (which would crash the drawing)
        trackManager.DrawTimescale = trkDrawTimescale.Maximum + trkDrawTimescale.Minimum - trkDrawTimescale.Value;
        trackManager.RedrawAll();
    }

    // Helper methods
    private Panel CreateTrackPanel(Track track)
    {
        track.TrackColor = TrackColors[nextColorIndex++ % TrackColors.Length];

        BufferedPanel pan = new()
        {
            BackColor = Settings.Color.TrackBg_Unselected,
            Width = panTracks.Width,
            Left = 0,
            Top = 0,
        };
        pan.Click += (sender, e) =>
        {
            trackManager.SelectTrack(track);
            SyncVolumeSlider();
            trackManager.RedrawAll();   // so the selection border moves
        };
        pan.DoubleClick += (sender, e) =>
        {
            track.ToggleDisabled();
            track.PanDrawSamples.Invalidate();
        };
        pan.Paint += (sender, e) =>
        {
            track.DrawSamples(e.Graphics, trackManager.DrawTimescale);
            if (trackManager.SelectedTrack == track)
            {
                // colored border around the selected track
                using Pen border = new(track.TrackColor, 3);
                e.Graphics.DrawRectangle(border, 1, 1, pan.Width - 3, pan.Height - 3);
            }
        };
        panTracks.Controls.Add(pan);
        track.LinkPanels(pan, panTracks);
        trackManager.SelectTrack(track);
        SyncVolumeSlider();

        return pan;
    }

    private void UpdateSizes()
    {
        const int PADDING = 15;

        panTracks.Left = PADDING;
        panTracks.Width = ClientSize.Width - (PADDING * 2);

        // Make room for the toolbar if it overlaps the zoom slider (e.g. at high display scaling)
        if (toolbar != null && trkDrawTimescale.Top < toolbar.Bottom + 2)
        {
            int shift = toolbar.Bottom + 2 - trkDrawTimescale.Top;
            trkDrawTimescale.Top += shift;
            panTracks.Top += shift;
            panTracks.Height -= shift;
        }

        int top = 0;
        if (trackManager is null) return;
        const int GAP = 4;   // space between tracks
        foreach (Track track in trackManager.Tracks)
        {
            track.PanDrawSamples.Left = 0;
            track.PanDrawSamples.Width = panTracks.Width;
            track.PanDrawSamples.Height = (panTracks.Height / trackManager.Tracks.Count) - GAP;
            track.PanDrawSamples.Top = top;
            track.PanDrawSamples.Invalidate();
            top += track.PanDrawSamples.Height + GAP;
        }
        trkDrawTimescale.Width = panTracks.Width;
        trkDrawTimescale.Left = PADDING;
        lblPlayhead.Left = 0;
        lblPlayhead.Height = panTracks.Height;
        lblPlayhead.BringToFront();
        ZoomToFit();   // make the longest track fill the width
    }

    // Picks the zoom so the longest track exactly fills the width of the track area
    private void ZoomToFit()
    {
        if (trackManager is null || trackManager.Tracks.Count == 0) return;

        int longest = trackManager.Tracks.Max(t => t.SampleCount);
        int width = Math.Max(1, panTracks.ClientSize.Width - 10);
        int samplesPerPixel = Math.Max(1, (int)Math.Ceiling(longest / (double)width));

        trackManager.DrawTimescale = samplesPerPixel;
        SyncZoomSlider();
        trackManager.RedrawAll();
    }

    // Zoom in (factor < 1) or out (factor > 1) from the current zoom
    private void ZoomBy(double factor)
    {
        int current = trackManager.DrawTimescale;
        int next = Math.Max(1, (int)Math.Round(current * factor));
        if (next == current && factor > 1) next++;   // make sure zoom-out always moves
        trackManager.DrawTimescale = next;
        SyncZoomSlider();
        trackManager.RedrawAll();
    }

    // Keep the zoom slider roughly in sync (very long files go past its range; that's fine)
    private void SyncZoomSlider()
    {
        trkDrawTimescale.Value = Math.Clamp(
            trkDrawTimescale.Maximum + trkDrawTimescale.Minimum - trackManager.DrawTimescale,
            trkDrawTimescale.Minimum, trkDrawTimescale.Maximum);
    }

    // Builds the toolbar under the menu (done in code so the form designer isn't needed)
    private void BuildToolbar()
    {
        toolbar = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10f),
            Padding = new Padding(6, 2, 6, 2)
        };

        ToolStripButton MakeButton(string text, string tip, Action onClick)
        {
            var b = new ToolStripButton(text)
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = tip,
                ForeColor = Color.White,
                Margin = new Padding(2, 1, 2, 1)
            };
            b.Click += (s, e) => onClick();
            return b;
        }

        var tbOpen = MakeButton("Open", "Open audio file (Ctrl+O)", () => menuFileOpen_Click(this, EventArgs.Empty));
        var tbSave = MakeButton("Save", "Save mix as .wav (Ctrl+S)", () => menuFileSave_Click(this, EventArgs.Empty));
        tbUndo = MakeButton("Undo", "Undo (Ctrl+Z)", () => menuEditUndo_Click(this, EventArgs.Empty));
        tbRedo = MakeButton("Redo", "Redo (Ctrl+Y)", () => menuEditRedo_Click(this, EventArgs.Empty));
        tbPlay = MakeButton("\u25B6 Play", "Play / Pause (Space)", () => btnPlay_Click(this, EventArgs.Empty));
        tbStop = MakeButton("\u25A0 Stop", "Stop and rewind to the start", StopPlayback);
        tbLoop = MakeButton("Loop", "Loop playback: start again at the end", () => { });
        tbLoop.CheckOnClick = true;
        tbLoop.CheckedChanged += (s, e) => tbLoop.ForeColor = tbLoop.Checked ? Color.LimeGreen : Color.White;
        var tbZoomOut = MakeButton("Zoom \u2212", "Zoom out", () => ZoomBy(2.0));
        var tbZoomIn = MakeButton("Zoom +", "Zoom in", () => ZoomBy(0.5));
        var tbFit = MakeButton("Fit", "Fit the longest track to the window", ZoomToFit);

        tbTime = new ToolStripLabel("0:00.0 / 0:00.0")
        {
            Alignment = ToolStripItemAlignment.Right,
            ForeColor = Color.White,
            Font = new Font("Consolas", 11f, FontStyle.Bold),
            Margin = new Padding(2, 1, 8, 1)
        };

        toolbar.Items.AddRange(new ToolStripItem[]
        {
            tbOpen, tbSave,
            new ToolStripSeparator(),
            tbUndo, tbRedo,
            new ToolStripSeparator(),
            tbPlay, tbStop, tbLoop,
            new ToolStripSeparator(),
            tbZoomOut, tbZoomIn, tbFit,
            tbTime
        });

        Controls.Add(toolbar);
        toolbar.BringToFront();   // docks it just below the menu bar
    }

    private float? AskFadeDuration(string title)
    {
        using Form dialog = new Form
        {
            Text = title,
            Width = 330,
            Height = 180,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        Label label = new Label
        {
            Text = "Fade duration (seconds):",
            Left = 20,
            Top = 20,
            Width = 260
        };

        NumericUpDown durationInput = new NumericUpDown
        {
            Left = 20,
            Top = 50,
            Width = 260,
            Minimum = 0.1m,
            Maximum = 3600m,
            DecimalPlaces = 1,
            Increment = 0.5m,
            Value = 3.0m
        };

        Button okButton = new Button
        {
            Text = "Apply",
            Left = 110,
            Top = 90,
            Width = 80,
            DialogResult = DialogResult.OK
        };

        Button cancelButton = new Button
        {
            Text = "Cancel",
            Left = 200,
            Top = 90,
            Width = 80,
            DialogResult = DialogResult.Cancel
        };

        dialog.Controls.Add(label);
        dialog.Controls.Add(durationInput);
        dialog.Controls.Add(okButton);
        dialog.Controls.Add(cancelButton);

        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            return (float)durationInput.Value;
        }

        return null;
    }

    private void trkVolume_Scroll(object sender, EventArgs e)
    {
        if (trackManager.SelectedTrack is null) return;
        trackManager.SelectedTrack.SetVolume(trkVolume.Value / 100.0f);
        trackManager.SelectedTrack.PanDrawSamples.Invalidate();
    }

    private void SyncVolumeSlider()
    {
        // Do nothing if the volume slider hasn't been initialized
        if (trkVolume is null)
            return;

        Track t = trackManager.SelectedTrack;
        trkVolume.Enabled = t is not null;
        if (t is not null)
        {
            trkVolume.Value = Math.Clamp((int)Math.Round(t.Volume * 100f), trkVolume.Minimum, trkVolume.Maximum);
        }
    }

    private void menuEditFadeIn_Click(object sender, EventArgs e)
    {
        Track track = trackManager.SelectedTrack;

        if (track is null)
        {
            MessageBox.Show("Please select a track first.");
            return;
        }

        float? duration = AskFadeDuration("Fade In");

        if (duration is null)
            return;

        undoManager.Record(track);
        track.FadeIn(duration.Value);

        // Refresh the waveform after modifying the samples
        track.PanDrawSamples.Invalidate();
        UpdateUndoMenu();
    }

    private void menuEditFadeOut_Click(object sender, EventArgs e)
    {
        Track track = trackManager.SelectedTrack;

        if (track is null)
        {
            MessageBox.Show("Please select a track first.");
            return;
        }

        float? duration = AskFadeDuration("Fade Out");

        if (duration is null)
            return;

        undoManager.Record(track);
        track.FadeOut(duration.Value);

        track.PanDrawSamples.Invalidate();
        UpdateUndoMenu();
    }

    // Window to show all the keyboard shorcuts
    private void ShowKeyboardShortcuts()
    {
        // Creating window and setting its appearance
        using Form dialog = new Form
        {
            Text = "Keyboard Shortcuts",
            Size = new Size(460, 430),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(35, 35, 35),
            ForeColor = Color.White
        };
        // Creating a list to show the shortcuts in two columns
        ListView shortcuts = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Location = new Point(20, 20),
            Size = new Size(400, 230),
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.White
        };
        // Column headers to distinguish the actions from their designated shortcut
        shortcuts.Columns.Add("Action", 260);
        shortcuts.Columns.Add("Shortcut", 130);

        // Storing the shortcuts we have available
        string[,] shortcutData =
        {
            { "Open Audio File", "Ctrl + O" },
            { "Save Audio File", "Ctrl + S" },
            { "Play / Pause", "Space" },
            { "Reverse Track", "Ctrl + R" },
            { "Flip Track", "Ctrl + F" },
            { "Shift Track Left", "Left Arrow" },
            { "Shift Track Right", "Right Arrow" },
            { "Delete Selected Track", "Delete" },
            { "Undo", "Ctrl + Z" },
            { "Redo", "Ctrl + Y" }
        };

        // Loop through the shortcuts and add each to the list
        for (int i = 0; i < shortcutData.GetLength(0); i++)
        {
            ListViewItem item = new ListViewItem(shortcutData[i, 0]);
            item.SubItems.Add(shortcutData[i, 1]);
            shortcuts.Items.Add(item);
        }

        // Creating a button to close the shortcuts window
        Button closeButton = new Button
        {
            Text = "Close",
            Size = new Size(100, 35),
            Location = new Point(320, 340),
            DialogResult = DialogResult.OK,
            BackColor = Color.DimGray,
            ForeColor = Color.White
        };

        dialog.Controls.Add(shortcuts);
        dialog.Controls.Add(closeButton);
        dialog.AcceptButton = closeButton;
        dialog.CancelButton = closeButton;

        dialog.ShowDialog(this);
    }
    private void menuHelpShortcuts_Click(object sender, EventArgs e)
    {
        ShowKeyboardShortcuts();
    }

    // Runs an instant edit on the selected track and saves an undo step first
    private void RunUndoable(Action<Track> edit)
    {
        Track track = trackManager.SelectedTrack;
        if (track is null) return;
        undoManager.Record(track);
        edit(track);
        track.PanDrawSamples.Invalidate();
        UpdateUndoMenu();
    }

    // Shows an edit dialog; only saves an undo step if the user clicks Apply
    private void ShowEditDialog(FrmEditMaster frm)
    {
        Track track = trackManager.SelectedTrack;
        if (track is null) return;
        using (frm)
        {
            var before = undoManager.Capture(track);
            frm.SetSound(track);
            if (frm.ShowDialog(this) == DialogResult.OK)
            {
                undoManager.Push(before);
                UpdateUndoMenu();
                SyncVolumeSlider();
            }
        }
    }

    // Greys out Undo/Redo in the menu when there's nothing to undo/redo
    private void UpdateUndoMenu()
    {
        menuEditUndo.Enabled = undoManager.CanUndo;
        menuEditRedo.Enabled = undoManager.CanRedo;
        if (tbUndo != null) tbUndo.Enabled = undoManager.CanUndo;
        if (tbRedo != null) tbRedo.Enabled = undoManager.CanRedo;
    }

    private void menuEditUndo_Click(object sender, EventArgs e)
    {
        undoManager.Undo();
        SyncVolumeSlider();
        UpdateUndoMenu();
    }

    private void menuEditRedo_Click(object sender, EventArgs e)
    {
        undoManager.Redo();
        SyncVolumeSlider();
        UpdateUndoMenu();
    }

    // Normalize: scale the track so its loudest sample reaches full volume (undoable)
    private void menuEditNormalize_Click(object sender, EventArgs e)
    {
        RunUndoable(t =>
        {
            float[] samples = t.CloneSamples();
            float peak = 0f;
            foreach (float sample in samples)
            {
                peak = Math.Max(peak, Math.Abs(sample));
            }
            if (peak == 0f) return; // silent track, nothing to do

            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] /= peak;
            }
            t.ReplaceSamples(samples);
        });
    }

    private void menuGenTone_Click(object sender, EventArgs e)
    {
        var settings = AskToneSettings();
        if (settings is null) return;   // user clicked Cancel

        float frequency = settings.Value.Frequency;
        float seconds = settings.Value.Seconds;

        const int SAMPLE_RATE = 44100;   // matches generated tracks in Track.cs
        const float AMPLITUDE = 0.5f;    // half volume so it isn't too loud

        int count = (int)(SAMPLE_RATE * seconds);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            samples[i] = AMPLITUDE * MathF.Sin(2 * MathF.PI * frequency * i / SAMPLE_RATE);
        }

        Track newTrack = new(samples);
        newTrack.Name = $"Tone {frequency:0} Hz";
        CreateTrackPanel(newTrack);
        trackManager.AddTrack(newTrack);
        UpdateSizes();
        newTrack.PanDrawSamples.Invalidate();
    }

    // Dialog asking for frequency and duration; returns null if cancelled.
    // Uses auto-sizing layout so nothing gets cut off with Windows display scaling.
    private (float Frequency, float Seconds)? AskToneSettings()
    {
        using Form dialog = new Form
        {
            Text = "Generate Tone",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12)
        };

        FlowLayoutPanel layout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };

        Label lblFrequency = new Label { Text = "Frequency (Hz):", AutoSize = true };
        NumericUpDown nudFrequency = new NumericUpDown
        {
            Width = 260,
            Minimum = 20,
            Maximum = 20000,
            Value = 440,
            Increment = 10
        };

        Label lblDuration = new Label { Text = "Duration (seconds):", AutoSize = true, Margin = new Padding(3, 12, 3, 3) };
        NumericUpDown nudDuration = new NumericUpDown
        {
            Width = 260,
            Minimum = 0.1m,
            Maximum = 60,
            Value = 2,
            DecimalPlaces = 1,
            Increment = 0.5m
        };

        FlowLayoutPanel buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 16, 0, 0)
        };
        Button ok = new Button { Text = "Apply", AutoSize = true, DialogResult = DialogResult.OK };
        Button cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        layout.Controls.AddRange(new Control[] { lblFrequency, nudFrequency, lblDuration, nudDuration, buttons });
        dialog.Controls.Add(layout);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            return ((float)nudFrequency.Value, (float)nudDuration.Value);
        }
        return null;
    }

    // A Panel that draws off-screen first, so it doesn't flicker when redrawn
    public class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}
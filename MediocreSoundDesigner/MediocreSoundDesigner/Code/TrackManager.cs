using NAudio.Wave;

namespace MediocreSoundDesigner;

public class TrackManager
{
    private Track pseudoCombinedTrack;
    private Panel panContainer;
    private bool mixDirty;   // the mix changed while paused; rebuild it on resume

    public List<Track> Tracks { get; private set; }
    public Track SelectedTrack { get; private set; }
    public int DrawTimescale { get; set; }
    public PlaybackState State => pseudoCombinedTrack?.State ?? PlaybackState.Stopped;

    public TrackManager(Panel panContainer)
    {
        Tracks = new();
        SelectedTrack = null;
        pseudoCombinedTrack = null;
        this.panContainer = panContainer;
    }

    public void AddTrack(Track track)
    {
        Tracks.Add(track);
    }

    // Adds newTrack right below existing (used by Duplicate)
    public void InsertTrackAfter(Track existing, Track newTrack)
    {
        int index = Tracks.IndexOf(existing);
        Tracks.Insert(index < 0 ? Tracks.Count : index + 1, newTrack);
    }

    // Is this track heard in the mix? (not disabled, not muted, and soloed if anything is soloed)
    public bool IsAudible(Track track)
    {
        if (track.IsDisabled || track.IsMuted) return false;
        bool anySoloed = Tracks.Any(t => t.IsSoloed);
        return !anySoloed || track.IsSoloed;
    }

    public void RemTrack(Track track)
    {
        Tracks.Remove(track);
        if (SelectedTrack == track)
        {
            SelectedTrack = null;
        }
    }

    public void SelectTrack(Track track)
    {
        SelectedTrack = track;
        Tracks.ForEach(track => {
            track.PanDrawSamples.BorderStyle = BorderStyle.None;
            track.PanDrawSamples.BackColor = Settings.Color.TrackBg_Unselected;
        });
        track.PanDrawSamples.BorderStyle = BorderStyle.Fixed3D;
        track.PanDrawSamples.BackColor = Settings.Color.TrackBg_Selected;
    }

    public void RedrawAll()
    {
        Tracks.ForEach(track => track.PanDrawSamples.Invalidate());
    }

    public void ApplySettings()
    {
        foreach (Track track in Tracks)
        {
            if (SelectedTrack == track)
            {
                track.PanDrawSamples.BackColor = Settings.Color.TrackBg_Selected;
            }
            else
            {
                track.PanDrawSamples.BackColor = Settings.Color.TrackBg_Unselected;
            }
        }
        RedrawAll();
    }

    public void SaveToFile(string filename)
    {
        CombineTracks();
        pseudoCombinedTrack.SaveToFile(filename);
    }

    // --- playback ---

    // Starts playing (rebuilds the mix so recent edits are heard). startSeconds = where to start.
    public void PlayAll(double startSeconds = 0)
    {
        CombineTracks();
        mixDirty = false;
        if (pseudoCombinedTrack.SampleCount == 0) return;   // nothing to play (e.g. every track disabled)
        long startSample = (long)(startSeconds * pseudoCombinedTrack.SampleRate * pseudoCombinedTrack.Channels);
        pseudoCombinedTrack.Play(startSample);
    }

    // Continues from where it was paused (rebuilding the mix if something changed meanwhile)
    public void ResumeAll()
    {
        if (mixDirty)
        {
            PlayAll(GetPlayheadSeconds());
        }
        else
        {
            pseudoCombinedTrack?.Resume();
        }
    }

    // Call after any edit, mute/solo or delete: playing audio updates right away, paused audio on resume
    public void MarkMixChanged()
    {
        PlaybackState state = State;
        if (state == PlaybackState.Playing)
        {
            PlayAll(GetPlayheadSeconds());
        }
        else if (state == PlaybackState.Paused)
        {
            mixDirty = true;
        }
    }

    public void PauseAll()
    {
        pseudoCombinedTrack?.Pause();
    }

    // Stops playback and rewinds to the start
    public void StopAll()
    {
        pseudoCombinedTrack?.Stop();
    }

    // Playhead position in pixels (for drawing the playhead line)
    public int GetPlayheadPos()
    {
        return (pseudoCombinedTrack?.GetPlayheadPos() ?? 0) / Math.Max(1, DrawTimescale);
    }

    // Playhead position in seconds (for the time display)
    public double GetPlayheadSeconds()
    {
        if (pseudoCombinedTrack is null) return 0;
        return pseudoCombinedTrack.GetPlayheadPos() / (double)(pseudoCombinedTrack.SampleRate * pseudoCombinedTrack.Channels);
    }

    // Length of the longest track in seconds (for the time display)
    public double TotalSeconds => Tracks.Count == 0 ? 0 : Tracks.Max(t => t.DurationSeconds);

    // Mixes all enabled tracks into one track for playing/saving.
    // The mix uses the tracks' own format: stereo if any track is stereo, at the first track's sample rate.
    private void CombineTracks()
    {
        pseudoCombinedTrack?.Dispose();

        List<Track> enabled = Tracks.Where(IsAudible).ToList();
        int channels = enabled.Count == 0 ? 1 : enabled.Max(t => t.Channels);
        int sampleRate = enabled.Count == 0 ? 44100 : enabled[0].SampleRate;

        // Get every track's samples in the mix format first, so we know how long the mix is
        List<float[]> parts = new();
        foreach (Track track in enabled)
        {
            float[] trackSamples = track.GetAdjustedSamples();
            if (track.Channels == 1 && channels == 2)
            {
                trackSamples = UpmixToStereo(trackSamples);
            }
            parts.Add(trackSamples);
        }

        int length = parts.Count == 0 ? 0 : parts.Max(p => p.Length);
        float[] combined = new float[length];
        foreach (float[] part in parts)
        {
            for (int i = 0; i < part.Length; i++)
            {
                combined[i] += part[i];
            }
        }

        pseudoCombinedTrack = new(combined, sampleRate, channels);
        pseudoCombinedTrack.LinkPanels(null, panContainer);
    }

    // Copies each mono sample to both left and right
    private static float[] UpmixToStereo(float[] mono)
    {
        float[] stereo = new float[mono.Length * 2];
        for (int i = 0; i < mono.Length; i++)
        {
            stereo[2 * i] = mono[i];
            stereo[2 * i + 1] = mono[i];
        }
        return stereo;
    }
}
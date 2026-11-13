namespace MediocreSoundDesigner;

/// <summary>
/// Keeps a history of track edits so they can be undone and redone.
/// Usage: call Record(track) right BEFORE changing a track's audio.
/// For dialogs: Capture(track) before showing it, then Push(snapshot) only if the user clicked Apply.
/// </summary>
public class UndoManager
{
    // A saved copy of one track's audio at a point in time
    public record Snapshot(Track Track, float[] Samples, float Volume, int DrawOffset);

    private readonly LinkedList<Snapshot> undoHistory = new(); // LinkedList so we can drop the oldest step
    private readonly Stack<Snapshot> redoHistory = new();

    public int MaxSteps { get; set; } = 20;

    public bool CanUndo => undoHistory.Count > 0;
    public bool CanRedo => redoHistory.Count > 0;

    // Take a snapshot of a track without adding it to the history yet
    public Snapshot Capture(Track track)
    {
        if (track is null) return null;
        return new Snapshot(track, track.CloneSamples(), track.Volume, track.DrawOffset);
    }

    // Add a snapshot to the history (a new edit wipes out anything that could be redone)
    public void Push(Snapshot snapshot)
    {
        if (snapshot is null) return;
        AddToUndo(snapshot);
        redoHistory.Clear();
    }

    // Shortcut: snapshot + push in one call, for edits that happen immediately (Flip, Reverse, ...)
    public void Record(Track track) => Push(Capture(track));

    // Returns the track that changed, or null if there was nothing to undo
    public Track Undo()
    {
        if (!CanUndo) return null;
        Snapshot previous = undoHistory.Last.Value;
        undoHistory.RemoveLast();
        redoHistory.Push(Capture(previous.Track)); // remember the current state so Redo can come back to it
        Restore(previous);
        return previous.Track;
    }

    // Returns the track that changed, or null if there was nothing to redo
    public Track Redo()
    {
        if (!CanRedo) return null;
        Snapshot next = redoHistory.Pop();
        AddToUndo(Capture(next.Track));
        Restore(next);
        return next.Track;
    }

    // Call when a track is deleted so Undo never tries to restore a track that no longer exists
    public void Forget(Track track)
    {
        List<Snapshot> keepUndo = undoHistory.Where(s => s.Track != track).ToList();
        undoHistory.Clear();
        foreach (Snapshot s in keepUndo) undoHistory.AddLast(s);

        // Stack enumerates top-first, so reverse before pushing back to keep the same order
        List<Snapshot> keepRedo = redoHistory.Where(s => s.Track != track).Reverse().ToList();
        redoHistory.Clear();
        foreach (Snapshot s in keepRedo) redoHistory.Push(s);
    }

    public void Clear()
    {
        undoHistory.Clear();
        redoHistory.Clear();
    }

    private void AddToUndo(Snapshot snapshot)
    {
        undoHistory.AddLast(snapshot);
        while (undoHistory.Count > MaxSteps)
        {
            undoHistory.RemoveFirst();
        }
    }

    private static void Restore(Snapshot snapshot)
    {
        snapshot.Track.ReplaceSamples(snapshot.Samples);
        snapshot.Track.SetVolume(snapshot.Volume);
        snapshot.Track.DrawOffset = snapshot.DrawOffset; // arrow-key shift
        snapshot.Track.PanDrawSamples?.Invalidate();
    }
}
namespace KOKONote.Core;

// All cross-thread access goes through this lock; UI never waits for disk I/O while holding it.
public sealed class Workspace
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, Note> notes;
    private readonly Dictionary<Guid, long> saved = new();
    private SessionState session;
    private long revision;
    private long savedRevision;

    public Workspace(StoredWorkspace loaded)
    {
        notes = loaded.Notes.ToDictionary(n => n.Id);
        foreach (var note in loaded.Notes) saved[note.Id] = note.Generation;
        session = Clone(loaded.Session);
    }

    public IReadOnlyList<Note> Notes { get { lock (sync) return notes.Values.Where(n => !n.Deleted).OrderBy(n => n.SortOrder).ToArray(); } }
    public SessionState Session { get { lock (sync) return Clone(session); } }
    public bool IsDirty { get { lock (sync) return revision != savedRevision || notes.Values.Any(Dirty); } }
    private bool Dirty(Note n) => !saved.TryGetValue(n.Id, out var generation) || generation != n.Generation;

    public Note Create()
    {
        lock (sync)
        {
            var now = DateTimeOffset.UtcNow;
            var note = new Note(Guid.NewGuid(), "新しいノート", "", now, now,
                notes.Count == 0 ? 0 : notes.Values.Max(n => n.SortOrder) + 1, false, 1);
            notes.Add(note.Id, note);
            session = session with { ActiveNoteId = note.Id };
            revision++;
            return note;
        }
    }

    public void Edit(Guid id, string? title = null, string? content = null, bool? archived = null)
    {
        lock (sync)
        {
            var old = notes[id];
            if (old.Deleted) throw new InvalidOperationException("Deleted note.");
            var next = old with { Title = title ?? old.Title, Content = content ?? old.Content, IsArchived = archived ?? old.IsArchived };
            if (next == old) return;
            notes[id] = next with { Generation = old.Generation + 1, UpdatedAt = DateTimeOffset.UtcNow };
        }
    }

    public bool Delete(Guid id, bool confirmed)
    {
        if (!confirmed) return false;
        lock (sync)
        {
            var note = notes[id];
            notes[id] = note with { Deleted = true, Generation = note.Generation + 1 };
            session.Views.Remove(id);
            if (session.ActiveNoteId == id) session = session with { ActiveNoteId = null };
            revision++;
            return true;
        }
    }

    public void SetSession(SessionState value)
    {
        lock (sync)
        {
            if (session.ActiveNoteId == value.ActiveNoteId && session.Window == value.Window &&
                session.Views.Count == value.Views.Count && session.Views.All(p => value.Views.TryGetValue(p.Key, out var v) && v == p.Value)) return;
            session = Clone(value); revision++;
        }
    }

    public IReadOnlyList<Note> Search(string query) => Notes.Where(n =>
        n.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || n.Content.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();

    internal SaveBatch Snapshot()
    {
        lock (sync) return new(notes.Values.Where(Dirty).ToArray(), Clone(session), revision);
    }

    internal void Acknowledge(SaveBatch batch)
    {
        lock (sync)
        {
            foreach (var n in batch.Notes)
            {
                saved[n.Id] = n.Generation;
                if (n.Deleted && notes.TryGetValue(n.Id, out var current) && current.Generation == n.Generation)
                { notes.Remove(n.Id); saved.Remove(n.Id); }
            }
            savedRevision = batch.Revision;
        }
    }

    private static SessionState Clone(SessionState state) => state with { Views = new(state.Views) };
}

public sealed class SaveCoordinator(Workspace workspace, INoteStore store)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task SaveAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!workspace.IsDirty) return;
            // Snapshot AFTER acquiring the gate so queued saves never write an older snapshot.
            var batch = workspace.Snapshot();
            await Task.Run(() => store.Save(batch)).ConfigureAwait(false);
            workspace.Acknowledge(batch); // Never acknowledge failed writes or newer generations.
        }
        finally { gate.Release(); }
    }
}

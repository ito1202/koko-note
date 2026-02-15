namespace FlashMem.Domain.Notes;

public sealed class NoteWorkspace
{
    private readonly List<Note> _notes;
    private NoteId _selectedNoteId;

    public NoteWorkspace(IEnumerable<Note> notes, NoteId? selectedNoteId = null)
    {
        _notes = notes.ToList();
        if (_notes.Count == 0)
        {
            _notes.Add(new Note(NoteId.New(), "Quick Memo", string.Empty, DateTimeOffset.UtcNow));
        }

        _selectedNoteId = ResolveInitialSelection(selectedNoteId);
    }

    public IReadOnlyList<Note> Notes => _notes;

    public IReadOnlyList<Note> VisibleNotes => _notes.Where(x => !x.IsArchived).ToList();

    public Note SelectedNote => _notes.First(x => x.Id == _selectedNoteId);

    public void MoveSelection(int step)
    {
        var visible = VisibleNotes;
        if (visible.Count <= 1 || step == 0)
        {
            return;
        }

        var currentIndex = -1;
        for (var index = 0; index < visible.Count; index++)
        {
            if (visible[index].Id == _selectedNoteId)
            {
                currentIndex = index;
                break;
            }
        }

        if (currentIndex < 0)
        {
            _selectedNoteId = visible[0].Id;
            return;
        }

        var nextIndex = Math.Clamp(currentIndex + step, 0, visible.Count - 1);
        _selectedNoteId = visible[nextIndex].Id;
    }

    public void Select(NoteId noteId)
    {
        var target = _notes.FirstOrDefault(x => x.Id == noteId && !x.IsArchived);
        if (target is null)
        {
            return;
        }

        _selectedNoteId = target.Id;
    }

    public bool ArchiveSelected(DateTimeOffset nowUtc)
    {
        if (VisibleNotes.Count <= 1)
        {
            return false;
        }

        var index = _notes.FindIndex(x => x.Id == _selectedNoteId);
        if (index < 0)
        {
            return false;
        }

        _notes[index] = _notes[index].Archive(nowUtc);
        _selectedNoteId = ResolveInitialSelection(null);
        return true;
    }

    public void CreateNote(DateTimeOffset nowUtc)
    {
        var note = new Note(
            Id: NoteId.New(),
            Title: "New Memo",
            Content: string.Empty,
            UpdatedAtUtc: nowUtc,
            IsArchived: false);
        _notes.Insert(0, note);
        _selectedNoteId = note.Id;
    }

    public bool DeleteSelected()
    {
        if (_notes.Count <= 1)
        {
            _notes[0] = _notes[0].Update("New Memo", string.Empty, DateTimeOffset.UtcNow);
            _selectedNoteId = _notes[0].Id;
            return false;
        }

        var index = _notes.FindIndex(x => x.Id == _selectedNoteId);
        if (index < 0)
        {
            return false;
        }

        _notes.RemoveAt(index);
        _selectedNoteId = ResolveInitialSelection(null);
        return true;
    }

    public void UpdateSelected(string title, string content, DateTimeOffset nowUtc)
    {
        var index = _notes.FindIndex(x => x.Id == _selectedNoteId);
        if (index < 0)
        {
            return;
        }

        _notes[index] = _notes[index].Update(title, content, nowUtc);
    }

    private NoteId ResolveInitialSelection(NoteId? preferredSelection)
    {
        if (preferredSelection is not null && _notes.Any(x => x.Id == preferredSelection && !x.IsArchived))
        {
            return preferredSelection.Value;
        }

        var firstVisible = _notes.FirstOrDefault(x => !x.IsArchived);
        if (firstVisible is not null)
        {
            return firstVisible.Id;
        }

        _notes[0] = _notes[0] with { IsArchived = false };
        return _notes[0].Id;
    }
}

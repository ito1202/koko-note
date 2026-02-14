using FlashMem.Domain.Notes;

namespace FlashMem.Application.Notes;

public sealed class NoteEditorService
{
    private readonly NoteWorkspace _workspace;

    public NoteEditorService(NoteWorkspace workspace)
    {
        _workspace = workspace;
    }

    public IReadOnlyList<Note> VisibleNotes => _workspace.VisibleNotes;

    public Note SelectedNote => _workspace.SelectedNote;

    public NoteWorkspace Workspace => _workspace;

    public void MoveSelectionDown()
    {
        _workspace.MoveSelection(1);
    }

    public void MoveSelectionUp()
    {
        _workspace.MoveSelection(-1);
    }

    public void Select(NoteId noteId)
    {
        _workspace.Select(noteId);
    }

    public bool ArchiveSelected(DateTimeOffset nowUtc)
    {
        return _workspace.ArchiveSelected(nowUtc);
    }

    public void UpdateSelected(string title, string content, DateTimeOffset nowUtc)
    {
        _workspace.UpdateSelected(title, content, nowUtc);
    }
}

using FlashMem.Domain.Notes;

namespace FlashMem.Domain.Tests;

public sealed class NoteWorkspaceTests
{
    [Fact]
    public void Initializes_With_First_NonArchived_Note_Selected()
    {
        var note1 = TestNote.Create("n1", archived: true);
        var note2 = TestNote.Create("n2");
        var note3 = TestNote.Create("n3");
        var workspace = new NoteWorkspace([note1, note2, note3]);

        Assert.Equal(note2.Id, workspace.SelectedNote.Id);
    }

    [Fact]
    public void MoveSelectionDown_Selects_Next_Visible_Note()
    {
        var note1 = TestNote.Create("n1");
        var note2 = TestNote.Create("n2");
        var workspace = new NoteWorkspace([note1, note2]);

        workspace.MoveSelection(1);

        Assert.Equal(note2.Id, workspace.SelectedNote.Id);
    }

    [Fact]
    public void ArchiveSelected_Hides_Note_And_Selects_Next()
    {
        var note1 = TestNote.Create("n1");
        var note2 = TestNote.Create("n2");
        var note3 = TestNote.Create("n3");
        var workspace = new NoteWorkspace([note1, note2, note3]);

        workspace.ArchiveSelected(DateTimeOffset.UtcNow);

        Assert.Equal(note2.Id, workspace.SelectedNote.Id);
        Assert.Single(workspace.Notes.Where(x => x.IsArchived));
        Assert.Equal(2, workspace.VisibleNotes.Count);
    }

    [Fact]
    public void ArchiveSelected_ReturnsFalse_When_Last_Visible_Note()
    {
        var note = TestNote.Create("n1");
        var workspace = new NoteWorkspace([note]);

        var result = workspace.ArchiveSelected(DateTimeOffset.UtcNow);

        Assert.False(result);
        Assert.False(workspace.SelectedNote.IsArchived);
    }

    [Fact]
    public void CreateNote_Adds_And_Selects_New_Note()
    {
        var workspace = new NoteWorkspace([TestNote.Create("n1")]);

        workspace.CreateNote(DateTimeOffset.UtcNow);

        Assert.Equal(2, workspace.Notes.Count);
        Assert.Equal("New Memo", workspace.SelectedNote.Title);
    }

    [Fact]
    public void DeleteSelected_Removes_Note_And_Reselects()
    {
        var note1 = TestNote.Create("n1");
        var note2 = TestNote.Create("n2");
        var workspace = new NoteWorkspace([note1, note2]);

        workspace.DeleteSelected();

        Assert.Single(workspace.Notes);
        Assert.Equal(note2.Id, workspace.SelectedNote.Id);
    }
}

file static class TestNote
{
    public static Note Create(string title, bool archived = false)
    {
        return new Note(
            NoteId.New(),
            title,
            $"{title}-content",
            DateTimeOffset.UtcNow,
            archived);
    }
}

using FlashMem.Application.Notes;
using FlashMem.Domain.Notes;

namespace FlashMem.Application.Tests;

public sealed class NoteEditorServiceTests
{
    [Fact]
    public void MoveSelectionDown_Changes_Selected_Note()
    {
        var workspace = new NoteWorkspace([NoteFactory.Create("A"), NoteFactory.Create("B")]);
        var service = new NoteEditorService(workspace);

        service.MoveSelectionDown();

        Assert.Equal("B", service.SelectedNote.Title);
    }

    [Fact]
    public void UpdateSelected_Changes_Title_And_Content()
    {
        var workspace = new NoteWorkspace([NoteFactory.Create("A")]);
        var service = new NoteEditorService(workspace);

        service.UpdateSelected("New Title", "New Content", DateTimeOffset.UtcNow);

        Assert.Equal("New Title", service.SelectedNote.Title);
        Assert.Equal("New Content", service.SelectedNote.Content);
    }
}

file static class NoteFactory
{
    public static Note Create(string title)
    {
        return new Note(NoteId.New(), title, $"{title}-content", DateTimeOffset.UtcNow);
    }
}

using FlashMem.Domain.Notes;

namespace FlashMem.Desktop.ViewModels;

public sealed class NoteListItemViewModel
{
    public NoteListItemViewModel(NoteId id, string title, DateTimeOffset updatedAtUtc)
    {
        Id = id;
        Title = title;
        UpdatedAtUtc = updatedAtUtc;
    }

    public NoteId Id { get; }

    public string Title { get; }

    public DateTimeOffset UpdatedAtUtc { get; }
}

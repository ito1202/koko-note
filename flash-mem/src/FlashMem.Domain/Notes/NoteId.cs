namespace FlashMem.Domain.Notes;

public readonly record struct NoteId(Guid Value)
{
    public static NoteId New() => new(Guid.NewGuid());
}

namespace FlashMem.Domain.Notes;

public sealed record Note(
    NoteId Id,
    string Title,
    string Content,
    DateTimeOffset UpdatedAtUtc,
    bool IsArchived = false)
{
    public Note Update(string title, string content, DateTimeOffset nowUtc)
    {
        return this with
        {
            Title = NormalizeTitle(title),
            Content = content,
            UpdatedAtUtc = nowUtc,
        };
    }

    public Note Archive(DateTimeOffset nowUtc)
    {
        return this with
        {
            IsArchived = true,
            UpdatedAtUtc = nowUtc,
        };
    }

    private static string NormalizeTitle(string title)
    {
        return string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
    }
}

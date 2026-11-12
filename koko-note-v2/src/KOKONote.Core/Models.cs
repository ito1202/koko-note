namespace KOKONote.Core;

public sealed record Note(Guid Id, string Title, string Content, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, int SortOrder, bool IsArchived, long Generation, bool Deleted = false);

public sealed record EditorView(int SelectionStart = 0, int SelectionLength = 0, int ScrollX = 0, int ScrollY = 0);
public sealed record WindowPlacement(int X = 100, int Y = 100, int Width = 800, int Height = 600, bool Maximized = false);
public sealed record SessionState(Guid? ActiveNoteId, WindowPlacement Window, Dictionary<Guid, EditorView> Views)
{
    public static SessionState Empty => new(null, new(), new());
}
public sealed record SaveBatch(IReadOnlyList<Note> Notes, SessionState Session, long Revision);
public sealed record StoredWorkspace(IReadOnlyList<Note> Notes, SessionState Session);

public interface INoteStore
{
    StoredWorkspace Load();
    void Save(SaveBatch batch);
}

public enum StorageFailure { Database, Encryption, Decryption, UnsupportedSchema }

public sealed class StorageException(StorageFailure failure) : Exception("Protected note storage failed.")
{
    public StorageFailure Failure { get; } = failure;
}

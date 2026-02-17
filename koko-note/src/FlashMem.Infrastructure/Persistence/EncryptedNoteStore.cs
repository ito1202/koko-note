using System.Text.Json;
using FlashMem.Application.Notes;
using FlashMem.Domain.Notes;
using FlashMem.Infrastructure.Security;

namespace FlashMem.Infrastructure.Persistence;

public sealed class EncryptedNoteStore : INoteStore
{
    private readonly string _filePath;
    private readonly IEncryptor _encryptor;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public EncryptedNoteStore(string filePath, IEncryptor encryptor)
    {
        _filePath = filePath;
        _encryptor = encryptor;
    }

    public async Task<NoteWorkspace> LoadAsync(string password, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new NoteWorkspace(Array.Empty<Note>());
        }

        var encryptedJson = await File.ReadAllTextAsync(_filePath, cancellationToken);
        var encryptedPayload = JsonSerializer.Deserialize<EncryptedPayload>(encryptedJson, JsonOptions)
            ?? throw new InvalidOperationException("Encrypted payload is invalid.");

        var plaintextJson = _encryptor.Decrypt(encryptedPayload, password);
        var model = JsonSerializer.Deserialize<StoredWorkspace>(plaintextJson, JsonOptions)
            ?? throw new InvalidOperationException("Workspace payload is invalid.");

        var notes = model.Notes
            .Select(x => new Note(
                new NoteId(x.Id),
                x.Title,
                x.Content,
                x.UpdatedAtUtc,
                x.IsArchived))
            .ToList();

        NoteId? selected = model.SelectedNoteId is null ? null : new NoteId(model.SelectedNoteId.Value);
        return new NoteWorkspace(notes, selected);
    }

    public async Task SaveAsync(
        NoteWorkspace workspace,
        string password,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var model = new StoredWorkspace(
            1,
            workspace.SelectedNote.Id.Value,
            workspace.Notes.Select(x => new StoredNote(
                x.Id.Value,
                x.Title,
                x.Content,
                x.UpdatedAtUtc,
                x.IsArchived)).ToList());
        var plaintextJson = JsonSerializer.Serialize(model, JsonOptions);
        var encryptedPayload = _encryptor.Encrypt(plaintextJson, password);
        var encryptedJson = JsonSerializer.Serialize(encryptedPayload, JsonOptions);
        var tempPath = $"{_filePath}.tmp";

        await File.WriteAllTextAsync(tempPath, encryptedJson, cancellationToken);
        File.Move(tempPath, _filePath, true);
    }

    private sealed record StoredWorkspace(
        int Version,
        Guid? SelectedNoteId,
        List<StoredNote> Notes);

    private sealed record StoredNote(
        Guid Id,
        string Title,
        string Content,
        DateTimeOffset UpdatedAtUtc,
        bool IsArchived);
}

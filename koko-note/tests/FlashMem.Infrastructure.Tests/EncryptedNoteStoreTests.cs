using FlashMem.Domain.Notes;
using FlashMem.Infrastructure.Persistence;
using FlashMem.Infrastructure.Security;

namespace FlashMem.Infrastructure.Tests;

public sealed class EncryptedNoteStoreTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTrips_Notes()
    {
        var tempDir = Directory.CreateTempSubdirectory("flashmem-tests-");
        try
        {
            var filePath = Path.Combine(tempDir.FullName, "notes.json.enc");
            var store = new EncryptedNoteStore(filePath, new AesGcmArgon2Encryptor());
            var workspace = new NoteWorkspace([new Note(NoteId.New(), "Title", "Body", DateTimeOffset.UtcNow)]);

            await store.SaveAsync(workspace, "pw");
            var loaded = await store.LoadAsync("pw");

            Assert.Single(loaded.Notes);
            Assert.Equal("Title", loaded.SelectedNote.Title);
        }
        finally
        {
            tempDir.Delete(true);
        }
    }

    [Fact]
    public async Task Saved_File_Does_Not_Contain_Plaintext()
    {
        var tempDir = Directory.CreateTempSubdirectory("flashmem-tests-");
        try
        {
            var filePath = Path.Combine(tempDir.FullName, "notes.json.enc");
            var store = new EncryptedNoteStore(filePath, new AesGcmArgon2Encryptor());
            var workspace = new NoteWorkspace([new Note(NoteId.New(), "PlainTitle", "PlainBody", DateTimeOffset.UtcNow)]);

            await store.SaveAsync(workspace, "pw");
            var content = await File.ReadAllTextAsync(filePath);

            Assert.DoesNotContain("PlainTitle", content);
            Assert.DoesNotContain("PlainBody", content);
        }
        finally
        {
            tempDir.Delete(true);
        }
    }
}

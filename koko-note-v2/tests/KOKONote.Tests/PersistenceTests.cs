using System.Text;
using KOKONote.Core;
using Microsoft.Data.Sqlite;

namespace KOKONote.Tests;

public class PersistenceTests
{
    [Fact]
    public async Task CreateUnicodeAndEmojiReload()
    {
        using var db = new TestDatabase();
        var workspace = new Workspace(db.Store.Load());
        var note = workspace.Create();
        workspace.Edit(note.Id, title: "秘密の下書き 🔐", content: "日本語の長文\r\n👩🏽‍💻😀\t終わり");
        await new SaveCoordinator(workspace, db.Store).SaveAsync();
        var loaded = Assert.Single(db.Store.Load().Notes);
        Assert.Equal(workspace.Notes[0], loaded);
        Assert.False(workspace.IsDirty);
    }

    [Fact]
    public async Task SessionAndClosedNotesSurviveRestart()
    {
        using var db = new TestDatabase();
        var w = new Workspace(db.Store.Load());
        var a = w.Create(); var b = w.Create();
        w.Edit(a.Id, archived: true);
        w.SetSession(new(b.Id, new(20, 30, 900, 700), new() { [b.Id] = new(5, 2, 11, 120) }));
        await new SaveCoordinator(w, db.Store).SaveAsync();
        var restarted = new Workspace(db.Store.Load());
        Assert.Equal(2, restarted.Notes.Count);
        Assert.True(restarted.Notes.Single(n => n.Id == a.Id).IsArchived);
        Assert.Equal(b.Id, restarted.Session.ActiveNoteId);
        Assert.Equal(new EditorView(5, 2, 11, 120), restarted.Session.Views[b.Id]);
        Assert.Equal(900, restarted.Session.Window.Width);
    }

    [Fact]
    public async Task RepeatedEditsAndRapidSwitchesPersistLatestGeneration()
    {
        using var db = new TestDatabase();
        var w = new Workspace(db.Store.Load());
        var notes = Enumerable.Range(0, 8).Select(_ => w.Create()).ToArray();
        var saver = new SaveCoordinator(w, db.Store);
        var saves = new List<Task>();
        for (var i = 0; i < 100; i++)
        {
            var n = notes[i % notes.Length];
            w.Edit(n.Id, content: $"日本語 {i} 😀");
            w.SetSession(w.Session with { ActiveNoteId = n.Id });
            saves.Add(saver.SaveAsync());
        }
        await Task.WhenAll(saves);
        Assert.Equal(w.Notes, db.Store.Load().Notes);
        Assert.False(w.IsDirty);
    }

    [Fact]
    public async Task TransactionFailureRollsBackEveryNoteAndRetainsMemory()
    {
        using var db = new TestDatabase();
        var w = new Workspace(db.Store.Load());
        var a = w.Create(); var b = w.Create();
        var saver = new SaveCoordinator(w, db.Store);
        await saver.SaveAsync();
        Execute(db.Path, $"CREATE TRIGGER fail_write BEFORE UPDATE ON Notes WHEN NEW.Id='{b.Id:D}' BEGIN SELECT RAISE(ABORT,'injected failure'); END;");
        w.Edit(a.Id, content: "Aの未保存内容"); w.Edit(b.Id, content: "Bの未保存内容");
        await Assert.ThrowsAsync<StorageException>(saver.SaveAsync);
        Assert.True(w.IsDirty);
        Assert.Equal("Aの未保存内容", w.Notes.Single(n => n.Id == a.Id).Content);
        Assert.All(db.Store.Load().Notes, n => Assert.Empty(n.Content));
        Execute(db.Path, "DROP TRIGGER fail_write;");
        await saver.SaveAsync();
        Assert.Equal(w.Notes, db.Store.Load().Notes);
    }

    [Fact]
    public async Task CorruptionFailsClosedWithoutReplacingCiphertext()
    {
        using var db = new TestDatabase();
        var w = new Workspace(db.Store.Load()); w.Create();
        await new SaveCoordinator(w, db.Store).SaveAsync();
        Execute(db.Path, "UPDATE Notes SET EncryptedContent=zeroblob(64);");
        var error = Assert.Throws<StorageException>(() => db.Store.Load());
        Assert.Equal(StorageFailure.Decryption, error.Failure);
        using var c = new SqliteConnection($"Data Source={db.Path};Pooling=False"); c.Open();
        using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT hex(EncryptedContent) FROM Notes;";
        Assert.Equal(new string('0', 128), cmd.ExecuteScalar());
    }

    [Fact]
    public async Task LowerGenerationCannotOverwriteNewerContent()
    {
        using var db = new TestDatabase();
        var w = new Workspace(db.Store.Load()); var n = w.Create();
        db.Store.Save(new([n with { Content = "new", Generation = 9 }], w.Session, 1));
        db.Store.Save(new([n with { Content = "old", Generation = 2 }], w.Session, 2));
        Assert.Equal("new", Assert.Single(db.Store.Load().Notes).Content);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task DeleteRequiresConfirmationAndCloseDoesNotDelete()
    {
        using var db = new TestDatabase(); var w = new Workspace(db.Store.Load()); var n = w.Create();
        var saver = new SaveCoordinator(w, db.Store); await saver.SaveAsync();
        Assert.False(w.Delete(n.Id, false));
        w.Edit(n.Id, archived: true); await saver.SaveAsync();
        Assert.Single(db.Store.Load().Notes);
        Assert.True(w.Delete(n.Id, true)); await saver.SaveAsync();
        Assert.Empty(db.Store.Load().Notes);
    }

    [Fact]
    public Task DatabaseWalJournalAndBackupContainNoPlaintextMarkers() => VerifyCiphertext();

    [WindowsFact]
    public Task DpapiDatabaseWalAndBackupContainNoPlaintextMarkers() => VerifyCiphertext(new DpapiProtector());

    private static async Task VerifyCiphertext(IContentProtector? protector = null)
    {
        using var db = new TestDatabase(protector); var w = new Workspace(db.Store.Load());
        using var keepWal = new SqliteConnection($"Data Source={db.Path};Pooling=False"); keepWal.Open();
        using var readTransaction = keepWal.BeginTransaction(deferred: true);
        using var pin = keepWal.CreateCommand(); pin.Transaction = readTransaction;
        pin.CommandText = "SELECT count(*) FROM Notes;"; pin.ExecuteScalar();
        var title = "TITLE-secret-" + Guid.NewGuid(); var content = "本文SECRET-😀-" + Guid.NewGuid();
        var n = w.Create(); w.Edit(n.Id, title: title, content: content);
        await new SaveCoordinator(w, db.Store).SaveAsync();
        Assert.True(File.Exists(db.Path + "-wal"));
        db.Store.Backup(System.IO.Path.Combine(db.Directory, "snapshot.bak"));
        using var cmd = keepWal.CreateCommand();
        cmd.Transaction = readTransaction;
        cmd.CommandText = "SELECT typeof(EncryptedTitle)||','||typeof(EncryptedContent) FROM Notes;";
        // The pinned snapshot precedes the write; inspect storage types from a new reader.
        using (var types = new SqliteConnection($"Data Source={db.Path};Pooling=False"))
        {
            types.Open(); using var typeQuery = types.CreateCommand(); typeQuery.CommandText = cmd.CommandText;
            Assert.Equal("blob,blob", typeQuery.ExecuteScalar());
        }
        foreach (var file in Directory.GetFiles(db.Directory))
        {
            var bytes = File.ReadAllBytes(file);
            foreach (var encoding in new[] { Encoding.UTF8, Encoding.Unicode })
            {
                Assert.False(bytes.AsSpan().IndexOf(encoding.GetBytes(title)) >= 0, file);
                Assert.False(bytes.AsSpan().IndexOf(encoding.GetBytes(content)) >= 0, file);
            }
        }
        var restored = new SqliteNoteStore(System.IO.Path.Combine(db.Directory, "snapshot.bak"), db.Protector).Load();
        Assert.Equal(content, Assert.Single(restored.Notes).Content);
        Assert.Equal(title, restored.Notes[0].Title);
        var backup = new SqliteNoteStore(System.IO.Path.Combine(db.Directory, "snapshot.bak"), new RejectProtector());
        Assert.Equal(StorageFailure.Decryption, Assert.Throws<StorageException>(() => backup.Load()).Failure);
    }

    [Fact]
    public async Task EncryptionFailureLeavesCommittedContentAndDirtyMemoryIntact()
    {
        var protector = new FailingProtector();
        using var db = new TestDatabase(protector); var w = new Workspace(db.Store.Load());
        var n = w.Create(); var saver = new SaveCoordinator(w, db.Store); await saver.SaveAsync();
        w.Edit(n.Id, content: "暗号化に失敗しても保持する本文"); protector.Fail = true;
        var error = await Assert.ThrowsAsync<StorageException>(saver.SaveAsync);
        Assert.Equal(StorageFailure.Encryption, error.Failure);
        Assert.True(w.IsDirty); Assert.Equal("暗号化に失敗しても保持する本文", w.Notes[0].Content);
        Assert.Empty(Assert.Single(db.Store.Load().Notes).Content);
        protector.Fail = false; await saver.SaveAsync(); Assert.False(w.IsDirty);
    }

    private sealed class FailingProtector : IContentProtector
    {
        private readonly TestProtector inner = new();
        public bool Fail;
        public byte[] Protect(string text, Guid id, string purpose) => Fail
            ? throw new StorageException(StorageFailure.Encryption) : inner.Protect(text, id, purpose);
        public string Unprotect(byte[] encrypted, Guid id, string purpose) => inner.Unprotect(encrypted, id, purpose);
    }

    [Fact]
    public void FutureAndUnknownSchemasAreRejected()
    {
        using var db = new TestDatabase();
        Execute(db.Path, "CREATE TABLE Unknown(Value TEXT); PRAGMA user_version=20;");
        Assert.Equal(StorageFailure.UnsupportedSchema, Assert.Throws<StorageException>(() => db.Store.Load()).Failure);
        Execute(db.Path, "PRAGMA user_version=0;");
        Assert.Equal(StorageFailure.UnsupportedSchema, Assert.Throws<StorageException>(() => db.Store.Load()).Failure);
    }

    [Fact]
    public void SearchUsesDecryptedMemoryAndMatchesClosedNotes()
    {
        var w = new Workspace(new([], SessionState.Empty)); var n = w.Create();
        w.Edit(n.Id, title: "会議", content: "Prompt 日本語", archived: true);
        Assert.Single(w.Search("prompt")); Assert.Single(w.Search("会議")); Assert.Empty(w.Search("not found"));
    }

    private sealed class RejectProtector : IContentProtector
    {
        public byte[] Protect(string text, Guid id, string purpose) => throw new NotSupportedException();
        public string Unprotect(byte[] encrypted, Guid id, string purpose) => throw new StorageException(StorageFailure.Decryption);
    }
    private static void Execute(string path, string sql)
    {
        using var c = new SqliteConnection($"Data Source={path};Pooling=False"); c.Open();
        using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery();
    }
}

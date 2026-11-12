using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace KOKONote.Core;

public sealed class SqliteNoteStore(string path, IContentProtector protector) : INoteStore
{
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false, DefaultTimeout = 2 }.ToString());
        try
        {
            connection.Open();
            Execute(connection, "PRAGMA synchronous=FULL; PRAGMA temp_store=MEMORY; PRAGMA trusted_schema=OFF; PRAGMA secure_delete=ON;");
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    public StoredWorkspace Load()
    {
        try
        {
            using var c = Open();
            var version = Convert.ToInt32(Scalar(c, "PRAGMA user_version;"), CultureInfo.InvariantCulture);
            if (version != 0 && version != 1) throw new StorageException(StorageFailure.UnsupportedSchema);
            if (version == 0)
            {
                // Never adopt or replace an unknown existing database.
                if (Convert.ToInt64(Scalar(c, "SELECT count(*) FROM sqlite_master WHERE name NOT LIKE 'sqlite_%';"), CultureInfo.InvariantCulture) != 0)
                    throw new StorageException(StorageFailure.UnsupportedSchema);
                Execute(c, "PRAGMA journal_mode=WAL;");
                using var tx = c.BeginTransaction();
                Execute(c, """
                    CREATE TABLE Notes (
                      Id TEXT PRIMARY KEY NOT NULL,
                      EncryptedTitle BLOB NOT NULL CHECK(typeof(EncryptedTitle)='blob'),
                      EncryptedContent BLOB NOT NULL CHECK(typeof(EncryptedContent)='blob'),
                      CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL,
                      SortOrder INTEGER NOT NULL, IsArchived INTEGER NOT NULL CHECK(IsArchived IN (0,1)),
                      Generation INTEGER NOT NULL CHECK(Generation>0));
                    CREATE TABLE AppState (Id INTEGER PRIMARY KEY CHECK(Id=1), Json TEXT NOT NULL);
                    PRAGMA user_version=1;
                    """, tx);
                tx.Commit();
            }
            else Execute(c, "PRAGMA journal_mode=WAL;");
            if (!string.Equals(Scalar(c, "PRAGMA quick_check;")?.ToString(), "ok", StringComparison.Ordinal))
                throw new StorageException(StorageFailure.Database);
            using var command = c.CreateCommand();
            command.CommandText = "SELECT Id,EncryptedTitle,EncryptedContent,CreatedAt,UpdatedAt,SortOrder,IsArchived,Generation FROM Notes ORDER BY SortOrder;";
            using var reader = command.ExecuteReader();
            var notes = new List<Note>();
            while (reader.Read())
            {
                var id = Guid.Parse(reader.GetString(0));
                notes.Add(new(id, protector.Unprotect((byte[])reader[1], id, "title"),
                    protector.Unprotect((byte[])reader[2], id, "content"),
                    DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                    DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                    reader.GetInt32(5), reader.GetBoolean(6), reader.GetInt64(7)));
            }
            reader.Close();
            var json = Scalar(c, "SELECT Json FROM AppState WHERE Id=1;") as string;
            var session = json == null ? SessionState.Empty : JsonSerializer.Deserialize<SessionState>(json)
                ?? throw new StorageException(StorageFailure.Database);
            if (session.Views is null || session.Window is null) throw new StorageException(StorageFailure.Database);
            return new(notes, session);
        }
        catch (StorageException) { throw; }
        catch (Exception e) when (e is SqliteException or JsonException or FormatException or InvalidCastException or OverflowException)
        { throw new StorageException(StorageFailure.Database); }
    }

    public void Save(SaveBatch batch)
    {
        try
        {
            // Encrypt all text before issuing SQL. Even journals and temporary SQLite pages see only ciphertext.
            var encrypted = batch.Notes.Select(n => (Note: n,
                Title: n.Deleted ? [] : protector.Protect(n.Title, n.Id, "title"),
                Content: n.Deleted ? [] : protector.Protect(n.Content, n.Id, "content"))).ToArray();
            using var c = Open();
            using var tx = c.BeginTransaction();
            foreach (var item in encrypted)
            {
                var n = item.Note;
                using var cmd = c.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = n.Deleted ? "DELETE FROM Notes WHERE Id=$id AND Generation <= $generation;" : """
                    INSERT INTO Notes (Id,EncryptedTitle,EncryptedContent,CreatedAt,UpdatedAt,SortOrder,IsArchived,Generation)
                    VALUES ($id,$title,$content,$created,$updated,$sort,$archived,$generation)
                    ON CONFLICT(Id) DO UPDATE SET EncryptedTitle=excluded.EncryptedTitle,
                    EncryptedContent=excluded.EncryptedContent,UpdatedAt=excluded.UpdatedAt,
                    SortOrder=excluded.SortOrder,IsArchived=excluded.IsArchived,Generation=excluded.Generation
                    WHERE excluded.Generation >= Notes.Generation;
                    """;
                cmd.Parameters.AddWithValue("$id", n.Id.ToString("D"));
                cmd.Parameters.AddWithValue("$generation", n.Generation);
                if (!n.Deleted)
                {
                    cmd.Parameters.Add("$title", SqliteType.Blob).Value = item.Title;
                    cmd.Parameters.Add("$content", SqliteType.Blob).Value = item.Content;
                    cmd.Parameters.AddWithValue("$created", n.CreatedAt.ToString("O"));
                    cmd.Parameters.AddWithValue("$updated", n.UpdatedAt.ToString("O"));
                    cmd.Parameters.AddWithValue("$sort", n.SortOrder);
                    cmd.Parameters.AddWithValue("$archived", n.IsArchived ? 1 : 0);
                }
                cmd.ExecuteNonQuery();
            }
            using var state = c.CreateCommand();
            state.Transaction = tx;
            state.CommandText = "INSERT INTO AppState(Id,Json) VALUES(1,$json) ON CONFLICT(Id) DO UPDATE SET Json=excluded.Json;";
            state.Parameters.AddWithValue("$json", JsonSerializer.Serialize(batch.Session));
            state.ExecuteNonQuery();
            tx.Commit();
        }
        catch (StorageException) { throw; }
        catch (SqliteException) { throw new StorageException(StorageFailure.Database); }
    }

    // Used before any future migration; a live WAL database must never be backed up with File.Copy.
    public void Backup(string destination)
    {
        if (File.Exists(destination)) throw new IOException("Backup already exists.");
        using var source = Open();
        using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination, Pooling = false }.ToString());
        target.Open();
        source.BackupDatabase(target);
    }

    private static object? Scalar(SqliteConnection c, string sql)
    { using var cmd = c.CreateCommand(); cmd.CommandText = sql; return cmd.ExecuteScalar(); }
    private static void Execute(SqliteConnection c, string sql, SqliteTransaction? tx = null)
    { using var cmd = c.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql; cmd.ExecuteNonQuery(); }
}

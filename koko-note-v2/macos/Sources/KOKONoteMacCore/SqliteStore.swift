import Foundation
import SQLite3

private let transient = unsafeBitCast(-1, to: sqlite3_destructor_type.self)

public final class Database {
    public let handle: OpaquePointer
    public init(path: String) throws {
        var pointer: OpaquePointer?
        guard sqlite3_open_v2(path, &pointer, SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE | SQLITE_OPEN_FULLMUTEX, nil) == SQLITE_OK, let pointer else {
            if let pointer { sqlite3_close(pointer) }; throw NoteError.database
        }
        sqlite3_busy_timeout(pointer, 2000)
        guard sqlite3_exec(pointer, "PRAGMA synchronous=FULL; PRAGMA temp_store=MEMORY; PRAGMA trusted_schema=OFF; PRAGMA secure_delete=ON;", nil, nil, nil) == SQLITE_OK else {
            sqlite3_close(pointer); throw NoteError.database
        }
        handle = pointer
    }
    deinit { sqlite3_close(handle) }
    public func execute(_ sql: String) throws {
        guard sqlite3_exec(handle, sql, nil, nil, nil) == SQLITE_OK else { throw NoteError.database }
    }
    public func statement(_ sql: String) throws -> Statement { try Statement(database: self, sql: sql) }
    public func scalar(_ sql: String) throws -> String? {
        let stmt = try statement(sql); return try stmt.next() ? stmt.text(0) : nil
    }
}

public final class Statement {
    private let database: Database // Keep the connection alive until finalize.
    private let handle: OpaquePointer
    init(database: Database, sql: String) throws {
        self.database = database
        var stmt: OpaquePointer?
        guard sqlite3_prepare_v2(database.handle, sql, -1, &stmt, nil) == SQLITE_OK, let stmt else { throw NoteError.database }
        handle = stmt
    }
    deinit { sqlite3_finalize(handle) }
    public func bind(_ index: Int32, text: String) throws {
        guard sqlite3_bind_text(handle, index, text, -1, transient) == SQLITE_OK else { throw NoteError.database }
    }
    public func bind(_ index: Int32, data: Data) throws {
        let result = data.withUnsafeBytes { sqlite3_bind_blob(handle, index, $0.baseAddress, Int32(data.count), transient) }
        guard result == SQLITE_OK else { throw NoteError.database }
    }
    public func bind(_ index: Int32, integer: Int64) throws {
        guard sqlite3_bind_int64(handle, index, integer) == SQLITE_OK else { throw NoteError.database }
    }
    public func bind(_ index: Int32, number: Double) throws {
        guard sqlite3_bind_double(handle, index, number) == SQLITE_OK else { throw NoteError.database }
    }
    @discardableResult public func next() throws -> Bool {
        let result = sqlite3_step(handle)
        guard result == SQLITE_ROW || result == SQLITE_DONE else { throw NoteError.database }
        return result == SQLITE_ROW
    }
    public func text(_ index: Int32) -> String { sqlite3_column_text(handle, index).map { String(cString: $0) } ?? "" }
    public func integer(_ index: Int32) -> Int64 { sqlite3_column_int64(handle, index) }
    public func number(_ index: Int32) -> Double { sqlite3_column_double(handle, index) }
    public func data(_ index: Int32) -> Data {
        guard let bytes = sqlite3_column_blob(handle, index) else { return Data() }
        return Data(bytes: bytes, count: Int(sqlite3_column_bytes(handle, index)))
    }
}

public final class SqliteStore: NoteStore {
    public let path: String
    private let cipher: ContentCipher
    public init(path: String, cipher: ContentCipher) { self.path = path; self.cipher = cipher }
    public func load() throws -> StoredWorkspace {
        let db = try Database(path: path)
        let version = try db.scalar("PRAGMA user_version;")
        guard version == "0" || version == "1" else { throw NoteError.schema }
        if version == "0" {
            guard try db.scalar("SELECT count(*) FROM sqlite_master WHERE name NOT LIKE 'sqlite_%';") == "0" else { throw NoteError.schema }
            guard try db.scalar("PRAGMA journal_mode=WAL;") == "wal" else { throw NoteError.database }
            try db.execute("BEGIN IMMEDIATE;")
            do {
                try db.execute("""
                CREATE TABLE Notes(Id TEXT PRIMARY KEY NOT NULL,
                    EncryptedTitle BLOB NOT NULL CHECK(typeof(EncryptedTitle)='blob'),
                    EncryptedContent BLOB NOT NULL CHECK(typeof(EncryptedContent)='blob'),
                    CreatedAt REAL NOT NULL,UpdatedAt REAL NOT NULL,SortOrder INTEGER NOT NULL,
                    IsArchived INTEGER NOT NULL CHECK(IsArchived IN (0,1)),Generation INTEGER NOT NULL CHECK(Generation>0));
                CREATE TABLE AppState(Id INTEGER PRIMARY KEY CHECK(Id=1),Json TEXT NOT NULL);
                PRAGMA user_version=1;
                COMMIT;
                """)
            } catch { try? db.execute("ROLLBACK;"); throw error }
        } else { guard try db.scalar("PRAGMA journal_mode=WAL;") == "wal" else { throw NoteError.database } }
        guard try db.scalar("PRAGMA quick_check;") == "ok" else { throw NoteError.database }
        let stmt = try db.statement("SELECT Id,EncryptedTitle,EncryptedContent,CreatedAt,UpdatedAt,SortOrder,IsArchived,Generation FROM Notes ORDER BY SortOrder;")
        var notes: [Note] = []
        while try stmt.next() {
            guard let id = UUID(uuidString: stmt.text(0)) else { throw NoteError.database }
            notes.append(Note(id: id, title: try cipher.decrypt(stmt.data(1), id: id, field: "title"),
                              content: try cipher.decrypt(stmt.data(2), id: id, field: "content"),
                              created: Date(timeIntervalSince1970: stmt.number(3)), updated: Date(timeIntervalSince1970: stmt.number(4)),
                              order: Int(stmt.integer(5)), archived: stmt.integer(6) != 0, generation: stmt.integer(7)))
        }
        var session = Session()
        if let json = try db.scalar("SELECT Json FROM AppState WHERE Id=1;") {
            do { session = try JSONDecoder().decode(Session.self, from: Data(json.utf8)) }
            catch { throw NoteError.database }
        }
        return StoredWorkspace(notes: notes, session: session)
    }
    public func save(_ snapshot: Snapshot) throws {
        let rows = try snapshot.notes.map { note in
            (note, try note.deleted ? Data() : cipher.encrypt(note.title, id: note.id, field: "title"),
             try note.deleted ? Data() : cipher.encrypt(note.content, id: note.id, field: "content"))
        }
        let db = try Database(path: path)
        try db.execute("BEGIN IMMEDIATE;")
        do {
            for (note, title, content) in rows {
                if note.deleted {
                    let stmt = try db.statement("DELETE FROM Notes WHERE Id=?1 AND Generation<=?2;")
                    try stmt.bind(1, text: note.id.uuidString); try stmt.bind(2, integer: note.generation); try stmt.next()
                } else {
                    let stmt = try db.statement("""
                    INSERT INTO Notes VALUES(?1,?2,?3,?4,?5,?6,?7,?8)
                    ON CONFLICT(Id) DO UPDATE SET EncryptedTitle=excluded.EncryptedTitle,EncryptedContent=excluded.EncryptedContent,
                    UpdatedAt=excluded.UpdatedAt,SortOrder=excluded.SortOrder,IsArchived=excluded.IsArchived,Generation=excluded.Generation
                    WHERE excluded.Generation>=Notes.Generation;
                    """)
                    try stmt.bind(1, text: note.id.uuidString); try stmt.bind(2, data: title); try stmt.bind(3, data: content)
                    try stmt.bind(4, number: note.created.timeIntervalSince1970); try stmt.bind(5, number: note.updated.timeIntervalSince1970)
                    try stmt.bind(6, integer: Int64(note.order)); try stmt.bind(7, integer: note.archived ? 1 : 0)
                    try stmt.bind(8, integer: note.generation); try stmt.next()
                }
            }
            let state = try db.statement("INSERT INTO AppState VALUES(1,?1) ON CONFLICT(Id) DO UPDATE SET Json=excluded.Json;")
            let json = try JSONEncoder().encode(snapshot.session)
            try state.bind(1, text: String(decoding: json, as: UTF8.self)); try state.next()
            try db.execute("COMMIT;")
        } catch { try? db.execute("ROLLBACK;"); throw error }
    }
    public func backup(to destination: String) throws {
        guard !FileManager.default.fileExists(atPath: destination) else { throw NoteError.database }
        let source = try Database(path: path), target = try Database(path: destination)
        guard let copy = sqlite3_backup_init(target.handle, "main", source.handle, "main") else { throw NoteError.database }
        let result = sqlite3_backup_step(copy, -1), finish = sqlite3_backup_finish(copy)
        guard result == SQLITE_DONE && finish == SQLITE_OK else { throw NoteError.database }
    }
}

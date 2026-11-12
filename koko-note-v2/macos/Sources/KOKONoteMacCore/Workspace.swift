import Foundation

public enum NoteError: Error, Equatable {
    case database, encryption, decryption, keychain(Int32), missingKey, schema, permissions, alreadyRunning
}

public struct Note: Equatable, Sendable {
    public var id: UUID
    public var title: String
    public var content: String
    public var created: Date
    public var updated: Date
    public var order: Int
    public var archived: Bool
    public var generation: Int64
    public var deleted = false
}

public struct EditorState: Codable, Equatable, Sendable {
    public var location = 0
    public var length = 0
    public var x: Double = 0
    public var y: Double = 0
    public init(location: Int = 0, length: Int = 0, x: Double = 0, y: Double = 0) {
        self.location = location; self.length = length; self.x = x; self.y = y
    }
}
public struct WindowState: Codable, Equatable, Sendable {
    public var x: Double = 100, y: Double = 100, width: Double = 800, height: Double = 600
    public init(x: Double = 100, y: Double = 100, width: Double = 800, height: Double = 600) {
        self.x = x; self.y = y; self.width = width; self.height = height
    }
}
public struct Session: Codable, Equatable, Sendable {
    public var active: UUID?
    public var window = WindowState()
    public var views: [UUID: EditorState] = [:]
    public init() {}
}
public struct StoredWorkspace {
    public var notes: [Note]
    public var session: Session
    public init(notes: [Note] = [], session: Session = Session()) { self.notes = notes; self.session = session }
}
public struct Snapshot {
    public var notes: [Note]
    public var session: Session
    public var revision: Int64
}

public protocol NoteStore { func load() throws -> StoredWorkspace; func save(_ snapshot: Snapshot) throws }

public final class Workspace {
    private let lock = NSLock()
    private var records: [UUID: Note]
    private var saved: [UUID: Int64]
    private var state: Session
    private var revision: Int64 = 0
    private var savedRevision: Int64 = 0
    // Persist Unix timestamps at millisecond precision; avoid floating epoch-conversion drift on reload.
    private func timestamp() -> Date { Date(timeIntervalSince1970: floor(Date().timeIntervalSince1970 * 1000) / 1000) }
    public init(_ stored: StoredWorkspace = StoredWorkspace()) {
        records = Dictionary(uniqueKeysWithValues: stored.notes.map { ($0.id, $0) })
        saved = Dictionary(uniqueKeysWithValues: stored.notes.map { ($0.id, $0.generation) })
        state = stored.session
    }
    private func sync<T>(_ action: () -> T) -> T { lock.lock(); defer { lock.unlock() }; return action() }
    private func dirty(_ note: Note) -> Bool { saved[note.id] != note.generation }
    public var notes: [Note] { sync { records.values.filter { !$0.deleted }.sorted { $0.order < $1.order } } }
    public var session: Session { sync { state } }
    public var isDirty: Bool { sync { revision != savedRevision || records.values.contains(where: dirty) } }
    @discardableResult public func create() -> Note {
        sync {
            let now = timestamp()
            let note = Note(id: UUID(), title: "新しいノート", content: "", created: now, updated: now,
                            order: (records.values.map(\.order).max() ?? -1) + 1, archived: false, generation: 1)
            records[note.id] = note; state.active = note.id; revision += 1; return note
        }
    }
    public func edit(_ id: UUID, title: String? = nil, content: String? = nil, archived: Bool? = nil) {
        sync {
            guard var note = records[id], !note.deleted else { return }
            let before = note
            if let title { note.title = title }; if let content { note.content = content }; if let archived { note.archived = archived }
            guard note != before else { return }
            note.generation += 1; note.updated = timestamp(); records[id] = note
        }
    }
    @discardableResult public func delete(_ id: UUID, confirmed: Bool) -> Bool {
        sync {
            guard confirmed, var note = records[id] else { return false }
            note.deleted = true; note.generation += 1; records[id] = note
            state.views.removeValue(forKey: id); if state.active == id { state.active = nil }; revision += 1
            return true
        }
    }
    public func setSession(_ session: Session) { sync { if state != session { state = session; revision += 1 } } }
    public func search(_ query: String) -> [Note] {
        notes.filter { $0.title.localizedCaseInsensitiveContains(query) || $0.content.localizedCaseInsensitiveContains(query) || query.isEmpty }
    }
    public func snapshot() -> Snapshot? {
        sync {
            let changed = records.values.filter(dirty).sorted { $0.order < $1.order }
            guard !changed.isEmpty || revision != savedRevision else { return nil }
            return Snapshot(notes: changed, session: state, revision: revision)
        }
    }
    public func acknowledge(_ snapshot: Snapshot) {
        sync {
            for note in snapshot.notes {
                saved[note.id] = note.generation
                if note.deleted && records[note.id]?.generation == note.generation {
                    records.removeValue(forKey: note.id); saved.removeValue(forKey: note.id)
                }
            }
            savedRevision = snapshot.revision
        }
    }
}

public final class SaveCoordinator {
    private let queue = DispatchQueue(label: "com.kokonote.mac.save", qos: .utility)
    private let workspace: Workspace
    private let store: NoteStore
    public init(workspace: Workspace, store: NoteStore) { self.workspace = workspace; self.store = store }
    public func save(completion: @escaping (Result<Void, Error>) -> Void) {
        queue.async {
            let result: Result<Void, Error>
            do {
                if let snapshot = self.workspace.snapshot() { try self.store.save(snapshot); self.workspace.acknowledge(snapshot) }
                result = .success(())
            } catch { result = .failure(error) }
            DispatchQueue.main.async { completion(result) }
        }
    }
}

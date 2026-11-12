import Testing
import Foundation
import CoreGraphics
import CryptoKit
import SQLite3
@testable import KOKONoteMacCore

@Suite(.serialized)
struct CoreTests {
    private let resources = TestResources()
    private func fixture() throws -> (URL, SqliteStore, ContentCipher) {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent("KOKO-tests-\(UUID())", isDirectory: true)
        try SecureLocation.prepare(directory)
        resources.directories.append(directory)
        let cipher = ContentCipher(key: SymmetricKey(size: .bits256))
        return (directory, SqliteStore(path: directory.appendingPathComponent("notes.db").path, cipher: cipher), cipher)
    }
    private func flush(_ workspace: Workspace, _ store: NoteStore) throws {
        if let snapshot = workspace.snapshot() { try store.save(snapshot); workspace.acknowledge(snapshot) }
    }
    @Test func testJapaneseEmojiRoundTripAndSessionRestore() throws {
        let (_, store, _) = try fixture(); let workspace = Workspace(try store.load())
        let note = workspace.create(); workspace.edit(note.id, title: "日本語🔐", content: "長文\n👩🏽‍💻😀\t下書き")
        var session = workspace.session; session.views[note.id] = EditorState(location: 2, length: 1, y: 150); session.window.width = 900
        workspace.setSession(session); try flush(workspace, store)
        let restored = try store.load(); expectEqual(restored.notes, workspace.notes); expectEqual(restored.session, session)
        expectFalse(workspace.isDirty)
    }
    @Test func testAuthenticationTamperAndFieldBinding() throws {
        let cipher = ContentCipher(key: SymmetricKey(size: .bits256)), id = UUID()
        var encrypted = try cipher.encrypt("秘密の本文", id: id, field: "content")
        expectEqual(try cipher.decrypt(encrypted, id: id, field: "content"), "秘密の本文")
        expectThrows(try cipher.decrypt(encrypted, id: UUID(), field: "content"))
        expectThrows(try cipher.decrypt(encrypted, id: id, field: "title"))
        encrypted[encrypted.count / 2] ^= 0x40
        expectThrows(try cipher.decrypt(encrypted, id: id, field: "content"))
    }
    @Test func testRapidEditsSwitchesAndCloseAreRetained() throws {
        let (_, store, _) = try fixture(); let workspace = Workspace(try store.load())
        let notes = (0..<8).map { _ in workspace.create() }
        for i in 0..<100 {
            let note = notes[i % notes.count]; workspace.edit(note.id, content: "内容 \(i) 😀")
            var session = workspace.session; session.active = note.id; workspace.setSession(session)
            try flush(workspace, store)
        }
        workspace.edit(notes[0].id, archived: true); try flush(workspace, store)
        expectEqual(try store.load().notes, workspace.notes); expectEqual(workspace.notes.count, 8)
        expectTrue(try store.load().notes[0].archived)
    }
    @Test func testOldAcknowledgementNeverMarksNewEditSaved() throws {
        let workspace = Workspace(); let note = workspace.create()
        let old = try require(workspace.snapshot()); workspace.edit(note.id, content: "newer"); workspace.acknowledge(old)
        expectTrue(workspace.isDirty); expectEqual(workspace.snapshot()?.notes.first?.content, "newer")
    }
    @Test func testTransactionFailureRollsBackAndMemoryCanRetry() throws {
        let (_, store, _) = try fixture(); let workspace = Workspace(try store.load())
        let a = workspace.create(), b = workspace.create(); try flush(workspace, store)
        let db = try Database(path: store.path)
        try db.execute("CREATE TRIGGER fail_write BEFORE UPDATE ON Notes WHEN NEW.Id='\(b.id.uuidString)' BEGIN SELECT RAISE(ABORT,'test failure'); END;")
        workspace.edit(a.id, content: "A保持"); workspace.edit(b.id, content: "B保持")
        expectThrows(try flush(workspace, store)); expectTrue(workspace.isDirty)
        expectTrue(try store.load().notes.allSatisfy { $0.content.isEmpty }); expectEqual(workspace.notes[0].content, "A保持")
        try db.execute("DROP TRIGGER fail_write;"); try flush(workspace, store); expectFalse(workspace.isDirty)
    }
    @Test func testTamperedDatabaseIsNotReplaced() throws {
        let (_, store, _) = try fixture(); let workspace = Workspace(try store.load()); workspace.create(); try flush(workspace, store)
        let db = try Database(path: store.path); try db.execute("UPDATE Notes SET EncryptedContent=zeroblob(64);")
        expectThrows(try store.load()) { expectEqual($0 as? NoteError, .decryption) }
        expectEqual(try db.scalar("SELECT hex(EncryptedContent) FROM Notes;"), String(repeating: "0", count: 128))
    }
    @Test func testDeletionRequiresConfirmation() throws {
        let (_, store, _) = try fixture(); let workspace = Workspace(try store.load()); let note = workspace.create(); try flush(workspace, store)
        expectFalse(workspace.delete(note.id, confirmed: false)); expectEqual(workspace.notes.count, 1)
        expectTrue(workspace.delete(note.id, confirmed: true)); try flush(workspace, store); expectTrue(try store.load().notes.isEmpty)
    }
    @Test func testUnknownSchemaFailsClosed() throws {
        let (_, store, _) = try fixture(); let db = try Database(path: store.path)
        try db.execute("CREATE TABLE Unknown(Value TEXT);")
        expectThrows(try store.load()) { expectEqual($0 as? NoteError, .schema) }
        try db.execute("PRAGMA user_version=99;")
        expectThrows(try store.load()) { expectEqual($0 as? NoteError, .schema) }
    }
    @Test func testDiskWalAndBackupContainOnlyCiphertext() throws {
        let (directory, store, cipher) = try fixture(); let workspace = Workspace(try store.load())
        let pin = try Database(path: store.path); try pin.execute("BEGIN;"); _ = try pin.scalar("SELECT count(*) FROM Notes;")
        let note = workspace.create(), title = "TITLE-secret-\(UUID())", content = "日本語SECRET-😀-\(UUID())"
        workspace.edit(note.id, title: title, content: content); try flush(workspace, store)
        expectTrue(FileManager.default.fileExists(atPath: store.path + "-wal"))
        let backup = directory.appendingPathComponent("snapshot.bak"); try store.backup(to: backup.path)
        for file in try FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil) {
            let data = try Data(contentsOf: file)
            for text in [title, content] {
                expectNil(data.range(of: Data(text.utf8)))
                expectNil(data.range(of: try require(text.data(using: .utf16LittleEndian))))
            }
        }
        let restored = try SqliteStore(path: backup.path, cipher: cipher).load()
        expectEqual(restored.notes.first?.content, content)
        try pin.execute("ROLLBACK;")
    }
    @Test func testStaleGenerationCannotOverwriteContent() throws {
        let (_, store, _) = try fixture(); let workspace = Workspace(try store.load()); let note = workspace.create()
        var latest = try require(workspace.snapshot()); latest.notes[0].content = "latest"; latest.notes[0].generation = 10
        try store.save(latest); try store.save(try require(workspace.snapshot()))
        expectEqual(try store.load().notes.first?.content, "latest"); expectEqual(try store.load().notes.first?.id, note.id)
    }
    @Test func testSearchIncludesClosedNotesWithoutDiskIndex() {
        let workspace = Workspace(); let note = workspace.create(); workspace.edit(note.id, title: "会議", content: "Prompt 日本語", archived: true)
        expectEqual(workspace.search("prompt").count, 1); expectEqual(workspace.search("会議").count, 1)
    }
    @Test func testDuplicateInstanceAndSecurePermissions() throws {
        let (directory, _, _) = try fixture(); let lease = try InstanceLock(directory: directory)
        try withExtendedLifetime(lease) { expectThrows(try InstanceLock(directory: directory)) { expectEqual($0 as? NoteError, .alreadyRunning) } }
        let attributes = try FileManager.default.attributesOfItem(atPath: directory.path)
        expectEqual((attributes[.posixPermissions] as? NSNumber)?.intValue, 0o700)
    }
    @Test func testSymlinkIsRejected() throws {
        let (directory, _, _) = try fixture(); let target = directory.appendingPathComponent("target")
        expectTrue(FileManager.default.createFile(atPath: target.path, contents: Data()))
        try FileManager.default.createSymbolicLink(at: directory.appendingPathComponent("link"), withDestinationURL: target)
        expectThrows(try SecureLocation.prepare(directory))
    }
    @Test func testWindowRestoresInsideScreen() {
        let area = CGRect(x: -1280, y: 0, width: 1280, height: 900)
        let restored = WindowGeometry.restore(WindowState(x: 99999, y: -99999, width: 800, height: 600), areas: [area])
        expectTrue(area.contains(restored))
    }
    @Test func testKeychainRoundTripAndMissingKeyNeverRegenerates() throws {
        let service = "com.kokonote.tests.\(UUID())"
        defer { try? KeychainKey.deleteTestKey(service: service) }
        expectThrows(try KeychainKey.loadOrCreate(service: service, allowCreate: false)) { expectEqual($0 as? NoteError, .missingKey) }
        let key = try KeychainKey.loadOrCreate(service: service, allowCreate: true)
        let again = try KeychainKey.loadOrCreate(service: service, allowCreate: false), id = UUID()
        let encrypted = try ContentCipher(key: key).encrypt("Keychain確認😀", id: id, field: "content")
        expectEqual(try ContentCipher(key: again).decrypt(encrypted, id: id, field: "content"), "Keychain確認😀")
    }
    @Test func testCoordinatorSavesLatestQueuedChanges() async throws {
        let (_, store, _) = try fixture(); let workspace = Workspace(try store.load()); let note = workspace.create()
        let saver = SaveCoordinator(workspace: workspace, store: store)
        workspace.edit(note.id, content: "first")
        await withCheckedContinuation { continuation in saver.save { _ in continuation.resume() } }
        workspace.edit(note.id, content: "last 日本語😀")
        let result: Result<Void, Error> = await withCheckedContinuation { continuation in saver.save { continuation.resume(returning: $0) } }
        try result.get(); expectFalse(workspace.isDirty); expectEqual(try store.load().notes.first?.content, "last 日本語😀")
    }
}

private final class TestResources {
    var directories: [URL] = []
    deinit { for directory in directories { try? FileManager.default.removeItem(at: directory) } }
}
private func expectEqual<T: Equatable>(_ lhs: T, _ rhs: T) { #expect(lhs == rhs) }
private func expectTrue(_ value: Bool) { #expect(value) }
private func expectFalse(_ value: Bool) { #expect(!value) }
private func expectNil<T>(_ value: T?) { if value != nil { Issue.record("Expected nil") } }
private func require<T>(_ value: T?) throws -> T { try #require(value) }
private func expectThrows<T>(_ expression: @autoclosure () throws -> T, check: ((Error) -> Void)? = nil) {
    do { _ = try expression(); Issue.record("Expected failure") } catch { check?(error) }
}

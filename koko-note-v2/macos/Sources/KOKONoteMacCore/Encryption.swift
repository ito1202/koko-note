import Foundation
import CryptoKit
import Security

public struct ContentCipher {
    private let key: SymmetricKey
    public init(key: SymmetricKey) { self.key = key }
    private func context(_ id: UUID, _ field: String) -> Data { Data("KOKONote.mac.v1/\(id.uuidString)/\(field)".utf8) }
    public func encrypt(_ text: String, id: UUID, field: String) throws -> Data {
        var plain = Data(text.utf8)
        defer { plain.resetBytes(in: plain.startIndex..<plain.endIndex) }
        do {
            let sealed = try AES.GCM.seal(plain, using: key, authenticating: context(id, field))
            guard let combined = sealed.combined else { throw NoteError.encryption }
            return Data([1]) + combined
        } catch { throw NoteError.encryption }
    }
    public func decrypt(_ encrypted: Data, id: UUID, field: String) throws -> String {
        do {
            guard encrypted.first == 1 else { throw NoteError.decryption }
            let box = try AES.GCM.SealedBox(combined: encrypted.dropFirst())
            var plain = try AES.GCM.open(box, using: key, authenticating: context(id, field))
            defer { plain.resetBytes(in: plain.startIndex..<plain.endIndex) }
            guard let text = String(data: plain, encoding: .utf8) else { throw NoteError.decryption }
            return text
        } catch { throw NoteError.decryption }
    }
}

public enum KeychainKey {
    public static let service = "com.kokonote.mac.v2"
    private static func query(_ service: String) -> [String: Any] {
        [kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: service,
         kSecAttrAccount as String: "content-key-v1", kSecAttrSynchronizable as String: false]
    }
    public static func loadOrCreate(service: String = service, allowCreate: Bool) throws -> SymmetricKey {
        var read = query(service)
        read[kSecReturnData as String] = true; read[kSecMatchLimit as String] = kSecMatchLimitOne
        var result: CFTypeRef?
        let status = SecItemCopyMatching(read as CFDictionary, &result)
        if status == errSecSuccess, var bytes = result as? Data {
            defer { bytes.resetBytes(in: bytes.startIndex..<bytes.endIndex) }
            guard bytes.count == 32 else { throw NoteError.missingKey }
            return SymmetricKey(data: bytes)
        }
        guard status == errSecItemNotFound else { throw NoteError.keychain(status) }
        guard allowCreate else { throw NoteError.missingKey }
        var bytes = Data(count: 32)
        defer { bytes.resetBytes(in: bytes.startIndex..<bytes.endIndex) }
        let randomStatus = bytes.withUnsafeMutableBytes { SecRandomCopyBytes(kSecRandomDefault, 32, $0.baseAddress!) }
        guard randomStatus == errSecSuccess else { throw NoteError.encryption }
        var add = query(service)
        add[kSecValueData as String] = bytes
        add[kSecAttrLabel as String] = "KOKO Note 2.0 — encrypted notes"
        let added = SecItemAdd(add as CFDictionary, nil)
        if added == errSecDuplicateItem { return try loadOrCreate(service: service, allowCreate: false) }
        guard added == errSecSuccess else { throw NoteError.keychain(added) }
        return SymmetricKey(data: bytes)
    }
    // For isolated automated test keys only; production never removes or silently replaces its key.
    public static func deleteTestKey(service: String) throws {
        guard service.hasPrefix("com.kokonote.tests.") else { throw NoteError.permissions }
        let status = SecItemDelete(query(service) as CFDictionary)
        guard status == errSecSuccess || status == errSecItemNotFound else { throw NoteError.keychain(status) }
    }
}

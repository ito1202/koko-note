import Foundation
import Darwin
import CoreGraphics

public enum SecureLocation {
    public static var applicationDirectory: URL {
        FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("KOKONote2", isDirectory: true)
    }
    public static func prepare(_ directory: URL) throws {
        let manager = FileManager.default
        try manager.createDirectory(at: directory, withIntermediateDirectories: true, attributes: [.posixPermissions: 0o700])
        try restrict(directory, directory: true)
        for file in try manager.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil) { try restrict(file, directory: false) }
    }
    private static func restrict(_ url: URL, directory: Bool) throws {
        let fd = open(url.path, O_RDONLY | O_NOFOLLOW | O_CLOEXEC | (directory ? O_DIRECTORY : 0))
        guard fd >= 0 else { throw NoteError.permissions }
        defer { close(fd) }
        var info = stat()
        guard fstat(fd, &info) == 0, info.st_uid == geteuid(),
              (info.st_mode & S_IFMT) == (directory ? S_IFDIR : S_IFREG) else { throw NoteError.permissions }
        guard let acl = acl_init(0) else { throw NoteError.permissions }
        defer { acl_free(UnsafeMutableRawPointer(acl)) }
        guard acl_set_fd(fd, acl) == 0, fchmod(fd, directory ? 0o700 : 0o600) == 0 else { throw NoteError.permissions }
    }
}

public final class InstanceLock {
    private let fd: Int32
    public init(directory: URL) throws {
        let descriptor = open(directory.appendingPathComponent(".instance.lock").path, O_CREAT | O_RDWR | O_NOFOLLOW | O_CLOEXEC, 0o600)
        guard descriptor >= 0 else { throw NoteError.permissions }
        guard flock(descriptor, LOCK_EX | LOCK_NB) == 0 else { close(descriptor); throw NoteError.alreadyRunning }
        fd = descriptor
    }
    deinit { flock(fd, LOCK_UN); close(fd) }
}

public enum WindowGeometry {
    public static func restore(_ state: WindowState, areas: [CGRect]) -> CGRect {
        guard let first = areas.first else { return CGRect(x: 100, y: 100, width: 800, height: 600) }
        let requested = CGRect(x: state.x, y: state.y, width: state.width, height: state.height)
        let area = areas.max { a, b in
            let ai = requested.intersection(a), bi = requested.intersection(b)
            return (ai.isNull ? 0 : ai.width * ai.height) < (bi.isNull ? 0 : bi.width * bi.height)
        } ?? first
        let width = min(max(state.width.isFinite ? state.width : 800, min(420, area.width)), area.width)
        let height = min(max(state.height.isFinite ? state.height : 600, min(280, area.height)), area.height)
        return CGRect(x: min(max(state.x.isFinite ? state.x : area.minX, area.minX), area.maxX - width),
                      y: min(max(state.y.isFinite ? state.y : area.minY, area.minY), area.maxY - height), width: width, height: height)
    }
}

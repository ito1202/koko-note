import AppKit
import KOKONoteMacCore
import Darwin

// Explicit integration-test mode. Uses isolated encrypted files and a test-only Keychain item.
// No external application receives key events or text. Normal launches never enter this path.
enum DesktopSelfTest {
    static func run() {
        Task { @MainActor in
            var checks: [String: Bool] = [:]
            let service = "com.kokonote.tests.\(UUID())"
            let directory = FileManager.default.temporaryDirectory.appendingPathComponent("KOKO-desktop-\(UUID())", isDirectory: true)
            var retainedWindow: EditorWindow?
            do {
                try SecureLocation.prepare(directory)
                let key = try KeychainKey.loadOrCreate(service: service, allowCreate: true)
                let store = SqliteStore(path: directory.appendingPathComponent("notes.db").path, cipher: ContentCipher(key: key))
                let workspace = Workspace(try store.load()); let first = workspace.create()
                let editorWindow = EditorWindow(workspace: workspace, saver: SaveCoordinator(workspace: workspace, store: store))
                retainedWindow = editorWindow
                checks["startsHidden"] = editorWindow.window?.isVisible == false
                editorWindow.present(); await pause(100)
                checks["showsNativeWindow"] = editorWindow.window?.isVisible == true
                checks["initialSave"] = await save(editorWindow)
                guard let editor = editorWindow.window?.firstResponder as? PlainEditor else { throw NoteError.database }
                let text = "Mac実機テスト用 日本語😀\n二行目"
                for part in ["Mac実機テスト用 ", "日本語😀", "\n二行目"] {
                    editor.insertText(part, replacementRange: NSRange(location: NSNotFound, length: 0)); await pause(150)
                }
                let duringTyping = try store.load().notes.first?.content
                checks["debouncesContinuousTyping"] = workspace.isDirty && duringTyping == ""
                await pause(650)
                let afterTyping = try store.load().notes.first?.content
                checks["autosavesFinalUnicodeText"] = !workspace.isDirty && afterTyping == text
                editor.setSelectedRange(NSRange(location: 2, length: 3))
                editorWindow.updateShortcutHints(commandHeld: true)
                checks["commandHoldShowsHints"] = editorWindow.shortcutHintsVisible
                let savedWithHints = await save(editorWindow)
                checks["hintsPreserveSaveState"] = savedWithHints && editorWindow.shortcutHintsVisible && editorWindow.saveStatusText == "保存済み"
                editorWindow.updateShortcutHints(commandHeld: false)
                checks["commandReleaseRestoresFooter"] = !editorWindow.shortcutHintsVisible && editorWindow.saveStatusText == "保存済み" && editor.selectedRange() == NSRange(location: 2, length: 3)
                editorWindow.updateShortcutHints(commandHeld: true)
                editorWindow.hide(); await pause(100)
                checks["hintsClearOnHide"] = !editorWindow.shortcutHintsVisible
                checks["hidesWithoutDestroying"] = editorWindow.window?.isVisible == false
                editorWindow.present(); await pause(100)
                checks["preservesSelection"] = editor.selectedRange() == NSRange(location: 2, length: 3)
                editor.setSelectedRange(NSRange(location: (editor.string as NSString).length, length: 0))
                editor.breakUndoCoalescing()
                editor.insertText("UndoProbe", replacementRange: NSRange(location: NSNotFound, length: 0))
                editor.breakUndoCoalescing(); editor.undoManager?.undo()
                checks["nativeUndo"] = editor.string == text
                editor.undoManager?.redo(); checks["nativeRedo"] = editor.string == text + "UndoProbe"
                _ = await save(editorWindow)
                checks["menuActionRouting"] = NSApp.sendAction(#selector(EditorWindow.newNote), to: nil, from: nil)
                await pause(100)
                let second = workspace.session.active
                checks["createsSecondTab"] = workspace.notes.count == 2 && second != first.id
                if let current = editorWindow.window?.firstResponder as? PlainEditor {
                    current.setMarkedText("にほんご", selectedRange: NSRange(location: 4, length: 0), replacementRange: NSRange(location: NSNotFound, length: 0))
                    checks["markedTextExcludedFromSnapshot"] = workspace.notes.first(where: { $0.id == second })?.content == ""
                    var ran = false; editorWindow.whenCommitted { ran = true }
                    checks["defersTransitionWhileComposing"] = !ran && current.composing
                    current.insertText("日本語", replacementRange: NSRange(location: NSNotFound, length: 0)); await pause(100)
                    checks["commitsBeforeDeferredTransition"] = ran && !current.composing && workspace.notes.first(where: { $0.id == second })?.content == "日本語"
                    if let event = NSEvent.keyEvent(with: .keyDown, location: .zero, modifierFlags: .control, timestamp: 0,
                        windowNumber: editorWindow.window?.windowNumber ?? 0, context: nil, characters: "\t", charactersIgnoringModifiers: "\t", isARepeat: false, keyCode: 48) {
                        checks["controlTabSwitches"] = current.performKeyEquivalent(with: event) && workspace.session.active == first.id
                    }
                }
                editorWindow.closeNote(); await pause(100)
                checks["closeArchivesWithoutDeleting"] = workspace.notes.count == 2 && workspace.notes.first(where: { $0.id == first.id })?.archived == true
                checks["searchesClosedContent"] = workspace.search("Mac実機テスト用").count == 1
                checks["finalSave"] = await save(editorWindow)
                let restored = Workspace(try store.load())
                checks["restoresAfterReload"] = restored.notes == workspace.notes && restored.session == workspace.session
                let firstHotkey = Hotkey(), duplicate = Hotkey()
                checks["hotkeyConflictDetected"] = duplicate.status != noErr
                withExtendedLifetime(firstHotkey) {}
            } catch { checks["setupOrExecutionFailed"] = false }
            retainedWindow?.window?.orderOut(nil); retainedWindow = nil
            try? KeychainKey.deleteTestKey(service: service)
            try? FileManager.default.removeItem(at: directory)
            let success = checks.count == 24 && checks.values.allSatisfy { $0 }
            let report: [String: Any] = ["success": success, "checks": checks,
                "scope": "In-process AppKit integration with real Keychain/SQLite. IME uses synthetic marked-text calls; physical input conversion and visual inspection are not verified."]
            if let data = try? JSONSerialization.data(withJSONObject: report, options: [.prettyPrinted, .sortedKeys]) {
                FileHandle.standardOutput.write(data); FileHandle.standardOutput.write(Data("\n".utf8))
            }
            exit(success ? 0 : 1)
        }
    }
    @MainActor private static func pause(_ milliseconds: UInt64) async { try? await Task.sleep(nanoseconds: milliseconds * 1_000_000) }
    @MainActor private static func save(_ editor: EditorWindow) async -> Bool {
        await withCheckedContinuation { continuation in editor.save(showError: false) { continuation.resume(returning: $0) } }
    }
}

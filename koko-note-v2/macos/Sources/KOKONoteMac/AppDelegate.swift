import AppKit
import KOKONoteMacCore

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var status: NSStatusItem?
    private var hotkey: Hotkey?
    private var instance: InstanceLock?
    private var controller: EditorWindow?
    private var ready = false
    private var terminating = false

    func applicationDidFinishLaunching(_ notification: Notification) {
        if ProcessInfo.processInfo.arguments.contains("--self-test") { DesktopSelfTest.run(); return }
        let menu = NSMenu()
        for (title, selector) in [("表示", #selector(show)), ("設定", #selector(settings)), ("終了", #selector(quit))] {
            let item = NSMenuItem(title: title, action: selector, keyEquivalent: ""); item.target = self; menu.addItem(item)
        }
        status = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        status?.button?.image = NSImage(systemSymbolName: "square.and.pencil", accessibilityDescription: "KOKO Note")
        status?.button?.toolTip = "KOKO Note 2.0 — Control + Shift + Space"; status?.menu = menu
        installMainMenu()
        do {
            let directory = SecureLocation.applicationDirectory
            try SecureLocation.prepare(directory); instance = try InstanceLock(directory: directory)
            hotkey = Hotkey(); hotkey?.pressed = { [weak self] in
                guard let self, self.ready, !self.terminating, NSApp.modalWindow == nil,
                      let editor = self.controller, editor.window?.attachedSheet == nil else { return }
                editor.whenCommitted {
                    if editor.window?.isVisible == true && editor.window?.isKeyWindow == true { editor.hide() }
                    else { editor.present() }
                }
            }
            let file = directory.appendingPathComponent("notes-mac.db")
            let allowCreate = !FileManager.default.fileExists(atPath: file.path)
            DispatchQueue.global(qos: .userInitiated).async {
                let result: Result<(Workspace, SaveCoordinator), Error>
                do {
                    let key = try KeychainKey.loadOrCreate(allowCreate: allowCreate)
                    let store = SqliteStore(path: file.path, cipher: ContentCipher(key: key))
                    let workspace = Workspace(try store.load()); if workspace.notes.isEmpty { workspace.create() }
                    result = .success((workspace, SaveCoordinator(workspace: workspace, store: store)))
                } catch { result = .failure(error) }
                DispatchQueue.main.async {
                    switch result {
                    case let .success((workspace, saver)):
                        self.controller = EditorWindow(workspace: workspace, saver: saver)
                        self.ready = true; self.controller?.save()
                        if self.hotkey?.status != 0 { self.hotkeyError() }
                    case .failure: self.failStartup()
                    }
                }
            }
        } catch NoteError.alreadyRunning {
            alert("既に起動しています。メニューバーから開いてください。")
            NSApp.terminate(nil)
        } catch { failStartup() }
    }
    private func failStartup() {
        alert("データを安全に開けませんでした。既存データを置き換えずに起動を中止します。Keychain、保存先のアクセス権、別プロセスの起動、DB破損を確認してください。")
        ready = false; NSApp.terminate(nil)
    }
    @objc func show() { guard ready, !terminating, NSApp.modalWindow == nil else { return }; controller?.whenCommitted { self.controller?.present() } }
    @objc func settings() {
        guard ready, !terminating, NSApp.modalWindow == nil else { return }
        controller?.whenCommitted {
            self.controller?.present()
            self.alert("ホットキー: Control + Shift + Space（固定）\n暗号化: Keychain + AES-256-GCM\n保存先: ~/Library/Application Support/KOKONote2\n\nOKでホットキー登録を再試行します。")
            self.hotkey?.retry(); if self.hotkey?.status != 0 { self.hotkeyError() }
        }
    }
    private func hotkeyError() { alert("ホットキーを登録できませんでした。他アプリの割り当てを確認してください。メニューバーから表示できます。") }
    @objc func quit() { NSApp.terminate(nil) }
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        guard ready, let controller else { return .terminateNow }
        guard !terminating else { return .terminateCancel }
        if controller.composing { controller.whenCommitted { NSApp.terminate(nil) }; return .terminateCancel }
        terminating = true; controller.setBusy(true)
        attemptExit(controller)
        return .terminateLater
    }
    private func attemptExit(_ editor: EditorWindow) {
        editor.save(showError: false) { succeeded in
            if succeeded { NSApp.reply(toApplicationShouldTerminate: true) }
            else {
                editor.setBusy(false); editor.present()
                let alert = NSAlert(); alert.messageText = "保存できないため終了できません"
                alert.informativeText = "未保存内容はメモリに保持しています。障害を解除して再試行するか、終了をキャンセルしてください。"
                alert.addButton(withTitle: "再試行"); alert.addButton(withTitle: "キャンセル")
                if alert.runModal() == .alertFirstButtonReturn { editor.setBusy(true); self.attemptExit(editor) }
                else { self.terminating = false; NSApp.reply(toApplicationShouldTerminate: false) }
            }
        }
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { false }
    private func alert(_ message: String) { let alert = NSAlert(); alert.messageText = "KOKO Note 2.0"; alert.informativeText = message; alert.runModal() }
    private func installMainMenu() {
        let main = NSMenu(), app = NSMenu(), root = NSMenuItem()
        let quit = NSMenuItem(title: "KOKO Noteを終了", action: #selector(quit), keyEquivalent: "q"); quit.target = self
        app.addItem(quit); root.submenu = app; main.addItem(root)
        let noteRoot = NSMenuItem(title: "ノート", action: nil, keyEquivalent: ""), notes = NSMenu(title: "ノート")
        for (title, action, key) in [("新規ノート", #selector(EditorWindow.newNote), "n"), ("ノート一覧・検索", #selector(EditorWindow.pickNote), "l"),
                                     ("名前変更", #selector(EditorWindow.renameNote), "r"), ("保存", #selector(EditorWindow.saveNote), "s"),
                                     ("ノートを閉じる", #selector(EditorWindow.closeNote), "w"), ("ノートを削除…", #selector(EditorWindow.deleteNote), "")] {
            notes.addItem(NSMenuItem(title: title, action: action, keyEquivalent: key))
        }
        noteRoot.submenu = notes; main.addItem(noteRoot)
        let editRoot = NSMenuItem(title: "編集", action: nil, keyEquivalent: ""), edit = NSMenu(title: "編集")
        edit.addItem(NSMenuItem(title: "元に戻す", action: Selector(("undo:")), keyEquivalent: "z"))
        let redo = NSMenuItem(title: "やり直す", action: Selector(("redo:")), keyEquivalent: "z"); redo.keyEquivalentModifierMask = [.command, .shift]; edit.addItem(redo)
        for (title, selector, key) in [("切り取り", #selector(NSText.cut(_:)), "x"), ("コピー", #selector(NSText.copy(_:)), "c"),
                                      ("貼り付け", #selector(NSText.paste(_:)), "v"), ("すべて選択", #selector(NSText.selectAll(_:)), "a")] {
            edit.addItem(NSMenuItem(title: title, action: selector, keyEquivalent: key))
        }
        editRoot.submenu = edit; main.addItem(editRoot); NSApp.mainMenu = main
    }
}

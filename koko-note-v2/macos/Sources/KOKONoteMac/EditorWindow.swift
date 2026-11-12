import AppKit
import KOKONoteMacCore

final class EditorWindow: NSWindowController, NSWindowDelegate, NSTabViewDelegate, NSTextViewDelegate {
    let workspace: Workspace
    private let saver: SaveCoordinator
    private let tabs = NSTabView()
    private let status = NSTextField(labelWithString: "保存済み")
    private let shortcutHints = NSTextField(wrappingLabelWithString:
        "⌘N 新規   ⌘S 保存   ⌘L 一覧・検索   ⌘W タブを閉じる   ⌘R 名前変更\n" +
        "⌃Tab 次へ / ⌃⇧Tab 前へ   ⌘Z 元に戻す / ⌘⇧Z やり直す\n" +
        "⌘C コピー   ⌘X 切り取り   ⌘V 貼り付け   ⌘A 全選択\n" +
        "削除:「ノート」→「ノートを削除…」（確認あり）")
    private var modifierMonitor: Any?
    private var editors: [UUID: PlainEditor] = [:]
    private var panes: [UUID: NSScrollView] = [:]
    private var selected: UUID?
    private var rebuilding = false
    private var debounce: Timer?
    private var pending: (() -> Void)?
    private var busy = false
    private var errorShown = false
    private var picker: NotePicker?
    var composing: Bool { editors.values.contains { $0.composing } }
    var shortcutHintsVisible: Bool { !shortcutHints.isHidden }
    var saveStatusText: String { status.stringValue }

    init(workspace: Workspace, saver: SaveCoordinator) {
        self.workspace = workspace; self.saver = saver
        let rect = WindowGeometry.restore(workspace.session.window, areas: NSScreen.screens.map(\.visibleFrame))
        let window = NSWindow(contentRect: rect, styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.setFrame(rect, display: false)
        window.title = "KOKO Note 2.0"; window.minSize = NSSize(width: 420, height: 280)
        window.isReleasedWhenClosed = false
        super.init(window: window)
        window.delegate = self
        let content = NSView(); window.contentView = content
        tabs.translatesAutoresizingMaskIntoConstraints = false; tabs.delegate = self; tabs.tabViewType = .topTabsBezelBorder
        status.translatesAutoresizingMaskIntoConstraints = false; status.font = .systemFont(ofSize: 11); status.textColor = .secondaryLabelColor
        shortcutHints.font = .systemFont(ofSize: 11); shortcutHints.isHidden = true
        shortcutHints.setAccessibilityLabel("ショートカット一覧")
        let footer = NSStackView(views: [shortcutHints, status])
        footer.orientation = .vertical; footer.alignment = .leading; footer.spacing = 5; footer.detachesHiddenViews = true
        footer.translatesAutoresizingMaskIntoConstraints = false
        content.addSubview(tabs); content.addSubview(footer)
        NSLayoutConstraint.activate([
            tabs.leadingAnchor.constraint(equalTo: content.leadingAnchor, constant: 4), tabs.trailingAnchor.constraint(equalTo: content.trailingAnchor, constant: -4),
            tabs.topAnchor.constraint(equalTo: content.topAnchor, constant: 4), tabs.bottomAnchor.constraint(equalTo: footer.topAnchor, constant: -4),
            footer.leadingAnchor.constraint(equalTo: content.leadingAnchor, constant: 12), footer.trailingAnchor.constraint(equalTo: content.trailingAnchor, constant: -12),
            footer.bottomAnchor.constraint(equalTo: content.bottomAnchor, constant: -6), shortcutHints.widthAnchor.constraint(equalTo: footer.widthAnchor)
        ])
        // Local flags only: no global keyboard monitoring, permissions, or event consumption.
        modifierMonitor = NSEvent.addLocalMonitorForEvents(matching: .flagsChanged) { [weak self] event in
            self?.updateShortcutHints(commandHeld: event.modifierFlags.contains(.command))
            return event
        }
        rebuild(active: workspace.session.active)
    }
    required init?(coder: NSCoder) { fatalError("Not used") }
    deinit { if let modifierMonitor { NSEvent.removeMonitor(modifierMonitor) } }
    func updateShortcutHints(commandHeld: Bool) {
        shortcutHints.isHidden = !(commandHeld && window?.isKeyWindow == true && NSApp.modalWindow == nil)
    }
    func windowDidBecomeKey(_ notification: Notification) { updateShortcutHints(commandHeld: NSEvent.modifierFlags.contains(.command)) }
    func windowDidResignKey(_ notification: Notification) { updateShortcutHints(commandHeld: false) }
    func whenCommitted(_ action: @escaping () -> Void) { if busy { return }; if composing { pending = action } else { action() } }
    func setBusy(_ value: Bool) {
        busy = value; if value { debounce?.invalidate(); debounce = nil }
        for editor in editors.values { editor.isEditable = !value }
    }
    func present() {
        guard let window else { return }
        let frame = window.frame
        window.setFrame(WindowGeometry.restore(WindowState(x: frame.minX, y: frame.minY, width: frame.width, height: frame.height),
                                               areas: NSScreen.screens.map(\.visibleFrame)), display: false)
        if window.isMiniaturized { window.deminiaturize(nil) }
        NSApp.activate(ignoringOtherApps: true); window.makeKeyAndOrderFront(nil)
        if let selected, let editor = editors[selected] { window.makeFirstResponder(editor) }
    }
    func hide() { updateShortcutHints(commandHeld: false); capture(); window?.orderOut(nil); save() }
    func windowShouldClose(_ sender: NSWindow) -> Bool { whenCommitted { self.hide() }; return false }
    func windowDidEndLiveResize(_ notification: Notification) { capture(); schedule() }
    func windowDidMove(_ notification: Notification) { if !rebuilding { capture(); schedule() } }

    private func rebuild(active: UUID?) {
        rebuilding = true
        for item in tabs.tabViewItems { tabs.removeTabViewItem(item) }
        for note in workspace.notes where !note.archived {
            let item = NSTabViewItem(identifier: note.id); item.label = note.title
            item.view = panes[note.id] ?? NSView(); tabs.addTabViewItem(item)
        }
        if let active, let item = tabs.tabViewItems.first(where: { $0.identifier as? UUID == active }) { tabs.selectTabViewItem(item) }
        selected = tabs.selectedTabViewItem?.identifier as? UUID
        rebuilding = false; ensureEditor()
    }
    private func ensureEditor() {
        guard let selected, let item = tabs.selectedTabViewItem else { return }
        if editors[selected] == nil, let note = workspace.notes.first(where: { $0.id == selected }) {
            let editor = PlainEditor(), scroll = NSScrollView()
            scroll.hasVerticalScroller = true; scroll.hasHorizontalScroller = false; scroll.borderType = .noBorder
            scroll.documentView = editor; editor.frame = scroll.contentView.bounds
            editor.string = note.content; editor.delegate = self
            editor.committed = { [weak self, weak editor] in
                guard let self, let editor else { return }
                self.workspace.edit(selected, content: editor.string); self.schedule()
                let action = self.pending; self.pending = nil; action?()
            }
            editor.shortcut = { [weak self] in self?.handleShortcut($0) ?? false }
            editors[selected] = editor; panes[selected] = scroll; item.view = scroll
            editor.restore(workspace.session.views[selected] ?? EditorState())
        }
        if let editor = editors[selected] { window?.makeFirstResponder(editor) }
    }
    func tabView(_ tabView: NSTabView, shouldSelect tabViewItem: NSTabViewItem?) -> Bool {
        if rebuilding { return true }
        if busy { return false }
        if composing {
            if let item = tabViewItem { pending = { [weak self] in self?.tabs.selectTabViewItem(item) } }
            return false
        }
        return true
    }
    func tabView(_ tabView: NSTabView, didSelect tabViewItem: NSTabViewItem?) {
        guard !rebuilding else { return }
        capture(); selected = tabViewItem?.identifier as? UUID; ensureEditor(); capture(); save()
    }
    func textDidChange(_ notification: Notification) {
        guard let editor = notification.object as? PlainEditor, let id = editors.first(where: { $0.value === editor })?.key else { return }
        if !editor.composing { workspace.edit(id, content: editor.string) }
        status.stringValue = "未保存"; schedule()
    }
    private func schedule() {
        guard !busy else { return }
        debounce?.invalidate()
        debounce = Timer.scheduledTimer(withTimeInterval: 0.5, repeats: false) { [weak self] _ in
            guard let self, !self.composing else { return }; self.save()
        }
    }
    func capture() {
        guard !composing, let window else { return }
        var session = workspace.session
        session.active = selected
        for (id, editor) in editors { session.views[id] = editor.capture() }
        let frame = window.frame
        session.window = WindowState(x: frame.minX, y: frame.minY, width: frame.width, height: frame.height)
        workspace.setSession(session)
    }
    func save(showError: Bool = true, completion: ((Bool) -> Void)? = nil) {
        debounce?.invalidate(); debounce = nil
        if composing { pending = { [weak self] in self?.save(showError: showError, completion: completion) }; return }
        capture(); status.stringValue = "保存中…"
        saver.save { [weak self] result in
            guard let self else { return }
            switch result {
            case .success:
                self.status.textColor = .secondaryLabelColor; self.status.stringValue = self.workspace.isDirty ? "未保存" : "保存済み"
                self.errorShown = false; completion?(!self.workspace.isDirty)
            case .failure:
                self.status.textColor = .systemRed; self.status.stringValue = "保存失敗 — ⌘Sで再試行"
                if showError && !self.errorShown {
                    self.errorShown = true; self.present()
                    let alert = NSAlert(); alert.messageText = "保存できませんでした"
                    alert.informativeText = "未保存内容はメモリに保持しています。空き容量・保存先の権限・Keychainを確認し、⌘Sで再試行してください。"; alert.runModal()
                }
                completion?(false)
            }
        }
    }
    @objc func newNote() { whenCommitted { self.capture(); let note = self.workspace.create(); self.rebuild(active: note.id); self.save() } }
    @objc func saveNote() { whenCommitted { self.errorShown = false; self.save() } }
    @objc func closeNote() {
        whenCommitted {
            guard let id = self.selected else { return }
            self.capture(); self.workspace.edit(id, archived: true); self.editors.removeValue(forKey: id); self.panes.removeValue(forKey: id)
            self.rebuild(active: nil); self.save()
        }
    }
    @objc func renameNote() {
        whenCommitted {
            guard let id = self.selected, let note = self.workspace.notes.first(where: { $0.id == id }) else { return }
            let alert = NSAlert(); alert.messageText = "ノート名変更"
            let input = NSTextField(string: note.title); input.frame = NSRect(x: 0, y: 0, width: 320, height: 24)
            alert.accessoryView = input; alert.addButton(withTitle: "変更"); alert.addButton(withTitle: "キャンセル")
            alert.window.initialFirstResponder = input
            guard alert.runModal() == .alertFirstButtonReturn, !input.stringValue.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }
            self.workspace.edit(id, title: String(input.stringValue.prefix(200))); self.tabs.selectedTabViewItem?.label = String(input.stringValue.prefix(200)); self.save()
        }
    }
    @objc func deleteNote() {
        whenCommitted {
            guard let id = self.selected else { return }
            let alert = NSAlert(); alert.messageText = "このノートを完全に削除しますか？"; alert.informativeText = "この操作は元に戻せません。"
            alert.addButton(withTitle: "キャンセル"); alert.addButton(withTitle: "削除")
            guard self.workspace.delete(id, confirmed: alert.runModal() == .alertSecondButtonReturn) else { return }
            self.editors.removeValue(forKey: id); self.panes.removeValue(forKey: id); self.rebuild(active: nil); self.save()
        }
    }
    @objc func pickNote() {
        whenCommitted {
            guard let window = self.window, window.attachedSheet == nil else { return }
            let picker = NotePicker(workspace: self.workspace) { [weak self] id in
                guard let self else { return }; self.picker = nil
                if let id { self.capture(); self.workspace.edit(id, archived: false); self.rebuild(active: id); self.save() }
            }
            self.picker = picker; picker.begin(on: window)
        }
    }
    private func handleShortcut(_ event: NSEvent) -> Bool {
        let modifiers = event.modifierFlags.intersection(.deviceIndependentFlagsMask)
        if event.keyCode == 48 && modifiers.contains(.control) && !modifiers.contains(.command) {
            whenCommitted {
                let count = self.tabs.numberOfTabViewItems
                guard count > 0, let item = self.tabs.selectedTabViewItem else { return }
                let index = self.tabs.indexOfTabViewItem(item)
                self.tabs.selectTabViewItem(at: (index + (modifiers.contains(.shift) ? count - 1 : 1)) % count)
            }
            return true
        }
        if composing && modifiers.contains(.command) && ["n", "s", "w", "l", "r"].contains(event.charactersIgnoringModifiers ?? "") { return true }
        return false
    }
}

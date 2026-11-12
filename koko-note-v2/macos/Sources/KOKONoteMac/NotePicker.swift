import AppKit
import KOKONoteMacCore

final class NotePicker: NSObject, NSTableViewDataSource, NSTableViewDelegate, NSSearchFieldDelegate {
    private let panel = NSPanel(contentRect: NSRect(x: 0, y: 0, width: 480, height: 360), styleMask: [.titled], backing: .buffered, defer: false)
    private let search = NSSearchField()
    private let table = NSTableView()
    private let workspace: Workspace
    private let completion: (UUID?) -> Void
    private var matches: [Note] = []
    init(workspace: Workspace, completion: @escaping (UUID?) -> Void) {
        self.workspace = workspace; self.completion = completion; super.init()
        panel.title = "ノート一覧・検索（閉じたノートを含む）"
        let content = NSView(); panel.contentView = content
        search.placeholderString = "タイトル・本文を検索"; search.delegate = self
        let scroll = NSScrollView(); scroll.documentView = table; scroll.hasVerticalScroller = true
        let column = NSTableColumn(identifier: NSUserInterfaceItemIdentifier("Title")); column.title = "ノート"; column.width = 420
        table.addTableColumn(column); table.headerView = nil; table.dataSource = self; table.delegate = self
        table.target = self; table.doubleAction = #selector(openNote)
        let open = NSButton(title: "開く", target: self, action: #selector(openNote)); open.keyEquivalent = "\r"
        let cancel = NSButton(title: "キャンセル", target: self, action: #selector(cancel)); cancel.keyEquivalent = "\u{1b}"
        let buttons = NSStackView(views: [cancel, open]); buttons.orientation = .horizontal
        for view in [search, scroll, buttons] { view.translatesAutoresizingMaskIntoConstraints = false; content.addSubview(view) }
        NSLayoutConstraint.activate([
            search.topAnchor.constraint(equalTo: content.topAnchor, constant: 12), search.leadingAnchor.constraint(equalTo: content.leadingAnchor, constant: 12), search.trailingAnchor.constraint(equalTo: content.trailingAnchor, constant: -12),
            scroll.topAnchor.constraint(equalTo: search.bottomAnchor, constant: 8), scroll.leadingAnchor.constraint(equalTo: content.leadingAnchor, constant: 12), scroll.trailingAnchor.constraint(equalTo: content.trailingAnchor, constant: -12),
            scroll.bottomAnchor.constraint(equalTo: buttons.topAnchor, constant: -12), buttons.bottomAnchor.constraint(equalTo: content.bottomAnchor, constant: -12), buttons.trailingAnchor.constraint(equalTo: content.trailingAnchor, constant: -12)
        ])
        refresh()
    }
    func begin(on window: NSWindow) { window.beginSheet(panel); panel.makeFirstResponder(search) }
    func numberOfRows(in tableView: NSTableView) -> Int { matches.count }
    func tableView(_ tableView: NSTableView, viewFor tableColumn: NSTableColumn?, row: Int) -> NSView? {
        NSTextField(labelWithString: matches[row].title + (matches[row].archived ? "（閉じたノート）" : ""))
    }
    func controlTextDidChange(_ obj: Notification) { refresh() }
    private func refresh() {
        matches = workspace.search(search.stringValue); table.reloadData()
        if !matches.isEmpty { table.selectRowIndexes(IndexSet(integer: 0), byExtendingSelection: false) }
    }
    @objc private func openNote() { if matches.indices.contains(table.selectedRow) { finish(matches[table.selectedRow].id) } }
    @objc private func cancel() { finish(nil) }
    private func finish(_ id: UUID?) { panel.sheetParent?.endSheet(panel); panel.orderOut(nil); completion(id) }
}

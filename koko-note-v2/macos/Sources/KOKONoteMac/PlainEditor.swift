import AppKit
import KOKONoteMacCore

final class PlainEditor: NSTextView {
    var committed: (() -> Void)?
    var shortcut: ((NSEvent) -> Bool)?
    private var awaitingCommit = false
    var composing: Bool { awaitingCommit || hasMarkedText() }
    convenience init() {
        self.init(frame: .zero)
        isRichText = false; importsGraphics = false; allowsUndo = true
        isAutomaticQuoteSubstitutionEnabled = false; isAutomaticDashSubstitutionEnabled = false
        isAutomaticTextReplacementEnabled = false; isAutomaticSpellingCorrectionEnabled = false
        isAutomaticLinkDetectionEnabled = false; isAutomaticDataDetectionEnabled = false
        isContinuousSpellCheckingEnabled = false
        font = NSFont.monospacedSystemFont(ofSize: 14, weight: .regular)
        textContainerInset = NSSize(width: 10, height: 10)
        isVerticallyResizable = true; isHorizontallyResizable = false
        autoresizingMask = [.width]; textContainer?.widthTracksTextView = true
        minSize = .zero; maxSize = NSSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)
        usesFindPanel = true
    }
    override func setMarkedText(_ string: Any, selectedRange: NSRange, replacementRange: NSRange) {
        awaitingCommit = true
        super.setMarkedText(string, selectedRange: selectedRange, replacementRange: replacementRange)
    }
    override func insertText(_ string: Any, replacementRange: NSRange) {
        let wasComposing = composing
        super.insertText(string, replacementRange: replacementRange)
        if wasComposing { finishComposition() }
    }
    override func unmarkText() { super.unmarkText(); finishComposition() }
    private func finishComposition() {
        DispatchQueue.main.async { [weak self] in
            guard let self, !self.hasMarkedText() else { return }
            self.awaitingCommit = false; self.committed?()
        }
    }
    override func performKeyEquivalent(with event: NSEvent) -> Bool {
        if shortcut?(event) == true { return true }
        return super.performKeyEquivalent(with: event)
    }
    override func paste(_ sender: Any?) { pasteAsPlainText(sender) }
    func capture() -> EditorState {
        let selection = selectedRange(), point = enclosingScrollView?.contentView.bounds.origin ?? .zero
        return EditorState(location: selection.location, length: selection.length, x: point.x, y: point.y)
    }
    func restore(_ state: EditorState) {
        let count = (string as NSString).length, location = min(max(0, state.location), (string as NSString).length)
        setSelectedRange(NSRange(location: location, length: min(max(0, state.length), count - location)))
        enclosingScrollView?.contentView.scroll(to: NSPoint(x: max(0, state.x), y: max(0, state.y)))
        if let scroll = enclosingScrollView { scroll.reflectScrolledClipView(scroll.contentView) }
    }
}

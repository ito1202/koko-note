using System.Runtime.InteropServices;
using KOKONote.Core;

namespace KOKONote.App;

internal sealed class PlainEditor : RichTextBox
{
    private readonly Font editorFont = new("Yu Gothic UI", 11);
    private int compositionVersion;
    public bool IsComposing { get; private set; }
    public event EventHandler? CompositionEnded;
    public PlainEditor()
    {
        Dock = DockStyle.Fill;
        BorderStyle = BorderStyle.None;
        DetectUrls = false;
        AcceptsTab = true;
        WordWrap = false;
        ScrollBars = RichTextBoxScrollBars.Both;
        EnableAutoDragDrop = false;
        HideSelection = false;
        MaxLength = int.MaxValue;
        // A replaceable editor preference, with no font stored in note content.
        Font = editorFont;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SendMessage(Handle, 0x0459, 1 | 8 | 32, 0); // EM_SETTEXTMODE, plain + multilevel undo
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x010D) { compositionVersion++; IsComposing = true; } // WM_IME_STARTCOMPOSITION
        if (m.Msg == 0x0302) // WM_PASTE: never admit RTF or images
        {
            try { if (Clipboard.ContainsText(TextDataFormat.UnicodeText)) SelectedText = Clipboard.GetText(TextDataFormat.UnicodeText); }
            catch (ExternalException) { MessageBox.Show(this, "クリップボードを読み取れませんでした。", "KOKO Note"); }
            return;
        }
        base.WndProc(ref m);
        if (m.Msg == 0x010E)
        {
            var version = compositionVersion;
            // Keep transitions blocked until committed WM_IME_CHAR/TextChanged messages have drained.
            if (!IsDisposed) BeginInvoke(() =>
            {
                if (IsDisposed || version != compositionVersion) return;
                IsComposing = false;
                CompositionEnded?.Invoke(this, EventArgs.Empty);
            });
        }
    }

    public EditorView CaptureView()
    {
        var scroll = new Point();
        Native.SendPoint(Handle, 0x04DD, 0, ref scroll); // EM_GETSCROLLPOS
        return new(SelectionStart, SelectionLength, scroll.X, scroll.Y);
    }
    public void RestoreView(EditorView view)
    {
        Select(Math.Clamp(view.SelectionStart, 0, TextLength), Math.Clamp(view.SelectionLength, 0, Math.Max(0, TextLength - Math.Clamp(view.SelectionStart, 0, TextLength))));
        var scroll = new Point(Math.Max(0, view.ScrollX), Math.Max(0, view.ScrollY));
        Native.SendPoint(Handle, 0x04DE, 0, ref scroll); // EM_SETSCROLLPOS
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) editorFont.Dispose();
    }
}

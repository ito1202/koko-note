using KOKONote.Core;

namespace KOKONote.App;

internal sealed class MainForm : Form, IMessageFilter
{
    private readonly Workspace workspace;
    private readonly SaveCoordinator saver;
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly ToolStripStatusLabel status = new() { Text = "保存済み" };
    private readonly Label shortcutHints = new()
    {
        Dock = DockStyle.Bottom, Visible = false, Padding = new(8, 6, 8, 6), UseMnemonic = false,
        AccessibleName = "ショートカット一覧",
        Text = "Ctrl+N 新規   Ctrl+S 保存   Ctrl+L 一覧・検索   Ctrl+W タブを閉じる   Ctrl+R / F2 名前変更\n" +
               "Ctrl+Tab 次へ / Ctrl+Shift+Tab 前へ   Ctrl+Z 元に戻す / Ctrl+Shift+Z・Ctrl+Y やり直す\n" +
               "Ctrl+C コピー   Ctrl+X 切り取り   Ctrl+V 貼り付け   Ctrl+A 全選択\n" +
               "削除:「ノート」→「ノートを削除…」（確認あり）"
    };
    private readonly System.Windows.Forms.Timer debounce = new() { Interval = 500 };
    private readonly Dictionary<Guid, PlainEditor> editors = new();
    private bool rebuilding;
    private bool saveErrorShown;
    private Action? afterComposition;
    public event EventHandler? ExitRequested;
    public event Action<double>? NoteSwitched;
    private long switchStarted;
    public bool IsComposing => editors.Values.Any(e => e.IsComposing);
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsBusy { get; set; }
    private Guid? ActiveId => tabs.SelectedTab?.Tag is Guid id ? id : null;
    private PlainEditor? Editor => ActiveId is Guid id ? editors.GetValueOrDefault(id) : null;
    internal bool ShortcutHintsVisible => shortcutHints.Visible;

    public MainForm(Workspace workspace, SaveCoordinator saver)
    {
        this.workspace = workspace; this.saver = saver;
        Text = "KOKO Note 2.0";
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new(420, 280);
        StartPosition = FormStartPosition.Manual;
        var restored = WindowGeometry.Restore(workspace.Session.Window, Screen.AllScreens.Select(s =>
            new WorkArea(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height)).ToArray());
        Bounds = new(restored.X, restored.Y, restored.Width, restored.Height);
        if (restored.Maximized) WindowState = FormWindowState.Maximized;
        var menu = new MenuStrip();
        var notes = new ToolStripMenuItem("ノート(&N)");
        void Item(string label, Keys keys, Action action) => notes.DropDownItems.Add(new ToolStripMenuItem(label, null,
            (_, _) => RunWhenCommitted(action)) { ShortcutKeys = keys });
        Item("新規(&N)", Keys.Control | Keys.N, NewNote);
        Item("一覧・検索(&L)", Keys.Control | Keys.L, PickNote);
        Item("名前変更(&R)", Keys.Control | Keys.R, RenameNote);
        Item("保存(&S)", Keys.Control | Keys.S, () => _ = SaveNowAsync(true));
        Item("閉じる(&W)", Keys.Control | Keys.W, CloseNote);
        Item("ノートを削除(&D)…", Keys.None, DeleteNote);
        var edit = new ToolStripMenuItem("編集(&E)");
        void EditItem(string label, Action<PlainEditor> action) => edit.DropDownItems.Add(label, null,
            (_, _) => RunWhenCommitted(() => { if (Editor is { } e) { e.Focus(); action(e); } }));
        EditItem("元に戻す", e => e.Undo()); EditItem("やり直す", e => e.Redo());
        EditItem("切り取り", e => e.Cut()); EditItem("コピー", e => e.Copy());
        EditItem("貼り付け", e => Native.SendMessage(e.Handle, 0x0302, 0, 0));
        EditItem("すべて選択", e => e.SelectAll());
        menu.Items.Add(notes); menu.Items.Add(edit);
        MainMenuStrip = menu;
        var bar = new StatusStrip(); bar.Items.Add(status);
        Controls.Add(tabs); Controls.Add(shortcutHints); Controls.Add(bar); Controls.Add(menu);
        ClientSizeChanged += (_, _) => SizeShortcutHints();
        Activated += (_, _) => BeginInvoke(() =>
        {
            if (!IsDisposed) UpdateShortcutHints(ModifierKeys.HasFlag(Keys.Control));
        });
        Deactivate += (_, _) => UpdateShortcutHints(false);
        SizeShortcutHints();
        Application.AddMessageFilter(this);
        tabs.Selecting += (_, e) => { if (IsComposing) e.Cancel = true; };
        tabs.Deselecting += (_, _) =>
        {
            switchStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!rebuilding && !IsComposing) CaptureSession();
        };
        tabs.SelectedIndexChanged += (_, _) =>
        {
            if (rebuilding) return;
            EnsureEditor(); tabs.Update(); CaptureSession();
            if (switchStarted != 0) NoteSwitched?.Invoke(System.Diagnostics.Stopwatch.GetElapsedTime(switchStarted).TotalMilliseconds);
            _ = SaveNowAsync();
        };
        debounce.Tick += async (_, _) => { debounce.Stop(); if (!IsComposing) await SaveNowAsync(); };
        FormClosing += (_, e) =>
        {
            // Dirty shutdowns are deferred in WM_QUERYENDSESSION; let an already-clean OS shutdown proceed.
            if (e.CloseReason == CloseReason.WindowsShutDown) return;
            e.Cancel = true; RunWhenCommitted(HideAndSave);
        };
        ResizeEnd += (_, _) => { CaptureSession(); ScheduleSave(); };
        RebuildTabs(workspace.Session.ActiveNoteId);
    }

    private void RebuildTabs(Guid? active)
    {
        rebuilding = true;
        try
        {
            // Keep native editor HWNDs and undo stacks alive across ordinary tab changes.
            foreach (var old in tabs.TabPages.Cast<TabPage>().ToArray())
            { old.Controls.Clear(); old.Dispose(); }
            tabs.TabPages.Clear();
            foreach (var n in workspace.Notes.Where(n => !n.IsArchived))
            {
                var page = new TabPage(n.Title) { Tag = n.Id };
                if (editors.TryGetValue(n.Id, out var editor)) page.Controls.Add(editor);
                tabs.TabPages.Add(page);
                if (n.Id == active) tabs.SelectedTab = page;
            }
        }
        finally { rebuilding = false; }
        EnsureEditor();
    }

    private void EnsureEditor()
    {
        if (ActiveId is not Guid id || tabs.SelectedTab is not { } page) return;
        if (!editors.TryGetValue(id, out var editor))
        {
            var note = workspace.Notes.Single(n => n.Id == id);
            editor = new PlainEditor();
            page.Controls.Add(editor);
            editor.Text = note.Content;
            editor.ClearUndo();
            editor.TextChanged += (_, _) =>
            {
                if (!editor.IsComposing) workspace.Edit(id, content: editor.Text);
                status.Text = "未保存";
                ScheduleSave();
            };
            editor.CompositionEnded += (_, _) =>
            {
                workspace.Edit(id, content: editor.Text);
                ScheduleSave();
                var pending = afterComposition; afterComposition = null; pending?.Invoke();
            };
            editors[id] = editor;
            editor.RestoreView(workspace.Session.Views.GetValueOrDefault(id) ?? new());
        }
        editor.Focus();
    }

    private void ScheduleSave()
    {
        if (IsBusy) return;
        debounce.Stop(); debounce.Start();
    }
    public void RunWhenCommitted(Action action)
    {
        if (IsBusy) return;
        if (IsComposing) afterComposition = action;
        else action();
    }
    public void ShowEditor()
    {
        var p = WindowGeometry.Restore(new(Left, Top, Width, Height), Screen.AllScreens.Select(s =>
            new WorkArea(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height)).ToArray());
        if (WindowState == FormWindowState.Normal) Bounds = new(p.X, p.Y, p.Width, p.Height);
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Show(); Activate(); Native.SetForegroundWindow(Handle); EnsureEditor();
    }
    public void HideAndSave()
    {
        UpdateShortcutHints(false); CaptureSession(); Hide(); _ = SaveNowAsync();
    }
    public bool PreFilterMessage(ref Message message)
    {
        if (message.Msg is 0x0100 or 0x0101 or 0x0104 or 0x0105 &&
            (Keys)(int)message.WParam is Keys.ControlKey or Keys.LControlKey or Keys.RControlKey &&
            Control.FromChildHandle(message.HWnd)?.FindForm() == this)
            UpdateShortcutHints(ModifierKeys.HasFlag(Keys.Control));
        return false; // Observe only; never consume an editor/IME key event.
    }
    internal void UpdateShortcutHints(bool controlHeld) => shortcutHints.Visible = controlHeld && Visible && ContainsFocus;
    private void SizeShortcutHints()
    {
        var width = Math.Max(1, ClientSize.Width - shortcutHints.Padding.Horizontal);
        shortcutHints.Height = TextRenderer.MeasureText(shortcutHints.Text, shortcutHints.Font,
            new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + shortcutHints.Padding.Vertical;
    }
    public void CaptureSession()
    {
        if (IsComposing) return;
        var views = workspace.Session.Views;
        foreach (var (id, editor) in editors) if (!editor.IsDisposed && editor.IsHandleCreated) views[id] = editor.CaptureView();
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        workspace.SetSession(new(ActiveId, new(bounds.X, bounds.Y, bounds.Width, bounds.Height,
            WindowState == FormWindowState.Maximized), views));
    }

    public async Task<bool> SaveNowAsync(bool manual = false)
    {
        debounce.Stop();
        if (IsComposing) { afterComposition ??= () => _ = SaveNowAsync(manual); return false; }
        CaptureSession();
        status.Text = "保存中…";
        try
        {
            await saver.SaveAsync();
            status.ForeColor = SystemColors.ControlText;
            status.Text = workspace.IsDirty ? "未保存" : "保存済み";
            saveErrorShown = false;
            return !workspace.IsDirty;
        }
        catch (Exception e) when (e is StorageException or IOException or UnauthorizedAccessException or System.Text.EncoderFallbackException)
        {
            status.ForeColor = Color.DarkRed; status.Text = "保存失敗 — Ctrl+Sで再試行";
            if (manual || !saveErrorShown)
            {
                saveErrorShown = true;
                ShowEditor();
                MessageBox.Show(this, "保存できませんでした。未保存の内容はメモリに保持しています。空き容量・アクセス権を確認し、Ctrl+Sで再試行してください。",
                    "KOKO Note — 保存失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }
    }

    private void NewNote()
    { CaptureSession(); var n = workspace.Create(); RebuildTabs(n.Id); _ = SaveNowAsync(); }
    private void PickNote()
    {
        using var picker = new NotePicker(workspace);
        if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedId is not Guid id) return;
        CaptureSession(); workspace.Edit(id, archived: false); RebuildTabs(id); _ = SaveNowAsync();
    }
    private void RenameNote()
    {
        if (ActiveId is not Guid id) return;
        var name = TextPrompt.Ask(this, workspace.Notes.Single(n => n.Id == id).Title);
        if (name == null) return;
        workspace.Edit(id, title: name); tabs.SelectedTab!.Text = name; _ = SaveNowAsync();
    }
    private void CloseNote()
    {
        if (ActiveId is not Guid id) return;
        CaptureSession(); workspace.Edit(id, archived: true);
        if (editors.Remove(id, out var editor)) editor.Dispose();
        RebuildTabs(null); _ = SaveNowAsync();
    }
    private void DeleteNote()
    {
        if (ActiveId is not Guid id) return;
        var confirmed = MessageBox.Show(this, "このノートを完全に削除しますか？この操作は元に戻せません。", "ノート削除",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        if (!workspace.Delete(id, confirmed)) return;
        if (editors.Remove(id, out var editor)) editor.Dispose();
        RebuildTabs(null); _ = SaveNowAsync();
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData is (Keys.Control | Keys.Tab) or (Keys.Control | Keys.Shift | Keys.Tab))
        {
            RunWhenCommitted(() => { var next = InteractionPolicy.NextTab(tabs.SelectedIndex, tabs.TabCount, keyData.HasFlag(Keys.Shift)); if (next >= 0) tabs.SelectedIndex = next; });
            return true;
        }
        if (IsComposing && keyData is (Keys.Control | Keys.N) or (Keys.Control | Keys.S) or (Keys.Control | Keys.W) or
            (Keys.Control | Keys.L) or (Keys.Control | Keys.R) or (Keys.Control | Keys.Shift | Keys.Z) or Keys.F2)
            return true;
        if (keyData == Keys.F2)
        {
            RunWhenCommitted(RenameNote);
            return true;
        }
        if (keyData == (Keys.Control | Keys.Shift | Keys.Z))
        {
            RunWhenCommitted(() => Editor?.Redo());
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0011) // WM_QUERYENDSESSION: capture state before deciding whether shutdown is safe.
        {
            CaptureSession();
            if (workspace.IsDirty || IsComposing)
            {
                m.Result = 0;
                BeginInvoke(() => RunWhenCommitted(() => ExitRequested?.Invoke(this, EventArgs.Empty)));
                return;
            }
        }
        base.WndProc(ref m);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Application.RemoveMessageFilter(this);
            debounce.Dispose();
            foreach (var editor in editors.Values) editor.Dispose();
        }
        base.Dispose(disposing);
    }
}

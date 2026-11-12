#if WINDOWS
using KOKONote.App;
using KOKONote.Core;

namespace KOKONote.Tests;

[CollectionDefinition("Windows desktop", DisableParallelization = true)]
public sealed class WindowsDesktopCollection;

[Collection("Windows desktop")]
public class WindowsUiTests
{
    [WindowsFact]
    public Task HotkeyConflictIsReportedAndUnregisterReleasesKey() => OnSta(async () =>
    {
        using var first = new HotkeyWindow();
        using var duplicate = new HotkeyWindow();
        Assert.NotEqual(0, duplicate.ErrorCode);
        if (first.ErrorCode == 0)
        {
            first.Dispose();
            Assert.True(duplicate.Retry());
            Assert.Equal(0, duplicate.ErrorCode);
        }
        await Task.CompletedTask;
    });

    [WindowsFact]
    public Task DebounceWaitsUntilContinuousTypingStops() => OnSta(async () =>
    {
        var w = new Workspace(new([], SessionState.Empty)); w.Create();
        var store = new RecordingStore();
        using var form = new MainForm(w, new SaveCoordinator(w, store));
        form.ShowEditor();
        var editor = form.Controls.OfType<TabControl>().Single().SelectedTab!.Controls.OfType<PlainEditor>().Single();
        for (var i = 0; i < 5; i++) { editor.AppendText("日本語😀"); await Task.Delay(150); }
        await Task.Delay(100);
        Assert.False(store.Written.Task.IsCompleted);
        var batch = await store.Written.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(string.Concat(Enumerable.Repeat("日本語😀", 5)), Assert.Single(batch.Notes).Content);
        Assert.Equal(1, store.Count);
        await form.SaveNowAsync();
    });

    [WindowsFact]
    public Task HiddenWindowPreservesSelectionAndNativeUndo() => OnSta(async () =>
    {
        var w = new Workspace(new([], SessionState.Empty)); w.Create();
        var store = new RecordingStore();
        using var form = new MainForm(w, new SaveCoordinator(w, store)); form.ShowEditor();
        var editor = form.Controls.OfType<TabControl>().Single().SelectedTab!.Controls.OfType<PlainEditor>().Single();
        editor.AppendText("abcdef"); editor.Select(2, 2);
        form.HideAndSave(); await Task.Delay(100); form.ShowEditor();
        Assert.Equal(2, editor.SelectionStart); Assert.Equal(2, editor.SelectionLength);
        editor.AppendText("追加"); editor.Undo(); Assert.DoesNotContain("追加", editor.Text);
        editor.Redo(); Assert.Contains("追加", editor.Text);
        await form.SaveNowAsync();
    });

    [WindowsFact]
    public Task CompositionBoundaryDefersTransitionsAndKeepsUncommittedTextOutOfWorkspace() => OnSta(async () =>
    {
        var w = new Workspace(new([], SessionState.Empty)); w.Create();
        var store = new RecordingStore();
        using var form = new MainForm(w, new SaveCoordinator(w, store)); form.ShowEditor();
        var editor = form.Controls.OfType<TabControl>().Single().SelectedTab!.Controls.OfType<PlainEditor>().Single();
        // Synthetic notifications test our gate only; real Microsoft IME still requires manual validation.
        Native.SendMessage(editor.Handle, 0x010D, 0, 0);
        editor.AppendText("未確定の日本語");
        var transitioned = false; form.RunWhenCommitted(() => transitioned = true);
        Assert.True(editor.IsComposing); Assert.False(transitioned); Assert.Empty(w.Notes[0].Content);
        Native.SendMessage(editor.Handle, 0x010E, 0, 0);
        Assert.True(editor.IsComposing); // Remains gated until posted completion is processed.
        await Task.Delay(50);
        Assert.False(editor.IsComposing); Assert.True(transitioned);
        Assert.Equal("未確定の日本語", w.Notes[0].Content);
        await form.SaveNowAsync();
    });

    [WindowsFact]
    public Task ControlHintsPreserveSaveStateSelectionAndClearOnHide() => OnSta(async () =>
    {
        var w = new Workspace(new([], SessionState.Empty)); w.Create();
        using var form = new MainForm(w, new SaveCoordinator(w, new RecordingStore())); form.ShowEditor();
        await Task.Yield(); // Drain activation's modifier refresh before simulating a held key.
        var editor = form.Controls.OfType<TabControl>().Single().SelectedTab!.Controls.OfType<PlainEditor>().Single();
        editor.AppendText("abcdef"); editor.Select(2, 2);
        form.UpdateShortcutHints(true);
        var controlMessage = Message.Create(editor.Handle, 0x0100, (nint)Keys.ControlKey, 0);
        Assert.False(form.PreFilterMessage(ref controlMessage));
        form.UpdateShortcutHints(true);
        Assert.True(form.ShortcutHintsVisible);
        await form.SaveNowAsync();
        Assert.True(form.ShortcutHintsVisible);
        var status = (ToolStripStatusLabel)form.Controls.OfType<StatusStrip>().Single().Items[0];
        Assert.Equal("保存済み", status.Text);
        form.UpdateShortcutHints(false);
        Assert.False(form.ShortcutHintsVisible);
        Assert.Equal("保存済み", status.Text);
        Assert.Equal(2, editor.SelectionStart); Assert.Equal(2, editor.SelectionLength);
        status.Text = "保存失敗 — Ctrl+Sで再試行";
        form.UpdateShortcutHints(true); form.UpdateShortcutHints(false);
        Assert.Equal("保存失敗 — Ctrl+Sで再試行", status.Text);
        form.UpdateShortcutHints(true); form.HideAndSave();
        Assert.False(form.ShortcutHintsVisible);
        await form.SaveNowAsync();
    });

    [WindowsFact]
    public Task WindowsShortcutsMatchMacActionsAndRetainNativeRedo() => OnSta(async () =>
    {
        var w = new Workspace(new([], SessionState.Empty)); w.Create();
        using var form = new MainForm(w, new SaveCoordinator(w, new RecordingStore())); form.ShowEditor();
        var menuItems = form.MainMenuStrip!.Items.OfType<ToolStripMenuItem>().First().DropDownItems.OfType<ToolStripMenuItem>();
        Assert.Equal(Keys.Control | Keys.R, menuItems.Single(item => item.Text?.StartsWith("名前変更", StringComparison.Ordinal) == true).ShortcutKeys);
        var editor = form.Controls.OfType<TabControl>().Single().SelectedTab!.Controls.OfType<PlainEditor>().Single();
        editor.AppendText("first"); editor.ClearUndo(); editor.AppendText("tail"); editor.Undo();
        Assert.Equal("first", editor.Text);
        // Exercise our command route directly; never inject keys into another process.
        var method = typeof(MainForm).GetMethod("ProcessCmdKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var message = Message.Create(editor.Handle, 0x0100, (nint)Keys.Z, 0);
        Assert.True(method.Invoke(form, [message, Keys.Control | Keys.Shift | Keys.Z]) is true);
        Assert.Equal("firsttail", editor.Text);
        await form.SaveNowAsync();
    });

    private static Task OnSta(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var context = new ApplicationContext();
            EventHandler? start = null;
            start = async (_, _) =>
            {
                Application.Idle -= start;
                try { await action(); completion.SetResult(); }
                catch (Exception error) { completion.SetException(error); }
                finally { context.ExitThread(); }
            };
            Application.Idle += start;
            // Creating a control installs WindowsFormsSynchronizationContext before the async action.
            using var control = new Control(); _ = control.Handle;
            Application.Run(context);
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
    private sealed class RecordingStore : INoteStore
    {
        public TaskCompletionSource<SaveBatch> Written { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Count;
        public StoredWorkspace Load() => throw new NotSupportedException();
        public void Save(SaveBatch batch) { Interlocked.Increment(ref Count); Written.TrySetResult(batch); }
    }
}
#endif

using KOKONote.Core;

namespace KOKONote.App;

internal sealed class ResidentContext : ApplicationContext
{
    private readonly HotkeyWindow hotkey = new();
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu = new();
    private MainForm? window;
    private FileStream? dataLock;
    private bool exiting;
    private bool startupFailed;
    private readonly PerformanceRecorder? metrics;

    public ResidentContext(PerformanceRecorder? metrics = null)
    {
        this.metrics = metrics;
        menu.Items.Add("表示", null, (_, _) => Show());
        menu.Items.Add("設定", null, (_, _) => Settings());
        menu.Items.Add("終了", null, async (_, _) => await RequestExit());
        tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "KOKO Note 2.0", Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => Show();
        hotkey.Pressed += (_, _) =>
        {
            metrics?.Interaction();
            if (window == null || exiting || window.OwnedForms.Any(f => f.Visible)) return;
            window.RunWhenCommitted(() =>
            {
                if (InteractionPolicy.Toggle(window.Visible, Native.GetForegroundWindow() == window.Handle, false) == ToggleAction.Hide)
                    window.HideAndSave();
                else ShowMeasured();
            });
        };
        Application.Idle += Initialize;
    }

    private async void Initialize(object? sender, EventArgs e)
    {
        Application.Idle -= Initialize;
        try
        {
            var path = SecureDirectory.Prepare();
            dataLock = new FileStream(Path.Combine(Path.GetDirectoryName(path)!, ".instance.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var store = new SqliteNoteStore(path, new DpapiProtector());
            var loaded = await Task.Run(store.Load);
            var workspace = new Workspace(loaded);
            if (workspace.Notes.Count == 0) workspace.Create();
            var saver = new SaveCoordinator(workspace, store);
            window = new MainForm(workspace, saver);
            window.NoteSwitched += milliseconds => metrics?.Switch(milliseconds);
            window.VisibleChanged += (_, _) => metrics?.Interaction();
            _ = window.Handle;
            window.ExitRequested += async (_, _) => await RequestExit();
            await window.SaveNowAsync();
            if (metrics != null) { metrics.Ready(); _ = metrics.SampleIdleAsync(() => window.Visible); }
            if (hotkey.ErrorCode != 0) HotkeyError();
        }
        catch (Exception error) when (error is StorageException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            startupFailed = true;
            MessageBox.Show("データを安全に開けませんでした。既存ノートを置き換えず起動を中止します。" +
                "\n別の起動中プロセス、Windowsユーザー、DPAPIキー、保存先のアクセス権・DB破損を確認してください。\n保存先: %LOCALAPPDATA%\\KOKONote",
                "KOKO Note — 起動失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ExitThread();
        }
    }

    private void Show()
    {
        if (exiting) return;
        if (window == null) { MessageBox.Show("ノートを読み込んでいます。", "KOKO Note"); return; }
        metrics?.Interaction();
        window.RunWhenCommitted(ShowMeasured);
    }
    private void ShowMeasured()
    {
        if (window == null) return;
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        window.ShowEditor(); window.Update();
        metrics?.Show(System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }
    private void Settings()
    {
        if (window == null || exiting) return;
        window.RunWhenCommitted(() =>
        {
            window.ShowEditor();
            var text = "ホットキー: Ctrl + Shift + Space（MVPでは固定）\n" +
                "保存先: %LOCALAPPDATA%\\KOKONote\n本文・タイトル: Windows DPAPI CurrentUser\n" +
                "ホットキー登録: " + (hotkey.ErrorCode == 0 ? "正常" : $"失敗（コード {hotkey.ErrorCode}）") +
                "\n\nOKでホットキー登録を再試行します。";
            if (MessageBox.Show(window, text, "KOKO Note — 設定", MessageBoxButtons.OKCancel) == DialogResult.OK && !hotkey.Retry()) HotkeyError();
        });
    }
    private void HotkeyError() => MessageBox.Show(window,
        $"Ctrl + Shift + Space を登録できませんでした（コード {hotkey.ErrorCode}）。他アプリの割り当てを確認してください。トレイから表示・設定できます。",
        "KOKO Note — ホットキー登録失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private async Task RequestExit()
    {
        if (exiting || startupFailed || window == null) return;
        if (window.IsComposing) { window.RunWhenCommitted(() => _ = RequestExit()); return; }
        exiting = true; menu.Enabled = false; window.IsBusy = true; window.Enabled = false;
        while (!await window.SaveNowAsync())
        {
            window.Enabled = true; window.ShowEditor();
            if (MessageBox.Show(window, "未保存の内容があります。保存を再試行しますか？キャンセルすると常駐を継続します。",
                "終了できません", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error) != DialogResult.Retry)
            { exiting = false; menu.Enabled = true; window.IsBusy = false; return; }
            window.Enabled = false;
        }
        ExitThread();
    }
    protected override void ExitThreadCore()
    {
        Application.Idle -= Initialize;
        if (metrics != null && !startupFailed)
        {
            try { metrics.Write(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { MessageBox.Show("性能測定結果を書き込めませんでした。ノートの保存とは独立したエラーです。", "KOKO Note — 測定失敗"); }
        }
        hotkey.Dispose(); tray.Visible = false; tray.Dispose(); menu.Dispose();
        window?.Dispose(); dataLock?.Dispose(); base.ExitThreadCore();
    }
}

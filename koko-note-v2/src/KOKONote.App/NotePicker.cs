using KOKONote.Core;

namespace KOKONote.App;

internal sealed class NotePicker : Form
{
    private readonly TextBox query = new() { Dock = DockStyle.Top, PlaceholderText = "タイトル・本文を検索" };
    private readonly ListBox list = new() { Dock = DockStyle.Fill, DisplayMember = nameof(Note.Title) };
    private readonly Workspace workspace;
    public Guid? SelectedId { get; private set; }
    public NotePicker(Workspace workspace)
    {
        this.workspace = workspace;
        Text = "ノート一覧（閉じたノートを含む）";
        Width = 480; Height = 400; StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false;
        var open = new Button { Text = "開く", Dock = DockStyle.Bottom, DialogResult = DialogResult.None };
        open.Click += (_, _) => Choose();
        list.DoubleClick += (_, _) => Choose();
        query.TextChanged += (_, _) => RefreshList();
        query.KeyDown += (_, e) => { if (e.KeyCode == Keys.Down && list.Items.Count > 0) { list.Focus(); e.Handled = true; } };
        Controls.Add(list); Controls.Add(query); Controls.Add(open);
        AcceptButton = open;
        var cancel = new Button { DialogResult = DialogResult.Cancel, Visible = false };
        Controls.Add(cancel); CancelButton = cancel;
        RefreshList();
    }
    private void RefreshList()
    {
        list.DataSource = workspace.Search(query.Text).ToArray();
    }
    private void Choose()
    {
        if (list.SelectedItem is not Note n) return;
        SelectedId = n.Id; DialogResult = DialogResult.OK; Close();
    }
}

internal static class TextPrompt
{
    public static string? Ask(IWin32Window owner, string value)
    {
        using var dialog = new Form { Text = "ノート名変更", Width = 420, Height = 150,
            FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false };
        var input = new TextBox { Text = value, Dock = DockStyle.Top, MaxLength = 200 };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 38 };
        var ok = new Button { Text = "変更", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "キャンセル", DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange([cancel, ok]); dialog.Controls.Add(input); dialog.Controls.Add(buttons);
        dialog.AcceptButton = ok; dialog.CancelButton = cancel;
        dialog.Shown += (_, _) => { input.SelectAll(); input.Focus(); };
        return dialog.ShowDialog(owner) == DialogResult.OK && !string.IsNullOrWhiteSpace(input.Text) ? input.Text : null;
    }
}

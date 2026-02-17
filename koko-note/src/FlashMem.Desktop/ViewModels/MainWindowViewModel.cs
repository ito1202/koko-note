using System.Collections.ObjectModel;
using FlashMem.Application.Notes;
using CommunityToolkit.Mvvm.ComponentModel;
using FlashMem.Domain.Notes;

namespace FlashMem.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly NoteEditorService _editorService;
    private readonly INoteStore _noteStore;
    private readonly string _password;
    private bool _suspendSync;
    private int _saveVersion;

    public MainWindowViewModel(NoteEditorService editorService, INoteStore noteStore, string password)
    {
        _editorService = editorService;
        _noteStore = noteStore;
        _password = password;
        ReloadFromDomain();
    }

    public ObservableCollection<NoteListItemViewModel> Notes { get; } = [];

    [ObservableProperty]
    private NoteListItemViewModel? selectedNoteItem;

    [ObservableProperty]
    private string noteTitle = string.Empty;

    [ObservableProperty]
    private string noteContent = string.Empty;

    public async Task ArchiveSelectedAsync()
    {
        if (!_editorService.ArchiveSelected(DateTimeOffset.UtcNow))
        {
            return;
        }

        ReloadFromDomain();
        await PersistAsync();
    }

    public async Task CreateNoteAsync()
    {
        _editorService.CreateNote(DateTimeOffset.UtcNow);
        ReloadFromDomain();
        await PersistImmediatelyAsync();
    }

    public async Task DeleteSelectedAsync()
    {
        _editorService.DeleteSelected();
        ReloadFromDomain();
        await PersistImmediatelyAsync();
    }

    public Task SaveNowAsync()
    {
        return PersistImmediatelyAsync();
    }

    public void MoveSelectionUp()
    {
        _editorService.MoveSelectionUp();
        ReloadFromDomain();
    }

    public void MoveSelectionDown()
    {
        _editorService.MoveSelectionDown();
        ReloadFromDomain();
    }

    partial void OnSelectedNoteItemChanged(NoteListItemViewModel? value)
    {
        if (_suspendSync || value is null)
        {
            return;
        }

        _editorService.Select(value.Id);
        SyncSelectedNoteToEditorFields();
    }

    partial void OnNoteTitleChanged(string value)
    {
        UpdateSelectedNote();
    }

    partial void OnNoteContentChanged(string value)
    {
        UpdateSelectedNote();
    }

    private void UpdateSelectedNote()
    {
        if (_suspendSync)
        {
            return;
        }

        _editorService.UpdateSelected(NoteTitle, NoteContent, DateTimeOffset.UtcNow);
        ReloadFromDomain();
        _ = PersistAsync();
    }

    private void ReloadFromDomain()
    {
        _suspendSync = true;
        try
        {
            var currentSelection = _editorService.SelectedNote.Id;
            Notes.Clear();
            foreach (var note in _editorService.VisibleNotes)
            {
                Notes.Add(new NoteListItemViewModel(note.Id, note.Title, note.UpdatedAtUtc));
            }

            SelectedNoteItem = Notes.FirstOrDefault(x => x.Id == currentSelection) ?? Notes.FirstOrDefault();
            SyncSelectedNoteToEditorFields();
        }
        finally
        {
            _suspendSync = false;
        }
    }

    private void SyncSelectedNoteToEditorFields()
    {
        var selected = _editorService.SelectedNote;
        NoteTitle = selected.Title;
        NoteContent = selected.Content;
    }

    private async Task PersistAsync()
    {
        var currentVersion = Interlocked.Increment(ref _saveVersion);
        await Task.Delay(150);
        if (currentVersion != _saveVersion)
        {
            return;
        }

        await _noteStore.SaveAsync(_editorService.Workspace, _password);
    }

    private Task PersistImmediatelyAsync()
    {
        Interlocked.Increment(ref _saveVersion);
        return _noteStore.SaveAsync(_editorService.Workspace, _password);
    }
}

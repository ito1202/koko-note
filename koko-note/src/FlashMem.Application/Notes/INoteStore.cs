using FlashMem.Domain.Notes;

namespace FlashMem.Application.Notes;

public interface INoteStore
{
    Task<NoteWorkspace> LoadAsync(string password, CancellationToken cancellationToken = default);

    Task SaveAsync(NoteWorkspace workspace, string password, CancellationToken cancellationToken = default);
}

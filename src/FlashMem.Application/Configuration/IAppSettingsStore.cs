namespace FlashMem.Application.Configuration;

public interface IAppSettingsStore
{
    Task<AppSettings> LoadAsync(bool isMacOs, CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

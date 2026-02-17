namespace FlashMem.Desktop.Services;

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? Triggered;

    Task StartAsync(CancellationToken cancellationToken = default);
}

using FlashMem.Domain.Input;
using SharpHook;
using SharpHook.Data;
using SharpHook.Providers;

namespace FlashMem.Desktop.Services;

public sealed class CtrlDoubleTapGlobalHotkeyService : IGlobalHotkeyService
{
    private readonly DoubleTapHotkeyDetector _detector;
    private readonly IGlobalHook _hook;
    private Task? _hookTask;

    public CtrlDoubleTapGlobalHotkeyService(DoubleTapHotkeyDetector detector)
    {
        _detector = detector;
        UioHookProvider.Instance.KeyTypedEnabled = false;
        _hook = new EventLoopGlobalHook();
        _hook.KeyPressed += HandleKeyPressed;
    }

    public event EventHandler? Triggered;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_hookTask is not null)
        {
            return _hookTask;
        }

        _hookTask = _hook.RunAsync();
        return _hookTask;
    }

    public void Dispose()
    {
        _hook.KeyPressed -= HandleKeyPressed;
        _hook.Dispose();
    }

    private void HandleKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (!IsControlKey(e.Data.KeyCode))
        {
            return;
        }

        if (_detector.RegisterTap(DateTimeOffset.UtcNow))
        {
            Triggered?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool IsControlKey(KeyCode keyCode)
    {
        return keyCode is KeyCode.VcLeftControl or KeyCode.VcRightControl;
    }
}

using FlashMem.Application.Configuration;
using FlashMem.Domain.Input;
using SharpHook;
using SharpHook.Data;
using SharpHook.Providers;

namespace FlashMem.Desktop.Services;

public sealed class ModifierDoubleTapGlobalHotkeyService : IGlobalHotkeyService
{
    private readonly DoubleTapHotkeyDetector _detector;
    private readonly GlobalHotkeyOptions _options;
    private readonly IGlobalHook _hook;
    private Task? _hookTask;

    public ModifierDoubleTapGlobalHotkeyService(GlobalHotkeyOptions options)
    {
        _options = options;
        _detector = new DoubleTapHotkeyDetector(options.DoubleTapWindow);
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
        if (!IsTargetModifier(e.Data.KeyCode, _options.TapKey))
        {
            return;
        }

        if (_detector.RegisterTap(DateTimeOffset.UtcNow))
        {
            Triggered?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool IsTargetModifier(KeyCode keyCode, HotkeyTapKey tapKey)
    {
        return tapKey switch
        {
            HotkeyTapKey.Shift => keyCode is KeyCode.VcLeftShift or KeyCode.VcRightShift,
            HotkeyTapKey.Control => keyCode is KeyCode.VcLeftControl or KeyCode.VcRightControl,
            HotkeyTapKey.Alt => keyCode is KeyCode.VcLeftAlt or KeyCode.VcRightAlt,
            HotkeyTapKey.Command => keyCode is KeyCode.VcLeftMeta or KeyCode.VcRightMeta,
            _ => false,
        };
    }
}

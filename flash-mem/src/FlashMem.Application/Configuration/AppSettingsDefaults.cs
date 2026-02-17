namespace FlashMem.Application.Configuration;

public static class AppSettingsDefaults
{
    public const int MinHotkeyWindowMs = 120;
    public const int MaxHotkeyWindowMs = 600;

    public static AppSettings Create(bool isMacOs)
    {
        return new AppSettings(
            HotkeyTapKey: isMacOs ? HotkeyTapKey.Shift : HotkeyTapKey.Control,
            HotkeyDoubleTapWindowMs: 300,
            StartAtLoginEnabled: true);
    }

    public static AppSettings Sanitize(AppSettings settings, bool isMacOs)
    {
        var tapKey = settings.HotkeyTapKey;
        if (!Enum.IsDefined(typeof(HotkeyTapKey), tapKey))
        {
            tapKey = isMacOs ? HotkeyTapKey.Shift : HotkeyTapKey.Control;
        }

        var windowMs = Math.Clamp(settings.HotkeyDoubleTapWindowMs, MinHotkeyWindowMs, MaxHotkeyWindowMs);
        return settings with
        {
            HotkeyTapKey = tapKey,
            HotkeyDoubleTapWindowMs = windowMs,
        };
    }
}

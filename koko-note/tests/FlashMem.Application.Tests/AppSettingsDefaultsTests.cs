using FlashMem.Application.Configuration;

namespace FlashMem.Application.Tests;

public sealed class AppSettingsDefaultsTests
{
    [Fact]
    public void Create_Uses_Shift_For_Mac()
    {
        var settings = AppSettingsDefaults.Create(isMacOs: true);

        Assert.Equal(HotkeyTapKey.Shift, settings.HotkeyTapKey);
    }

    [Fact]
    public void Create_Uses_Control_For_NonMac()
    {
        var settings = AppSettingsDefaults.Create(isMacOs: false);

        Assert.Equal(HotkeyTapKey.Control, settings.HotkeyTapKey);
    }

    [Fact]
    public void Sanitize_Clamps_DoubleTapWindow()
    {
        var settings = new AppSettings(HotkeyTapKey.Shift, 1, true);

        var sanitized = AppSettingsDefaults.Sanitize(settings, isMacOs: true);

        Assert.Equal(AppSettingsDefaults.MinHotkeyWindowMs, sanitized.HotkeyDoubleTapWindowMs);
    }
}

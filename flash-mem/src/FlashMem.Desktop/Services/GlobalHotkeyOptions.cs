using FlashMem.Application.Configuration;

namespace FlashMem.Desktop.Services;

public readonly record struct GlobalHotkeyOptions(
    HotkeyTapKey TapKey,
    TimeSpan DoubleTapWindow);

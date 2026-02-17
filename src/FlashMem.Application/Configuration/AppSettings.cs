namespace FlashMem.Application.Configuration;

public sealed record AppSettings(
    HotkeyTapKey HotkeyTapKey,
    int HotkeyDoubleTapWindowMs,
    bool StartAtLoginEnabled);

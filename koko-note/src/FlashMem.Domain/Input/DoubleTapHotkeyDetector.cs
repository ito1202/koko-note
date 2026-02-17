namespace FlashMem.Domain.Input;

public sealed class DoubleTapHotkeyDetector
{
    private readonly TimeSpan _window;
    private DateTimeOffset? _lastTapUtc;

    public DoubleTapHotkeyDetector(TimeSpan window)
    {
        _window = window;
    }

    public bool RegisterTap(DateTimeOffset tapUtc)
    {
        if (_lastTapUtc is null)
        {
            _lastTapUtc = tapUtc;
            return false;
        }

        var elapsed = tapUtc - _lastTapUtc.Value;
        _lastTapUtc = tapUtc;
        return elapsed >= TimeSpan.Zero && elapsed <= _window;
    }
}

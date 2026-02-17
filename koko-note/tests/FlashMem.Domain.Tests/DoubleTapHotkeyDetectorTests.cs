using FlashMem.Domain.Input;

namespace FlashMem.Domain.Tests;

public sealed class DoubleTapHotkeyDetectorTests
{
    [Fact]
    public void ReturnsTrue_When_SecondTap_Within_Window()
    {
        var detector = new DoubleTapHotkeyDetector(TimeSpan.FromMilliseconds(300));
        var firstTap = DateTimeOffset.Parse("2026-02-14T10:00:00+00:00");
        var secondTap = firstTap.AddMilliseconds(250);

        var first = detector.RegisterTap(firstTap);
        var second = detector.RegisterTap(secondTap);

        Assert.False(first);
        Assert.True(second);
    }

    [Fact]
    public void ReturnsFalse_When_SecondTap_Is_Too_Late()
    {
        var detector = new DoubleTapHotkeyDetector(TimeSpan.FromMilliseconds(300));
        var firstTap = DateTimeOffset.Parse("2026-02-14T10:00:00+00:00");
        var secondTap = firstTap.AddMilliseconds(500);

        detector.RegisterTap(firstTap);
        var result = detector.RegisterTap(secondTap);

        Assert.False(result);
    }
}

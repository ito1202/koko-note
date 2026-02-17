using FlashMem.Application.UI;

namespace FlashMem.Application.Tests;

public sealed class PopupPositionCalculatorTests
{
    [Fact]
    public void Calculate_Uses_Cursor_RightBottom_Offset()
    {
        var workingArea = new ScreenBounds(0, 0, 1920, 1080);

        var position = PopupPositionCalculator.Calculate(
            cursorX: 100,
            cursorY: 100,
            popupWidth: 800,
            popupHeight: 500,
            workingArea: workingArea);

        Assert.Equal(112, position.X);
        Assert.Equal(112, position.Y);
    }

    [Fact]
    public void Calculate_Clamps_Inside_WorkingArea()
    {
        var workingArea = new ScreenBounds(0, 0, 1280, 720);

        var position = PopupPositionCalculator.Calculate(
            cursorX: 1260,
            cursorY: 700,
            popupWidth: 640,
            popupHeight: 480,
            workingArea: workingArea);

        Assert.Equal(640, position.X);
        Assert.Equal(240, position.Y);
    }
}

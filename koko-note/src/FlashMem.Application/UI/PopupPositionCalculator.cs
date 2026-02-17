namespace FlashMem.Application.UI;

public static class PopupPositionCalculator
{
    private const int DefaultOffset = 12;

    public static PopupPosition Calculate(
        int cursorX,
        int cursorY,
        int popupWidth,
        int popupHeight,
        ScreenBounds workingArea)
    {
        var x = cursorX + DefaultOffset;
        var y = cursorY + DefaultOffset;

        if (x + popupWidth > workingArea.Right)
        {
            x = workingArea.Right - popupWidth;
        }

        if (y + popupHeight > workingArea.Bottom)
        {
            y = workingArea.Bottom - popupHeight;
        }

        x = Math.Clamp(x, workingArea.Left, workingArea.Right - popupWidth);
        y = Math.Clamp(y, workingArea.Top, workingArea.Bottom - popupHeight);

        return new PopupPosition(x, y);
    }
}

public readonly record struct ScreenBounds(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

public readonly record struct PopupPosition(int X, int Y);

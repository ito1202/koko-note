using Avalonia;

namespace FlashMem.Desktop.Services;

public interface ICursorPositionProvider
{
    PixelPoint GetCursorPosition();
}

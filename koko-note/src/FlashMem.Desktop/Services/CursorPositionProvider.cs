using System.Runtime.InteropServices;
using Avalonia;

namespace FlashMem.Desktop.Services;

public sealed class CursorPositionProvider : ICursorPositionProvider
{
    public PixelPoint GetCursorPosition()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            GetCursorPos(out var point);
            return new PixelPoint(point.X, point.Y);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var handle = CGEventCreate(IntPtr.Zero);
            if (handle == IntPtr.Zero)
            {
                return new PixelPoint(0, 0);
            }

            try
            {
                var point = CGEventGetLocation(handle);
                return new PixelPoint((int)Math.Round(point.X), (int)Math.Round(point.Y));
            }
            finally
            {
                CFRelease(handle);
            }
        }

        return new PixelPoint(0, 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinPoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out WinPoint point);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern CGPoint CGEventGetLocation(IntPtr @event);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr handle);
}

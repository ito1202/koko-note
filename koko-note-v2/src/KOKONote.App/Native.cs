using System.Runtime.InteropServices;

namespace KOKONote.App;

internal static class Native
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint SendMessage(nint hwnd, int message, nint wParam, nint lParam);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    internal static extern nint SendPoint(nint hwnd, int message, nint wParam, ref Point point);
}

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private bool registered;
    private bool disposed;
    public event EventHandler? Pressed;
    public int ErrorCode { get; private set; }
    public HotkeyWindow()
    {
        CreateHandle(new CreateParams { Caption = "KOKO Note hotkey", Parent = new nint(-3) });
        Retry();
    }
    public bool Retry()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (registered) return true;
        registered = Native.RegisterHotKey(Handle, 1, 0x0002 | 0x0004 | 0x4000, (uint)Keys.Space);
        ErrorCode = registered ? 0 : Marshal.GetLastWin32Error();
        return registered;
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && m.WParam == 1) Pressed?.Invoke(this, EventArgs.Empty);
        base.WndProc(ref m);
    }
    public void Dispose()
    {
        if (disposed) return;
        if (registered) Native.UnregisterHotKey(Handle, 1);
        registered = false;
        DestroyHandle(); disposed = true;
    }
}

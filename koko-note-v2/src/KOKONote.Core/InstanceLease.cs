namespace KOKONote.Core;

public sealed class InstanceLease : IDisposable
{
    private readonly Mutex mutex;
    private bool disposed;
    public bool IsFirst { get; }
    public InstanceLease(string name)
    {
        mutex = new Mutex(true, name, out var first);
        IsFirst = first;
    }
    public void Dispose()
    {
        if (disposed) return;
        if (IsFirst) mutex.ReleaseMutex();
        mutex.Dispose(); disposed = true;
    }
}

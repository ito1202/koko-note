using System.Security.Cryptography;
using System.Text;
using KOKONote.Core;

namespace KOKONote.Tests;

// Portable tests validate persistence boundaries; this is NEVER used by the application.
internal sealed class TestProtector : IContentProtector
{
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    public byte[] Protect(string text, Guid id, string purpose)
    {
        var plain = Encoding.UTF8.GetBytes(text);
        var data = new byte[28 + plain.Length];
        RandomNumberGenerator.Fill(data.AsSpan(0, 12));
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(data.AsSpan(0, 12), plain, data.AsSpan(28), data.AsSpan(12, 16), Encoding.UTF8.GetBytes(id + purpose));
        return data;
    }
    public string Unprotect(byte[] encrypted, Guid id, string purpose)
    {
        try
        {
            var plain = new byte[encrypted.Length - 28];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(encrypted.AsSpan(0, 12), encrypted.AsSpan(28), encrypted.AsSpan(12, 16), plain, Encoding.UTF8.GetBytes(id + purpose));
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception e) when (e is CryptographicException or ArgumentException or OverflowException)
        { throw new StorageException(StorageFailure.Decryption); }
    }
}

internal sealed class TestDatabase : IDisposable
{
    public string Directory { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KOKO-test-" + Guid.NewGuid().ToString("N"));
    public string Path => System.IO.Path.Combine(Directory, "notes.db");
    public SqliteNoteStore Store { get; }
    public IContentProtector Protector { get; }
    public TestDatabase(IContentProtector? protector = null)
    {
        System.IO.Directory.CreateDirectory(Directory);
        Protector = protector ?? new TestProtector();
        Store = new(Path, Protector);
    }
    public void Dispose() => System.IO.Directory.Delete(Directory, true);
}

public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    { if (!OperatingSystem.IsWindows()) Skip = "Windows DPAPI/native GUI required; not executed on this OS."; }
}

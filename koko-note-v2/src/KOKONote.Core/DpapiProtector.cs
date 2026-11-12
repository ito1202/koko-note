using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace KOKONote.Core;

public interface IContentProtector
{
    byte[] Protect(string text, Guid id, string purpose);
    string Unprotect(byte[] encrypted, Guid id, string purpose);
}

// Native DPAPI avoids an additional NuGet dependency. No LOCAL_MACHINE flag, no UI prompt.
public sealed class DpapiProtector : IContentProtector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Blob { public int Length; public nint Data; }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob input, string? description, ref Blob entropy,
        nint reserved, nint prompt, uint flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob input, nint description, ref Blob entropy,
        nint reserved, nint prompt, uint flags, out Blob output);
    [DllImport("kernel32.dll")]
    private static extern nint LocalFree(nint memory);

    private static readonly UTF8Encoding Encoding = new(false, true);
    public byte[] Protect(string text, Guid id, string purpose)
    {
        var plain = Encoding.GetBytes(text);
        try { return Transform(plain, id, purpose, true); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public string Unprotect(byte[] encrypted, Guid id, string purpose)
    {
        var plain = Transform(encrypted, id, purpose, false);
        try { return Encoding.GetString(plain); }
        catch (DecoderFallbackException) { throw new StorageException(StorageFailure.Decryption); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private static byte[] Transform(byte[] data, Guid id, string purpose, bool protect)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DPAPI requires Windows.");
        var entropy = Encoding.GetBytes($"KOKONote.v1/{id:D}/{purpose}");
        var pin = GCHandle.Alloc(data, GCHandleType.Pinned);
        var entropyPin = GCHandle.Alloc(entropy, GCHandleType.Pinned);
        Blob output = default;
        try
        {
            var input = new Blob { Length = data.Length, Data = pin.AddrOfPinnedObject() };
            var additional = new Blob { Length = entropy.Length, Data = entropyPin.AddrOfPinnedObject() };
            var ok = protect
                ? CryptProtectData(ref input, null, ref additional, 0, 0, 1, out output)
                : CryptUnprotectData(ref input, 0, ref additional, 0, 0, 1, out output);
            if (!ok) throw new StorageException(protect ? StorageFailure.Encryption : StorageFailure.Decryption);
            var result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, result.Length);
            return result;
        }
        finally
        {
            // DPAPI owns output; clear unmanaged plaintext before LocalFree.
            if (output.Data != 0)
            {
                if (!protect) for (var i = 0; i < output.Length; i++) Marshal.WriteByte(output.Data, i, 0);
                LocalFree(output.Data);
            }
            entropyPin.Free(); pin.Free();
        }
    }
}

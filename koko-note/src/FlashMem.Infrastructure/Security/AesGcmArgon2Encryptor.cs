using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace FlashMem.Infrastructure.Security;

public sealed class AesGcmArgon2Encryptor : IEncryptor
{
    private const int SaltLength = 16;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int KeyLength = 32;

    public EncryptedPayload Encrypt(string plaintext, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagLength];
        var key = DeriveKey(password, salt);

        using var aes = new AesGcm(key, TagLength);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        return new EncryptedPayload(
            Convert.ToBase64String(salt),
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(ciphertext),
            Convert.ToBase64String(tag));
    }

    public string Decrypt(EncryptedPayload payload, string password)
    {
        var salt = Convert.FromBase64String(payload.SaltBase64);
        var nonce = Convert.FromBase64String(payload.NonceBase64);
        var ciphertext = Convert.FromBase64String(payload.CiphertextBase64);
        var tag = Convert.FromBase64String(payload.TagBase64);
        var plaintext = new byte[ciphertext.Length];
        var key = DeriveKey(password, salt);

        using var aes = new AesGcm(key, tag.Length);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            Iterations = 3,
            DegreeOfParallelism = 4,
            MemorySize = 64 * 1024,
        };

        return argon2.GetBytes(KeyLength);
    }
}

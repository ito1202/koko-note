namespace FlashMem.Infrastructure.Security;

public interface IEncryptor
{
    EncryptedPayload Encrypt(string plaintext, string password);

    string Decrypt(EncryptedPayload payload, string password);
}

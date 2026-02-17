using FlashMem.Infrastructure.Security;

namespace FlashMem.Infrastructure.Tests;

public sealed class EncryptionTests
{
    [Fact]
    public void Encrypt_Then_Decrypt_Returns_Original_Text()
    {
        var encryptor = new AesGcmArgon2Encryptor();
        const string plaintext = "sensitive memo text";
        const string password = "correct horse battery staple";

        var payload = encryptor.Encrypt(plaintext, password);
        var decrypted = encryptor.Decrypt(payload, password);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Decrypt_With_Wrong_Password_Throws()
    {
        var encryptor = new AesGcmArgon2Encryptor();
        var payload = encryptor.Encrypt("memo", "password-a");

        Assert.ThrowsAny<Exception>(() => encryptor.Decrypt(payload, "password-b"));
    }
}

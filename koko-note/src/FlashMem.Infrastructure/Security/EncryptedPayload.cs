namespace FlashMem.Infrastructure.Security;

public sealed record EncryptedPayload(
    string SaltBase64,
    string NonceBase64,
    string CiphertextBase64,
    string TagBase64);
